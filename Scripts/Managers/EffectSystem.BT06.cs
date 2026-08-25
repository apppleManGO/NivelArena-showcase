// Assets/Scripts/Managers/EffectSystem.BT06.cs
// ─────────────────────────────────────────────────────────────────────────
// BT06 effectType 구현 — 격리 파일.
//  테마: 체인(어태커 조건부) + 스킬존 조건부 버프 액티브(메인/어택, 버프:1·2) + 광전사 부여 + 디펜더.
//  핸들러는 기존 레지스트리에 InitBT06Handlers()로 등록 (Awake 호출).
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class EffectSystem : MonoBehaviour
{
    // ── BT06 전용 상태 ──────────────────────────────────────────
    // 광전사 오라: Key(오라 소유자,lane) — 그 레인의 조우 유닛(상대)은 광전사 취급, 상대 턴 끝까지
    private HashSet<string> _bt06BerserkAura = new();
    // 전체 적 광전사: Idx(당하는 쪽) — 상대 턴 끝까지
    private bool[] _bt06AllEnemiesBerserker = new bool[2];
    // 레인 히트=1 오라: Key(오라 소유자,lane) — 그 레인에 있는/배치되는 상대 유닛 히트=1, 상대 턴 끝까지
    private HashSet<string> _bt06HitSetOneAura = new();
    // 고비용 배치 차단: Key(차단당하는 쪽,lane) → 코스트 임계값, 상대 턴 끝까지
    private Dictionary<string, int> _bt06DeployBlockHighCost = new();
    // 어태커 효과 발동 불가: Idx(막힌 쪽) — 상대 턴 끝까지
    private bool[] _bt06AttackerEffectsDisabled = new bool[2];
    // 부여된 "공격 시 상대 드로우1": Key(공격자,lane) — 상대 턴 끝까지
    private HashSet<string> _bt06AttackerDrawPenalty = new();
    // 스킬존에서 임시 0코스트화된 카드 (이번 턴)
    private HashSet<CardData> _bt06ZeroCostSkillZoneCards = new();
    // 상대 드로우 감지 패시브 1회 사용: Key(패시브 소유자,lane)
    private HashSet<string> _bt06DrawOnOppDrawUsed = new();

    // 레인 귀속 상태 정리/이동 훅 (ForEachLaneState에서 호출 — Boosts.cs 주석 참고)
    // 오라 2종은 "오라 소유자,lane" 키라 소유자가 떠나면 사라지는 게 맞다
    // (오라를 받는 상대 유닛이 죽어도 키가 반대편이라 영향 없음 = 의도대로).
    // ※ _bt06DeployBlockHighCost는 "그 존에 배치 불가"라 자리 귀속 = 제외
    private void ForEachLaneStateBT06(ILaneStateOp op)
    {
        op.Apply(_bt06BerserkAura);
        op.Apply(_bt06HitSetOneAura);
        op.Apply(_bt06AttackerDrawPenalty);
        op.Apply(_bt06DrawOnOppDrawUsed);
        op.Apply(_bt06ChainInfiltrateThisAttack);
    }

    private void InitBT06Handlers()
    {
        // 어태커
        _attackerHandlers["AttackerDebuffEncounter"]       = BT06_AttackerDebuffEncounter;
        _attackerHandlers["AttackerBuffAllAllies"]         = BT06_AttackerBuffAllAllies;
        _attackerHandlers["ChainAttackerDebuffAll"]        = BT06_ChainAttackerDebuffAll;
        _attackerHandlers["ChainAttackerDebuffEncounterDraw"] = BT06_ChainAttackerDebuffEncounterDraw;
        _attackerHandlers["ChainAttackerInfiltratePower"]  = BT06_ChainAttackerInfiltratePower;
        _attackerHandlers["ChainAttackerPiercePower"]      = BT06_ChainAttackerPiercePower;
        _attackerHandlers["ChainAttackerPowerBoost"]       = BT06_ChainAttackerPowerBoost;

        // 엔트리
        _entryHandlers["EntryRevealSearchCardType"]  = BT06_EntryRevealSearchCardType;
        _entryHandlers["EntryDebuffAllEnemiesDraw"]  = BT06_EntryDebuffAllEnemiesDraw;
        _entryHandlers["EntryDisableAttackLowHit"]   = BT06_EntryDisableAttackLowHit;
        _entryHandlers["EntryDiscardAllDrawTo"]      = BT06_EntryDiscardAllDrawTo;
        _entryHandlers["EntryGainBerserkEncounterAura"] = BT06_EntryGainBerserkEncounterAura;
        _entryHandlers["EntryMillActivateSkill"]     = BT06_EntryMillActivateSkill;
        _entryHandlers["EntryRecoverSkillLowCost"]   = BT06_EntryRecoverSkillLowCost;
        _entryHandlers["EntryZeroCostSkillDebuffEncounter"] = BT06_EntryZeroCostSkillDebuffEncounter;
        _entryHandlers["EntryZeroCostSkillDraw"]     = BT06_EntryZeroCostSkillDraw;
        _entryHandlers["EntryZeroCostSkillLowCost"]  = BT06_EntryZeroCostSkillLowCost;
        _entryHandlers["EntryBlockHighCostDeployLane"] = BT06_EntryBlockHighCostDeployLane;

        // 스킬
        _skillHandlers["RevealSearchCardType"]     = BT06_RevealSearchCardType;
        _skillHandlers["DiscardHandDrawTo"]        = BT06_DiscardHandDrawTo;
        _skillHandlers["RecoverUnitByPower"]       = BT06_RecoverUnitByPower;
        _skillHandlers["RecoverUnitByPowerMin"]    = BT06_RecoverUnitByPowerMin;
        _skillHandlers["GrantDuelist"]             = BT06_GrantDuelist;
        _skillHandlers["DebuffEnemyDrawIfTrashed"] = BT06_DebuffEnemyDrawIfTrashed;
        _skillHandlers["SkillForceAttackLowCost"]  = BT06_SkillForceAttackLowCost;
        _skillHandlers["DebuffEnemyPerHandDiff"]   = BT06_DebuffEnemyPerHandDiff;
        _skillHandlers["DisableExitDebuffEnemy"]   = BT06_DisableExitDebuffEnemy;
        _skillHandlers["GrantExtraAttackLowCost"]  = BT06_GrantExtraAttackLowCost;
        _skillHandlers["DebuffTwoEnemies"]         = BT06_DebuffTwoEnemies;
        _skillHandlers["DiscardDebuffPerCard"]     = BT06_DiscardDebuffPerCard;
        _skillHandlers["DiscardOneDraw"]           = BT06_DiscardOneDraw;
        _skillHandlers["GrantBerserkEncounterAura"] = BT06_GrantBerserkEncounterAura;
        _skillHandlers["GrantEnemiesAttackerDrawPenalty"] = BT06_GrantEnemiesAttackerDrawPenalty;
        _skillHandlers["GrantLaneHitSetOne"]       = BT06_GrantLaneHitSetOne;
        _skillHandlers["DrawBothPenalty"]          = BT06_DrawBothPenalty;
        _skillHandlers["ActivateAllyEntry"]        = BT06_ActivateAllyEntry;
        _skillHandlers["GrantAllEnemiesBerserk"]   = BT06_GrantAllEnemiesBerserk;
        _skillHandlers["DrawPerDefenderDisableAttackers"] = BT06_DrawPerDefenderDisableAttackers;
        _skillHandlers["DiscardSkillDamage"]       = BT06_DiscardSkillDamage;
        _skillHandlers["ReturnSkillsDamage"]       = BT06_ReturnSkillsDamage;
        _skillHandlers["DiscardAllDrawTo"]         = BT06_DiscardAllDrawTo;
        _skillHandlers["DamageOpponent"]           = BT06_DamageOpponent;

        // 아이템(엑시트는 OnItemsTrashedWithUnit, 어태커는 OnAttackDeclared에서 개별 처리)
    }

    // ═══════════════════ 어태커 ═══════════════════

    // BT06-002 하얀 고양이 루: 어태커 — 이 공격이 끝날 때까지 조우 파워-N
    private void BT06_AttackerDebuffEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
            EffectActions.Buff(!isPlayer, lane, -value, 0, BoostUntil.EndOfAttack);
    }

    // BT06-016 온천 수행자 벤타나: 어태커 — 다른 모든 아군 이번 턴 파워+N
    private void BT06_AttackerBuffAllAllies(bool isPlayer, int lane, CardData card, int value, string et)
    {
        for (int i = 0; i < 3; i++)
        {
            if (i == lane) continue;
            if (FieldManager.Instance.GetUnit(isPlayer, i) != null)
                EffectActions.Buff(isPlayer, i, value, 0, BoostUntil.EndOfTurn);
        }
    }

    // BT06-006/012 체인:C 어태커 — 모든 상대 유닛 이번 턴 파워-N
    private void BT06_ChainAttackerDebuffAll(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var parts = et.Split(':');
        int need = int.Parse(parts[1]);
        int amount = int.Parse(parts[2]);
        if (GetChainCount(isPlayer, lane) < need) return;
        for (int i = 0; i < 3; i++)
            if (FieldManager.Instance.GetUnit(!isPlayer, i) != null)
                EffectActions.Buff(!isPlayer, i, -amount, 0, BoostUntil.EndOfTurn);
    }

    // BT06-020 체인:C 어태커 — 조우 파워-N(이번턴). 승리(트래시)했다면 드로우1은 OnAttackWon에서 처리
    private void BT06_ChainAttackerDebuffEncounterDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var parts = et.Split(':');
        int need = int.Parse(parts[1]);
        int amount = int.Parse(parts[2]);
        if (GetChainCount(isPlayer, lane) >= need && FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
            AddTurnBoost(!isPlayer, lane, -amount);
    }

    // BT06-013 체인:C 어태커 — 이 공격 침투[1] + 파워+N
    private void BT06_ChainAttackerInfiltratePower(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var parts = et.Split(':');
        int need = int.Parse(parts[1]);
        int power = int.Parse(parts[2]);
        if (GetChainCount(isPlayer, lane) < need) return;
        EffectActions.Buff(isPlayer, lane, power, 0, BoostUntil.EndOfAttack);
        _bt06ChainInfiltrateThisAttack.Add(Key(isPlayer, lane));
    }

    // BT06-009 체인:C 어태커 — 이 공격 관통[1] + 파워+N
    private void BT06_ChainAttackerPiercePower(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var parts = et.Split(':');
        int need = int.Parse(parts[1]);
        int power = int.Parse(parts[2]);
        if (GetChainCount(isPlayer, lane) < need) return;
        EffectActions.Buff(isPlayer, lane, power, 0, BoostUntil.EndOfAttack);
        _grantedPenetration[Key(isPlayer, lane)] = 1;
    }

    // BT06-008/014 체인:C 어태커 — 이 공격 파워+N
    private void BT06_ChainAttackerPowerBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var parts = et.Split(':');
        int need = int.Parse(parts[1]);
        int amount = int.Parse(parts[2]);
        if (GetChainCount(isPlayer, lane) >= need)
            AddAttackBoost(isPlayer, lane, amount);
    }

    // 이 공격에서만 유효한 부여형 침투[1] (체인 조건) — OnAttackUnblocked에서 소비 후 제거
    private HashSet<string> _bt06ChainInfiltrateThisAttack = new();

    // ═══════════════════ 엔트리 ═══════════════════

    // BT06-003/047: 엔트리 — 덱 위 N장 공개, 해당 카드 종류 1장 패로, 나머지 트래시
    // 형식: "EntryRevealSearchCardType:종류:N"
    private void BT06_EntryRevealSearchCardType(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var parts = et.Split(':');
        var wantType = BT06_ParseCardTypeKorean(parts[1]);
        int reveal = parts.Length > 2 && int.TryParse(parts[2], out int r) ? r : 3;
        EffectActions.RevealPickToHand(isPlayer, reveal, c => c.Type == wantType,
            $"패에 넣을 {parts[1]} 카드를 선택하세요");
    }

    // BT06-022 비키니 에이전트 실비아: 엔트리 — 모든 상대 유닛 이번턴 파워-N, 트래시된 수만큼 드로우
    // (이 룰셋은 파워 0 이하 자동 트래시 규칙이 없어 "트래시했다면 드로우" 조건은 근사 불가 — 디버프만 적용)
    private void BT06_EntryDebuffAllEnemiesDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
        for (int i = 0; i < 3; i++)
            if (FieldManager.Instance.GetUnit(!isPlayer, i) != null)
                AddTurnBoost(!isPlayer, i, -value);
        Debug.Log($"[엔트리] {card.CardName} → 상대 전체 파워-{value} (자동 트래시 규칙 없어 드로우 조건은 근사 생략)");
    }

    // BT06-062 데이드림 바니 모르페아: 엔트리 — 히트 N 이하 상대 1장 상대턴 끝까지 공격 불가
    private void BT06_EntryDisableAttackLowHit(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, !isPlayer,
            (l, u) => GetEffectiveHit(!isPlayer, l, u) <= value,
            "공격 불가로 만들 상대 유닛을 선택하세요",
            picked => DisableAttack(!isPlayer, picked, BoostUntil.EndOfOpponentTurn));
    }

    // BT06-023 프로즌 퀸 빌헬미나: 엔트리 — 패 전부 트래시할 수 있다 → 패 N장 될 때까지 드로우
    private void BT06_EntryDiscardAllDrawTo(bool isPlayer, int lane, CardData card, int value, string et)
        => BT06_DiscardAllDrawToImpl(isPlayer, value, optional: true);

    // BT06-057 여름휴가 달비: 엔트리 — 상대 턴 끝까지 "조우 유닛은 광전사" 오라 획득
    private void BT06_EntryGainBerserkEncounterAura(bool isPlayer, int lane, CardData card, int value, string et)
        => _bt06BerserkAura.Add(Key(isPlayer, lane));

    // BT06-054/055/063: 엔트리 — 덱 위 N장 트래시, 그중 스킬 1장 골라 발동 가능
    private void BT06_EntryMillActivateSkill(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var milled = EffectActions.Mill(isPlayer, value);
        var skillCandidates = milled.Where(c => c.Type == CardType.Skill).ToList();
        if (skillCandidates.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "발동할 스킬을 선택하세요 (선택 안 해도 됨)", skillCandidates,
            picked => ExecuteSkillEffect(isPlayer, picked, -1));
    }

    // BT06-049 정화의 무녀 그라나데: 엔트리 — 트래시에서 N코스트 이하 스킬 1장 패로
    private void BT06_EntryRecoverSkillLowCost(bool isPlayer, int lane, CardData card, int value, string et)
        => EffectActions.RecoverFromTrash(isPlayer, c => c.Type == CardType.Skill && c.Cost <= value,
            $"트래시에서 회수할 {value}코스트 이하 스킬을 선택하세요");

    // BT06-005: 엔트리 — 스킬존 스킬 1장 골라 0코스트화(이번턴) + 조우 파워-N
    private void BT06_EntryZeroCostSkillDebuffEncounter(bool isPlayer, int lane, CardData card, int value, string et)
        => BT06_PickZeroCostSkill(isPlayer, null, picked =>
        {
            if (FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
                AddTurnBoost(!isPlayer, lane, -value);
        });

    // BT06-011: 엔트리 — 스킬존 스킬 1장 골라 0코스트화(이번턴) + 드로우N
    private void BT06_EntryZeroCostSkillDraw(bool isPlayer, int lane, CardData card, int value, string et)
        => BT06_PickZeroCostSkill(isPlayer, null, picked => EffectActions.Draw(isPlayer, value));

    // BT06-053: 엔트리 — 스킬존에서 N코스트 이하 스킬 1장 골라 0코스트화(이번턴)
    private void BT06_EntryZeroCostSkillLowCost(bool isPlayer, int lane, CardData card, int value, string et)
        => BT06_PickZeroCostSkill(isPlayer, c => c.Cost <= value, null);

    // BT06-061 해변의 정의 미카엘라: 엔트리 — 상대턴 끝까지, 상대는 패에서 N코스트 이상 유닛을 이 레인에 배치 불가
    private void BT06_EntryBlockHighCostDeployLane(bool isPlayer, int lane, CardData card, int value, string et)
        => _bt06DeployBlockHighCost[Key(!isPlayer, lane)] = value;

    // 공용: 스킬존 카드 1장 선택 → 0코스트화(이번턴) + 선택적 부가효과
    private void BT06_PickZeroCostSkill(bool isPlayer, System.Func<CardData, bool> extraFilter, System.Action<CardData> then)
    {
        var owner = GetOwner(isPlayer);
        var candidates = owner.SkillZone.Where(c => extraFilter == null || extraFilter(c)).ToList();
        if (candidates.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "0코스트로 만들 스킬존 카드를 선택하세요 (선택 안 해도 됨)", candidates,
            picked =>
            {
                _bt06ZeroCostSkillZoneCards.Add(picked);
                Debug.Log($"[BT06] {picked.CardName} 0코스트화 (이번 턴)");
                then?.Invoke(picked);
            });
    }

    public bool IsSkillZeroCostGranted(CardData card) => _bt06ZeroCostSkillZoneCards.Contains(card);

    private static CardType BT06_ParseCardTypeKorean(string kr) => kr switch
    {
        "유닛" => CardType.Unit,
        "스킬" => CardType.Skill,
        "아이템" => CardType.Item,
        _ => CardType.Unit
    };

    // ═══════════════════ 스킬 ═══════════════════

    private void BT06_RevealSearchCardType(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var parts = et.Split(':');
        var wantType = BT06_ParseCardTypeKorean(parts[1]);
        int reveal = parts.Length > 2 && int.TryParse(parts[2], out int r) ? r : 2;
        EffectActions.RevealPickToHand(isPlayer, reveal, c => c.Type == wantType,
            $"패에 넣을 {parts[1]} 카드를 선택하세요");
    }

    // BT06-029: 패 중 원하는 만큼 골라 트래시(취소 가능) → 패가 N장 될 때까지 드로우
    private void BT06_DiscardHandDrawTo(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        EffectActions.DiscardHandOptional(isPlayer, owner.Hand.Count, _ =>
        {
            int need = value - owner.Hand.Count;
            if (need > 0) EffectActions.Draw(isPlayer, need);
        });
    }

    // BT06-031: 트래시에서 트리거 없고 파워 N 이하인 유닛 1장 회수
    private void BT06_RecoverUnitByPower(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.RecoverFromTrash(isPlayer,
            c => c.Type == CardType.Unit && !c.IsTrigger && c.AttackPower <= value,
            $"트래시에서 회수할 파워 {value} 이하 유닛을 선택하세요");

    // BT06-071: 트래시에서 트리거 없고 파워 N 이상인 유닛 1장 회수
    private void BT06_RecoverUnitByPowerMin(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.RecoverFromTrash(isPlayer,
            c => c.Type == CardType.Unit && !c.IsTrigger && c.AttackPower >= value,
            $"트래시에서 회수할 파워 {value} 이상 유닛을 선택하세요");

    // BT06-032: 아군 1장에 이번턴 어태커 듀얼리스트 부여
    private void BT06_GrantDuelist(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            "듀얼리스트를 부여할 유닛을 선택하세요",
            picked => _grantedDuelistThisTurn.Add(Key(isPlayer, picked)));

    // BT06-033: 상대 1장 이번턴 파워-N, 이 효과로 트래시했다면 드로우1 (파워<=0 자동 트래시 규칙 없어 디버프만 적용)
    private void BT06_DebuffEnemyDrawIfTrashed(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        if (targetLane < 0) return;
        AddTurnBoost(!isPlayer, targetLane, -value);
    }

    // BT06-034: N코스트 이하 아군 1장 고름 — 조우 유닛 있으면 그 유닛으로 즉시 공격
    private void BT06_SkillForceAttackLowCost(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => u.Cost <= value,
            "공격할 유닛을 선택하세요",
            picked =>
            {
                if (FieldManager.Instance.GetUnit(!isPlayer, picked) != null)
                    StartCoroutine(CombatManager.Instance.ForceAttackLane(isPlayer, picked));
            });

    // BT06-035: 상대 1장 고름, 양측 패 장수 차이 1장마다 이번턴 파워-N
    private void BT06_DebuffEnemyPerHandDiff(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        if (targetLane < 0) return;
        int diff = Mathf.Abs(GetOwner(isPlayer).Hand.Count - GetOwner(!isPlayer).Hand.Count);
        if (diff > 0) AddTurnBoost(!isPlayer, targetLane, -diff * value);
    }

    // BT06-036: 이번턴 상대 엑시트 발동 불가 + 상대 1장 이번턴 파워-N
    private void BT06_DisableExitDebuffEnemy(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        _exitDisabledThisTurn[Idx(!isPlayer)] = true;
        if (targetLane >= 0) AddTurnBoost(!isPlayer, targetLane, -value);
    }

    // BT06-037: N코스트 이하 아군 1장 — 이 턴 어택 페이즈 중 1번 더 공격 가능
    private void BT06_GrantExtraAttackLowCost(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => u.Cost <= value,
            "추가 공격을 부여할 유닛을 선택하세요",
            picked => AddExtraAttack(isPlayer, picked));

    // BT06-038: 상대 2장까지 골라 이번턴 파워-N
    private void BT06_DebuffTwoEnemies(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => BT06_DebuffTwoEnemiesStep(isPlayer, value, 2);

    private void BT06_DebuffTwoEnemiesStep(bool isPlayer, int amount, int remaining, List<int> picked = null)
    {
        picked ??= new List<int>();
        if (remaining <= 0) return;
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => !picked.Contains(l),
            $"파워-{amount}로 만들 상대 유닛을 선택하세요 (취소 가능, {remaining}장 남음)",
            lane =>
            {
                AddTurnBoost(!isPlayer, lane, -amount);
                picked.Add(lane);
                BT06_DebuffTwoEnemiesStep(isPlayer, amount, remaining - 1, picked);
            });
    }

    // BT06-039: 상대 1장 고름. 패를 원하는 수만큼 골라 트래시, 트래시한 장수 1장마다 그 유닛 이번턴 파워-N
    private void BT06_DiscardDebuffPerCard(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        if (targetLane < 0) return;
        var owner = GetOwner(isPlayer);
        EffectActions.DiscardHandOptional(isPlayer, owner.Hand.Count, discarded =>
        {
            if (discarded > 0) AddTurnBoost(!isPlayer, targetLane, -discarded * value);
        });
    }

    // BT06-068: 패 1장 골라 트래시할 수 있다 → 드로우N
    private void BT06_DiscardOneDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
        {
            if (discarded >= 1) EffectActions.Draw(isPlayer, value);
        });

    // BT06-069: 아군 1장 고름 — 상대턴 끝까지 "조우 유닛은 광전사" 오라 획득
    private void BT06_GrantBerserkEncounterAura(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            "광전사 오라를 부여할 유닛을 선택하세요",
            picked => _bt06BerserkAura.Add(Key(isPlayer, picked)));

    // BT06-070: 상대 2장까지 고름 — 상대턴 끝까지 "어태커: 상대(나)는 드로우1" 부여
    private void BT06_GrantEnemiesAttackerDrawPenalty(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => BT06_GrantDrawPenaltyStep(isPlayer, value);

    private void BT06_GrantDrawPenaltyStep(bool isPlayer, int remaining, List<int> picked = null)
    {
        picked ??= new List<int>();
        if (remaining <= 0) return;
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => !picked.Contains(l),
            $"「어태커: 드로우 부여」를 걸 상대 유닛을 선택하세요 (취소 가능, {remaining}장 남음)",
            lane =>
            {
                _bt06AttackerDrawPenalty.Add(Key(!isPlayer, lane));
                picked.Add(lane);
                BT06_GrantDrawPenaltyStep(isPlayer, remaining - 1, picked);
            });
    }

    // BT06-072: 아군 1장 고름 — 상대턴 끝까지 "이 레인에 상대 유닛 배치되면 히트=1" 오라 획득 + 현재 조우 유닛 히트도 즉시 1로
    private void BT06_GrantLaneHitSetOne(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            "오라를 부여할 유닛을 선택하세요",
            picked => _bt06HitSetOneAura.Add(Key(isPlayer, picked)));

    // BT06-073: 드로우3, 상대도 드로우1
    private void BT06_DrawBothPenalty(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var parts = et.Split(':');
        int mine = int.Parse(parts[1]);
        int theirs = int.Parse(parts[2]);
        EffectActions.Draw(isPlayer, mine);
        EffectActions.Draw(!isPlayer, theirs);
    }

    // BT06-075: 엔트리를 가진 아군 1장 선택, 그 엔트리 효과 재발동 (근사: 전체 엔트리 효과 재실행)
    private void BT06_ActivateAllyEntry(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer,
            (l, u) => u.Keywords != null && u.Keywords.Contains("엔트리"),
            "엔트리 효과를 재발동할 유닛을 선택하세요",
            picked =>
            {
                var target = FieldManager.Instance.GetUnit(isPlayer, picked);
                if (target != null) OnUnitPlaced(isPlayer, picked, target);
            });

    // BT06-076: 모든 상대 유닛, 상대턴 끝까지 광전사 획득
    private void BT06_GrantAllEnemiesBerserk(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => _bt06AllEnemiesBerserker[Idx(!isPlayer)] = true;

    // BT06-077: 디펜더 가진 아군 수만큼 드로우, 상대턴 끝까지 상대는 어태커 효과 발동 불가
    private void BT06_DrawPerDefenderDisableAttackers(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        int cnt = 0;
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.Keywords != null && u.Keywords.Contains("디펜더")) cnt++;
        }
        if (cnt > 0) EffectActions.Draw(isPlayer, cnt);
        _bt06AttackerEffectsDisabled[Idx(!isPlayer)] = true;
    }

    // BT06-078: 패 스킬 1장 트래시 → 상대 1대미지
    private void BT06_DiscardSkillDamage(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var skillCards = owner.Hand.Where(c => c.Type == CardType.Skill).ToList();
        if (skillCards.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "트래시할 스킬 카드를 선택하세요", skillCards,
            picked =>
            {
                owner.RemoveFromHand(picked);
                owner.AddToTrash(picked);
                EffectActions.RefreshUI();
                EffectActions.DamageOpponent(isPlayer, value);
            });
    }

    // BT06-079: 트래시에서 트리거없고 카드명 다른 스킬 N장 골라 덱 맨 아래에 → 상대 M대미지
    // 형식: "ReturnSkillsDamage:N:M"
    private void BT06_ReturnSkillsDamage(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var parts = et.Split(':');
        int returnCount = int.Parse(parts[1]);
        int damage = int.Parse(parts[2]);
        BT06_ReturnSkillsStep(isPlayer, returnCount, damage, skill.CardName, new HashSet<string>());
    }

    private void BT06_ReturnSkillsStep(bool isPlayer, int remaining, int damage, string excludeName, HashSet<string> usedNames)
    {
        var owner = GetOwner(isPlayer);
        if (remaining <= 0) { EffectActions.DamageOpponent(isPlayer, damage); return; }
        var candidates = owner.TrashPile
            .Where(c => c.Type == CardType.Skill && !c.IsTrigger && c.CardName != excludeName && !usedNames.Contains(c.CardName))
            .ToList();
        if (candidates.Count == 0) { EffectActions.DamageOpponent(isPlayer, damage); return; }
        EffectActions.PickFromCards(isPlayer, $"덱 맨 아래에 놓을 스킬을 선택하세요 ({remaining}장 남음)", candidates,
            picked =>
            {
                EffectActions.MoveCard(isPlayer, picked, CardZone.Trash, CardZone.DeckBottom);
                usedNames.Add(picked.CardName);
                BT06_ReturnSkillsStep(isPlayer, remaining - 1, damage, excludeName, usedNames);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // BT06-080: 패 전부 트래시 → 패 N장 될 때까지 드로우
    private void BT06_DiscardAllDrawTo(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => BT06_DiscardAllDrawToImpl(isPlayer, value, optional: false);

    private void BT06_DiscardAllDrawToImpl(bool isPlayer, int targetSize, bool optional)
    {
        var owner = GetOwner(isPlayer);
        if (optional)
        {
            EffectActions.DiscardHandOptional(isPlayer, owner.Hand.Count, _ =>
            {
                int need = targetSize - owner.Hand.Count;
                if (need > 0) EffectActions.Draw(isPlayer, need);
            });
        }
        else
        {
            EffectActions.DiscardHand(isPlayer, owner.Hand.Count, _ =>
            {
                int need = targetSize - owner.Hand.Count;
                if (need > 0) EffectActions.Draw(isPlayer, need);
            });
        }
    }

    // BT06-082: 상대에게 N대미지
    private void BT06_DamageOpponent(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.DamageOpponent(isPlayer, value);

    // ═══════════════════ 액티브 (버프:1/2 스킬존 게이트) ═══════════════════
    // TriggerActiveEffect에서 훅으로 호출됨 (EffectSystem.Active.cs)

    private static readonly HashSet<string> _bt06ActiveTypes = new()
    {
        "ActiveGrantDefenderBoost",
        "BuffActiveSkillZoneDraw", "BuffActiveSkillZoneDefendersDamage",
        "BuffActiveSkillZoneBerserkEncounter",
        "BuffActiveAttackSkillZoneDebuff", "BuffActiveAttackSkillZoneRecoverSkill",
        "BuffActiveAttackSkillZoneTrashEnemy", "BuffActiveAttackSkillZoneReturnSkillDamage",
        "BuffActiveAttackSkillZone2DebuffAll", "BuffActiveAttackSkillZone2Debuff",
        "BuffActiveAttackSkillZone2BounceEncounter", "BuffActiveSkillZone2ReturnSkillsDamage",
    };

    private bool TryBT06Active(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var (type, _) = Parse(et);
        if (!_bt06ActiveTypes.Contains(type)) return false;
        var owner = GetOwner(isPlayer);
        int skillZoneCount = owner.SkillZone.Count;

        switch (type)
        {
            // BT06-046: 아군 1장에 상대턴 끝까지 「디펜더 파워+N」 부여
            case "ActiveGrantDefenderBoost":
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
                    "디펜더 파워 부여를 받을 유닛을 선택하세요",
                    picked => GrantDefenderBoost(isPlayer, picked, value));
                break;

            // BT06-044: 버프:1 액티브:메인 — 스킬존≥1 → 드로우N
            case "BuffActiveSkillZoneDraw":
                if (skillZoneCount >= 1) EffectActions.Draw(isPlayer, value);
                break;

            // BT06-056: 버프:1 액티브:메인 — 스킬존≥1 → 디펜더 아군 2장 고름 → 상대 N대미지, 고른 2장 이번턴 공격불가
            case "BuffActiveSkillZoneDefendersDamage":
                if (skillZoneCount >= 1)
                    BT06_PickDefendersForDamageStep(isPlayer, value, 2);
                break;

            // BT06-061/064: 버프:N 액티브:메인/어택 — 스킬존≥N → 상대턴 끝까지 광전사 오라 획득 (이 유닛의 레인)
            case "BuffActiveSkillZoneBerserkEncounter":
                if (skillZoneCount >= value) _bt06BerserkAura.Add(Key(isPlayer, lane));
                break;

            // BT06-004/017/021: 버프:1 액티브:어택 — 스킬존≥1 → 상대 1장 이번턴 파워-N
            case "BuffActiveAttackSkillZoneDebuff":
                if (skillZoneCount >= 1)
                    EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
                        "파워를 감소시킬 상대 유닛을 선택하세요",
                        picked => AddTurnBoost(!isPlayer, picked, -value));
                break;

            // BT06-022: 버프:1 액티브:어택 — 스킬존≥1 → 트래시에서 N코스트 이하 스킬 회수
            case "BuffActiveAttackSkillZoneRecoverSkill":
                if (skillZoneCount >= 1)
                    EffectActions.RecoverFromTrash(isPlayer, c => c.Type == CardType.Skill && c.Cost <= value,
                        $"트래시에서 회수할 {value}코스트 이하 스킬을 선택하세요");
                break;

            // BT06-023: 버프:1 액티브:어택 — 스킬존≥1 → N코스트 이하 상대 유닛 트래시
            case "BuffActiveAttackSkillZoneTrashEnemy":
                if (skillZoneCount >= 1)
                    EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => u.Cost <= value,
                        $"트래시할 {value}코스트 이하 상대 유닛을 선택하세요",
                        picked => EffectActions.TrashUnit(!isPlayer, picked));
                break;

            // BT06-060: 버프:1 액티브:어택 — 스킬존≥1 → 트래시에서 트리거없고 N코스트 이상 스킬 1장 덱 맨아래 → 상대 1대미지
            case "BuffActiveAttackSkillZoneReturnSkillDamage":
                if (skillZoneCount >= 1)
                    EffectActions.PickFromCards(isPlayer, "덱 맨 아래에 놓을 스킬을 선택하세요",
                        owner.TrashPile.Where(c => c.Type == CardType.Skill && !c.IsTrigger && c.Cost >= value).ToList(),
                        picked =>
                        {
                            EffectActions.MoveCard(isPlayer, picked, CardZone.Trash, CardZone.DeckBottom);
                            EffectActions.DamageOpponent(isPlayer, 1);
                        },
                        aiPick: list => list.OrderByDescending(c => c.Cost).First());
                break;

            // BT06-024: 버프:2 액티브:어택 — 스킬존≥2 → 모든 상대 유닛 이번턴 파워-N
            case "BuffActiveAttackSkillZone2DebuffAll":
                if (skillZoneCount >= 2)
                    for (int i = 0; i < 3; i++)
                        if (FieldManager.Instance.GetUnit(!isPlayer, i) != null)
                            AddTurnBoost(!isPlayer, i, -value);
                break;

            // BT06-025: 버프:2 액티브:어택 — 스킬존≥2 → 상대 1장 이번턴 파워-N
            case "BuffActiveAttackSkillZone2Debuff":
                if (skillZoneCount >= 2)
                    EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
                        "파워를 감소시킬 상대 유닛을 선택하세요",
                        picked => AddTurnBoost(!isPlayer, picked, -value));
                break;

            // BT06-058: 버프:2 액티브:어택 — 스킬존≥2 & 조우 유닛 N코 이상 → 조우+장착아이템 패로 반환, 이 유닛 히트 이번턴 1
            case "BuffActiveAttackSkillZone2BounceEncounter":
            {
                var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
                if (skillZoneCount >= 2 && enc != null && enc.Cost >= value)
                {
                    EffectActions.BounceUnit(!isPlayer, lane); // 조우 유닛+장착 아이템 전부 패로 복귀 (블록이 자동 처리)
                    _turnHitBoosts[Key(isPlayer, lane)] = 1 - card.Hit; // 이번턴 히트를 1로 근사 설정
                }
                break;
            }

            // BT06-062: 버프:2 액티브:메인 — 스킬존≥2 → 트래시에서 트리거없고 카드명 다른 스킬 N장 덱 맨아래 → 상대 M대미지, 이 유닛 이번턴 공격불가
            // 형식: "BuffActiveSkillZone2ReturnSkillsDamage:N:M"
            case "BuffActiveSkillZone2ReturnSkillsDamage":
            {
                var parts = et.Split(':');
                int returnCount = int.Parse(parts[1]);
                int damage = int.Parse(parts[2]);
                if (skillZoneCount >= 2)
                {
                    BT06_ReturnSkillsStep(isPlayer, returnCount, damage, card.CardName, new HashSet<string>());
                    DisableAttack(isPlayer, lane, BoostUntil.EndOfTurn);
                }
                break;
            }

            default:
                return false;
        }
        return true;
    }

    // BT06-056: 디펜더 아군을 remaining장까지 선택 → 상대 damage대미지, 고른 유닛들 이번턴 공격 불가
    private void BT06_PickDefendersForDamageStep(bool isPlayer, int damage, int remaining, List<int> picked = null)
    {
        picked ??= new List<int>();
        if (remaining <= 0)
        {
            EffectActions.DamageOpponent(isPlayer, damage);
            foreach (var l in picked) DisableAttack(isPlayer, l, BoostUntil.EndOfTurn);
            return;
        }
        EffectActions.SelectUnit(isPlayer, isPlayer,
            (l, u) => u.Keywords != null && u.Keywords.Contains("디펜더") && !picked.Contains(l),
            $"공격 불가로 지정할 디펜더 유닛을 선택하세요 ({remaining}장 남음, 취소 시 종료)",
            lane =>
            {
                picked.Add(lane);
                BT06_PickDefendersForDamageStep(isPlayer, damage, remaining - 1, picked);
            });
    }

    // ═══════════════════ 패시브 ═══════════════════

    public int GetBT06PassiveBonus(bool isPlayer, int lane, CardData card)
    {
        int bonus = 0;
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            switch (type)
            {
                // BT06-045: 필드의 광전사 가진 모든 상대 유닛 파워-N
                case "PassiveBerserkerEnemiesDebuff":
                    for (int i = 0; i < 3; i++)
                    {
                        var enemy = FieldManager.Instance.GetUnit(!isPlayer, i);
                        if (enemy != null && IsBerserker(!isPlayer, i, enemy))
                            bonus -= value;
                    }
                    break;
            }
        }

        // BT06-015: 체인 가진 모든 아군 유닛에게 파워+N을 부여하는 다른 카드가 필드에 있으면 적용
        if (card.EffectTypes.Any(e => e.StartsWith("Chain")))
            for (int i = 0; i < 3; i++)
            {
                var other = FieldManager.Instance.GetUnit(isPlayer, i);
                if (other == null) continue;
                foreach (var et in other.EffectTypes)
                {
                    var (t, v) = Parse(et);
                    if (t == "PassiveChainAlliesBoost") bonus += v;
                }
            }

        // BT06-052: 버프 키워드 가진 모든 아군 유닛에게 파워+N을 부여하는 다른 카드가 필드에 있으면 적용
        if (card.Keywords != null && card.Keywords.Contains("버프"))
            for (int i = 0; i < 3; i++)
            {
                var other = FieldManager.Instance.GetUnit(isPlayer, i);
                if (other == null) continue;
                foreach (var et in other.EffectTypes)
                {
                    var (t, v) = Parse(et);
                    if (t == "PassiveBuffKeywordAlliesBoost") bonus += v;
                }
            }

        return bonus;
    }

    // BT06-045/구 광전사 통합 판정: 인쇄 키워드 + BT04 부여 + BT06 오라/전체부여
    public bool IsBerserker(bool isPlayer, int lane, CardData card)
    {
        if (card == null) return false;
        if (card.EffectTypes.Contains("PassiveBerserker") || card.EffectTypes.Contains("Berserker")) return true;
        if (HasGrantedBerserkerToUnit(isPlayer, lane, card)) return true; // 기존 BT04 부여(상대 필드 스캔 방식, 내부에서 자동 반전)
        if (_bt06AllEnemiesBerserker[Idx(isPlayer)]) return true;
        if (_bt06BerserkAura.Contains(Key(!isPlayer, lane))) return true; // 상대(오라 소유자) 쪽 같은 레인에 오라가 있으면 이 유닛은 그 오라의 "조우 유닛"
        return false;
    }

    // BT06-054 풀 파티 세헤라자드: 상대가 비트리거 효과로 드로우하면 턴당 1회 나도 드로우
    public void NotifyEffectDraw(bool isPlayer, int count)
    {
        if (count <= 0) return;
        bool watcherIsPlayer = !isPlayer;
        for (int i = 0; i < 3; i++)
        {
            var passive = FieldManager.Instance.GetUnit(watcherIsPlayer, i);
            if (passive == null || !passive.EffectTypes.Contains("PassiveDrawOnOpponentEffectDraw")) continue;
            string key = Key(watcherIsPlayer, i);
            if (_bt06DrawOnOppDrawUsed.Contains(key)) continue;
            _bt06DrawOnOppDrawUsed.Add(key);
            EffectActions.Draw(watcherIsPlayer, 1);
            Debug.Log($"[BT06 패시브] {passive.CardName} → 상대 드로우 감지, 자신도 드로우1");
        }

        // SB01-004: 부여된 "효과로 드로우하면 자신에게 1대미지" (상대 턴 끝까지)
        if (_sb01DrawPunish[Idx(isPlayer)])
        {
            GetOwner(isPlayer).TakeDamage(1);
            Debug.Log($"[SB01 벌칙] {(isPlayer ? "플레이어" : "AI")} 효과 드로우 → 1대미지");
        }
    }

    // ═══════════════════ 아이템 ═══════════════════

    // BT06-041 천둥의 망치: 어태커 — 조우 파워-N(이번 공격). 이 효과로 트래시했다면 아이템 자기 트래시 가능 → 드로우M
    // 형식: "ItemAttackerDebuffTrashSelfDraw:N:M"
    // OnAttackDeclared의 아이템 루프에서 호출 (EffectSystem.Attacker.cs)
    public void BT06_ItemAttackerDebuffTrashSelfDraw(bool isPlayer, int lane, CardData item, string et)
    {
        var parts = et.Split(':');
        int debuff = int.Parse(parts[1]);
        int draw = int.Parse(parts[2]);
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc == null) return;
        int before = GetEffectivePower(!isPlayer, lane, enc);
        EffectActions.Buff(!isPlayer, lane, -debuff, 0, BoostUntil.EndOfAttack);
        // "트래시했다면" 자동 트래시 규칙이 없어, 디버프 후 파워가 0 이하가 되면 트래시된 것으로 근사 판정
        if (before - debuff > 0) return;
        EffectActions.PickFromCards(isPlayer, $"[{item.CardName}] 트래시하고 드로우{draw}장 받으시겠습니까?",
            new List<CardData> { item },
            picked =>
            {
                FieldManager.Instance.UnequipItem(isPlayer, lane, picked);
                GetOwner(isPlayer).AddToTrash(picked);
                EffectActions.Draw(isPlayer, draw);
                EffectActions.RefreshUI();
            });
    }

    // BT06-084 사신의 수의: 가디언 상쇄 — 인접 레인 공격 시 장착 아이템 자체를 트래시해 방어 (GetGuardianWallOptions cost=-1로 표시)
    public void ProcessGuardianCounter(bool defenderIsPlayer, int wallLane)
    {
        var item = FieldManager.Instance.GetEquippedItems(defenderIsPlayer, wallLane)
            .FirstOrDefault(it => it.EffectTypes.Contains("ItemGuardianCounter"));
        if (item == null) return;
        FieldManager.Instance.UnequipItem(defenderIsPlayer, wallLane, item);
        GetOwner(defenderIsPlayer).AddToTrash(item);
        EffectActions.RefreshUI();
        Debug.Log($"[가디언 상쇄] {item.CardName} 트래시로 방어");
    }
}
