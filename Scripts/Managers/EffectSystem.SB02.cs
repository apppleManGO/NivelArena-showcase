// Assets/Scripts/Managers/EffectSystem.SB02.cs
// ─────────────────────────────────────────────────────────────────────────
// SB02 (콜라보 세트, 마지막 세트) effectType 구현 — 격리 파일.
//  테마: 믹스 + 이스케이프 + 크레딧 + 「이 턴 덱 맨 아래에 놓인 카드 수」 참조 + 히트 임계값 + 아이템/암드.
//  기존 인프라 재사용: HasMixCondition, ProcessEscapeUnits, BT04 대미지존 헬퍼, 크레딧, BT05 덱-밑 이동.
//  핸들러는 기존 레지스트리에 InitSB02Handlers()로 등록 (Awake 호출).
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class EffectSystem : MonoBehaviour
{
    // 이 턴 동안 자신의 덱 맨 아래에 놓인 카드 수 (SB02 다수 참조) — OnTurnEnd에서 초기화
    private int[] _cardsToDeckBottomThisTurn = { 0, 0 };

    // 카드를 지정 존에서 덱 맨 아래로 (카운트 포함)
    private void SB02_CardToDeckBottom(bool isPlayer, CardData card, CardZone fromZone)
    {
        EffectActions.MoveCard(isPlayer, card, fromZone, CardZone.DeckBottom);
        _cardsToDeckBottomThisTurn[Idx(isPlayer)]++;
    }

    // 트래시에서 트리거 없는 카드 최대 count장을 덱 맨 아래로.
    // 카드 텍스트가 "골라 ... 원하는 순서대로 놓는다"이므로 플레이어가 1장씩 직접 선택(AI는 고코스트 우선 자동).
    // 선택이 팝업 기반이라 비동기 — 후속 효과는 onDone(실제로 놓은 장수)에서 이어갈 것.
    private void SB02_ReturnTrashToBottom(bool isPlayer, int count, System.Action<int> onDone = null)
    {
        var owner = GetOwner(isPlayer);
        var pool = owner.TrashPile.Where(c => !c.IsTrigger).ToList();
        PickCardsToDeckBottom(isPlayer, pool, CardZone.Trash, Mathf.Min(count, pool.Count), onDone);
    }

    private void InitSB02Handlers()
    {
        _entryHandlers["EntryMillReturnCards"]       = SB02_EntryMillReturnCards;
        _entryHandlers["EntryReturnCardsDraw"]       = SB02_EntryReturnCardsDraw;
        _entryHandlers["EntryRecoverByBottomCount"]  = SB02_EntryRecoverByBottomCount;
        _entryHandlers["EntryReturnItemsTrashEnemy"] = SB02_EntryReturnItemsTrashEnemy;
        _entryHandlers["EntryEquipItemsFromTrash"]   = SB02_EntryEquipItemsFromTrash;
        _entryHandlers["MixEntryEquipFromDamageZone"] = SB02_MixEntryEquipFromDamageZone;
        _entryHandlers["MixEntryRevealSearchLevelUp"] = SB02_MixEntryRevealSearchLevelUp;
        _exitHandlers["MixExitDebuffDraw"]           = SB02_MixExitDebuffDraw;
        _skillHandlers["ReturnCardsDraw"]            = SB02_ReturnCardsDraw;
        _skillHandlers["ReturnCardsDebuffMixDraw"]   = SB02_ReturnCardsDebuffMixDraw;
        _skillHandlers["SwapDamageZoneItems"]        = SB02_SwapDamageZoneItems;
        _skillHandlers["DeployTwoLowCostMixBuff"]    = SB02_DeployTwoLowCostMixBuff;
        _skillHandlers["DrawPerEscapeActivate"]      = SB02_DrawPerEscapeActivate;
        _attackerHandlers["AttackerDiscardChooseEffects"] = SB02_AttackerDiscardTiered;
        _attackerHandlers["AttackerDiscardHandThresholds"] = SB02_AttackerDiscardTiered;
        _attackerHandlers["AttackerDiscardDebuffPerHandDiff"] = SB02_AttackerDiscardDebuffPerHandDiff;
        _attackerHandlers["ItemAttackerSetHit"]      = SB02_ItemAttackerSetHit;
        _attackerHandlers["ItemAttackerRevealTakeTrashSelf"] = SB02_ItemAttackerRevealTakeTrashSelf;
        _entryHandlers["EntryGrantHandScalingAura"]  = SB02_EntryGrantHandScalingAura;
        _exitHandlers["MixExitDeployEscapeUnit"]     = SB02_MixExitDeployEscapeUnit;
    }

    // 패/트래시에서 이스케이프 유닛 1장을 빈 유닛 존에 배치 (공용)
    private void SB02_DeployEscapeUnit(bool isPlayer)
    {
        var owner = GetOwner(isPlayer);
        var cands = owner.Hand.Concat(owner.TrashPile)
            .Where(c => c.Type == CardType.Unit && c.Keywords.Contains("이스케이프")).ToList();
        if (cands.Count == 0) return;
        int empty = -1;
        for (int i = 0; i < 3; i++) if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { empty = i; break; }
        if (empty < 0) return;
        EffectActions.PickFromCards(isPlayer, "배치할 이스케이프 유닛을 선택하세요", cands,
            pk => { owner.RemoveFromHand(pk); owner.RemoveFromTrash(pk); FieldManager.Instance.ForcePlace(isPlayer, empty, pk); OnUnitPlaced(isPlayer, empty, pk); },
            aiPick: l => l.OrderByDescending(c => c.Cost).First());
    }

    // SB02-027: 믹스 엑시트 — 이스케이프 유닛 배치
    private void SB02_MixExitDeployEscapeUnit(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (HasMixCondition(isPlayer, card.Attribute)) SB02_DeployEscapeUnit(isPlayer);
    }

    // 필드에 MixPassiveCreditAdjust(믹스 충족) 유닛이 있으면 크레딧 트래시 장수 조정 가능 (근사: +0)
    private bool SB02_HasCreditAdjust(bool isPlayer)
    {
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.EffectTypes.Contains("MixPassiveCreditAdjust") && HasMixCondition(isPlayer, u.Attribute))
                return true;
        }
        return false;
    }

    // 자신 턴 종료 시: ItemEndTurnDisableEncounter — 공격했고 방어당하지 않았으면 조우 상대 다음 턴 공격 불가 (근사)
    public void ProcessSB02TurnEnd(bool isPlayerTurn)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            bool has = FieldManager.Instance.GetEquippedItems(isPlayerTurn, lane)
                .Any(it => it.EffectTypes.Contains("ItemEndTurnDisableEncounter"));
            if (!has) continue;
            var enc = FieldManager.Instance.GetUnit(!isPlayerTurn, lane);
            if (enc != null) DisableAttack(!isPlayerTurn, lane, BoostUntil.EndOfOpponentTurn);
        }
    }

    // ── 패시브 (GetPassiveBonus / GetEffectiveHit 훅) ──

    private int GetSB02PassiveBonus(bool isPlayer, int lane, CardData card)
    {
        int bonus = 0;
        int hand = GetOwner(isPlayer).Hand.Count;
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            switch (type)
            {
                case "PassivePowerPerHandBonus":       bonus += hand * value; break;       // 패 1장마다 +N
                case "PassiveThresholdPenetrationHit":  bonus -= hand * 1000; break;         // 패 1장마다 -1000 (관통/히트는 별도)
                case "PassiveHitThresholds":
                case "PassiveHitThresholdsMixPenetration":
                case "PassiveHitThresholdsReveal":
                    if (GetEffectiveHit(isPlayer, lane, card) >= 2) bonus += value;          // 히트≥2 → +N
                    break;
                case "MixArmedPowerPerItemAllyHit":
                    if (HasMixCondition(isPlayer, card.Attribute))
                        bonus += value * FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count;
                    break;
            }
        }
        return bonus;
    }

    private int GetSB02HitBonus(bool isPlayer, int lane, CardData card)
    {
        int hit = 0;
        int hand = GetOwner(isPlayer).Hand.Count;
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (type == "PassivePowerPerHandBonus" && hand >= 8) hit += 1;                    // 패≥8 히트+1
            else if (type == "PassiveThresholdPenetrationHit"
                     && GetEffectivePower(isPlayer, lane, card) >= value) hit += 1;           // 파워≥N 히트+1
        }
        // 오라: 이스케이프/암드 아군에게 주는 히트
        bool armed = FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count > 0;
        for (int i = 0; i < 3; i++)
        {
            var ally = FieldManager.Instance.GetUnit(isPlayer, i);
            if (ally?.EffectTypes == null) continue;
            foreach (var et in ally.EffectTypes)
            {
                var (type, _) = Parse(et);
                if (type == "MixPassiveEscapeAlliesHitBoost" && i != lane
                    && card.Keywords.Contains("이스케이프") && HasMixCondition(isPlayer, ally.Attribute)) hit += 1;
                else if (type == "MixArmedPowerPerItemAllyHit" && armed
                    && card.Keywords.Contains("암드") && HasMixCondition(isPlayer, ally.Attribute)) hit += 1;
            }
        }
        return hit;
    }

    // SB02-026: 다른 아군 1장에 「패 1장마다 파워+500(패≥8 히트+1)」 부여 — 근사: 현재 패 기준 즉시 버프
    private void SB02_EntryGrantHandScalingAura(bool isPlayer, int lane, CardData card, int value, string et)
    {
        int hand = GetOwner(isPlayer).Hand.Count;
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => l != lane,
            "패 스케일링을 부여할 다른 자신 유닛을 선택하세요",
            pk => EffectActions.Buff(isPlayer, pk, hand * value, hand >= 8 ? 1 : 0, BoostUntil.EndOfOpponentTurn));
    }

    // ── 액티브 (TriggerActiveEffect 훅) ──
    private static readonly HashSet<string> _sb02ActiveTypes = new()
    {
        "ActiveAttackAllyHitBoost", "ActiveBounceDeployByBottomUnits", "ActiveDiscardRecoverCreditUnit",
        "MixActiveActivateAllyEscape", "MixActiveAttackTrashEncounterByBottom",
        "MixActiveAttackTrashEnemyByBottom", "MixArmedActiveTrashEncounterByDamageZone",
    };

    private bool TrySB02Active(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var (type, _) = Parse(et);
        if (!_sb02ActiveTypes.Contains(type)) return false;
        int bottom = _cardsToDeckBottomThisTurn[Idx(isPlayer)];
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);

        switch (type)
        {
            case "ActiveAttackAllyHitBoost": // 3코이하 다른 아군1 히트+1
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => l != lane && u.Cost <= value,
                    "히트+1을 줄 다른 자신 유닛을 선택하세요", pk => AddTurnHitBoost(isPlayer, pk, 1));
                break;
            case "MixActiveActivateAllyEscape":
            case "ActiveBounceDeployByBottomUnits": // 이스케이프 아군1 발동 (근사: 덱 맨 아래로)
            {
                if (type.StartsWith("Mix") && !HasMixCondition(isPlayer, card.Attribute)) break;
                int el = -1;
                for (int i = 0; i < 3; i++)
                    if (i != lane && FieldManager.Instance.GetUnit(isPlayer, i) is { } u && u.Keywords.Contains("이스케이프")) { el = i; break; }
                if (el >= 0) BT05_MoveUnitToDeckBottom(isPlayer, el);
                break;
            }
            case "ActiveDiscardRecoverCreditUnit": // 패 1~3장 트래시 → 크레딧 유닛 회수 (근사: 크레딧 유닛 회수)
                EffectActions.DiscardHandOptional(isPlayer, Mathf.Min(3, GetOwner(isPlayer).Hand.Count), d =>
                {
                    if (d < 1) return;
                    EffectActions.RecoverFromTrash(isPlayer,
                        c => c.Type == CardType.Unit && !c.IsTrigger && c.Keywords.Contains("크레딧"),
                        "트래시에서 회수할 크레딧 유닛을 선택하세요");
                });
                break;
            case "MixActiveAttackTrashEncounterByBottom": // 믹스 + 조우 코스트 ≤ 덱밑 수면 조우 트래시
                if (HasMixCondition(isPlayer, card.Attribute) && enc != null && enc.Cost <= bottom)
                    EffectActions.TrashUnit(!isPlayer, lane);
                break;
            case "MixActiveAttackTrashEnemyByBottom": // 믹스 + 코스트 ≤ 덱밑 수인 상대 유닛 트래시
                if (HasMixCondition(isPlayer, card.Attribute))
                    EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => u.Cost <= bottom,
                        "트래시할 상대 유닛을 선택하세요", pk => EffectActions.TrashUnit(!isPlayer, pk));
                break;
            case "MixArmedActiveTrashEncounterByDamageZone": // 믹스+암드 + 조우 코스트 < DZ 아이템 코스트합이면 조우 트래시
            {
                if (!HasMixCondition(isPlayer, card.Attribute) || FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count == 0) break;
                int dzSum = GetOwner(isPlayer).DamageZone.Where(c => c.Type == CardType.Item).Sum(c => c.Cost);
                if (enc != null && enc.Cost < dzSum) EffectActions.TrashUnit(!isPlayer, lane);
                break;
            }
        }
        return true;
    }

    // ── 엔트리 ──

    // SB02-018: 덱top3 트래시(가능) → 트래시 트리거없는 카드 1~3장 덱 맨 아래로
    private void SB02_EntryMillReturnCards(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.Mill(isPlayer, value);
        SB02_ReturnTrashToBottom(isPlayer, 3);
    }

    // SB02-020: 트래시 트리거없는 카드 N장 덱 맨 아래로 → 드로우1
    private void SB02_EntryReturnCardsDraw(bool isPlayer, int lane, CardData card, int value, string et)
        => SB02_ReturnTrashToBottom(isPlayer, value, moved =>
        {
            if (moved > 0) EffectActions.Draw(isPlayer, 1);
        });

    // SB02-019: 코스트 ≤ (이 턴 덱 맨 아래 놓인 수)인 트리거없는 카드 트래시에서 패로
    private void SB02_EntryRecoverByBottomCount(bool isPlayer, int lane, CardData card, int value, string et)
    {
        int cap = _cardsToDeckBottomThisTurn[Idx(isPlayer)];
        EffectActions.RecoverFromTrash(isPlayer, c => !c.IsTrigger && c.Cost <= cap,
            $"트래시에서 회수할 {cap}코스트 이하 카드를 선택하세요");
    }

    // SB02-036: 3코 이상 상대 유닛1 → 그 코스트만큼 트래시 아이템을 덱 맨 아래로 (트래시는 근사 생략)
    private void SB02_EntryReturnItemsTrashEnemy(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => u.Cost >= value,
            $"대상이 될 {value}코스트 이상 상대 유닛을 선택하세요",
            enemyLane =>
            {
                var enemy = FieldManager.Instance.GetUnit(!isPlayer, enemyLane);
                int budget = enemy?.Cost ?? 0;
                var owner = GetOwner(isPlayer);
                var items = owner.TrashPile.Where(c => c.Type == CardType.Item).ToList();
                // "그 유닛의 코스트만큼 아이템을 골라 덱 맨 아래에 원하는 순서로" → 남은 예산 이하만 후보로 1장씩 선택
                PickCardsToDeckBottom(isPlayer, items, CardZone.Trash, items.Count,
                    onDone: moved =>
                    {
                        // "그러면 그 유닛을 트래시한다" — 실제로 놓았을 때만 발동 (이전 구현엔 트래시가 통째로 빠져 있었음)
                        if (moved > 0) EffectActions.TrashUnit(!isPlayer, enemyLane);
                    },
                    budget: budget);
            });
    }

    // SB02-035: 트래시에서 카드명 다른 아이템 2장까지 → 암드 아군에 사이즈 무시 장착
    private void SB02_EntryEquipItemsFromTrash(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var items = owner.TrashPile.Where(c => c.Type == CardType.Item).GroupBy(c => c.CardName).Select(g => g.First()).ToList();
        if (items.Count == 0) return;
        int armedLane = Enumerable.Range(0, 3).FirstOrDefault(
            i => FieldManager.Instance.GetUnit(isPlayer, i) != null && FieldManager.Instance.GetEquippedItems(isPlayer, i).Count > 0);
        var target = FieldManager.Instance.GetUnit(isPlayer, armedLane);
        if (target == null || FieldManager.Instance.GetEquippedItems(isPlayer, armedLane).Count == 0) return;
        foreach (var it in items.Take(value))
        {
            owner.RemoveFromTrash(it);
            FieldManager.Instance.EquipItem(isPlayer, armedLane, it);
        }
    }

    // SB02-034: 믹스 → 대미지 존 아이템 2장까지 다른 아군에 사이즈 무시 장착
    private void SB02_MixEntryEquipFromDamageZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        var owner = GetOwner(isPlayer);
        var items = owner.DamageZone.Where(c => c.Type == CardType.Item).Take(value).ToList();
        if (items.Count == 0) return;
        int tgt = Enumerable.Range(0, 3).FirstOrDefault(i => i != lane && FieldManager.Instance.GetUnit(isPlayer, i) != null);
        if (FieldManager.Instance.GetUnit(isPlayer, tgt) == null) return;
        foreach (var it in items)
        {
            owner.RemoveFromDamageZone(it);
            FieldManager.Instance.EquipItem(isPlayer, tgt, it);
        }
        DamageZoneView.NotifyDamageChanged(); HUDView.NotifyStatusChanged();
    }

    // SB02-013: 믹스 → 덱top N장 공개, 1장 패, 나머지 트래시 (레벨업 조건부는 근사 생략)
    private void SB02_MixEntryRevealSearchLevelUp(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        var owner = GetOwner(isPlayer);
        int n = Mathf.Min(value, owner.DrawPile.Count);
        if (n == 0) return;
        var revealed = owner.PeekDeckTop(n);
        owner.RemoveDeckTop(n);
        EffectActions.PickFromCards(isPlayer, "패에 넣을 카드를 선택하세요", new List<CardData>(revealed),
            pk => { revealed.Remove(pk); owner.AddToHand(pk); foreach (var r in revealed) owner.AddToTrash(r); HandView.Instance?.RefreshHand(); },
            aiPick: l => l.OrderByDescending(c => c.Cost).First());
    }

    // ── 엑시트 ──

    // SB02-002: 믹스 → 상대 유닛1 파워-N (트래시 시 드로우는 근사: 파워 0 이하면 트래시+드로우)
    private void SB02_MixExitDebuffDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMixCondition(isPlayer, card.Attribute)) return;
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
            $"파워-{value}를 줄 상대 유닛을 선택하세요",
            pk =>
            {
                var u = FieldManager.Instance.GetUnit(!isPlayer, pk);
                AddTurnBoost(!isPlayer, pk, -value);
                if (u != null && GetEffectivePower(!isPlayer, pk, u) <= 0) { EffectActions.TrashUnit(!isPlayer, pk); EffectActions.Draw(isPlayer, 1); }
            });
    }

    // ── 스킬 ──

    // SB02-023: 트래시 트리거없는 카드 N장 덱 맨 아래로 → 드로우M ("...:N:M")
    private void SB02_ReturnCardsDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var p = et.Split(':');
        int drawN = p.Length > 2 && int.TryParse(p[2], out int m) ? m : value;
        SB02_ReturnTrashToBottom(isPlayer, value, moved =>
        {
            if (moved > 0) EffectActions.Draw(isPlayer, drawN);
        });
    }

    // SB02-007: 트래시 카드 2장 덱 맨 아래로 → 상대 유닛1 파워-N (믹스면 드로우1)
    private void SB02_ReturnCardsDebuffMixDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        // 덱 밑에 놓는 선택이 팝업이라, 후속 디버프 선택은 그게 끝난 뒤에 이어져야 팝업이 겹치지 않는다
        SB02_ReturnTrashToBottom(isPlayer, 2, _ =>
            EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
                $"파워-{value}를 줄 상대 유닛을 선택하세요",
                pk =>
                {
                    AddTurnBoost(!isPlayer, pk, -value);
                    if (HasMixCondition(isPlayer, skill.Attribute)) EffectActions.Draw(isPlayer, 1);
                }));
    }

    // SB02-039: 대미지 존 아이템 원하는 수만큼 트래시 → 같은 수 트래시 트리거없는 카드를 대미지 존으로
    private void SB02_SwapDamageZoneItems(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var dzItems = owner.DamageZone.Where(c => c.Type == CardType.Item).ToList();
        int n = dzItems.Count;
        foreach (var it in dzItems) { owner.RemoveFromDamageZone(it); owner.AddToTrash(it); }
        var back = owner.TrashPile.Where(c => !c.IsTrigger && !dzItems.Contains(c)).OrderByDescending(c => c.Cost).Take(n).ToList();
        foreach (var c in back) { owner.RemoveFromTrash(c); owner.AddToDamageZone(c); }
        DamageZoneView.NotifyDamageChanged(); HUDView.NotifyStatusChanged();
    }

    // SB02-015: 패에서 3코 이하 유닛 2장까지 빈 존에 사이즈 무시 배치 (믹스면 배치 유닛 버프 근사 생략)
    private void SB02_DeployTwoLowCostMixBuff(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var cands = owner.Hand.Where(c => c.Type == CardType.Unit && c.Cost <= value).ToList();
        int placed = 0;
        foreach (var u in cands.OrderByDescending(c => c.Cost))
        {
            if (placed >= 2) break;
            int empty = -1;
            for (int i = 0; i < 3; i++) if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { empty = i; break; }
            if (empty < 0) break;
            owner.RemoveFromHand(u);
            FieldManager.Instance.ForcePlace(isPlayer, empty, u);
            OnUnitPlaced(isPlayer, empty, u);
            if (HasMixCondition(isPlayer, skill.Attribute)) AddTurnBoost(isPlayer, empty, 1000);
            placed++;
        }
        HandView.Instance?.RefreshHand();
    }

    // SB02-031: 이스케이프 아군 수만큼 드로우. 2장 이상 드로우했으면 이스케이프 아군1의 이스케이프 발동(근사: 덱밑)
    private void SB02_DrawPerEscapeActivate(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        int cnt = Enumerable.Range(0, 3).Count(i =>
            FieldManager.Instance.GetUnit(isPlayer, i) is { } u && u.Keywords.Contains("이스케이프"));
        if (cnt <= 0) return;
        EffectActions.Draw(isPlayer, cnt);
        if (cnt >= 2)
        {
            int el = -1;
            for (int i = 0; i < 3; i++)
                if (FieldManager.Instance.GetUnit(isPlayer, i) is { } u && u.Keywords.Contains("이스케이프")) { el = i; break; }
            if (el >= 0) BT05_MoveUnitToDeckBottom(isPlayer, el);
        }
    }

    // ── 어태커 ──

    // SB02-004/005: 패 N장까지 트래시 → 트래시 수에 따른 계층 효과 (근사: 트래시 수만큼 조우 파워 감소)
    private void SB02_AttackerDiscardTiered(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        int max = et.Contains("HandThresholds") ? 2 : 3;
        if (SB02_HasCreditAdjust(isPlayer)) max += 1; // MixPassiveCreditAdjust: 트래시 장수 조정
        int avail = Mathf.Min(max, owner.Hand.Count);
        if (avail <= 0) return;
        EffectActions.DiscardHandOptional(isPlayer, avail, discarded =>
        {
            if (discarded <= 0) return;
            var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (enc != null) AddTurnBoost(!isPlayer, lane, -value * discarded / Mathf.Max(1, max));
            AddAttackBoost(isPlayer, lane, value); // 계층 파워 근사
        });
    }

    // SB02-006: 어태커 — 패1 트래시 → 양측 패 차이 1장마다 조우 파워-N
    private void SB02_AttackerDiscardDebuffPerHandDiff(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.DiscardHandOptional(isPlayer, 1, d =>
        {
            if (d < 1) return;
            int diff = Mathf.Abs(GetOwner(isPlayer).Hand.Count - GetOwner(!isPlayer).Hand.Count);
            if (FieldManager.Instance.GetUnit(!isPlayer, lane) != null) AddTurnBoost(!isPlayer, lane, -diff * value);
        });
    }

    // SB02-016: 아이템 어태커 — 이 유닛 히트를 상대 턴 끝까지 N으로
    private void SB02_ItemAttackerSetHit(bool isPlayer, int lane, CardData card, int value, string et)
    {
        int cur = GetEffectiveHit(isPlayer, lane, card);
        AddTurnHitBoost(isPlayer, lane, value - cur);
    }

    // SB02-008: 아이템 어태커 — 덱top1 공개 후 패/트래시 선택 (자기 트래시 근사 생략)
    private void SB02_ItemAttackerRevealTakeTrashSelf(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        if (owner.DrawPile.Count == 0) return;
        var top = owner.DrawPile[0]; owner.RemoveFromDeckAt(0);
        EffectActions.PickFromCards(isPlayer, $"{top.CardName} — 패에 넣기(선택) / 취소 시 트래시", new List<CardData> { top },
            pk => { owner.AddToHand(pk); HandView.Instance?.RefreshHand(); },
            aiPick: l => l[0], onCancel: () => owner.AddToTrash(top));
    }
}
