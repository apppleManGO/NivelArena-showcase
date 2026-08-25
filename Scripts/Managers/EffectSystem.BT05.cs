// Assets/Scripts/Managers/EffectSystem.BT05.cs
// ─────────────────────────────────────────────────────────────────────────
// BT05 effectType 구현 — 격리 파일.
//  테마: 믹스(자신 필드에 X속성 외 카드 조건) + 이스케이프 + 레벨링크 + 아이템/암드.
//  대부분 기존 인프라(HasMixCondition/이스케이프/레벨링크/돌파·듀얼리스트 부여/아이템)를 재사용.
//  핸들러는 기존 레지스트리에 InitBT05Handlers()로 등록 (Awake 호출).
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class EffectSystem : MonoBehaviour
{
    private void InitBT05Handlers()
    {
        // 어태커
        _attackerHandlers["MixAttackerDebuffEncounter"]       = BT05_MixAttackerDebuffEncounter;
        _attackerHandlers["AttackerDebuffEncounterRecoverSkill"] = BT05_AttackerDebuffEncounterRecoverSkill;
        // 엔트리
        _entryHandlers["MixEntryDebuffEncounter"]             = BT05_MixEntryDebuffEncounter;
        _entryHandlers["EntryGrantExitReturn"]                = BT05_EntryGrantExitReturn;
        _entryHandlers["EntryMillDrawIfItem"]                 = BT05_EntryMillDrawIfItem;
        _entryHandlers["EntryDiscardDrawIfItemDouble"]        = BT05_EntryDiscardDrawIfItemDouble;
        _entryHandlers["EntryTrashAllyOrDiscard"]             = BT05_EntryTrashAllyOrDiscard;
        _entryHandlers["EntryRevealMillOrShuffleback"]        = BT05_EntryRevealMillOrShuffleback;
        // 엑시트
        _exitHandlers["ExitMixDebuffEnemy"]                   = BT05_ExitMixDebuffEnemy;
        // 스킬
        _skillHandlers["BuffEquippedAllyHit"]                 = BT05_BuffEquippedAllyHit;
        _skillHandlers["GrantAlliesUnderLevelDuelist"]        = BT05_GrantAlliesUnderLevelDuelist;
        _skillHandlers["DisableDefendEnemyByLevel"]           = BT05_DisableDefendEnemyByLevel;
        _skillHandlers["TrashHandItemsDraw"]                  = BT05_TrashHandItemsDraw;
        _skillHandlers["TrashHandUnitTrashLowerCostFieldUnit"] = BT05_TrashHandUnitTrashLowerCostFieldUnit;

        // ── 배치 2 ──
        _entryHandlers["EntryActivateTrashExitUnit"]         = BT05_EntryActivateTrashExitUnit;
        _entryHandlers["MixEntryMillRecoverFromDamageZone"]  = BT05_MixEntryMillRecoverFromDamageZone;
        _entryHandlers["EntryMillDeckBottomDebuffEncounterDouble"] = BT05_EntryMillDeckBottomDebuffEncounterDouble;
        _exitHandlers["MixExitTrashLowestCostEnemy"]         = BT05_MixExitTrashLowestCostEnemy;
        _exitHandlers["MixExitReviveNonTriggerLowCost"]      = BT05_MixExitReviveNonTriggerLowCost;
        _exitHandlers["MixExitTrashUnencounteredEnemy"]      = BT05_MixExitTrashUnencounteredEnemy;
        _skillHandlers["GrantEnemyDamageZoneOnTrash"]        = BT05_GrantEnemyDamageZoneOnTrash;
        _skillHandlers["GrantLowCostAttackerConditionalBreakthrough"] = BT05_GrantLowCostAttackerConditionalBreakthrough;
        _skillHandlers["UnitDeckBottomRecoverLowerCostNonTrigger"] = BT05_UnitDeckBottomRecoverLowerCostNonTrigger;
        _skillHandlers["SwapUnitDeckBottomDeploySizeIgnore"] = BT05_SwapUnitDeckBottomDeploySizeIgnore;
        _skillHandlers["SwapUnitRevealDeploySizeIgnoreZeroCost"] = BT05_SwapUnitRevealDeploySizeIgnoreZeroCost;
        _skillHandlers["TrashExitUnitDeckBottomBuffAllyHit"] = BT05_TrashExitUnitDeckBottomBuffAllyHit;
        _skillHandlers["TrashEquippedAllyDraw2MixDamage"]    = BT05_TrashEquippedAllyDraw2MixDamage;
        _skillHandlers["DrawDiscardRecoverUniqueItems"]      = BT05_DrawDiscardRecoverUniqueItems;

        // ── 배치 7 ──
        _attackerHandlers["AttackerConditionalBreakthroughByHand"] = BT05_AttackerConditionalBreakthroughByHand;
        _attackerHandlers["AttackerItemCountBoostTrashLane"] = BT05_AttackerItemCountBoost;
        _attackerHandlers["AttackerPenetrationBySkillCount"] = BT05_AttackerPenetrationBySkillCount;
        _attackerHandlers["MixAttackerLevelBreakthroughOrLevelUp"] = BT05_MixAttackerLevelBreakthroughOrLevelUp;
        _entryHandlers["EntryDiscardReturnHighCostEncounterHitOne"] = BT05_EntryDiscardReturnEncounterHitOne;
        _entryHandlers["EntryDiscardReturnLowCostEncounterHitOne"]  = BT05_EntryDiscardReturnEncounterHitOne;
        _entryHandlers["EntryLockDefenderExit"]              = BT05_EntryLockDefenderExit;
        _entryHandlers["MixEntryAllyDeckBottomEscapeDamage"] = BT05_MixEntryAllyDeckBottomEscapeDamage;
        _entryHandlers["MixEntryConditionalSizeBoostOrLevelUp"] = BT05_MixEntryConditionalSizeBoostOrLevelUp;
        _entryHandlers["MixEntryTrashSkillActivateBySkillZone"] = BT05_MixEntryTrashSkillActivateBySkillZone;
        _exitHandlers["MixExitReturnToHand"]                 = BT05_MixExitReturnToHand;
        _exitHandlers["MixExitTrashToDeckDamage"]            = BT05_MixExitTrashToDeckDamage;
        _exitHandlers["MixExitDamageZoneRecoverSwap"]        = BT05_MixExitDamageZoneRecoverSwap;
        _skillHandlers["ActivateAllyEscapeEffect"]           = BT05_ActivateAllyEscapeEffect;
        _skillHandlers["ActivateTrashExitUnitMixDouble"]     = BT05_ActivateTrashExitUnitMixDouble;
        _skillHandlers["EscapeAllyEncounterReturnOrDrawByHit"] = BT05_EscapeAllyEncounterReturnOrDrawByHit;
        _skillHandlers["RecoverItemEquipMixSelfTrash"]       = BT05_RecoverItemEquipMixSelfTrash;
        _skillHandlers["SwapAllyDeckBottomLockEnemyDefend"]  = BT05_SwapAllyDeckBottomLockEnemyDefend;
        _triggerHandlers["TriggerTrashSelfActivateExitUnit"] = BT05_TriggerTrashSelfActivateExitUnit;
        _triggerHandlers["TriggerTrashSelfRecoverNonTriggerByCostSum"] = BT05_TriggerTrashSelfRecoverNonTriggerByCostSum;
    }

    // BT05-080: 아군이 장착한 아이템 1장을 다른 아군에 이동 (액티브:어택)
    private void BT05_ItemActiveMoveItem(bool isPlayer, CardData sourceItem)
    {
        // 장착 아이템이 있는 아군 레인을 찾아 첫 아이템을 다른 아군으로 이동 (간소화)
        for (int from = 0; from < 3; from++)
        {
            var items = FieldManager.Instance.GetEquippedItems(isPlayer, from);
            var moveItem = items.FirstOrDefault(it => it != sourceItem);
            if (moveItem == null) continue;
            for (int to = 0; to < 3; to++)
            {
                if (to == from || FieldManager.Instance.GetUnit(isPlayer, to) == null) continue;
                FieldManager.Instance.UnequipItem(isPlayer, from, moveItem);
                FieldManager.Instance.EquipItem(isPlayer, to, moveItem);
                Debug.Log($"[BT05] {moveItem.CardName} 레인{from}→{to} 이동");
                return;
            }
        }
    }

    // ═══════════════════ 배치 7 ═══════════════════

    // BT05-050: 패 N장 이상이면 이 공격 돌파
    private void BT05_AttackerConditionalBreakthroughByHand(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (GetOwner(isPlayer).Hand.Count >= value) _breakthroughGrants.Add(Key(isPlayer, lane));
    }

    // BT05-073: 어태커 — 장착 아이템 수만큼 파워+N (이 공격)
    private void BT05_AttackerItemCountBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        int cnt = FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count;
        if (cnt > 0) AddAttackBoost(isPlayer, lane, cnt * value);
    }

    // BT05-011: 어태커 — 스킬존 스킬 수만큼 관통 부여
    private void BT05_AttackerPenetrationBySkillCount(bool isPlayer, int lane, CardData card, int value, string et)
    {
        int cnt = GetOwner(isPlayer).SkillZone.Count;
        if (cnt > 0) _grantedPenetration[Key(isPlayer, lane)] = cnt;
    }

    // BT05-024: 믹스 어태커 — 타 속성 카드 있고 리더레벨≥N이면 조우 방어 불가(돌파). 아니면 리더 레벨+1
    private void BT05_MixAttackerLevelBreakthroughOrLevelUp(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (HasMixCondition(isPlayer, card.Attribute) && GetOwner(isPlayer).LeaderLevel >= value)
            _breakthroughGrants.Add(Key(isPlayer, lane));
        else
            EffectActions.LevelUp(isPlayer);
    }

    // BT05-051/052: 조우 유닛이 N코 이하(Low)/이상(High)이면 패1 트래시 가능 → 조우 패로 + 이 유닛 히트=1
    private void BT05_EntryDiscardReturnEncounterHitOne(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc == null) return;
        bool high = et.StartsWith("EntryDiscardReturnHighCost");
        bool cond = high ? enc.Cost >= value : enc.Cost <= value;
        if (!cond) return;
        EffectActions.DiscardHandOptional(isPlayer, 1, d =>
        {
            if (d < 1) return;
            EffectActions.BounceUnit(!isPlayer, lane);
            int cur = GetEffectiveHit(isPlayer, lane, card);
            AddTurnHitBoost(isPlayer, lane, 1 - cur); // 히트 = 1로 설정
        });
    }

    // BT05-007: 이 턴이 끝날 때까지 상대는 엑시트(디펜더 근사 생략) 발동 불가
    private void BT05_EntryLockDefenderExit(bool isPlayer, int lane, CardData card, int value, string et)
    {
        _exitDisabledThisTurn[Idx(!isPlayer)] = true;
        Debug.Log($"[BT05] {card.CardName} → 상대 엑시트 잠금 (디펜더 잠금은 근사 미구현)");
    }

    // BT05-055: 믹스 → 자신 유닛1 덱 맨 아래로. 그 유닛이 이스케이프면 상대 1대미지
    private void BT05_MixEntryAllyDeckBottomEscapeDamage(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "덱 맨 아래로 놓을 자신 유닛을 선택하세요",
            pk =>
            {
                var u = FieldManager.Instance.GetUnit(isPlayer, pk);
                bool esc = u != null && u.Keywords.Contains("이스케이프");
                BT05_MoveUnitToDeckBottom(isPlayer, pk);
                if (esc) EffectActions.DamageOpponent(isPlayer, 1);
            }, onCancel: () => { });
    }

    // BT05-021: 믹스 + 리더레벨≥N이면 리더 레벨+1 (사이즈+5는 근사). 아니면 패1 트래시 후 드로우1
    private void BT05_MixEntryConditionalSizeBoostOrLevelUp(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (HasMixCondition(isPlayer, card.Attribute) && GetOwner(isPlayer).LeaderLevel >= value)
            EffectActions.LevelUp(isPlayer);
        else
            EffectActions.DiscardHandOptional(isPlayer, 1, d => { if (d >= 1) EffectActions.Draw(isPlayer, 1); });
    }

    // BT05-011: 믹스 + 스킬존에 2코이상 스킬 2장+이면 1장 트래시하고 그 효과 발동
    private void BT05_MixEntryTrashSkillActivateBySkillZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        var owner = GetOwner(isPlayer);
        var cands = owner.SkillZone.Where(c => c.Cost >= 2).ToList();
        if (cands.Count < 2) return;
        EffectActions.PickFromCards(isPlayer, "트래시하고 발동할 스킬을 선택하세요", cands,
            pk => { owner.RemoveFromSkillZone(pk); owner.AddToTrash(pk); ExecuteSkillEffect(isPlayer, pk, lane); },
            aiPick: l => l.OrderByDescending(c => c.Cost).First());
    }

    // BT05-041: 믹스 엑시트 — 트래시의 트리거 없는 카드 9장까지 덱 맨 아래로
    private void BT05_MixExitReturnToHand(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        var owner = GetOwner(isPlayer);
        var cards = owner.TrashPile.Where(c => !c.IsTrigger).OrderByDescending(c => c.Cost).Take(9).ToList();
        foreach (var c in cards) { owner.RemoveFromTrash(c); owner.AddToDeckBottom(c); }
    }

    // BT05-041: 믹스 엑시트 — 덱 맨 아래로 놓은 수만큼(N까지) 상대 대미지 (근사: 고정 M)
    private void BT05_MixExitTrashToDeckDamage(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        var p = et.Split(':');
        int dmg = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 1;
        EffectActions.DamageOpponent(isPlayer, dmg);
    }

    // BT05-069: 믹스 엑시트 — DZ 카드1 패로 → 트래시 카드1 DZ로
    private void BT05_MixExitDamageZoneRecoverSwap(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        var owner = GetOwner(isPlayer);
        if (owner.DamageZone.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "패로 가져올 대미지 존 카드를 선택하세요", new List<CardData>(owner.DamageZone),
            dz =>
            {
                owner.RemoveFromDamageZone(dz); owner.AddToHand(dz);
                DamageZoneView.NotifyDamageChanged(); HUDView.NotifyStatusChanged(); HandView.Instance?.RefreshHand();
                var trashCands = owner.TrashPile.Where(c => !c.IsTrigger).ToList();
                if (trashCands.Count == 0) return;
                EffectActions.PickFromCards(isPlayer, "대미지 존에 놓을 트래시 카드를 선택하세요", trashCands,
                    tc => { owner.RemoveFromTrash(tc); PlaceToDamageZone(isPlayer, tc); },
                    aiPick: l => l.OrderByDescending(c => c.Cost).First());
            },
            aiPick: l => l.OrderByDescending(c => c.Cost).First());
    }

    // BT05-057: 이스케이프 아군 1장의 이스케이프 효과 발동 (근사: 덱 맨 아래로 + 드로우1)
    private void BT05_ActivateAllyEscapeEffect(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => u.Keywords.Contains("이스케이프"),
            "이스케이프를 발동할 아군을 선택하세요",
            pk => { BT05_MoveUnitToDeckBottom(isPlayer, pk); EffectActions.Draw(isPlayer, 1); });
    }

    // BT05-044: 트래시 엑시트 유닛 효과 발동 (믹스면 2회 근사) → 덱 맨 아래로
    private void BT05_ActivateTrashExitUnitMixDouble(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        BT05_ActivateTrashExitOnce(isPlayer);
        if (HasMixCondition(isPlayer, skill.Attribute)) BT05_ActivateTrashExitOnce(isPlayer);
    }

    private void BT05_ActivateTrashExitOnce(bool isPlayer)
    {
        var owner = GetOwner(isPlayer);
        var cands = owner.TrashPile.Where(c => c.Type == CardType.Unit && c.Keywords.Contains("엑시트") && !c.IsTrigger).ToList();
        if (cands.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "엑시트 효과를 발동할 유닛을 선택하세요", cands,
            picked =>
            {
                owner.RemoveFromTrash(picked);
                foreach (var pet in picked.EffectTypes)
                {
                    var (pt, pv) = Parse(pet);
                    if (pt.StartsWith("Exit") && _exitHandlers.TryGetValue(pt, out var h))
                        h(isPlayer, -1, picked, pv, pet);
                }
                owner.AddToDeckBottom(picked);
            },
            aiPick: l => l.OrderByDescending(c => c.Cost).First());
    }

    // BT05-058: 조우 중인 이스케이프 아군1의 히트만큼 드로우 (상대 조우 귀환 선택은 근사 생략)
    private void BT05_EscapeAllyEncounterReturnOrDrawByHit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer,
            (l, u) => u.Keywords.Contains("이스케이프") && FieldManager.Instance.GetUnit(!isPlayer, l) != null,
            "조우 중인 이스케이프 아군을 선택하세요",
            pk =>
            {
                var u = FieldManager.Instance.GetUnit(isPlayer, pk);
                if (u != null) EffectActions.Draw(isPlayer, Mathf.Max(1, GetEffectiveHit(isPlayer, pk, u)));
            });
    }

    // BT05-077: 트래시 아이템1 아군에 사이즈 무시 장착. 믹스면 이 카드 트래시(스킬이라 무효과)
    private void BT05_RecoverItemEquipMixSelfTrash(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var items = owner.TrashPile.Where(c => c.Type == CardType.Item).ToList();
        if (items.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "장착할 아이템을 선택하세요", items,
            it => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "장착 대상 아군을 선택하세요",
                al => { owner.RemoveFromTrash(it); FieldManager.Instance.EquipItem(isPlayer, al, it); }),
            aiPick: l => l.OrderByDescending(c => c.Cost).First());
    }

    // BT05-029: 자신 유닛1과 그 파워 이하 상대 유닛1 → 자신 유닛 덱 맨 아래로, 상대 유닛 이번 턴 방어 불가
    private void BT05_SwapAllyDeckBottomLockEnemyDefend(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "덱 맨 아래로 놓을 자신 유닛을 선택하세요",
            allyLane =>
            {
                var ally = FieldManager.Instance.GetUnit(isPlayer, allyLane);
                int pow = ally != null ? GetEffectivePower(isPlayer, allyLane, ally) : 0;
                EffectActions.SelectUnit(isPlayer, !isPlayer, (l2, u2) => GetEffectivePower(!isPlayer, l2, u2) <= pow,
                    "방어 불가로 만들 상대 유닛을 선택하세요",
                    enemyLane => { BT05_MoveUnitToDeckBottom(isPlayer, allyLane); DisableDefendLane(!isPlayer, enemyLane); });
            });
    }

    // BT05-044 트리거: 이 카드 트래시, 트래시 엑시트 유닛 효과 발동
    private void BT05_TriggerTrashSelfActivateExitUnit(bool isPlayer, CardData card, int value, string et)
    {
        EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
        BT05_ActivateTrashExitOnce(isPlayer);
    }

    // BT05-005 트리거: 이 카드 트래시, 트래시에서 트리거 없는 카드를 코스트합 N 이하로 회수
    private void BT05_TriggerTrashSelfRecoverNonTriggerByCostSum(bool isPlayer, CardData card, int value, string et)
    {
        EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
        EffectActions.RecoverFromTrash(isPlayer, c => !c.IsTrigger && c.Cost <= value,
            $"트래시에서 회수할 {value}코스트 이하 카드를 선택하세요");
    }

    // ── 액티브 (TriggerActiveEffect 훅) ──
    private static readonly HashSet<string> _bt05ActiveTypes = new()
    {
        "ActiveMainDiscardGrantCostBreakthrough", "ActiveMainDiscardRecoverItemEquipSizeIgnore",
        "ActiveMainDiscardRecoverNonTriggerSkillByCost", "ActiveMainDiscardSkillZoneDamageNoAttack",
        "ActiveMainDualChoiceGrantExitReturnOrTrashAlly", "ActiveMainGrantEscapeDrawDamage",
        "ActiveMainRevealAllyHitPickDeckBottom", "ActiveMainTrashAllyGrantBreakthrough",
        "MixActiveDebuffEnemyPerSkillCount", "LevelLinkActiveDiscardPowerBreakthrough",
    };

    private bool TryBT05Active(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var (type, _) = Parse(et);
        if (!_bt05ActiveTypes.Contains(type)) return false;
        var owner = GetOwner(isPlayer);

        switch (type)
        {
            case "ActiveMainDiscardGrantCostBreakthrough": // 아군1 + 패1 트래시 → 그 유닛 돌파(이번 턴)
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "돌파를 부여할 자신 유닛을 선택하세요",
                    pk => EffectActions.DiscardHandOptional(isPlayer, 1, d => { if (d >= 1) _breakthroughGrants.Add(Key(isPlayer, pk)); }));
                break;
            case "ActiveMainTrashAllyGrantBreakthrough": // 아군1 트래시 → 이 유닛 돌파(이번 턴)
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "트래시할 자신 유닛을 선택하세요",
                    pk => { EffectActions.TrashUnit(isPlayer, pk); _breakthroughGrants.Add(Key(isPlayer, lane)); });
                break;
            case "ActiveMainDiscardRecoverNonTriggerSkillByCost": // 패1 트래시 → 트리거없고 N코이하 스킬 패로
                EffectActions.DiscardHandOptional(isPlayer, 1, d =>
                {
                    if (d < 1) return;
                    EffectActions.RecoverFromTrash(isPlayer, c => c.Type == CardType.Skill && !c.IsTrigger && c.Cost <= value,
                        $"트래시에서 회수할 {value}코스트 이하 스킬을 선택하세요");
                });
                break;
            case "ActiveMainDiscardRecoverItemEquipSizeIgnore": // 패1 트래시 → 트래시 아이템1 아군에 사이즈 무시 장착
                EffectActions.DiscardHandOptional(isPlayer, 1, d =>
                {
                    if (d < 1) return;
                    var items = owner.TrashPile.Where(c => c.Type == CardType.Item).ToList();
                    if (items.Count == 0) return;
                    EffectActions.PickFromCards(isPlayer, "장착할 아이템을 선택하세요", items,
                        it => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "장착 대상 아군을 선택하세요",
                            al => { owner.RemoveFromTrash(it); FieldManager.Instance.EquipItem(isPlayer, al, it); }),
                        aiPick: l => l.OrderByDescending(c => c.Cost).First());
                });
                break;
            case "ActiveMainDiscardSkillZoneDamageNoAttack": // 패1 트래시 가능 → 스킬존 수만큼 대미지 + 이 유닛 공격 불가
                EffectActions.DiscardHandOptional(isPlayer, 1, d =>
                {
                    if (d < 1) return;
                    int cnt = owner.SkillZone.Count;
                    if (cnt > 0) EffectActions.DamageOpponent(isPlayer, cnt);
                    DisableAttack(isPlayer, lane, BoostUntil.EndOfTurn);
                });
                break;
            case "ActiveMainDualChoiceGrantExitReturnOrTrashAlly": // 근사: 아군1에 엑시트 귀환 부여
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "「엑시트 귀환」을 부여할 자신 유닛을 선택하세요",
                    pk => _grantedExitReturn.Add(Key(isPlayer, pk)));
                break;
            case "ActiveMainGrantEscapeDrawDamage": // 아군1 덱 맨 아래로 → 드로우1 + 상대 1대미지 (근사)
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "덱 맨 아래로 놓을 자신 유닛을 선택하세요",
                    pk => { BT05_MoveUnitToDeckBottom(isPlayer, pk); EffectActions.Draw(isPlayer, 1); EffectActions.DamageOpponent(isPlayer, 1); });
                break;
            case "MixActiveDebuffEnemyPerSkillCount": // 믹스면 상대 유닛1 파워-(스킬존 수 × N)
                if (HasMixCondition(isPlayer, card.Attribute))
                {
                    int deb = owner.SkillZone.Count * value;
                    EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true, $"파워-{deb}를 줄 상대 유닛", pk => AddTurnBoost(!isPlayer, pk, -deb));
                }
                break;
            case "LevelLinkActiveDiscardPowerBreakthrough": // 레벨링크:N 액티브 — 리더레벨≥N이면 패 트래시→돌파 (근사)
            {
                var lp = et.Split(':');
                int reqLv = value;
                if (GetOwner(isPlayer).LeaderLevel >= reqLv)
                    EffectActions.DiscardHandOptional(isPlayer, 1, d => { if (d >= 1) _breakthroughGrants.Add(Key(isPlayer, lane)); });
                break;
            }
            case "ActiveMainRevealAllyHitPickDeckBottom": // 아군1 히트만큼 덱 공개 → 1장 패, 나머지 트래시
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "기준이 될 자신 유닛을 선택하세요",
                    pk =>
                    {
                        var u = FieldManager.Instance.GetUnit(isPlayer, pk);
                        int n = u != null ? Mathf.Min(GetEffectiveHit(isPlayer, pk, u), owner.DrawPile.Count) : 0;
                        if (n == 0) return;
                        var revealed = owner.PeekDeckTop(n);
                        owner.RemoveDeckTop(n);
                        EffectActions.PickFromCards(isPlayer, "패에 넣을 카드를 선택하세요", new List<CardData>(revealed),
                            hp => { revealed.Remove(hp); owner.AddToHand(hp); foreach (var r in revealed) owner.AddToTrash(r); HandView.Instance?.RefreshHand(); },
                            aiPick: l => l.OrderByDescending(c => c.Cost).First());
                    });
                break;
        }
        return true;
    }

    // ── 아이템 어태커 (OnAttackDeclared 아이템 루프에서 호출) ──
    private void BT05_ItemAttacker(bool isPlayer, int lane, CardData unit, CardData item, string type, string et)
    {
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        switch (type)
        {
            case "ItemAttackerDraw": // 드로우N
                EffectActions.Draw(isPlayer, Parse(et).value > 0 ? Parse(et).value : 1);
                break;
            case "ItemAttackerConditionalBreakthroughByCostDiff": // 조우보다 코스트 N+ 낮으면 돌파
            {
                int diff = Parse(et).value;
                if (enc != null && unit.Cost <= enc.Cost - diff) _breakthroughGrants.Add(Key(isPlayer, lane));
                break;
            }
            case "ItemAttackerConditionalPenetration": // 조우보다 파워 N+ 높으면 관통[M] ("...:N:M")
            {
                var p = et.Split(':');
                int need = Parse(et).value, pen = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 1;
                if (enc != null && GetEffectivePower(isPlayer, lane, unit) >= GetEffectivePower(!isPlayer, lane, enc) + need)
                    _grantedPenetration[Key(isPlayer, lane)] = pen;
                break;
            }
            case "ItemMixAttackerPenetration": // 믹스면 관통[N]
                if (HasMixCondition(isPlayer, unit.Attribute))
                    _grantedPenetration[Key(isPlayer, lane)] = Parse(et).value > 0 ? Parse(et).value : 1;
                break;
            case "ItemAttackerRecoverByEquippedCount": // 트래시에서 코스트≤장착수인 스킬/아이템 1장 패로
            {
                int cap = FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count;
                EffectActions.RecoverFromTrash(isPlayer,
                    c => !c.IsTrigger && c.Cost <= cap && (c.Type == CardType.Skill || c.Type == CardType.Item),
                    $"트래시에서 회수할 {cap}코스트 이하 스킬/아이템을 선택하세요");
                break;
            }
        }
    }

    // ── 패시브 (GetPassiveBonus / GetEffectiveHit 훅) ──

    private int GetBT05PassiveBonus(bool isPlayer, int lane, CardData card)
    {
        int bonus = 0;
        int lv = GetOwner(isPlayer).LeaderLevel;
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            var p = et.Split(':');
            switch (type)
            {
                case "LevelLinkPowerBoost": // 리더레벨≥N이면 +M
                    if (lv >= value && p.Length > 2 && int.TryParse(p[2], out int m1)) bonus += m1;
                    break;
                case "LevelLinkPowerHitBoost": // 리더레벨≥N이면 +M (히트는 GetBT05HitBonus)
                    if (lv >= value && p.Length > 2 && int.TryParse(p[2], out int m2)) bonus += m2;
                    break;
                case "MixPowerBoost": // 믹스면 +N
                    if (HasMixCondition(isPlayer, card.Attribute)) bonus += value;
                    break;
                case "MixArmedPowerHitBoost": // 믹스+암드면 +N (히트는 별도)
                    if (HasMixCondition(isPlayer, card.Attribute) && FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count > 0)
                        bonus += value;
                    break;
                case "MixArmedPowerPerItemTypeThresholdAttacker": // 믹스면 장착 아이템 종류당 +N
                    if (HasMixCondition(isPlayer, card.Attribute))
                    {
                        int types = FieldManager.Instance.GetEquippedItems(isPlayer, lane).Select(it => it.CardName).Distinct().Count();
                        bonus += value * types;
                    }
                    break;
                case "PassiveEscapeAlliesBoost": // 이스케이프 아군 오라 (자기 포함)
                    // 아래 오라 합산에서 처리
                    break;
            }
        }
        // 오라: 다른/모든 아군의 Grant 패시브가 이 카드에 주는 보너스
        for (int i = 0; i < 3; i++)
        {
            var ally = FieldManager.Instance.GetUnit(isPlayer, i);
            if (ally?.EffectTypes == null) continue;
            foreach (var et in ally.EffectTypes)
            {
                var (type, value) = Parse(et);
                var p = et.Split(':');
                if (type == "PassiveEscapeAlliesBoost" && card.Keywords.Contains("이스케이프"))
                    bonus += value;
                else if (type == "PassiveGrantAllAttackerBoost" && i != lane) // 다른 모든 아군에 어태커 파워+N (근사: 상시)
                    bonus += value;
                else if (type == "PassiveGrantAllAttackerPlunderBoost" && i != lane) // 파워 부분만 (약탈은 근사 생략)
                {
                    if (p.Length > 2 && int.TryParse(p[2], out int pw)) bonus += pw;
                }
            }
        }
        return bonus;
    }

    private int GetBT05HitBonus(bool isPlayer, int lane, CardData card)
    {
        int hit = 0;
        int lv = GetOwner(isPlayer).LeaderLevel;
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (type == "LevelLinkPowerHitBoost")
            {
                var p = et.Split(':'); // N:M:H
                if (lv >= value && p.Length > 3 && int.TryParse(p[3], out int h)) hit += h;
            }
            else if (type == "MixArmedPowerHitBoost") // 믹스+암드면 히트+H ("N:H")
            {
                var p = et.Split(':');
                if (HasMixCondition(isPlayer, card.Attribute) && FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count > 0
                    && p.Length > 2 && int.TryParse(p[2], out int h)) hit += h;
            }
        }
        return hit;
    }

    // BT05-049 DefenderDiscardHitMinusOneEndAttack: 방어 유닛이 「공격 유닛 히트-1」만큼 패 트래시하면 공격 종료
    // CombatManager.ResolveEncounter에서 호출 — true면 전투 무효
    public bool TryDefenderDiscardEndAttack(bool defenderIsPlayer, int lane, CardData attacker)
    {
        var def = FieldManager.Instance.GetUnit(defenderIsPlayer, lane);
        // SB02-027 MixPassiveEscapeCancelAttack: 믹스면 이 유닛을 덱 맨 아래로 놓아 공격 종료
        if (def != null && def.EffectTypes.Contains("MixPassiveEscapeCancelAttack")
            && HasMixCondition(defenderIsPlayer, def.Attribute))
        {
            BT05_MoveUnitToDeckBottom(defenderIsPlayer, lane);
            Debug.Log($"[SB02] {def.CardName} → 덱 맨 아래로, 공격 종료");
            return true;
        }
        if (def == null || !def.EffectTypes.Contains("DefenderDiscardHitMinusOneEndAttack")) return false;
        int need = Mathf.Max(0, GetEffectiveHit(!defenderIsPlayer, lane, attacker) - 1);
        var owner = GetOwner(defenderIsPlayer);
        if (owner.Hand.Count < need) return false; // 지불 불가
        if (!defenderIsPlayer)
        {
            // AI: 이기지 못하는 조우면 발동 (간소화: 항상 발동)
            for (int i = 0; i < need; i++) ApplyForceDiscard(defenderIsPlayer);
            return true;
        }
        // 플레이어: need장 트래시 후 종료 (팝업 없이 자동 — 방어 이득이므로)
        for (int i = 0; i < need; i++) ApplyForceDiscard(defenderIsPlayer);
        return true;
    }

    // BT05-046 ItemPassiveEndOpponentTurnDiscardOrTrashUnit: 상대 턴 끝에 패1 트래시(가능) 아니면 유닛 트래시
    // OnTurnEnd(isPlayerTurn)에서 호출 — 소유자 O의 "상대 턴 끝" = O가 !isPlayerTurn일 때
    private void ProcessBT05EndOpponentTurn(bool isPlayerTurn)
    {
        bool owner = !isPlayerTurn; // 이 플레이어의 상대 턴이 방금 끝남
        for (int lane = 0; lane < 3; lane++)
        {
            var unit = FieldManager.Instance.GetUnit(owner, lane);
            if (unit == null) continue;
            bool has = FieldManager.Instance.GetEquippedItems(owner, lane)
                .Any(it => it.EffectTypes.Contains("ItemPassiveEndOpponentTurnDiscardOrTrashUnit"));
            if (!has) continue;
            if (GetOwner(owner).Hand.Count > 0)
                EffectActions.DiscardHand(owner, 1); // 패 트래시로 유지
            else
                CombatManager.Instance?.TrashUnitPublic(owner, lane); // 패 없으면 유닛 트래시
        }
    }

    // 유닛을 주인의 덱 맨 아래로 (이스케이프식 이동) — 공용 헬퍼
    private void BT05_MoveUnitToDeckBottom(bool isPlayer, int lane)
    {
        var unit = FieldManager.Instance.GetUnit(isPlayer, lane);
        if (unit == null) return;
        FieldManager.Instance.RemoveUnit(isPlayer, lane);
        GetOwner(isPlayer).AddToDeckBottom(unit);
        NotifyAllyMovedToDeckBottom(isPlayer, lane, unit);
        EffectActions.RefreshUI();
    }

    // ── 배치 2 핸들러 ──

    // BT05-036: 트래시존 엑시트(트리거 없는) 유닛 효과를 하나 발동 → 그 카드 덱 맨 아래로
    private void BT05_EntryActivateTrashExitUnit(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var cands = owner.TrashPile.Where(c => c.Type == CardType.Unit && c.Keywords.Contains("엑시트") && !c.IsTrigger).ToList();
        if (cands.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "엑시트 효과를 발동할 유닛을 선택하세요", cands,
            picked =>
            {
                owner.RemoveFromTrash(picked);
                foreach (var pet in picked.EffectTypes)
                {
                    var (pt, _) = Parse(pet);
                    if (pt.StartsWith("Exit") && _exitHandlers.TryGetValue(pt, out var h))
                        h(isPlayer, -1, picked, Parse(pet).value, pet);
                }
                owner.AddToDeckBottom(picked); // 덱 맨 아래
                EffectActions.RefreshUI();
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // BT05-065: 믹스 → 덱 맨 위 N장 트래시 → 대미지 존에서 1장 패로
    private void BT05_MixEntryMillRecoverFromDamageZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        EffectActions.Mill(isPlayer, value);
        var owner = GetOwner(isPlayer);
        if (owner.DamageZone.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "패에 넣을 대미지 존 카드를 선택하세요", new List<CardData>(owner.DamageZone),
            picked =>
            {
                owner.RemoveFromDamageZone(picked); owner.AddToHand(picked);
                DamageZoneView.NotifyDamageChanged(); HUDView.NotifyStatusChanged(); HandView.Instance?.RefreshHand();
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // BT05-010: 트래시존 카드1(트리거 없는) 덱 맨 아래로 → 조우 유닛 파워-N (믹스면 2배 근사)
    private void BT05_EntryMillDeckBottomDebuffEncounterDouble(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var cands = owner.TrashPile.Where(c => !c.IsTrigger).ToList();
        int mult = HasMixCondition(isPlayer, card.Attribute) ? 2 : 1;
        void Debuff() { if (FieldManager.Instance.GetUnit(!isPlayer, lane) != null) AddTurnBoost(!isPlayer, lane, -value * mult); }
        if (cands.Count == 0) { Debuff(); return; }
        EffectActions.PickFromCards(isPlayer, "덱 맨 아래로 놓을 트래시 카드를 선택하세요", cands,
            picked => { owner.RemoveFromTrash(picked); owner.AddToDeckBottom(picked); Debuff(); },
            aiPick: list => list.OrderBy(c => c.Cost).First(), onCancel: Debuff);
    }

    // BT05-040: 믹스 엑시트 — 타 속성 카드 있으면 최저 코스트 상대 유닛 트래시
    private void BT05_MixExitTrashLowestCostEnemy(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        var enemy = Enumerable.Range(0, 3)
            .Select(i => (i, u: FieldManager.Instance.GetUnit(!isPlayer, i)))
            .Where(t => t.u != null).OrderBy(t => t.u.Cost).FirstOrDefault();
        if (enemy.u != null) EffectActions.TrashUnit(!isPlayer, enemy.i);
    }

    // BT05-037: 믹스 엑시트 — 트래시존에서 트리거 없고 N코이하 유닛 1장 빈 존에 배치
    private void BT05_MixExitReviveNonTriggerLowCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        var owner = GetOwner(isPlayer);
        var cands = owner.TrashPile.Where(c => c.Type == CardType.Unit && !c.IsTrigger && c.Cost <= value).ToList();
        if (cands.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, $"배치할 {value}코스트 이하 유닛을 선택하세요", cands,
            picked =>
            {
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) == null)
                    {
                        owner.RemoveFromTrash(picked);
                        FieldManager.Instance.ForcePlace(isPlayer, i, picked);
                        OnUnitPlaced(isPlayer, i, picked);
                        break;
                    }
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // BT05-038: 믹스 엑시트 — 조우 유닛을 가지지 않은(다이렉트 가능) 상대 유닛 1장 트래시
    private void BT05_MixExitTrashUnencounteredEnemy(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        EffectActions.SelectUnit(isPlayer, !isPlayer,
            (l, u) => FieldManager.Instance.GetUnit(isPlayer, l) == null, // 조우 상대 없음
            "트래시할 상대 유닛(조우 없는)을 선택하세요",
            picked => EffectActions.TrashUnit(!isPlayer, picked));
    }

    // BT05-015: 상대 유닛 1장에 「트래시 시 자신 트래시 대신 대미지 존으로」 부여 (이번 턴)
    private void BT05_GrantEnemyDamageZoneOnTrash(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
            "「트래시 시 대미지 존으로」를 부여할 상대 유닛을 선택하세요",
            picked => _grantDamageZoneOnTrash.Add(Key(!isPlayer, picked)));
    }

    // BT05-026: 3코스트 이하 아군 1장에 이번 턴 조건부 돌파 부여 (조우보다 파워 높으면 돌파 — 근사: 이번 턴 돌파)
    private void BT05_GrantLowCostAttackerConditionalBreakthrough(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => u.Cost <= value,
            $"돌파를 부여할 {value}코스트 이하 아군을 선택하세요",
            picked => _breakthroughGrants.Add(Key(isPlayer, picked)));
    }

    // BT05-013: 트리거 없는 아군 1장 덱 맨 아래로 → 그보다 코스트 낮고 트리거 없는 카드 트래시존에서 패로
    private void BT05_UnitDeckBottomRecoverLowerCostNonTrigger(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => !u.IsTrigger,
            "덱 맨 아래로 놓을 아군 유닛을 선택하세요",
            pickedLane =>
            {
                var unit = FieldManager.Instance.GetUnit(isPlayer, pickedLane);
                int cap = unit?.Cost ?? 0;
                BT05_MoveUnitToDeckBottom(isPlayer, pickedLane);
                EffectActions.RecoverFromTrash(isPlayer, c => !c.IsTrigger && c.Cost < cap,
                    $"트래시에서 회수할 {cap}코스트 미만 카드를 선택하세요");
            });
    }

    // BT05-027: 아군 1장 덱 맨 아래로 → 패에서 그 코스트+N 이하 유닛을 그 자리에 사이즈 무시 배치
    private void BT05_SwapUnitDeckBottomDeploySizeIgnore(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            "덱 맨 아래로 놓을 아군 유닛을 선택하세요",
            pickedLane =>
            {
                var unit = FieldManager.Instance.GetUnit(isPlayer, pickedLane);
                int cap = (unit?.Cost ?? 0) + value;
                BT05_MoveUnitToDeckBottom(isPlayer, pickedLane);
                var owner = GetOwner(isPlayer);
                var cands = owner.Hand.Where(c => c.Type == CardType.Unit && c.Cost <= cap).ToList();
                if (cands.Count == 0) return;
                EffectActions.PickFromCards(isPlayer, $"배치할 {cap}코스트 이하 유닛을 선택하세요", cands,
                    dep =>
                    {
                        owner.RemoveFromHand(dep);
                        FieldManager.Instance.ForcePlace(isPlayer, pickedLane, dep);
                        OnUnitPlaced(isPlayer, pickedLane, dep);
                    },
                    aiPick: l => l.OrderByDescending(c => c.Cost).First());
            });
    }

    // BT05-028: 아군 1장 덱 맨 아래로 → 덱 위 N장 공개, 유닛 1장 빈 존에 사이즈 무시 배치(0코스트화 근사 생략)
    private void BT05_SwapUnitRevealDeploySizeIgnoreZeroCost(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            "덱 맨 아래로 놓을 아군 유닛을 선택하세요",
            pickedLane =>
            {
                BT05_MoveUnitToDeckBottom(isPlayer, pickedLane);
                RevealAndDeployUnit(isPlayer, value);
            });
    }

    // BT05-042: 트래시존 엑시트(트리거 없는) 유닛 덱 맨 아래로 → 그 코스트 이하 아군 히트+N
    private void BT05_TrashExitUnitDeckBottomBuffAllyHit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var cands = owner.TrashPile.Where(c => c.Type == CardType.Unit && c.Keywords.Contains("엑시트") && !c.IsTrigger).ToList();
        if (cands.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "덱 맨 아래로 놓을 엑시트 유닛을 선택하세요", cands,
            picked =>
            {
                owner.RemoveFromTrash(picked); owner.AddToDeckBottom(picked);
                int cap = picked.Cost;
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => u.Cost <= cap,
                    $"히트+{value}를 줄 {cap}코스트 이하 아군을 선택하세요",
                    allyLane => AddTurnHitBoost(isPlayer, allyLane, value));
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // BT05-078: 아이템 장착 아군 1장 트래시 → 드로우2 + 믹스면 상대 1대미지
    private void BT05_TrashEquippedAllyDraw2MixDamage(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer,
            (l, u) => FieldManager.Instance.GetEquippedItems(isPlayer, l).Count > 0,
            "트래시할 아이템 장착 아군을 선택하세요",
            picked =>
            {
                EffectActions.TrashUnit(isPlayer, picked);
                EffectActions.Draw(isPlayer, 2);
                if (HasMixCondition(isPlayer, skill.Attribute)) EffectActions.DamageOpponent(isPlayer, 1);
            });
    }

    // BT05-076: 드로우N → 패N장 트래시 → 트래시존 유니크·카드명 다른 아이템 N장까지 패로 ("...:N:M")
    private void BT05_DrawDiscardRecoverUniqueItems(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var p = et.Split(':');
        int discardN = p.Length > 2 && int.TryParse(p[2], out int m) ? m : value;
        EffectActions.Draw(isPlayer, value);
        EffectActions.DiscardHand(isPlayer, discardN, _ =>
        {
            var owner = GetOwner(isPlayer);
            var items = owner.TrashPile.Where(c => c.Type == CardType.Item && c.Keywords.Contains("유니크"))
                .GroupBy(c => c.CardName).Select(g => g.First()).Take(discardN).ToList();
            foreach (var it in items) { owner.RemoveFromTrash(it); owner.AddToHand(it); }
            HandView.Instance?.RefreshHand();
        });
    }

    // ── 어태커 ──

    // BT05-002: 믹스 어태커 — 타 속성 카드 있으면 조우 유닛 파워-N (이 공격)
    private void BT05_MixAttackerDebuffEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (HasMixCondition(isPlayer, card.Attribute))
            AddTurnBoost(!isPlayer, lane, -value);
    }

    // BT05-009: 어태커 — 조우 유닛 파워-N + 트래시에서 M코스트 이하 스킬 회수 ("...:N:M")
    private void BT05_AttackerDebuffEncounterRecoverSkill(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var p = et.Split(':');
        int cap = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 2;
        if (FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
            AddTurnBoost(!isPlayer, lane, -value);
        EffectActions.RecoverFromTrash(isPlayer,
            c => c.Type == CardType.Skill && c.Cost <= cap,
            $"트래시에서 회수할 {cap}코스트 이하 스킬을 선택하세요");
    }

    // ── 엔트리 ──

    // BT05-003: 믹스 엔트리 — 타 속성 카드 있으면 조우 유닛 파워-N (이번 턴)
    private void BT05_MixEntryDebuffEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (HasMixCondition(isPlayer, card.Attribute) && FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
            AddTurnBoost(!isPlayer, lane, -value);
    }

    // BT05-034: 다른 자신 유닛 1장에 「엑시트 귀환」 부여 (상대 턴 끝까지 — 근사: 이번 턴)
    private void BT05_EntryGrantExitReturn(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => l != lane,
            "「엑시트 귀환」을 부여할 다른 자신 유닛을 선택하세요",
            picked => _grantedExitReturn.Add(Key(isPlayer, picked)));
    }

    // BT05-066: 덱 맨 위 N장 트래시. 아이템 1장 이상이면 드로우1
    private void BT05_EntryMillDrawIfItem(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var milled = EffectActions.Mill(isPlayer, value);
        if (milled.Any(c => c.Type == CardType.Item)) EffectActions.Draw(isPlayer, 1);
    }

    // BT05-070: 패 N장까지 트래시→그만큼 드로우. 트래시한 아이템 2+이면 추가 드로우1 ("...:N:M")
    private void BT05_EntryDiscardDrawIfItemDouble(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var p = et.Split(':');
        int itemThresh = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 2;
        var owner = GetOwner(isPlayer);
        var before = new List<CardData>(owner.Hand);
        EffectActions.DiscardHandOptional(isPlayer, value, discarded =>
        {
            if (discarded <= 0) return;
            EffectActions.Draw(isPlayer, discarded);
            // 트래시된 아이템 수 추정 (트래시 파일 최근 discarded장 중 아이템)
            int itemCnt = owner.TrashPile.Skip(System.Math.Max(0, owner.TrashPile.Count - discarded)).Count(c => c.Type == CardType.Item);
            if (itemCnt >= itemThresh) EffectActions.Draw(isPlayer, 1);
        });
    }

    // BT05-040: 다른 자신 유닛 1장 트래시. 못 하면(취소) 패 1장 트래시
    private void BT05_EntryTrashAllyOrDiscard(bool isPlayer, int lane, CardData card, int value, string et)
    {
        bool anyOther = Enumerable.Range(0, 3).Any(i => i != lane && FieldManager.Instance.GetUnit(isPlayer, i) != null);
        if (!anyOther) { EffectActions.DiscardHand(isPlayer, 1); return; }
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => l != lane,
            "트래시할 다른 자신 유닛을 선택하세요 (취소 시 패 트래시)",
            picked => EffectActions.TrashUnit(isPlayer, picked),
            onCancel: () => EffectActions.DiscardHand(isPlayer, 1));
    }

    // BT05-072: 덱 맨 위 N장 공개 → N장까지 트래시, 나머지는 덱에 넣고 섞기
    private void BT05_EntryRevealMillOrShuffleback(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        int n = Mathf.Min(value, owner.DrawPile.Count);
        if (n == 0) return;
        var revealed = owner.PeekDeckTop(n);
        owner.RemoveDeckTop(n);
        // AI/간소화: 유닛 아닌(스킬/아이템) 카드는 남기고 나머지 트래시 — 여기선 전부 셔플백 후 트래시 선택
        EffectActions.PickFromCards(isPlayer, "트래시할 공개 카드를 선택하세요 (취소 시 전부 덱으로)", new List<CardData>(revealed),
            picked =>
            {
                revealed.Remove(picked); owner.AddToTrash(picked);
                foreach (var r in revealed) owner.AddToDeckBottom(r);
                EffectActions.ShuffleDeck(isPlayer);
            },
            aiPick: list => list.OrderBy(c => c.Cost).First(),
            onCancel: () => { foreach (var r in revealed) owner.AddToDeckBottom(r); EffectActions.ShuffleDeck(isPlayer); });
    }

    // ── 엑시트 ──

    // BT05-033: 상대 유닛 1장 파워-N. 믹스면 추가 파워-M ("...:N:M")
    private void BT05_ExitMixDebuffEnemy(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var p = et.Split(':');
        int extra = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 0;
        int total = value + (HasMixCondition(isPlayer, card.Attribute) ? extra : 0);
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
            $"파워-{total}를 줄 상대 유닛을 선택하세요",
            picked => AddTurnBoost(!isPlayer, picked, -total));
    }

    // ── 스킬 ──

    // BT05-075: 아이템 장착 아군 1장 이번 턴 히트+N
    private void BT05_BuffEquippedAllyHit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer,
            (l, u) => FieldManager.Instance.GetEquippedItems(isPlayer, l).Count > 0,
            $"히트+{value}를 줄 아이템 장착 아군을 선택하세요",
            picked => AddTurnHitBoost(isPlayer, picked, value));
    }

    // BT05-014: 리더 레벨 이하 코스트 아군 전체 이번 턴 듀얼리스트
    private void BT05_GrantAlliesUnderLevelDuelist(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        int lv = GetOwner(isPlayer).LeaderLevel;
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.Cost <= lv) _grantedDuelistThisTurn.Add(Key(isPlayer, i));
        }
    }

    // BT05-059: 코스트가 리더 레벨 이상인 상대 유닛 1장 이번 턴 방어 불가
    private void BT05_DisableDefendEnemyByLevel(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        int lv = GetOwner(isPlayer).LeaderLevel;
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => u.Cost >= lv,
            "방어 불가로 만들 상대 유닛을 선택하세요",
            picked => DisableDefendLane(!isPlayer, picked));
    }

    // BT05-074: 패의 아이템 카드 1장 이상 트래시 → 그만큼 드로우
    private void BT05_TrashHandItemsDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var items = owner.Hand.Where(c => c.Type == CardType.Item).ToList();
        if (items.Count == 0) return;
        // 간소화: 아이템 전부 트래시 → 그 수만큼 드로우 (플레이어도 자동 — 순이득)
        int cnt = items.Count;
        foreach (var it in items) { owner.RemoveFromHand(it); owner.AddToTrash(it); }
        HandView.Instance?.RefreshHand();
        EffectActions.Draw(isPlayer, cnt);
    }

    // BT05-043: 패의 유닛 1장 트래시 → 그보다 코스트 낮은 상대 필드 유닛 1장 트래시
    private void BT05_TrashHandUnitTrashLowerCostFieldUnit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var units = owner.Hand.Where(c => c.Type == CardType.Unit).ToList();
        if (units.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "트래시할 패 유닛을 선택하세요", units,
            picked =>
            {
                owner.RemoveFromHand(picked); owner.AddToTrash(picked);
                HandView.Instance?.RefreshHand();
                EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => u.Cost < picked.Cost,
                    $"트래시할 {picked.Cost}코스트 미만 상대 유닛을 선택하세요",
                    enemyLane => EffectActions.TrashUnit(!isPlayer, enemyLane));
            },
            aiPick: list => list.OrderBy(c => c.Cost).First());
    }
}
