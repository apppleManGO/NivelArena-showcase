// Assets/Scripts/Managers/CombatManager.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    // 이번 어택 페이즈에 공격을 마친 (측, 레인). 온라인은 양측이 공격하므로 side로 구분.
    private HashSet<(bool isPlayer, int lane)> _attacked = new();
    public event System.Action OnAttackStateChanged;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void BeginAttackPhase()
    {
        _attacked.Clear();
        OnAttackStateChanged?.Invoke();
    }

    // ST07 사랑의 증거: 스킬로 즉시 강제 공격 (턴/페이즈 무관, 정상 어택 페이즈 흐름과 별개의 보너스 공격)
    // 플레이어 발동 시 AI가 방어 자동 판단, AI 발동 시 플레이어에게 방어 팝업 표시
    public IEnumerator ForceAttackLane(bool isAttackerIsPlayer, int lane)
    {
        CardData attacker = FieldManager.Instance.GetUnit(isAttackerIsPlayer, lane);
        if (attacker == null) yield break;
        if (!EffectSystem.Instance.CanUnitAttack(isAttackerIsPlayer, lane, attacker)) yield break;

        CardData defender = FieldManager.Instance.GetUnit(!isAttackerIsPlayer, lane);
        bool defenderDisabled = defender != null && EffectSystem.Instance.IsDefendDisabled(!isAttackerIsPlayer, lane);
        bool breakthrough = !EffectSystem.Instance.CanDefend(attacker, defender ?? attacker)
                          || EffectSystem.Instance.HasGrantedBreakthrough(isAttackerIsPlayer, lane, defender)
                          || defenderDisabled;

        if (defender != null && breakthrough)
        {
            ProcessDirectAttack(isAttackerIsPlayer, lane, attacker);
        }
        else if (defender != null)
        {
            bool blocks;
            if (IsDuelist(isAttackerIsPlayer, lane, attacker))
            {
                blocks = true; // ST10 듀얼리스트: 조우 유닛은 가능하다면 반드시 방어
            }
            else
            {
                // 방어 결정을 중개소로 (방어자=공격자의 상대). 로컬 인간=팝업 / AI=자동
                bool decided = false, result = false;
                var pw = PreviewPowers(isAttackerIsPlayer, lane, attacker, lane, defender);
                ChoiceBroker.DecideDefense(!isAttackerIsPlayer, attacker, defender,
                    autoDecide: () => AIDecideDefense(pw.atk, pw.def),
                    onDecided: c => { result = c; decided = true; },
                    attackerPower: pw.atk, defenderPower: pw.def);
                yield return new WaitUntil(() => decided);
                blocks = result;
            }

            if (blocks) ResolveEncounter(isAttackerIsPlayer, lane, attacker, defender);
            else ProcessDirectAttack(isAttackerIsPlayer, lane, attacker);
        }
        else
        {
            ProcessDirectAttack(isAttackerIsPlayer, lane, attacker);
        }

        yield return new WaitWhile(() => SkillZoneSlot.IsTriggerTargeting);

        // 3c: 공격(코루틴) 완료 후 결과 상태 복제 (관문 직후 브로드캐스트는 공격 전이라 놓침)
        if (NetHub.IsOnline && NetHub.IsAuthority) NetHub.BroadcastState();
    }

    // 플레이어(호스트) 기준 — LaneSlot UI/기존 호출부 호환용
    public bool CanAttack(int lane) => CanAttack(true, lane);

    // side별 공격 가능 여부. 온라인 클라 공격(isPlayer=false)도 이걸로 1회 제한을 받는다.
    public bool CanAttack(bool isPlayer, int lane)
    {
        var unit = FieldManager.Instance.GetUnit(isPlayer, lane);
        if (unit == null) return false;
        if (!EffectSystem.Instance.CanUnitAttack(isPlayer, lane, unit)) return false; // 공격 불가 상태
        // 이미 공격한 레인은 추가 공격 부여 시에만 재공격 가능
        return !_attacked.Contains((isPlayer, lane)) || EffectSystem.Instance.HasExtraAttack(isPlayer, lane);
    }

    // 공격 1회 소진 기록(또는 추가공격 소비). 관문/전투가 호출.
    public void MarkAttacked(bool isPlayer, int lane)
    {
        if (_attacked.Contains((isPlayer, lane)))
            EffectSystem.Instance.ConsumeExtraAttack(isPlayer, lane);
        else
            _attacked.Add((isPlayer, lane));
        OnAttackStateChanged?.Invoke();
    }

    // ── 플레이어 공격 ─────────────────────────────────────────────

    public IEnumerator PlayerAttackLane(int lane)
    {
        if (!CanAttack(lane)) yield break;

        CardData attacker = FieldManager.Instance.GetUnit(true, lane);
        if (attacker == null) yield break;

        CardData defender = FieldManager.Instance.GetUnit(false, lane);

        bool breakthrough = !EffectSystem.Instance.CanDefend(attacker, defender ?? attacker)
                         || EffectSystem.Instance.HasGrantedBreakthrough(true, lane, defender)
                         || (defender != null && EffectSystem.Instance.IsDefendDisabled(false, lane)); // 방어 불가 상태
        if (defender != null && breakthrough)
        {
            Debug.Log($"[돌파] {attacker.CardName} → {defender.CardName} 방어 불가");
            ProcessDirectAttack(true, lane, attacker);
        }
        else if (defender != null)
        {
            // 방어 결정을 중개소로 (방어자=상대, 지금은 AI=false → 자동. 3단계엔 원격 인간)
            bool aiBlocks;
            if (IsDuelist(true, lane, attacker))
            {
                aiBlocks = true; // 듀얼리스트: 무조건 방어
            }
            else
            {
                bool decided = false, result = false;
                var pw = PreviewPowers(true, lane, attacker, lane, defender); // 공격자=플레이어
                ChoiceBroker.DecideDefense(false, attacker, defender,
                    autoDecide: () => AIDecideDefense(pw.atk, pw.def),
                    onDecided: c => { result = c; decided = true; },
                    attackerPower: pw.atk, defenderPower: pw.def);
                yield return new WaitUntil(() => decided);
                aiBlocks = result;
            }
            // 인쇄값이 아니라 실제 판정에 쓰인 수치를 남긴다 (버프/아이템/리더 패시브 반영)
            var logPw = PreviewPowers(true, lane, attacker, lane, defender);
            Debug.Log($"[방어 판단] {(aiBlocks ? "방어" : "패스")} " +
                      $"(공격:{logPw.atk} vs 방어:{logPw.def})");

            if (aiBlocks)
                ResolveEncounter(true, lane, attacker, defender);
            else
                ProcessDirectAttack(true, lane, attacker); // 패스 → 다이렉트
        }
        else
        {
            // 빈 레인 — AI의 인접 가디언 방벽 방어 판단
            var wall = DecideAIGuardianWall(lane, attacker);
            if (wall.lane >= 0)
            {
                var guardian = FieldManager.Instance.GetUnit(false, wall.lane);
                if (wall.cost < 0)
                {
                    ToastView.Show($"{NetHub.OpponentLabel}: {guardian.CardName} 가디언 상쇄 발동!");
                    EffectSystem.Instance.ProcessGuardianCounter(false, wall.lane);
                }
                else
                {
                    ToastView.Show($"{NetHub.OpponentLabel}: {guardian.CardName} 가디언 방벽 발동! (패 {wall.cost}장 트래시)");
                    EffectActions.DiscardHand(false, wall.cost);
                }
                ResolveEncounterCross(true, lane, wall.lane, attacker, guardian);
            }
            else
            {
                ProcessDirectAttack(true, lane, attacker);
            }
        }

        // 트리거로 프라이즈 선택 UI가 떴으면 플레이어가 고를 때까지 대기
        yield return new WaitWhile(() => SkillZoneSlot.IsTriggerTargeting);

        // 추가 공격 소모 또는 공격 기록 (호스트/플레이어 측)
        MarkAttacked(true, lane);

        // 3c: 플레이어 공격(코루틴) 완료 후 결과 상태 복제
        if (NetHub.IsOnline && NetHub.IsAuthority) NetHub.BroadcastState();
    }

    // AI의 가디언 방벽 방어 판단: 인접 가디언이 이길 수 있을 때만 (lane -1 = 방어 안 함)
    private (int lane, int cost) DecideAIGuardianWall(int attackedLane, CardData attacker)
    {
        foreach (var (glane, cost) in EffectSystem.Instance.GetGuardianWallOptions(false, attackedLane))
        {
            var guardian = FieldManager.Instance.GetUnit(false, glane);
            if (guardian == null) continue;
            int gPower = GetEffectivePower(false, glane, guardian)
                       + EffectSystem.Instance.GetDefenderPowerBonus(false, glane);
            int aPower = GetEffectivePower(true, attackedLane, attacker);
            if (gPower > aPower) return (glane, cost);
        }
        return (-1, 0);
    }

    // ── AI 공격 ───────────────────────────────────────────────────

    public IEnumerator ProcessAIAttackPhase()
    {
        for (int lane = 0; lane < 3; lane++)
        {
            CardData attacker = FieldManager.Instance.GetUnit(false, lane);
            if (attacker == null) continue;
            if (!EffectSystem.Instance.CanUnitAttack(false, lane, attacker))
            {
                Debug.Log($"[AI] 레인{lane} {attacker.CardName} 공격 불가 상태 — 스킵");
                continue;
            }

            CardData defender = FieldManager.Instance.GetUnit(true, lane);
            // 방어 불가 상태인 플레이어 유닛은 방어 없이 다이렉트 취급
            bool defenderDisabled = defender != null && EffectSystem.Instance.IsDefendDisabled(true, lane);

            // 조우 레인: 파워 비교 후 이길 수 없으면 아이템 장착 시도 → 그래도 안되면 공격 포기
            if (defender != null && EffectSystem.Instance.CanDefend(attacker, defender))
            {
                int aiPower  = GetEffectivePower(false, lane, attacker);
                int plPower  = GetEffectivePower(true,  lane, defender);

                if (aiPower < plPower)
                {
                    // BT01 광전사: 이길 수 없어도 조우 유닛이 있으면 반드시 공격
                    bool berserker = EffectSystem.Instance.IsBerserker(false, lane, attacker);
                    bool boosted = false;
                    if (!berserker)
                    {
                        // 아이템으로 역전 가능한지 시도
                        boosted = TryEquipItemForLane(lane, plPower - aiPower);
                        aiPower = GetEffectivePower(false, lane, attacker); // 재계산
                    }

                    if (aiPower < plPower && !berserker)
                    {
                        ToastView.Show($"{NetHub.OpponentLabel}: 레인{lane + 1} 공격 포기 ({attacker.CardName})");
                        Debug.Log($"[AI] 레인{lane} 공격 포기: AI({aiPower}) < 플레이어({plPower})");
                        continue; // 이길 수 없으면 공격 안 함
                    }
                    if (boosted)
                        Debug.Log($"[AI] 레인{lane} 아이템 장착 후 공격: AI({aiPower}) >= 플레이어({plPower})");
                }

                // 돌파 효과/방어 불가 상태로 방어 불가
                if (!EffectSystem.Instance.CanDefend(attacker, defender)
                    || EffectSystem.Instance.HasGrantedBreakthrough(false, lane, defender)
                    || defenderDisabled)
                {
                    ToastView.Show($"{NetHub.OpponentLabel}: {attacker.CardName}이(가) 레인{lane + 1} 돌파 공격!");
                    Debug.Log($"[돌파] {attacker.CardName} → {defender.CardName} 방어 불가");
                    ProcessDirectAttack(false, lane, attacker);
                }
                else if (IsDuelist(false, lane, attacker))
                {
                    // ST10 듀얼리스트: 조우 유닛은 가능하다면 반드시 방어 — 팝업 없이 강제 방어
                    ToastView.Show($"{NetHub.OpponentLabel}: {attacker.CardName}이(가) 레인{lane + 1} 공격! (듀얼리스트 — 강제 방어)");
                    ResolveEncounter(false, lane, attacker, defender);
                }
                else
                {
                    ToastView.Show($"{NetHub.OpponentLabel}: {attacker.CardName}이(가) 레인{lane + 1} 공격!");
                    // 방어자(플레이어)에게 방어 결정 요청 — 중개소 경유
                    bool playerBlocks = false;
                    bool decided      = false;

                    var pw = PreviewPowers(false, lane, attacker, lane, defender); // 공격자=AI
                    ChoiceBroker.DecideDefense(true, attacker, defender,
                        autoDecide: () => AIDecideDefense(pw.atk, pw.def),
                        onDecided: c => { playerBlocks = c; decided = true; },
                        attackerPower: pw.atk, defenderPower: pw.def);

                    yield return new WaitUntil(() => decided);

                    if (playerBlocks)
                        ResolveEncounter(false, lane, attacker, defender);
                    else
                        ProcessDirectAttack(false, lane, attacker);
                }
            }
            else if (defender != null) // 돌파 효과 (CanDefend=false)
            {
                Debug.Log($"[돌파] {attacker.CardName} → {defender.CardName} 방어 불가");
                ProcessDirectAttack(false, lane, attacker);
            }
            else
            {
                // 다이렉트 어택 — 플레이어의 인접 가디언 방벽 방어 기회
                var wallOptions = EffectSystem.Instance.GetGuardianWallOptions(true, lane);
                bool guarded = false;
                if (wallOptions.Count > 0)
                {
                    var (glane, cost) = wallOptions[0]; // 첫 후보 (후보 2개면 첫 번째 우선)
                    var guardian = FieldManager.Instance.GetUnit(true, glane);

                    bool useWall = false, decided = false;
                    // 가디언 방벽: 공격 레인(lane)과 방어 유닛 레인(glane)이 다름
                    var pw = PreviewPowers(false, lane, attacker, glane, guardian);
                    ChoiceBroker.DecideDefense(true, attacker, guardian,
                        autoDecide: () => AIDecideDefense(pw.atk, pw.def),
                        onDecided: c => { useWall = c; decided = true; },
                        attackerPower: pw.atk, defenderPower: pw.def);
                    yield return new WaitUntil(() => decided);

                    if (useWall)
                    {
                        if (cost < 0)
                        {
                            EffectSystem.Instance.ProcessGuardianCounter(true, glane);
                            ToastView.Show($"{guardian.CardName} 가디언 상쇄 발동!");
                        }
                        else
                        {
                            bool discardDone = false;
                            EffectActions.DiscardHand(true, cost, _ => discardDone = true);
                            yield return new WaitUntil(() => discardDone);
                            ToastView.Show($"{guardian.CardName} 가디언 방벽 발동!");
                        }
                        ResolveEncounterCross(false, lane, glane, attacker, guardian);
                        guarded = true;
                    }
                }

                if (!guarded)
                {
                    ToastView.Show($"{NetHub.OpponentLabel}: {attacker.CardName}이(가) 레인{lane + 1} 다이렉트 공격!");
                    ProcessDirectAttack(false, lane, attacker);
                }
            }

            // 트리거로 프라이즈 선택 UI가 떴으면 플레이어가 고를 때까지 대기
            yield return new WaitWhile(() => SkillZoneSlot.IsTriggerTargeting);

            yield return new WaitForSeconds(0.5f);
        }
    }

    // AI 손패의 아이템으로 레인 파워를 높일 수 있는지 시도 (needBoost: 역전에 필요한 파워 차이)
    private bool TryEquipItemForLane(int lane, int needBoost)
    {
        PlayerController ai = PlayerController.AIInstance;
        var items = ai.Hand
            .Where(c => c.Type == CardType.Item
                     && FieldManager.Instance.CanPlayCard(false, c, ai.Size))
            .ToList();

        // ItemPowerBoost가 가장 큰 아이템 우선
        items.Sort((a, b) =>
        {
            int pa = GetItemPowerBoost(a), pb = GetItemPowerBoost(b);
            return pb.CompareTo(pa);
        });

        foreach (var item in items)
        {
            int boost = GetItemPowerBoost(item);
            if (boost <= 0) continue;
            if (!FieldManager.Instance.EquipItem(false, lane, item)) continue;

            ai.RemoveFromHand(item);
            ai.AddToSkillZone(item); // 코스트 차감
            AIHandView.NotifyHandChanged();
            HUDView.NotifyStatusChanged();
            Debug.Log($"[AI] 아이템 {item.CardName}(+{boost}) → 레인{lane} 장착");
            return true;
        }
        return false;
    }

    private int GetItemPowerBoost(CardData item)
    {
        foreach (var et in item.EffectTypes)
        {
            if (et.StartsWith("ItemPowerBoost:") || et.StartsWith("AttackerPowerBoost:"))
            {
                if (int.TryParse(et.Substring(et.IndexOf(':') + 1), out int v)) return v;
            }
        }
        return 0;
    }

    // ── 전투 판정 ─────────────────────────────────────────────────

    private void ResolveEncounter(bool isAttacker, int lane, CardData atk, CardData def)
    {
        // 어태커 효과 발동
        EffectSystem.Instance.OnAttackDeclared(isAttacker, lane, atk);
        EffectSystem.Instance.OnDefendDeclared(!isAttacker, lane, def);

        // BT05-049 DefenderDiscardHitMinusOneEndAttack: 방어 유닛이 공격 히트-1만큼 패 트래시 → 공격 종료(무효)
        if (EffectSystem.Instance.TryDefenderDiscardEndAttack(!isAttacker, lane, atk))
        {
            Debug.Log($"[디펜더] {def.CardName} → 공격 종료 (무효화)");
            return;
        }

        int atkPower = GetEffectivePower(isAttacker, lane, atk);
        int defPower = GetEffectivePower(!isAttacker, lane, def)
                     + EffectSystem.Instance.GetDefenderPowerBonus(!isAttacker, lane);

        Debug.Log($"레인{lane} 조우: {atk.CardName}({atkPower}) vs {def.CardName}({defPower})");

        if (atkPower >= defPower) // 룰북 7.4.3: 공격 파워가 방어 파워 이상이면 방어 유닛 트래시
        {
            TrashUnit(!isAttacker, lane);
            ProcessMutualDestruction(def, isAttacker, lane, atk); // 공멸: 죽은 방어 유닛이 공격 유닛을 끌고 감
            EffectSystem.Instance.OnAttackWon(isAttacker, lane, atk, def);
        }
        else if (defPower > atkPower)
        {
            // DefenderFinisher: 방어 승리 시 공격 유닛도 트래시
            if (EffectSystem.Instance.HasDefenderFinisher(!isAttacker, lane))
            {
                TrashUnit(isAttacker, lane);
                Debug.Log($"[DefenderFinisher] 방어 승리 → 공격 유닛 트래시");
            }
            else
            {
                TrashUnit(isAttacker, lane);
            }
            ProcessMutualDestruction(atk, !isAttacker, lane, def); // 공멸: 죽은 공격 유닛이 방어 유닛을 끌고 감
        }
        else
        {
            TrashUnit(isAttacker, lane);
            TrashUnit(!isAttacker, lane);
            Debug.Log("동점 — 양쪽 트래시");
        }

        // ST10 사막의 꽃 실비아: 공격/방어한 전투가 끝날 때 (살아남았다면) 자기 트래시
        TrashSelfIfPassive(isAttacker, lane, atk);
        TrashSelfIfPassive(!isAttacker, lane, def);

        EffectSystem.Instance.OnAttackEnd(isAttacker, lane);
    }

    // 공멸(ExitMutualDestruction) — ST03-007/017, BT01-067, BT02-024
    //  "이 유닛을 전투로 트래시한 상대 유닛의 코스트가 이 유닛의 코스트 이하라면 그 유닛을 트래시한다"
    //  ※ 전투 트래시 한정이라 EffectSystem 엑시트 핸들러가 아니라(트래시한 상대 정보가 없음) 여기서 처리한다.
    //  ※ killer가 이미 트래시된 경우는 건너뛴다 — 동점 양패나 양쪽 다 공멸일 때의 무한 연쇄를 끊는 가드.
    private void ProcessMutualDestruction(CardData dead, bool killerIsPlayer, int killerLane, CardData killer)
    {
        if (dead == null || killer == null) return;
        if (!dead.EffectTypes.Contains("ExitMutualDestruction")) return;
        if (killer.Cost > dead.Cost) return;
        if (FieldManager.Instance.GetUnit(killerIsPlayer, killerLane) != killer) return; // 이미 사라짐
        Debug.Log($"[공멸] {dead.CardName}({dead.Cost}코) → {killer.CardName}({killer.Cost}코) 트래시");
        TrashUnit(killerIsPlayer, killerLane);
    }

    // BT07-085: 아이템 액티브:어택 — 인접 레인의 상대 유닛을 대상으로 강제 전투(그 유닛만 방어 가능, 방어 거부 불가)
    public void ForceAttackAdjacentLane(bool isPlayer, int atkLane, int targetLane)
    {
        var attacker = FieldManager.Instance.GetUnit(isPlayer, atkLane);
        var defender = FieldManager.Instance.GetUnit(!isPlayer, targetLane);
        if (attacker == null || defender == null) return;
        ResolveEncounterCross(isPlayer, atkLane, targetLane, attacker, defender);
        EffectSystem.Instance.DisableAttack(isPlayer, atkLane, BoostUntil.EndOfTurn);
    }

    // 크로스 레인 전투 (가디언 방벽 — 공격 레인과 방어 유닛 레인이 다름)
    private void ResolveEncounterCross(bool isAttacker, int atkLane, int defLane, CardData atk, CardData def)
    {
        EffectSystem.Instance.OnAttackDeclared(isAttacker, atkLane, atk);
        EffectSystem.Instance.OnDefendDeclared(!isAttacker, defLane, def);

        int atkPower = GetEffectivePower(isAttacker, atkLane, atk);
        int defPower = GetEffectivePower(!isAttacker, defLane, def)
                     + EffectSystem.Instance.GetDefenderPowerBonus(!isAttacker, defLane);

        Debug.Log($"[가디언 방벽] 레인{atkLane}→{defLane}: {atk.CardName}({atkPower}) vs {def.CardName}({defPower})");

        if (atkPower >= defPower) // 동점 = 공격 승 (룰북 7.4.3)
        {
            TrashUnit(!isAttacker, defLane);
            ProcessMutualDestruction(def, isAttacker, atkLane, atk); // 공멸
            EffectSystem.Instance.OnAttackWon(isAttacker, atkLane, atk, def);
        }
        else
        {
            TrashUnit(isAttacker, atkLane);
            if (EffectSystem.Instance.HasDefenderFinisher(!isAttacker, defLane))
                Debug.Log("[DefenderFinisher] 방어 승리 → 공격 유닛 트래시");
            ProcessMutualDestruction(atk, !isAttacker, defLane, def); // 공멸
        }

        TrashSelfIfPassive(isAttacker, atkLane, atk);
        TrashSelfIfPassive(!isAttacker, defLane, def);

        EffectSystem.Instance.OnAttackEnd(isAttacker, atkLane);
    }

    private void ProcessDirectAttack(bool isAttacker, int lane, CardData attacker)
    {
        EffectSystem.Instance.OnAttackDeclared(isAttacker, lane, attacker);

        PlayerController target = isAttacker
            ? PlayerController.AIInstance
            : PlayerController.PlayerInstance;

        int hit = EffectSystem.Instance.GetEffectiveHit(isAttacker, lane, attacker);
        Debug.Log($"[다이렉트] {attacker.CardName} → {target.name} {hit}대미지");
        target.TakeDamage(hit);

        // 룰북: 침투[N] — 방어당하지 않은 공격(다이렉트/돌파/방어 패스)이므로 여기서 발동
        EffectSystem.Instance.OnAttackUnblocked(isAttacker, lane, attacker);

        // ST10 사막의 꽃 실비아: 공격한 전투가 끝날 때 (살아남았다면) 자기 트래시
        TrashSelfIfPassive(isAttacker, lane, attacker);

        EffectSystem.Instance.OnAttackEnd(isAttacker, lane);
    }

    // ST10 사막의 꽃 실비아: 이 유닛이 해당 전투에서 살아남았고 PassiveSelfTrashAfterCombat을 가졌다면 트래시
    private void TrashSelfIfPassive(bool isPlayer, int lane, CardData card)
    {
        if (card != null
            && card.EffectTypes.Contains("PassiveSelfTrashAfterCombat")
            && FieldManager.Instance.GetUnit(isPlayer, lane) == card)
            TrashUnit(isPlayer, lane);
    }

    // ST10 듀얼리스트: 이 유닛의 조우 유닛은 가능하다면 반드시 방어해야 한다 (ST06 리나크처럼 이번 턴만 부여되는 경우도 포함)
    private bool IsDuelist(bool isPlayer, int lane, CardData attacker) =>
        attacker.EffectTypes.Contains("AttackerDuelist")
        || EffectSystem.Instance.HasGrantedDuelist(isPlayer, lane)
        // BT07-086 스팅어 미사일: 아이템 어태커 듀얼리스트
        || FieldManager.Instance.GetEquippedItems(isPlayer, lane).Any(it => it.EffectTypes.Contains("ItemAttackerDuelist"));

    // 호출부가 PreviewPowers로 계산해 둔 "실제 전투에 쓰이는 수치"를 그대로 받는다.
    // ResolveEncounter의 승패 판정(atkPower >= defPower면 방어 유닛 트래시)과 같은 식이라
    // AI는 이길 수 있을 때만 방어한다.
    private bool AIDecideDefense(int attackerPower, int defenderPower)
        => defenderPower > attackerPower; // 동점이면 방어 유닛이 트래시되므로 방어 안 함

    // ── 유틸 ─────────────────────────────────────────────────────

    private void TrashUnit(bool isPlayer, int lane)
    {
        CardData card = FieldManager.Instance.GetUnit(isPlayer, lane);
        if (card == null) return;

        // BT02 ArmedOnTrashSacrificeItemSurvive: 아이템 희생으로 생존
        if (EffectSystem.Instance.TryArmedSacrificeItemSurvive(isPlayer, lane, card)) return;

        var equippedItems = new List<CardData>(FieldManager.Instance.GetEquippedItems(isPlayer, lane));
        FieldManager.Instance.RemoveUnit(isPlayer, lane, willResolveExit: true);

        PlayerController owner = isPlayer
            ? PlayerController.PlayerInstance
            : PlayerController.AIInstance;
        owner.AddToTrash(card);

        EffectSystem.Instance.OnUnitTrashed(isPlayer, lane, card); // 엑시트 효과
        EffectSystem.Instance.OnItemsTrashedWithUnit(isPlayer, equippedItems, card); // 장착 아이템 엑시트(ST09 생사부, ST06 흑요석 반지 등)

        Debug.Log($"[트래시] {card.CardName} → {owner.name}");
    }

    // 리더 패시브는 EffectSystem.GetEffectivePower 안으로 통합됨 — 여기서 또 더하면 이중 가산.
    private int GetEffectivePower(bool isPlayer, int lane, CardData card)
        => EffectSystem.Instance.GetEffectivePower(isPlayer, lane, card);

    // 방어 팝업에 보여줄 "실제 전투 수치" 미리 계산 — ResolveEncounter/ResolveEncounterCross의 계산식과 동일하게 유지할 것.
    // 가디언 방벽처럼 공격 레인과 방어 레인이 다른 경우가 있어 레인을 각각 받는다.
    private (int atk, int def) PreviewPowers(bool attackerIsPlayer, int atkLane, CardData attacker,
                                             int defLane, CardData defender)
    {
        int atk = attacker != null ? GetEffectivePower(attackerIsPlayer, atkLane, attacker) : 0;
        int def = 0;
        if (defender != null)
            def = GetEffectivePower(!attackerIsPlayer, defLane, defender)
                + EffectSystem.Instance.GetDefenderPowerBonus(!attackerIsPlayer, defLane);
        return (atk, def);
    }

    // EffectSystem 등 외부에서 호출 가능한 공개 래퍼
    public void TrashUnitPublic(bool isPlayer, int lane) => TrashUnit(isPlayer, lane);

    public void ProcessAttackPhase(bool isAttacker) { }
}
