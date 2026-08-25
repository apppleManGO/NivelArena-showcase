// Assets/Scripts/Managers/EffectSystem.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public partial class EffectSystem : MonoBehaviour
{
    public void ExecuteSkillEffect(bool isPlayer, CardData skill, int targetLane)
    {
        Debug.Log($"[스킬 발동] {skill.CardName} → 레인{targetLane}");

        foreach (var et in skill.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (_skillHandlers != null && _skillHandlers.TryGetValue(type, out var handler))
                handler(isPlayer, skill, targetLane, value, et);
        }

        HUDView.NotifyStatusChanged();
    }

    // BT01 압도: 상대 유닛 M장 각각 파워-N (재귀 선택)
    private void DebuffEnemyUnitsStep(bool isPlayer, CardData skill, int debuffVal, int remaining)
    {
        if (remaining <= 0) return;
        EffectActions.SelectUnit(isPlayer, !isPlayer,
            (l, u) => true,
            $"파워-{debuffVal}를 줄 상대 유닛을 선택하세요 ({remaining}장 남음)",
            picked =>
            {
                AddTurnBoost(!isPlayer, picked, -debuffVal);
                Debug.Log($"[스킬] {skill.CardName} → 레인{picked} 파워-{debuffVal}");
                DebuffEnemyUnitsStep(isPlayer, skill, debuffVal, remaining - 1);
            },
            aiPick: list => list.OrderByDescending(l => GetEffectivePower(!isPlayer, l,
                FieldManager.Instance.GetUnit(!isPlayer, l))).First());
    }

    // BT01 뷰티 풀 샷: 코스트 합계 costLimit 이하가 되도록 상대 유닛 순차 트래시
    private void TrashEnemiesByCostLimitStep(bool isPlayer, CardData skill, int costLimit, int remaining, int usedCost)
    {
        if (remaining <= 0) return;
        EffectActions.SelectUnit(isPlayer, !isPlayer,
            (l, u) => usedCost + u.Cost <= costLimit,
            $"트래시할 상대 유닛을 선택하세요 (코스트 합 {costLimit} 이하, {remaining}장 남음)",
            picked =>
            {
                var unit = FieldManager.Instance.GetUnit(!isPlayer, picked);
                int cost = unit?.Cost ?? 0;
                EffectActions.TrashUnit(!isPlayer, picked);
                TrashEnemiesByCostLimitStep(isPlayer, skill, costLimit, remaining - 1, usedCost + cost);
            },
            aiPick: list => list.OrderByDescending(l => FieldManager.Instance.GetUnit(!isPlayer, l)?.Cost ?? 0).First());
    }

    // BT01 EX 매거진: 트래시에서 엑시트 유닛을 코스트합 budget 이하로 최대 picks장 회수
    private void RecoverExitUnitsByCostStep(bool isPlayer, CardData skill, int budget, int remaining, int usedCost)
    {
        if (remaining <= 0) return;
        var candidates = GetOwner(isPlayer).TrashPile
            .Where(c => c.Type == CardType.Unit && c.Keywords.Contains("엑시트") && usedCost + c.Cost <= budget)
            .ToList();
        if (candidates.Count == 0) return;
        EffectActions.PickFromCards(isPlayer,
            $"트래시에서 회수할 엑시트 유닛을 선택하세요 (코스트 합 {budget} 이하, {remaining}장 남음, 취소 가능)",
            candidates,
            picked =>
            {
                EffectActions.MoveCard(isPlayer, picked, CardZone.Trash, CardZone.Hand);
                Debug.Log($"[스킬] {skill.CardName} → {picked.CardName} 회수");
                RecoverExitUnitsByCostStep(isPlayer, skill, budget, remaining - 1, usedCost + picked.Cost);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // ST10 다, 당당하게 노출을!: 트래시에서 카드를 최대 remainingPicks장까지 순차 선택 회수.
    // 매 선택마다 budget = 현재 패 장수 - 지금까지 회수한 코스트 합 을 재계산 (취소 시 중단)
    private void RecoverByHandCountStep(bool isPlayer, CardData skill, int remainingPicks, int costSum)
    {
        if (remainingPicks <= 0) return;
        var owner = GetOwner(isPlayer);
        int budget = owner.Hand.Count - costSum;
        var candidates = owner.TrashPile
            .Where(c => c.CardId != skill.CardId && c.Cost <= budget)
            .ToList();
        if (candidates.Count == 0) return;

        EffectActions.PickFromCards(isPlayer,
            $"트래시에서 회수할 카드를 선택하세요 (코스트 합 ≤ 패 장수, {remainingPicks}장 남음, 취소 가능)",
            candidates,
            picked =>
            {
                EffectActions.MoveCard(isPlayer, picked, CardZone.Trash, CardZone.Hand);
                Debug.Log($"[스킬] {skill.CardName} → {picked.CardName} 회수");
                RecoverByHandCountStep(isPlayer, skill, remainingPicks - 1, costSum + picked.Cost);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // ST06 찬란한 영원: 트래시에서 카드를 최대 remainingPicks장까지 순차 선택 회수.
    // 매 선택마다 budget = 대미지 존 카드 수 - 지금까지 회수한 코스트 합 을 재계산 (취소 시 중단)
    private void RecoverByDamageCountStep(bool isPlayer, CardData skill, int remainingPicks, int costSum)
    {
        if (remainingPicks <= 0) return;
        var owner = GetOwner(isPlayer);
        int budget = owner.DamageZone.Count - costSum;
        var candidates = owner.TrashPile
            .Where(c => c.CardId != skill.CardId && c.Cost <= budget)
            .ToList();
        if (candidates.Count == 0) return;

        EffectActions.PickFromCards(isPlayer,
            $"트래시에서 회수할 카드를 선택하세요 (코스트 합 ≤ 대미지 존 카드 수, {remainingPicks}장 남음, 취소 가능)",
            candidates,
            picked =>
            {
                EffectActions.MoveCard(isPlayer, picked, CardZone.Trash, CardZone.Hand);
                Debug.Log($"[스킬] {skill.CardName} → {picked.CardName} 회수");
                RecoverByDamageCountStep(isPlayer, skill, remainingPicks - 1, costSum + picked.Cost);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // ST06 빛의 루엘: 패에서 스킬 카드를 최대 remaining장까지 순차 선택 트래시 (취소 시 지금까지 트래시한 만큼으로 확정)
    private void DiscardSkillsForDebuffStep(bool isPlayer, int lane, CardData card, int perCost, int remaining, int costSum)
    {
        var owner = GetOwner(isPlayer);
        var candidates = owner.Hand.Where(c => c.Type == CardType.Skill).ToList();
        if (remaining <= 0 || candidates.Count == 0)
        {
            FinishDiscardSkillsDebuff(isPlayer, lane, card, perCost, costSum);
            return;
        }

        EffectActions.PickFromCards(isPlayer,
            $"트래시할 스킬 카드를 선택하세요 (최대 {remaining}장, 취소 시 종료)",
            candidates,
            picked =>
            {
                owner.RemoveFromHand(picked);
                owner.AddToTrash(picked);
                EffectActions.RefreshUI();
                DiscardSkillsForDebuffStep(isPlayer, lane, card, perCost, remaining - 1, costSum + picked.Cost);
            },
            onCancel: () => FinishDiscardSkillsDebuff(isPlayer, lane, card, perCost, costSum));
    }

    private void FinishDiscardSkillsDebuff(bool isPlayer, int lane, CardData card, int perCost, int costSum)
    {
        if (costSum <= 0) return;
        var encounter = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (encounter == null) return;
        AddTurnBoost(!isPlayer, lane, -costSum * perCost);
        Debug.Log($"[액티브] {card.CardName} → 트래시 코스트합{costSum} → 조우 파워-{costSum * perCost}");
    }

    // BT02 ProcessItemExpiresEndOfOpponentTurn: 상대 턴 종료 시 만료 아이템 트래시
    private void ProcessItemExpiresEndOfOpponentTurn()
    {
        foreach (var key in _itemExpiresEndOfOpponentTurn)
        {
            // key = Key(isPlayer, lane) = "p_0" / "a_0"
            // ※ 예전엔 parts[0]을 "True"와 비교해 iP가 항상 false였다 → 상대 턴이 끝나면
            //    누구 아이템이든 항상 AI 쪽 레인만 만료되던 버그. "p"로 비교해야 한다.
            var parts = key.Split('_');
            if (parts.Length < 2) continue;
            bool iP = parts[0] == "p";
            if (!int.TryParse(parts[1], out int ln)) continue;
            var items = FieldManager.Instance.GetEquippedItems(iP, ln);
            foreach (var item in items.ToList())
            {
                FieldManager.Instance.UnequipItem(iP, ln, item);
                GetOwner(iP).AddToTrash(item);
                Debug.Log($"[아이템] {item.CardName} 만료(상대 턴 끝) → 트래시");
            }
        }
    }

    // BT02 소다 : 트윙클링 바니: 트래시 아이템을 최대 value장까지 덱 맨 아래로 이동 후 조우 트래시
    private void EntryTrashItemsToDeckStep(bool isPlayer, int lane, CardData card, int value, List<CardData> candidates, CardData encounter)
    {
        if (value <= 0 || candidates.Count == 0)
        {
            // 완료 후 조우 트래시
            if (encounter != null)
            {
                Debug.Log($"[엔트리] {card.CardName} → 조우 {encounter.CardName} 트래시");
                CombatManager.Instance.TrashUnitPublic(!isPlayer, lane);
            }
            return;
        }
        EffectActions.PickFromCards(isPlayer, $"덱 맨 아래로 이동할 아이템 선택 (최대 {value}장, 취소 시 종료)", candidates,
            picked =>
            {
                GetOwner(isPlayer).RemoveFromTrash(picked);
                GetOwner(isPlayer).AddToDeckBottom(picked); // 덱 맨 아래 (Add). 고른 순서 = 쌓이는 순서
                candidates.Remove(picked);
                EntryTrashItemsToDeckStep(isPlayer, lane, card, value - 1, candidates, encounter);
            },
            onCancel: () =>
            {
                if (encounter != null) CombatManager.Instance.TrashUnitPublic(!isPlayer, lane);
            });
    }

    // BT02 고양이 보은: 덱 위 revealN장 공개, 아이템 최대 maxItems장 패로, 나머지 덱 맨 아래
    private void RevealSearchItemsToDeckBottomStep(bool isPlayer, CardData skill, int revealN, int maxItems)
    {
        var owner = GetOwner(isPlayer);
        if (owner.DrawPile.Count == 0) return;
        int cnt = Mathf.Min(revealN, owner.DrawPile.Count);
        // "덱 맨 위에서 공개" — DrawPile[0]이 맨 위 (드로우가 index 0에서 가져감)
        var revealed = owner.PeekDeckTop(cnt);
        owner.RemoveDeckTop(cnt);
        var items = revealed.Where(c => c.Type == CardType.Item).ToList();
        var rest = revealed.Where(c => c.Type != CardType.Item).ToList();
        // 비아이템은 바로 덱 맨 아래로 (Add = 맨 아래)
        foreach (var r in rest) owner.AddToDeckBottom(r);
        if (items.Count == 0) return;

        void PickNext(List<CardData> remaining, int picks)
        {
            if (picks <= 0 || remaining.Count == 0)
            {
                foreach (var r2 in remaining) owner.AddToDeckBottom(r2); // 안 고른 나머지도 덱 맨 아래로
                HandView.Instance?.RefreshHand();
                return;
            }
            EffectActions.PickFromCards(isPlayer, $"패로 가져올 아이템 선택 (최대 {picks}장 남음, 취소 시 종료)", remaining,
                picked =>
                {
                    remaining.Remove(picked);
                    owner.AddToHand(picked);
                    PickNext(remaining, picks - 1);
                },
                onCancel: () =>
                {
                    foreach (var r2 in remaining) owner.AddToDeckBottom(r2);
                    HandView.Instance?.RefreshHand();
                });
        }
        PickNext(items, maxItems);
    }

    private void ApplyBuffGuardianAlly(bool isPlayer, CardData skill, int value, bool isHit)
    {
        var guardians = new List<CardData>();
        var guardianLanes = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.Keywords.Contains("가디언"))
            {
                guardians.Add(u);
                guardianLanes.Add(i);
            }
        }
        if (guardians.Count == 0) return;

        void Apply(int laneIdx)
        {
            string key = Key(isPlayer, laneIdx);
            if (isHit)
                _opponentTurnHitBoosts[key] = _opponentTurnHitBoosts.GetValueOrDefault(key) + value;
            else
                _opponentTurnBoosts[key] = _opponentTurnBoosts.GetValueOrDefault(key) + value;
            Debug.Log($"[스킬] {skill.CardName} → 레인{laneIdx} 가디언 {(isHit ? "히트" : "파워")}+{value}");
        }

        if (guardians.Count == 1)
        {
            Apply(guardianLanes[0]);
            return;
        }

        if (isPlayer)
        {
            HandPickPopup.Instance?.Show(
                $"[{skill.CardName}] 버프를 줄 가디언 유닛을 선택하세요",
                guardians,
                picked =>
                {
                    int idx = guardianLanes[guardians.IndexOf(picked)];
                    Apply(idx);
                });
        }
        else
        {
            Apply(guardianLanes[0]);
        }
    }

    private void ApplyBuffGuardianAllyBreakthrough(bool isPlayer, CardData skill)
    {
        var guardians = new List<CardData>();
        var guardianLanes = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.Keywords.Contains("가디언"))
            {
                guardians.Add(u);
                guardianLanes.Add(i);
            }
        }
        if (guardians.Count == 0) return;

        void Apply(int laneIdx)
        {
            _breakthroughGrants.Add(Key(isPlayer, laneIdx));
            Debug.Log($"[스킬] {skill.CardName} → 레인{laneIdx} 가디언 돌파 부여");
        }

        if (guardians.Count == 1) { Apply(guardianLanes[0]); return; }

        if (isPlayer)
        {
            HandPickPopup.Instance?.Show(
                $"[{skill.CardName}] 돌파를 줄 가디언 유닛을 선택하세요",
                guardians,
                picked =>
                {
                    int idx = guardianLanes[guardians.IndexOf(picked)];
                    Apply(idx);
                });
        }
        else
        {
            Apply(guardianLanes[0]);
        }
    }

    private void ApplyTrashFieldByHandCost(bool isPlayer)
    {
        var owner = GetOwner(isPlayer);
        var unitCandidates = owner.Hand.Where(c => c.Type == CardType.Unit).ToList();
        if (unitCandidates.Count == 0) return;

        if (isPlayer)
        {
            HandPickPopup.Instance?.Show(
                "트래시할 패 유닛을 선택하세요",
                unitCandidates,
                handUnit =>
                {
                    owner.RemoveFromHand(handUnit);
                    owner.AddToTrash(handUnit);
                    HandView.Instance?.RefreshHand();
                    TrashFieldUnitByHandCost(isPlayer, owner, handUnit);
                }
            );
        }
        else
        {
            // AI는 가장 비싼 유닛 자동 선택
            var handUnit = unitCandidates.OrderByDescending(c => c.Cost).First();
            owner.RemoveFromHand(handUnit);
            owner.AddToTrash(handUnit);
            TrashFieldUnitByHandCost(isPlayer, owner, handUnit);
        }
    }

    private void TrashFieldUnitByHandCost(bool isPlayer, PlayerController owner, CardData handUnit)
    {
        // 필드에서 그 유닛보다 코스트 낮은 유닛 트래시
        for (int i = 0; i < 3; i++)
        {
            var fieldUnit = FieldManager.Instance.GetUnit(isPlayer, i);
            if (fieldUnit != null && fieldUnit.Cost < handUnit.Cost)
            {
                FieldManager.Instance.RemoveUnit(isPlayer, i, willResolveExit: true);
                owner.AddToTrash(fieldUnit);
                OnUnitTrashed(isPlayer, i, fieldUnit);
                Debug.Log($"[흑화] {handUnit.CardName} 트래시 → {fieldUnit.CardName} 트래시");
                break;
            }
        }
    }

}
