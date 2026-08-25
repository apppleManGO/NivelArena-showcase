// Assets/Scripts/Managers/EffectSystem.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public partial class EffectSystem : MonoBehaviour
{
    private void ProcessLevelLinkEscapeDeploy(bool isPlayer)
        => RevealAndDeployUnit(isPlayer, 3, 5000, 1);

    // 덱 위 revealCount장 공개 → 유닛 1장 선택해 사이즈 무시 배치(+선택적 이번 턴 버프), 나머지 트래시.
    // ST08 수아(레벨링크 이스케이프)/ST08 같이 한잔하겠어? 공용
    private void RevealAndDeployUnit(bool isPlayer, int revealCount, int buffPower = 0, int buffHit = 0)
    {
        var owner = GetOwner(isPlayer);
        int take = Mathf.Min(revealCount, owner.DrawPile.Count);
        if (take == 0) return;

        var revealed = owner.PeekDeckTop(take);
        owner.RemoveDeckTop(take);

        var unitCandidates = revealed.Where(c => c.Type == CardType.Unit).ToList();
        bool hasEmptyLane = Enumerable.Range(0, 3).Any(i => FieldManager.Instance.GetUnit(isPlayer, i) == null);

        if (unitCandidates.Count == 0 || !hasEmptyLane)
        {
            owner.AddToTrash(revealed);
            EffectActions.RefreshUI();
            Debug.Log("[공개 배치] 배치 가능한 유닛 없음 — 전부 트래시");
            return;
        }

        EffectActions.PickFromCards(isPlayer, "사이즈 무시로 배치할 유닛을 선택하세요", unitCandidates,
            picked =>
            {
                revealed.Remove(picked);
                owner.AddToTrash(revealed);

                int emptyLane = -1;
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { emptyLane = i; break; }

                if (emptyLane >= 0)
                {
                    FieldManager.Instance.PlaceUnit(isPlayer, emptyLane, picked);
                    OnUnitPlaced(isPlayer, emptyLane, picked);
                    if (buffPower != 0 || buffHit != 0)
                        EffectActions.Buff(isPlayer, emptyLane, buffPower, buffHit, BoostUntil.EndOfTurn);
                    Debug.Log($"[공개 배치] {picked.CardName} 배치 (레인{emptyLane})");
                }
                EffectActions.RefreshUI();
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // ── 공격/방어 제한 · 추가 공격 · 가디언 방벽 ─────────────────

    // 해당 레인 유닛 공격 금지 (EndOfTurn = 이번 턴 / EndOfOpponentTurn = 상대 턴 끝까지)
    public void DisableAttack(bool isPlayer, int lane, BoostUntil until)
    {
        var set = until == BoostUntil.EndOfOpponentTurn ? _attackDisabledOppTurn : _attackDisabledTurn;
        set.Add(Key(isPlayer, lane));
        Debug.Log($"[제한] {(isPlayer ? "플레이어" : "AI")} 레인{lane} 공격 불가 ({until})");
    }

    // BT06-061 해변의 정의 미카엘라: 상대 턴 끝까지, 상대는 이 레인에 N코스트 이상 유닛 배치 불가
    // BT07-015: 상대 턴 끝까지, 상대는 이 레인에 N코스트 이하 유닛 배치 불가 (부여형)
    // BT07-033: 포지션:사이드 — 사이드에 있는 한 계속, 상대는 이 레인에 N코스트 이하 유닛 배치 불가 (라이브 스캔)
    public bool IsLaneDeployBlocked(bool isPlayer, int lane, CardData card)
    {
        if (card == null || card.Type != CardType.Unit) return false;
        if (_bt06DeployBlockHighCost.TryGetValue(Key(isPlayer, lane), out int highThreshold) && card.Cost >= highThreshold)
            return true;
        if (_bt07DeployBlockLowCost.TryGetValue(Key(isPlayer, lane), out int lowThreshold) && card.Cost <= lowThreshold)
            return true;
        // 상대(!isPlayer) 필드의 사이드 유닛이 PositionSideBlockLowCostDeploy를 가지고 있으면 실시간 확인
        var blocker = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (blocker != null && IsSideLane(lane))
            foreach (var et in blocker.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "PositionSideBlockLowCostDeploy" && card.Cost <= value) return true;
            }
        // SB01-009: 레벨링크 — 상대 리더 레벨이 조건 이상이면 그 유닛이 있는 레인에 저비용 유닛 배치 불가 (실시간 확인)
        if (blocker != null)
            foreach (var et in blocker.EffectTypes)
            {
                if (!et.StartsWith("LevelLinkBlockLowCostDeploy")) continue;
                var parts = et.Split(':');
                int lvlReq = int.Parse(parts[1]);
                int costCap = int.Parse(parts[2]);
                if (GetOwner(!isPlayer).LeaderLevel >= lvlReq && card.Cost <= costCap) return true;
            }
        return false;
    }

    // 해당 레인 유닛이 공격 가능한가 (공격 금지 상태 + PassiveCannotAttack 체크)
    public bool CanUnitAttack(bool isPlayer, int lane, CardData card)
    {
        if (card == null) return true;
        if (card.EffectTypes.Contains("PassiveCannotAttack")) return false; // ST11 셀리아
        // BT07-071: 필드에 〈이브〉가 없거나 리더 레벨이 N 이하면 공격 불가
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (type != "PassiveCannotAttackUnlessEve") continue;
            bool hasEve = false;
            for (int i = 0; i < 3; i++)
            {
                var u = FieldManager.Instance.GetUnit(isPlayer, i);
                if (u != null && IsEveCard(u)) { hasEve = true; break; }
            }
            if (!hasEve || GetOwner(isPlayer).LeaderLevel <= value) return false;
        }
        string key = Key(isPlayer, lane);
        return !_attackDisabledTurn.Contains(key) && !_attackDisabledOppTurn.Contains(key);
    }

    // 해당 레인 유닛 이번 턴 방어 불가 (ST11 블랙 오더)
    public void DisableDefendLane(bool isPlayer, int lane)
    {
        _defendDisabledTurn.Add(Key(isPlayer, lane));
        Debug.Log($"[제한] {(isPlayer ? "플레이어" : "AI")} 레인{lane} 방어 불가 (이번 턴)");
    }

    public bool IsDefendDisabled(bool isPlayer, int lane)
        => _defendDisabledTurn.Contains(Key(isPlayer, lane));

    // 어택 페이즈 추가 공격 부여/소모
    public void AddExtraAttack(bool isPlayer, int lane, int count = 1)
    {
        string key = Key(isPlayer, lane);
        _extraAttacks[key] = _extraAttacks.GetValueOrDefault(key) + count;
        Debug.Log($"[부여] 레인{lane} 추가 공격 +{count}");
    }

    public bool HasExtraAttack(bool isPlayer, int lane)
        => _extraAttacks.GetValueOrDefault(Key(isPlayer, lane)) > 0;

    public void ConsumeExtraAttack(bool isPlayer, int lane)
    {
        string key = Key(isPlayer, lane);
        if (_extraAttacks.GetValueOrDefault(key) > 0)
            _extraAttacks[key]--;
    }

    // 가디언 방벽: 공격당한 레인의 인접 레인에서 방어 가능한 (레인, 트래시 비용) 목록
    // 조건: GuardianWall:N 보유 + 방어측 패가 N장 이상
    // GuardianSacrifice: 자기 자신 트래시로 방어 (패 비용 대신 유닛 자기희생)
    public List<(int lane, int cost)> GetGuardianWallOptions(bool defenderIsPlayer, int attackedLane)
    {
        var options = new List<(int, int)>();
        var owner = GetOwner(defenderIsPlayer);
        foreach (int adj in new[] { attackedLane - 1, attackedLane + 1 })
        {
            if (adj < 0 || adj > 2) continue;
            var unit = FieldManager.Instance.GetUnit(defenderIsPlayer, adj);
            if (unit == null) continue;
            // 유닛 + 장착 아이템의 effectTypes 모두 검사 (BT05 ItemGuardianWall)
            var sources = unit.EffectTypes.Concat(
                FieldManager.Instance.GetEquippedItems(defenderIsPlayer, adj).SelectMany(it => it.EffectTypes));
            foreach (var et in sources)
            {
                var (type, value) = Parse(et);
                if ((type == "GuardianWall" || type == "ItemGuardianWall") && owner.Hand.Count >= Mathf.Max(value, 1))
                    options.Add((adj, Mathf.Max(value, 1)));
                // GuardianSacrifice: 패 비용 없이 자기 유닛 트래시로 방어 (비용 0으로 표기, 별도 처리 필요)
                else if (type == "GuardianSacrifice")
                    options.Add((adj, 0)); // cost=0 = 자기희생
                // BT06-084 사신의 수의: 가디언 상쇄 — 장착 아이템 자체를 트래시해서 방어 (cost=-1로 표기)
                else if (type == "ItemGuardianCounter")
                    options.Add((adj, -1));
            }
        }
        return options;
    }

    // GuardianSacrifice 방어 시 해당 유닛 자기 트래시
    public void ProcessGuardianSacrifice(bool defenderIsPlayer, int wallLane)
    {
        var unit = FieldManager.Instance.GetUnit(defenderIsPlayer, wallLane);
        if (unit != null && unit.EffectTypes.Any(e => e.StartsWith("GuardianSacrifice")))
        {
            EffectActions.TrashUnit(defenderIsPlayer, wallLane);
            Debug.Log($"[가디언 희생] {unit.CardName} 자기 트래시로 방어");
        }
    }

    // 효과 대미지 발생 통지 (EffectActions.DamageOpponent가 호출) — 몽환 나비 연동
    public void NotifyEffectDamage(bool byPlayer)
    {
        if (_effectDamageDraw[Idx(byPlayer)])
        {
            GetOwner(byPlayer).DrawCard(1);
            HandView.Instance?.RefreshHand();
            Debug.Log("[몽환 나비] 효과 대미지 → 드로우1");
        }
    }

    // ── 부스트 관리 ──────────────────────────────────────────────

    public void AddAttackBoost(bool isPlayer, int lane, int amount)
    {
        string key = Key(isPlayer, lane);
        _attackBoosts[key] = _attackBoosts.GetValueOrDefault(key) + amount;
        if (amount < 0) CheckLethalPower();
        LaneSlot.RefreshAllLanes(); // 카드 수치변동 텍스트(+N/-N) 즉시 반영
    }

    public void AddTurnBoost(bool isPlayer, int lane, int amount)
    {
        string key = Key(isPlayer, lane);
        _turnBoosts[key] = _turnBoosts.GetValueOrDefault(key) + amount;
        if (amount < 0) CheckLethalPower();
        LaneSlot.RefreshAllLanes(); // 카드 수치변동 텍스트(+N/-N) 즉시 반영
    }

    // 룰: 유닛의 공격력이 0 이하가 되면 즉시 트래시(사망). 파워를 깎는 효과 적용 후 호출.
    //  · GetEffectivePower가 0에 클램프되므로 ==0이면 실질 0 이하. 유닛 기본파워는 전부 >0이라
    //    디버프(부스트 음수/디버프 아우라)로만 0이 됨 → 배치 즉시 사망 오작동 없음.
    private bool _checkingLethal;
    public void CheckLethalPower()
    {
        if (_checkingLethal) return; // 트래시→엑시트효과→부스트 재귀 방지
        _checkingLethal = true;
        try
        {
            var lethal = new System.Collections.Generic.List<(bool p, int l)>();
            for (int s = 0; s < 2; s++)
            {
                bool p = s == 0;
                for (int l = 0; l < 3; l++)
                {
                    var u = FieldManager.Instance.GetUnit(p, l);
                    if (u != null && GetEffectivePower(p, l, u) <= 0) lethal.Add((p, l));
                }
            }
            foreach (var (p, l) in lethal)
            {
                var u = FieldManager.Instance.GetUnit(p, l);
                if (u == null) continue;                       // 그새 제거됨
                if (GetEffectivePower(p, l, u) > 0) continue;  // 그새 회복됨
                Debug.Log($"[룰] {(p ? "플레이어" : "AI")} 레인{l} {u.CardName} 공격력 0 → 트래시(사망)");
                EffectActions.TrashUnit(p, l);
            }
        }
        finally { _checkingLethal = false; }
    }

    public void AddTurnHitBoost(bool isPlayer, int lane, int amount)
    {
        string key = Key(isPlayer, lane);
        _turnHitBoosts[key] = _turnHitBoosts.GetValueOrDefault(key) + amount;
    }

    // 블록 라이브러리(EffectActions.Buff)용 기간 지정 버프 — 기존 저장소로 라우팅
    public void AddBoost(bool isPlayer, int lane, int power, int hit, BoostUntil until)
    {
        string key = Key(isPlayer, lane);
        switch (until)
        {
            case BoostUntil.EndOfTurn:
                if (power != 0) _turnBoosts[key]    = _turnBoosts.GetValueOrDefault(key) + power;
                if (hit   != 0) _turnHitBoosts[key] = _turnHitBoosts.GetValueOrDefault(key) + hit;
                break;
            case BoostUntil.EndOfOpponentTurn:
                if (power != 0) _opponentTurnBoosts[key]    = _opponentTurnBoosts.GetValueOrDefault(key) + power;
                if (hit   != 0) _opponentTurnHitBoosts[key] = _opponentTurnHitBoosts.GetValueOrDefault(key) + hit;
                break;
            case BoostUntil.EndOfAttack:
                if (power != 0) _attackBoosts[key] = _attackBoosts.GetValueOrDefault(key) + power;
                // 공격 단위 히트 부스트 저장소는 아직 없음 — 이번 턴으로 근사 (공격 후 턴 종료 시 제거)
                if (hit   != 0) _turnHitBoosts[key] = _turnHitBoosts.GetValueOrDefault(key) + hit;
                break;
        }
        if (power < 0) CheckLethalPower(); // 디버프면 0 이하 유닛 사망 체크
        LaneSlot.RefreshAllLanes(); // 카드 수치변동 텍스트(+N/-N) 즉시 반영
    }

    private int GetAttackBoost(bool isPlayer, int lane)
        => _attackBoosts.GetValueOrDefault(Key(isPlayer, lane));

    private int GetTurnBoost(bool isPlayer, int lane)
        => _turnBoosts.GetValueOrDefault(Key(isPlayer, lane));

    private string Key(bool isPlayer, int lane)
        => $"{(isPlayer ? "p" : "a")}_{lane}";

    // ── 레인 귀속 상태 정리/이동 ──────────────────────────────────
    // 부스트·부여 상태의 키는 Key(isPlayer,lane) = "자리" 기준이지 "유닛" 기준이 아니다.
    // 따라서 유닛이 레인을 떠날 때 치워주지 않으면, 같은 턴에 그 자리를 다시 채운
    // 새 유닛이 이전 유닛의 버프·디버프·제한을 그대로 물려받는다.
    // 반대로 유닛이 이동(BT07)하면 상태가 따라가야 하는데, 자리 기준이라 제자리에 남는다.
    //
    // ★ 새 레인 귀속 상태를 추가하면 ForEachLaneState에 한 줄만 추가할 것.
    //   정리(ClearLaneState)와 이동(MoveLaneState)이 자동으로 함께 지원된다.

    // 레인 키 하나에 적용할 동작 (제거 / 두 레인 맞바꾸기)
    private interface ILaneStateOp
    {
        void Apply<T>(Dictionary<string, T> dict);
        void Apply(HashSet<string> set);
    }

    private readonly struct ClearOp : ILaneStateOp
    {
        private readonly string _key;
        public ClearOp(string key) { _key = key; }
        public void Apply<T>(Dictionary<string, T> dict) => dict.Remove(_key);
        public void Apply(HashSet<string> set) => set.Remove(_key);
    }

    private readonly struct SwapOp : ILaneStateOp
    {
        private readonly string _a, _b;
        public SwapOp(string a, string b) { _a = a; _b = b; }
        public void Apply<T>(Dictionary<string, T> dict)
        {
            bool ha = dict.TryGetValue(_a, out var va);
            bool hb = dict.TryGetValue(_b, out var vb);
            if (hb) dict[_a] = vb; else dict.Remove(_a);
            if (ha) dict[_b] = va; else dict.Remove(_b);
        }
        public void Apply(HashSet<string> set)
        {
            bool ha = set.Contains(_a), hb = set.Contains(_b);
            if (hb) set.Add(_a); else set.Remove(_a);
            if (ha) set.Add(_b); else set.Remove(_b);
        }
    }

    // 레인에 귀속되는 모든 상태에 op를 적용.
    // includeExitGrants=false면 "트래시된 뒤에야 읽히는" 부여(엑시트 계열)는 건드리지 않는다 —
    // OnUnitTrashed가 RemoveUnit 이후에 실행되므로 여기서 지우면 부여 엑시트가 발동하지 못한다.
    private void ForEachLaneState(ILaneStateOp op, bool includeExitGrants)
    {
        // 파워/히트 부스트
        op.Apply(_attackBoosts);
        op.Apply(_turnBoosts);
        op.Apply(_turnHitBoosts);
        op.Apply(_opponentTurnBoosts);
        op.Apply(_opponentTurnHitBoosts);

        // 공격/방어 제한·부여
        op.Apply(_breakthroughGrants);
        op.Apply(_attackerGrants);
        op.Apply(_attackDisabledTurn);
        op.Apply(_attackDisabledOppTurn);
        op.Apply(_defendDisabledTurn);
        op.Apply(_extraAttacks);
        op.Apply(_grantedDefenderBoost);
        op.Apply(_conditionalBreakthroughGrants);
        op.Apply(_grantedDefenderFinisher);
        op.Apply(_grantedDuelistThisTurn);
        op.Apply(_grantedPenetration);
        op.Apply(_grantedAttackerTrashEncounter);
        op.Apply(_grantedAttackerPlunder);
        op.Apply(_grantedBerserkerToEnemyMinCost);

        // 기타 부여/사용 기록
        op.Apply(_zeroCostGrants);
        op.Apply(_unitActiveUsedThisTurn);
        op.Apply(_deckBottomDamageUsedThisTurn);
        op.Apply(_effectTrashDamageUsedThisTurn);
        op.Apply(_grantedEscapeDamage);
        op.Apply(_grantedPassiveDrawOnEquip);
        op.Apply(_discardDrawUsedThisTurn);
        op.Apply(_itemExpiresEndOfOpponentTurn);

        // 세트별 격리 파일의 레인 귀속 상태
        ForEachLaneStateBT04(op);
        ForEachLaneStateBT06(op);
        ForEachLaneStateBT07(op);
        ForEachLaneStateSB01(op);

        if (includeExitGrants) ForEachExitGrant(op);

        // ※ 의도적 제외 (자리에 걸리는 제한이라 유닛이 떠나도 유지돼야 함):
        //   _bt06DeployBlockHighCost / _bt07DeployBlockLowCost — "이 존에 배치 불가"
        // ※ BT07 이동 추적(_bt07MovedThisTurn/_bt07MoveCountThisTurn/_bt07MoveReactionUsed)은
        //   애초에 카드 인스턴스 키라 자리와 무관 = 손댈 필요 없음
    }

    // 트래시된 뒤 OnUnitTrashed에서 읽히는 부여들 (엑시트 타이밍)
    private void ForEachExitGrant(ILaneStateOp op)
    {
        op.Apply(_grantedExit);
        op.Apply(_grantedExitReturn);
        op.Apply(_grantDamageZoneOnTrash);
        op.Apply(_sb01DamageZoneOnTrashGrant);
    }

    // 유닛이 레인을 떠날 때 호출 (트래시/업그레이드/덱 귀환 등).
    // keepExitGrants=true = 트래시 경로 — 엑시트 부여는 OnUnitTrashed가 소비한 뒤 스스로 정리한다.
    public void ClearLaneState(bool isPlayer, int lane, bool keepExitGrants = false)
    {
        if (lane < 0) return;
        ForEachLaneState(new ClearOp(Key(isPlayer, lane)), includeExitGrants: !keepExitGrants);
    }

    // 엑시트 해결이 끝난 뒤 남은 엑시트 부여 정리 (OnUnitTrashed 종료 시)
    public void ClearExitGrants(bool isPlayer, int lane)
    {
        if (lane < 0) return;
        ForEachExitGrant(new ClearOp(Key(isPlayer, lane)));
    }

    // BT07 이동/스왑: 레인 귀속 상태를 유닛을 따라 함께 옮긴다 (룰북 4.9 — 이동해도 같은 유닛).
    public void MoveLaneState(bool isPlayer, int fromLane, int toLane)
    {
        if (fromLane < 0 || toLane < 0 || fromLane == toLane) return;
        ForEachLaneState(new SwapOp(Key(isPlayer, fromLane), Key(isPlayer, toLane)),
                         includeExitGrants: true);
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    private PlayerController GetOwner(bool isPlayer)
        => isPlayer ? PlayerController.PlayerInstance : PlayerController.AIInstance;

    // 룰북: AttackerBreakthrough:N — 어태커가 N코스트 이하 유닛에게 방어 불가 부여
    public bool CanDefend(CardData attacker, CardData defender)
    {
        foreach (var et in attacker.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (type == "AttackerBreakthrough" && defender.Cost <= value)
                return false;
        }
        return true;
    }

    // "EffectType:1000" → ("EffectType", 1000)
    private (string type, int value) Parse(string effectType)
    {
        int idx = effectType.IndexOf(':');
        if (idx < 0) return (effectType, 0);
        string type = effectType.Substring(0, idx);
        int.TryParse(effectType.Substring(idx + 1), out int value);
        return (type, value);
    }

    // ── BT03 헬퍼 메서드 ──────────────────────────────────────────

    // BT03 트로니-스위트: 암드 엑시트 — 비장착 아군 N장까지 트래시 (재귀)
    private void ArmedExitTrashUnequippedStep(bool isPlayer, CardData source, int remaining, int maxTrash)
    {
        if (remaining <= 0) return;
        var candidates = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u != source && FieldManager.Instance.GetEquippedItems(isPlayer, i).Count == 0)
                candidates.Add(i);
        }
        if (candidates.Count == 0) return;

        if (isPlayer)
        {
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => FieldManager.Instance.GetEquippedItems(isPlayer, l).Count == 0,
                $"트래시할 비장착 아군을 선택하세요 (남은 {remaining}장)",
                pickedLane =>
                {
                    EffectActions.TrashUnit(isPlayer, pickedLane);
                    ArmedExitTrashUnequippedStep(isPlayer, source, remaining - 1, maxTrash);
                },
                onCancel: null);
        }
        else
        {
            // AI: 가장 약한 비장착 유닛부터 트래시
            int lane = candidates.OrderBy(l => FieldManager.Instance.GetUnit(isPlayer, l)?.AttackPower ?? 0).First();
            EffectActions.TrashUnit(isPlayer, lane);
            ArmedExitTrashUnequippedStep(isPlayer, source, remaining - 1, maxTrash);
        }
    }

    // BT03 슈엔: 엔트리 — 아이템 N장을 덱 맨 아래로 이동 (재귀), 이후 코스트합 이하 상대 유닛 트래시
    private void EntryTrashItemsToDeckBottomByCostStep(bool isPlayer, int lane, CardData source, int encounterCostCap, List<CardData> remaining, int movedCostSum)
    {
        if (remaining.Count == 0)
        {
            // 이동 완료 — 조우 유닛 트래시 (코스트합 이하)
            var encounter = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (encounter != null && encounter.Cost <= movedCostSum)
                EffectActions.TrashUnit(!isPlayer, lane);
            return;
        }

        EffectActions.PickFromCards(isPlayer, $"덱 맨 아래로 보낼 아이템을 선택하세요 (코스트합으로 조우 유닛 트래시)", remaining,
            picked =>
            {
                remaining.Remove(picked);
                GetOwner(isPlayer).RemoveFromTrash(picked);
                GetOwner(isPlayer).AddToDeckBottom(picked);
                Debug.Log($"[슈엔 엔트리] {picked.CardName}(코스트{picked.Cost}) 덱 밑으로");
                EntryTrashItemsToDeckBottomByCostStep(isPlayer, lane, source, encounterCostCap, remaining, movedCostSum + picked.Cost);
            },
            onCancel: () =>
            {
                // 취소 시 현재까지 합산 코스트로 판정
                var encounter = FieldManager.Instance.GetUnit(!isPlayer, lane);
                if (encounter != null && encounter.Cost <= movedCostSum)
                    EffectActions.TrashUnit(!isPlayer, lane);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // BT03 아이템 엑시트: 조건부 N코스트 이하 상대 유닛 트래시 (ItemExitConditionalTrashLowCostEnemy)
    private void ProcessItemExitConditionalTrashLowCostEnemy(bool isPlayer, int lane, CardData item)
    {
        foreach (var et in item.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (type != "ItemExitConditionalTrashLowCostEnemy") continue;
            var encounter = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (encounter != null && encounter.Cost <= value)
            {
                EffectActions.TrashUnit(!isPlayer, lane);
                Debug.Log($"[아이템 엑시트] {item.CardName} → 상대 {encounter.CardName}({encounter.Cost}코 ≤ {value}) 트래시");
            }
        }
    }

    // BT03 수면: 아이템 엑시트 — 패1 트래시 후 이 유닛을 다시 배치 (ItemExitDiscardSelfDeploy)
    private void ProcessItemExitDiscardSelfDeploy(bool isPlayer, int lane, CardData item, CardData unit)
    {
        bool hasEffect = item.EffectTypes.Any(e => e.StartsWith("ItemExitDiscardSelfDeploy"));
        if (!hasEffect) return;

        EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
        {
            if (discarded < 1) return;
            // 유닛이 이미 트래시에 있으므로 꺼내서 빈 레인에 재배치
            GetOwner(isPlayer).RemoveFromTrash(unit);
            int emptyLane = -1;
            for (int i = 0; i < 3; i++)
                if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { emptyLane = i; break; }
            if (emptyLane >= 0)
            {
                FieldManager.Instance.PlaceUnit(isPlayer, emptyLane, unit);
                OnUnitPlaced(isPlayer, emptyLane, unit);
                Debug.Log($"[아이템 엑시트] {item.CardName} → {unit.CardName} 패1 트래시 후 레인{emptyLane}에 재배치");
            }
            else
                GetOwner(isPlayer).AddToTrash(unit); // 빈 레인 없으면 다시 트래시
            EffectActions.RefreshUI();
        });
    }

    // BT03 새벽: 아이템 패시브 — 유닛이 트래시될 때 자기 자신도 트래시, 유닛을 장착 아이템과 함께 패로 귀환
    // (ItemPassiveSacrificeSelfReturnUnitWithItems) — OnItemsTrashedWithUnit에서 처리
    // ※ 이 효과는 "아이템이 먼저 자기 트래시 → 유닛을 트래시에서 패로"가 아니라,
    //   "유닛이 트래시될 때 아이템도 함께 트래시되며 유닛을 패로 되돌린다"는 근사 처리
}
