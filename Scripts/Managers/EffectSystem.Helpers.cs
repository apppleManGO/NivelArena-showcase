// Assets/Scripts/Managers/EffectSystem.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public partial class EffectSystem : MonoBehaviour
{
    private void ApplyMassDiscard(bool isPlayer, int lane, CardData card)
    {
        var owner = GetOwner(isPlayer);
        int count = owner.Hand.Count;
        owner.AddToTrash(owner.Hand);
        owner.ClearHand();
        HandView.Instance?.RefreshHand();

        if (count >= 2)
        {
            var encounter = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (encounter != null)
            {
                FieldManager.Instance.RemoveUnit(!isPlayer, lane);
                GetOwner(!isPlayer).AddToTrash(encounter);
                Debug.Log($"[엔트리] {card.CardName} → 조우 유닛 트래시");
            }
        }
        Debug.Log($"[엔트리] {card.CardName} → 패 {count}장 트래시");
    }

    private void ApplyForceDiscard(bool isPlayer)
    {
        var owner = GetOwner(isPlayer);
        if (owner.Hand.Count == 0) return;

        // SB02-028 MixPassiveHandProtect: 믹스 충족 시 상대의 강제 패 트래시를 무효 (자신 효과에 의한 트래시는 별개 경로라 영향 없음)
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.EffectTypes.Contains("MixPassiveHandProtect") && HasMixCondition(isPlayer, u.Attribute))
            {
                Debug.Log($"[SB02] {u.CardName} → 패 보호 (강제 트래시 무효)");
                return;
            }
        }

        PickAndTrash(isPlayer, owner, () => OnHandDiscarded(isPlayer));
    }

    // 패 트래시 후 훅: PassiveDiscardDrawOnce / _handTrashedThisTurn 플래그
    private void OnHandDiscarded(bool isPlayer)
    {
        _handTrashedThisTurn[Idx(isPlayer)] = true;
        string key = Key(isPlayer, -1);
        if (_discardDrawUsedThisTurn.Contains(key)) return;
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u?.EffectTypes == null) continue;
            var (type, val) = Parse(u.EffectTypes.FirstOrDefault(e => e.StartsWith("PassiveDiscardDrawOnce")) ?? "");
            if (type == "PassiveDiscardDrawOnce")
            {
                _discardDrawUsedThisTurn.Add(key);
                EffectActions.Draw(isPlayer, val > 0 ? val : 1);
                Debug.Log($"[패시브] {u.CardName} PassiveDiscardDrawOnce → 드로우1");
                break;
            }
        }
    }

    // 플레이어면 HandPickPopup, AI면 랜덤으로 1장 트래시 후 onDone 호출
    private void PickAndTrash(bool isPlayer, PlayerController owner, System.Action onDone)
    {
        if (isPlayer)
        {
            HandPickPopup.Instance?.Show(
                "트래시할 카드를 선택하세요",
                new System.Collections.Generic.List<CardData>(owner.Hand),
                card =>
                {
                    owner.RemoveFromHand(card);
                    owner.AddToTrash(card);
                    HandView.Instance?.RefreshHand();
                    HUDView.NotifyStatusChanged();
                    Debug.Log($"[강제 트래시] 플레이어 → {card.CardName} (선택)");
                    onDone?.Invoke();
                }
            );
        }
        else
        {
            var discard = owner.Hand[Random.Range(0, owner.Hand.Count)];
            owner.RemoveFromHand(discard);
            owner.AddToTrash(discard);
            HandView.Instance?.RefreshHand();
            HUDView.NotifyStatusChanged();
            Debug.Log($"[강제 트래시] AI → {discard.CardName} (랜덤)");
            onDone?.Invoke();
        }
    }

    private void RecoverExitUnitFromTrash(bool isPlayer)
    {
        var owner = GetOwner(isPlayer);
        var candidates = owner.TrashPile
            .Where(c => c.Type == CardType.Unit && c.Cost <= 2 && c.Keywords.Contains("엑시트"))
            .ToList();

        if (candidates.Count == 0)
        {
            Debug.Log("[엑시트] 회수 가능한 엑시트 유닛 없음");
            return;
        }

        if (isPlayer)
        {
            HandPickPopup.Instance?.Show(
                "트래시에서 엑시트 유닛을 선택하세요 (2코스트 이하)",
                candidates,
                picked =>
                {
                    owner.RemoveFromTrash(picked);
                    owner.AddToHand(picked);
                    HandView.Instance?.RefreshHand();
                    Debug.Log($"[엑시트] {picked.CardName} 트래시→패");
                });
        }
        else
        {
            var picked = candidates.OrderByDescending(c => c.AttackPower).First();
            owner.RemoveFromTrash(picked);
            owner.AddToHand(picked);
            Debug.Log($"[엑시트] {picked.CardName} 트래시→패 (AI)");
        }
    }

    private void TrashTargetLane(bool isPlayer, int lane, int maxCost)
    {
        var unit = FieldManager.Instance.GetUnit(isPlayer, lane);
        if (unit == null) return;

        FieldManager.Instance.RemoveUnit(isPlayer, lane, willResolveExit: true);
        GetOwner(isPlayer).AddToTrash(unit);
        OnUnitTrashed(isPlayer, lane, unit);
        Debug.Log($"[트리거] {unit.CardName} 트래시 ({maxCost}코이하)");
    }

    // 트리거 효과의 대상 레인 선택: 후보가 1개면 즉시 발동, 여러 개면(플레이어 본인 트리거일 때만)
    // 직접 선택 UI(프라이즈 선택)로 진입. AI 트리거는 항상 첫 후보로 자동 발동.
    private void ResolveTriggerPick(bool isPlayer, int costLimit, System.Func<int, bool> eligible, System.Action<int> onPicked, string guideMessage)
    {
        var candidates = Enumerable.Range(0, 3).Where(eligible).ToList();
        if (candidates.Count == 0) return;

        if (!isPlayer || candidates.Count == 1)
        {
            onPicked(candidates[0]);
            return;
        }

        SkillZoneSlot.EnterTriggerTargeting(SkillZoneSlot.TargetMode.EnemyLane, costLimit, onPicked, guideMessage, triggerOwnerIsPlayer: isPlayer);
    }

    // ── 카운터/부여/조건 헬퍼 ────────────────────────────────────

    // 체인 조건용 공격 횟수 (해당 레인 유닛의 ItemChainCountBoost 아이템 포함)
    public int GetChainCount(bool isPlayer, int lane)
    {
        int count = _attackCountThisTurn[Idx(isPlayer)];
        foreach (var item in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
            foreach (var et in item.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "ItemChainCountBoost") count += value;
            }
        return count;
    }

    // 믹스 조건: 자신의 필드(유닛/아이템/스킬존)에 해당 속성 이외의 카드가 있는가
    public bool HasMixCondition(bool isPlayer, string attribute)
    {
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.Attribute != attribute) return true;
            foreach (var it in FieldManager.Instance.GetEquippedItems(isPlayer, i))
                if (it.Attribute != attribute) return true;
        }
        foreach (var s in GetOwner(isPlayer).SkillZone)
            if (s.Attribute != attribute) return true;
        return false;
    }

    // 이번 턴 「어태커 파워+N」 부여 (공격 선언 시 자동 적용, 턴 종료 시 제거)
    public void GrantAttackerBoost(bool isPlayer, int lane, int amount)
    {
        string key = Key(isPlayer, lane);
        _attackerGrants[key] = _attackerGrants.GetValueOrDefault(key) + amount;
        Debug.Log($"[부여] 레인{lane}에 「어태커 파워+{amount}」");
    }

    // ST07: 부여된 엑시트 실행 (호문클루스 공격 횟수만큼 스케일링)
    private void ExecuteGrantedExit(bool isPlayer, CardData card, string type, int value)
    {
        int count = _homunculusAttackCountThisTurn[Idx(isPlayer)];
        if (count <= 0) return;

        switch (type)
        {
            // BT04-052: 패1 트래시 가능 → 코스트 ≤ 호문클루스 공격 수인 카드 트래시존에서 패로
            case "AttackerGainExitRecoverByAttackCount":
                EffectActions.DiscardHandOptional(isPlayer, 1, d =>
                {
                    if (d < 1) return;
                    EffectActions.RecoverFromTrash(isPlayer, c => c.Cost <= count,
                        $"트래시에서 회수할 {count}코스트 이하 카드를 선택하세요");
                });
                break;

            // BT04-067: 코스트 ≤ 공격 수이고 〈라비〉 이외의 《호문클루스》 유닛을 트래시존에서 빈 유닛 존에 배치
            case "AttackerGainExitReviveByAttackCount":
            {
                var owner = GetOwner(isPlayer);
                var cands = owner.TrashPile.Where(c => c.Type == CardType.Unit && c.Cost <= count
                    && c.CardName != null && !c.CardName.Contains("라비")
                    && ((c.Faction != null && c.Faction.Contains("호문클루스")) || c.Keywords.Contains("호문클루스"))).ToList();
                if (cands.Count == 0) break;
                EffectActions.PickFromCards(isPlayer, "배치할 《호문클루스》 유닛을 선택하세요", cands,
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
                    aiPick: l => l.OrderByDescending(c => c.Cost).First());
                break;
            }

            // ST07 카일론: 카드를 호문클루스 공격 수만큼 드로우
            case "AttackerGainExitDrawPerAttack":
                EffectActions.Draw(isPlayer, count);
                Debug.Log($"[엑시트-부여] {card.CardName} → 드로우{count}");
                break;

            // ST07 필리스: 덱 위 공격 수만큼 공개, 1장 패로, 나머지 트래시
            case "AttackerGainExitRevealPerAttack":
                EffectActions.RevealPickToHand(isPlayer, count, c => true, "패에 넣을 카드를 선택하세요");
                Debug.Log($"[엑시트-부여] {card.CardName} → 덱 위 {count}장 공개");
                break;

            // ST07 세크레트: 공격 수 이하 코스트인 상대 유닛 1장 트래시
            case "AttackerGainExitTrashEnemyPerAttack":
                EffectActions.SelectUnit(isPlayer, !isPlayer,
                    (l, u) => u.Cost <= count,
                    $"트래시할 {count}코스트 이하 상대 유닛을 선택하세요",
                    picked => EffectActions.TrashUnit(!isPlayer, picked));
                break;

            // ST07 페네: 덱 위 공격 수만큼 트래시, 그중 호문클루스 카드 1장마다 상대에게 1대미지
            case "AttackerGainExitMillDamagePerAttack":
            {
                var milled = EffectActions.Mill(isPlayer, count);
                int hits = milled.Count(c => c.Faction != null && c.Faction.Contains("호문클루스"));
                if (hits > 0)
                    EffectActions.DamageOpponent(isPlayer, hits);
                Debug.Log($"[엑시트-부여] {card.CardName} → 덱 위 {count}장 트래시, 호문클루스 {hits}장 → {hits}대미지");
                break;
            }

            // ST07 라비: 상대 유닛 파워-value, 호문클루스 공격 수만큼 반복 발동 가능
            case "AttackerGainExitDebuffPerAttack":
                RepeatDebuffEnemy(isPlayer, count, value);
                break;
        }
    }

    // ST07 하르세티: 트래시의 호문클루스(트리거 없는) 카드를 최대 maxCount장까지 순차 선택해 덱 맨 아래로.
    // 취소하거나 후보 소진 시 종료 → 1장 이상 옮겼으면 조우 유닛 트래시.
    private void RepeatReturnHomunculusToBottom(bool isPlayer, int lane, int maxCount, int done)
    {
        if (done >= maxCount) { FinishReturnCardsTrashEncounter(isPlayer, lane, done); return; }

        var candidates = GetOwner(isPlayer).TrashPile
            .Where(c => c.Faction != null && c.Faction.Contains("호문클루스") && !c.IsTrigger)
            .ToList();
        if (candidates.Count == 0) { FinishReturnCardsTrashEncounter(isPlayer, lane, done); return; }

        EffectActions.PickFromCards(isPlayer,
            $"덱 맨 아래에 놓을 《호문클루스》 카드를 선택하세요 ({done}/{maxCount}, 취소로 종료)",
            candidates,
            picked =>
            {
                EffectActions.MoveCard(isPlayer, picked, CardZone.Trash, CardZone.DeckBottom);
                RepeatReturnHomunculusToBottom(isPlayer, lane, maxCount, done + 1);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First(),
            onCancel: () => FinishReturnCardsTrashEncounter(isPlayer, lane, done));
    }

    private void FinishReturnCardsTrashEncounter(bool isPlayer, int lane, int moved)
    {
        if (moved > 0)
            EffectActions.TrashUnit(!isPlayer, lane);
    }

    // ST11 사도 모르페아/독사의 손길: 상대 필드 최저 코스트 유닛+장착 아이템을 패로 되돌림
    private void BounceLowestCostEnemy(bool isPlayer)
    {
        CardData lowest = null;
        int lowestLane = -1;
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(!isPlayer, i);
            if (u == null) continue;
            if (lowest == null || u.Cost < lowest.Cost) { lowest = u; lowestLane = i; }
        }
        if (lowestLane >= 0)
            EffectActions.BounceUnit(!isPlayer, lowestLane);
    }

    // 라비 전용: 상대 유닛을 골라 파워-amount, remaining회 반복(대상 없으면 자동 종료)
    private void RepeatDebuffEnemy(bool isPlayer, int remaining, int amount)
    {
        if (remaining <= 0) return;
        EffectActions.SelectUnit(isPlayer, !isPlayer,
            (l, u) => true,
            $"파워-{amount}를 줄 상대 유닛을 선택하세요 (남은 발동 {remaining}회)",
            picked =>
            {
                EffectActions.Buff(!isPlayer, picked, -amount, 0, BoostUntil.EndOfTurn);
                RepeatDebuffEnemy(isPlayer, remaining - 1, amount);
            });
    }

    // ST07 어비스 드레이크 가죽갑옷: 자신의 턴이 끝날 때 장착 유닛 트래시 → 드로우N
    private void ProcessItemEndTurnTrashUnit(bool isPlayer)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            var items = new List<CardData>(FieldManager.Instance.GetEquippedItems(isPlayer, lane));
            foreach (var item in items)
            {
                var (type, value) = Parse(item.EffectTypes.FirstOrDefault(e => e.StartsWith("ItemEndTurnTrashUnitDraw")) ?? "");
                if (type != "ItemEndTurnTrashUnitDraw") continue;

                var unit = FieldManager.Instance.GetUnit(isPlayer, lane);
                if (unit == null) break;
                EffectActions.TrashUnit(isPlayer, lane);
                EffectActions.Draw(isPlayer, value);
                Debug.Log($"[아이템] {item.CardName} → {unit.CardName} 트래시, 드로우{value}");
                break; // 유닛이 트래시되며 이 레인의 나머지 아이템도 함께 트래시됨
            }
        }
    }

    // 해당 카드가 지정한 페이즈에 사용 가능한 유닛 액티브 효과를 가지고 있는가
    // ("ActiveAttack~" = 어택 페이즈 전용, 그 외 "Active~" = 메인 페이즈)
    // 버프:N 게이트가 붙은 "BuffActiveSkillZone~"/"BuffActiveAttackSkillZone~" 계열은 "Active"로 시작하지 않아
    // StartsWith("Active")로는 걸러지지 않던 버그 수정 (2026-07-22, BT06 작업 중 발견 — Contains로 완화)
    public bool HasActiveEffectForPhase(CardData card, PhaseType phase)
    {
        foreach (var et in card.EffectTypes)
        {
            if (!et.Contains("Active")) continue;
            string type = et.Contains(':') ? et.Substring(0, et.IndexOf(':')) : et;
            bool isAttackType = type.Contains("ActiveAttack");
            if ((phase == PhaseType.Attack) == isAttackType) return true;
        }
        return false;
    }

    // 유닛 본체 + 장착 아이템(예: ST11 독사의 손길)까지 함께 확인
    public bool HasActiveEffectForPhase(bool isPlayer, int lane, CardData card, PhaseType phase)
    {
        if (HasActiveEffectForPhase(card, phase)) return true;
        foreach (var item in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
            if (HasActiveEffectForPhase(item, phase)) return true;
        return false;
    }

    // 어택 페이즈 유닛 액티브: 아직 이번 턴 미사용이고 해당 페이즈 효과를 가지고 있는가 (장착 아이템 포함)
    public bool CanUseUnitActive(bool isPlayer, int lane, CardData card, PhaseType phase)
        => HasActiveEffectForPhase(isPlayer, lane, card, phase) && !_unitActiveUsedThisTurn.Contains(Key(isPlayer, lane));

    public void MarkUnitActiveUsed(bool isPlayer, int lane)
        => _unitActiveUsedThisTurn.Add(Key(isPlayer, lane));

    // ST07 후미르: 대상이 (정적 키워드든 어태커 부여로든) 엑시트를 가지고 있는가
    private bool HasGrantedExit(bool isPlayer, int lane)
        => _grantedExit.ContainsKey(Key(isPlayer, lane));

    // 《성약》 스킬 또는 카드별 자체 재발동 금지(ST08 타락의 유열 속으로 등)를 지금 발동할 수 있는가
    // (SkillZoneSlot/AIController에서 사용 전 체크)
    public bool CanCastSkill(bool isPlayer, CardData skill)
        => !(_covenantLocked[Idx(isPlayer)] && skill.Keywords.Contains("성약"))
        && !_selfLockedSkillsThisTurn.Contains(SelfLockKey(isPlayer, skill.CardId));

    private HashSet<string> _selfLockedSkillsThisTurn = new(); // "{isPlayer}_{cardId}" — 이번 턴 재발동 금지
    private string SelfLockKey(bool isPlayer, string cardId) => $"{(isPlayer ? "p" : "a")}_{cardId}";

    // ST09 트로피컬 디멘션 아비게일(유닛): 자신/상대 턴마다 1번씩, 다른 아군이 덱 밑에 놓이면 그 히트만큼 상대에게 대미지
    // ※ 이동한 유닛은 이미 필드에서 제거된 뒤라 아이템/버프가 반영 안 된 카드 원본 Hit를 사용(근사)
    private void NotifyAllyMovedToDeckBottom(bool isPlayer, int movedFromLane, CardData movedUnit)
    {
        for (int i = 0; i < 3; i++)
        {
            var passive = FieldManager.Instance.GetUnit(isPlayer, i);
            if (passive == null || !passive.EffectTypes.Contains("PassiveDeckBottomDamage")) continue;

            string key = Key(isPlayer, i);
            if (_deckBottomDamageUsedThisTurn.Contains(key)) continue;
            _deckBottomDamageUsedThisTurn.Add(key);

            EffectActions.DamageOpponent(isPlayer, movedUnit.Hit);
            Debug.Log($"[패시브] {passive.CardName} → {movedUnit.CardName} 덱 밑 이동 감지, 상대 {movedUnit.Hit}대미지");
        }
    }

    // ST09 매지컬 래빗 엠마: 자신/상대 턴마다 1번씩, 자신의 효과로 유닛이 트래시되면 패1 트래시 가능 → 그 수만큼 상대에게 대미지
    // ※ 범위 한정: 현재 ST09 자체 효과(EntryTrashBothByCost/TrashBothLowerCost/ExitTrashLowCost/TrashAllyDamageMixDraw)에서만 호출
    //   (모든 효과-트래시 경로를 전수 연동하려면 EffectActions.TrashUnit 시그니처 확장이 필요해 범위 밖으로 둠)
    private void NotifyEffectTrash(bool causedByIsPlayer, int count)
    {
        if (count <= 0) return;
        for (int i = 0; i < 3; i++)
        {
            var passive = FieldManager.Instance.GetUnit(causedByIsPlayer, i);
            if (passive == null || !passive.EffectTypes.Contains("PassiveEffectTrashDamage")) continue;

            string key = Key(causedByIsPlayer, i);
            if (_effectTrashDamageUsedThisTurn.Contains(key)) continue;

            EffectActions.DiscardHandOptional(causedByIsPlayer, 1, discarded =>
            {
                if (discarded == 0) return;
                _effectTrashDamageUsedThisTurn.Add(key);
                EffectActions.DamageOpponent(causedByIsPlayer, count);
                Debug.Log($"[패시브] {passive.CardName} → 효과 트래시 {count}건 감지, 상대 {count}대미지");
            });
        }
    }

    // ST09 해변가 키아라: 패 제한 트래시가 발생했다면(PlayerController 집계) 그 수만큼 상대에게 대미지
    public void NotifyEndTurnDiscardDamage(bool isPlayer, int discardedCount)
    {
        EffectActions.DamageOpponent(isPlayer, discardedCount);
        Debug.Log($"[패시브] 패 제한 트래시 {discardedCount}장 → 상대 {discardedCount}대미지");
    }

    // ST09 생사부: 유닛과 함께 트래시된 장착 아이템의 엑시트 효과 처리
    // (CombatManager.TrashUnit / EffectActions.TrashUnit 두 경로에서 호출 — 그 외 개별 RemoveUnit 호출부는 범위 밖)
    // trashedUnit: 함께 트래시된 장착 유닛 (ST06 흑요석 반지 — 그 유닛의 코스트 기준 회수 조건에 필요)
    public void OnItemsTrashedWithUnit(bool isPlayer, List<CardData> items, CardData trashedUnit)
    {
        foreach (var item in items)
            foreach (var et in item.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "ItemExitDraw")
                {
                    EffectActions.Draw(isPlayer, value);
                    Debug.Log($"[아이템 엑시트] {item.CardName} → 드로우{value}");
                }
                // ST06 흑요석 반지: 엑시트 — 트래시에서 장착 유닛보다 코스트 낮은 유닛 1장 회수
                else if (type == "ItemExitRecoverLowerCost")
                {
                    int cap = trashedUnit != null ? trashedUnit.Cost : 0;
                    EffectActions.RecoverFromTrash(isPlayer,
                        c => c.Type == CardType.Unit && c.Cost < cap,
                        $"[{item.CardName}] 트래시에서 회수할 {cap}코스트 미만 유닛을 선택하세요");
                }
                // BT06-042 반역의 결의: 엑시트 — 트래시에서 자기 이외의 N코스트 이하 카드 1장 패로
                else if (type == "ItemExitRecoverLowCost")
                {
                    EffectActions.RecoverFromTrash(isPlayer,
                        c => c.CardName != item.CardName && c.Cost <= value,
                        $"[{item.CardName}] 트래시에서 회수할 {value}코스트 이하 카드를 선택하세요");
                }
                // BT02 아이템: 유닛이 트래시될 때 자기 히트만큼 패 트래시하면 아이템+유닛 생존 (근사: 아이템만 패 귀환)
                else if (type == "ItemOnTrashHitDiscardSurvive")
                {
                    // 유닛은 이미 트래시됨 — 아이템만 회수하고 히트만큼 패 트래시
                    GetOwner(isPlayer).AddToHand(item);
                    GetOwner(isPlayer).RemoveFromTrash(item);
                    HandView.Instance?.RefreshHand();
                    for (int ih = 0; ih < item.Hit; ih++) ApplyForceDiscard(isPlayer);
                    Debug.Log($"[아이템 엑시트] {item.CardName} 히트{item.Hit}만큼 패 트래시 후 아이템 패 귀환");
                }
                // BT01 아이템: 유닛이 트래시될 때 이 아이템이 함께 트래시되는 대신 패로 돌아온다
                else if (type == "ItemExitReturnToHand")
                {
                    GetOwner(isPlayer).AddToHand(item);
                    GetOwner(isPlayer).RemoveFromTrash(item);
                    HandView.Instance?.RefreshHand();
                    Debug.Log($"[아이템 엑시트] {item.CardName} → 패로 귀환");
                }
                // BT03 수면: 아이템 엑시트 — 패1 트래시 → 장착 유닛 패로 귀환
                else if (type == "ItemPassiveSacrificeSelfReturnUnitWithItems")
                {
                    // 아이템은 이미 trashedUnit과 함께 트래시됨
                    // 패 1장 트래시 후 유닛을 트래시에서 패로 귀환
                    EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
                    {
                        if (discarded < 1) return;
                        GetOwner(isPlayer).RemoveFromTrash(trashedUnit);
                        GetOwner(isPlayer).AddToHand(trashedUnit);
                        HandView.Instance?.RefreshHand();
                        Debug.Log($"[아이템 엑시트] {item.CardName} → {trashedUnit?.CardName} 패로 귀환");
                    });
                }
                // BT03 아이템 엑시트: N코스트 이하 상대 유닛 트래시 (조건: 유닛 코스트 이하)
                else if (type == "ItemExitConditionalTrashLowCostEnemy")
                {
                    // lane은 이 함수에 없으므로 조우 레인을 직접 찾아야 함
                    // 근사: 같은 레인의 상대 유닛을 찾기 위해 trashedUnit의 레인을 역추적
                    for (int li = 0; li < 3; li++)
                    {
                        var enc = FieldManager.Instance.GetUnit(!isPlayer, li);
                        if (enc != null && enc.Cost <= value)
                        {
                            EffectActions.TrashUnit(!isPlayer, li);
                            Debug.Log($"[아이템 엑시트] {item.CardName} → 상대 {enc.CardName}({enc.Cost}코 ≤ {value}) 트래시");
                            break; // 1장만
                        }
                    }
                }
                // BT03 아이템 어태커: 패N장 트래시 → 조우 유닛 강제 히트 차감 or 트래시
                else if (type == "ItemAttackerDiscardForceHitDiscardOrTrashEncounter")
                {
                    // 어태커 타이밍은 OnAttackDeclared에서 별도 처리 (OnItemsTrashedWithUnit은 유닛 트래시 시)
                    // 여기서는 스킵 (어태커 효과는 공격 선언 시 처리)
                }
                // BT03 아이템 엑시트: 패1 트래시 → 이 유닛 재배치
                else if (type == "ItemExitDiscardSelfDeploy")
                {
                    ProcessItemExitDiscardSelfDeploy(isPlayer, -1, item, trashedUnit);
                }
                // BT05-047 ItemExitReturn: 엑시트 귀환 — 장착 유닛을 패로 되돌림
                else if (type == "ItemExitReturn" && trashedUnit != null)
                {
                    GetOwner(isPlayer).RemoveFromTrash(trashedUnit);
                    GetOwner(isPlayer).AddToHand(trashedUnit);
                    HandView.Instance?.RefreshHand();
                    Debug.Log($"[BT05 아이템 엑시트] {item.CardName} → {trashedUnit.CardName} 패로 귀환");
                }
                // BT04-084 ItemExitSelfDeploy: 빈 유닛 존에 이 유닛을 배치
                else if (type == "ItemExitSelfDeploy" && trashedUnit != null)
                {
                    for (int i = 0; i < 3; i++)
                        if (FieldManager.Instance.GetUnit(isPlayer, i) == null)
                        {
                            GetOwner(isPlayer).RemoveFromTrash(trashedUnit);
                            FieldManager.Instance.ForcePlace(isPlayer, i, trashedUnit);
                            OnUnitPlaced(isPlayer, i, trashedUnit);
                            break;
                        }
                }
            }
    }

    // BT02/BT03 아이템 장착 시 훅
    public void OnItemEquipped(bool isPlayer, int lane, CardData item)
    {
        if (item.EffectTypes.Contains("ItemExpiresEndOfOpponentTurn"))
            _itemExpiresEndOfOpponentTurn.Add(Key(isPlayer, lane));

        // BT03 죽으면 안 돼: GrantPassiveDrawOnEquip — 장착 유닛에 "장착 시 드로우N" 부여
        string key = Key(isPlayer, lane);
        if (_grantedPassiveDrawOnEquip.Contains(key))
        {
            EffectActions.Draw(isPlayer, 1);
            Debug.Log($"[부여 패시브] 장착 시 드로우1 ({item.CardName})");
        }

        // BT05-073 MixPassiveDrawOnEquip: 장착 유닛이 이 패시브 보유 + 믹스면 아이템 장착 시 드로우1
        var unit = FieldManager.Instance.GetUnit(isPlayer, lane);
        if (unit != null && unit.EffectTypes.Contains("MixPassiveDrawOnEquip")
            && HasMixCondition(isPlayer, unit.Attribute))
        {
            EffectActions.Draw(isPlayer, 1);
            Debug.Log($"[BT05 믹스] 장착 시 드로우1 ({unit.CardName})");
        }
    }

    // ── 침투[N] (방어당하지 않은 공격 — 다이렉트/돌파/패스 시 드로우N) ──

    public void OnAttackUnblocked(bool isPlayer, int lane, CardData attacker)
    {
        int total = 0;

        foreach (var et in attacker.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (type == "AttackerInfiltrate") // ST11 사도 모르페아
                total += value;
            else if (type == "MixAttackerInfiltrate" && HasMixCondition(isPlayer, attacker.Attribute)) // ST09 바냐
                total += value;
        }

        foreach (var item in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
            foreach (var et in item.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "ItemAttackerInfiltrate") // ST09 만년한파
                    total += value;
            }

        // ST09 펠릭스: 믹스 조건이면 필드의 모든 자신 유닛(공격자 포함)에 침투[N] 부여
        for (int i = 0; i < 3; i++)
        {
            var ally = FieldManager.Instance.GetUnit(isPlayer, i);
            if (ally == null) continue;
            foreach (var et in ally.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "MixGrantInfiltrateAll" && HasMixCondition(isPlayer, ally.Attribute))
                    total += value;
            }
        }

        // BT06-013 체인 조건부 부여형 침투[1] (이 공격 한정)
        string chainInfKey = Key(isPlayer, lane);
        if (_bt06ChainInfiltrateThisAttack.Contains(chainInfKey))
        {
            total += 1;
            _bt06ChainInfiltrateThisAttack.Remove(chainInfKey);
        }

        if (total > 0)
        {
            EffectActions.Draw(isPlayer, total);
            Debug.Log($"[침투] {attacker.CardName} → 드로우{total}");
        }
    }

    // ── 이스케이프 ────────────────────────────────────────────────

    // 자신의 메인 페이즈가 시작할 때 호출 (TurnManager.MainPhase)
    public void OnMainPhaseStart(bool isPlayerTurn) => ProcessEscapeUnits(isPlayerTurn);

    private void ProcessEscapeUnits(bool isPlayer)
    {
        var owner = GetOwner(isPlayer);
        for (int lane = 0; lane < 3; lane++)
        {
            var unit = FieldManager.Instance.GetUnit(isPlayer, lane);
            if (unit == null) continue;

            string key = Key(isPlayer, lane);
            bool granted = _grantedEscapeDamage.TryGetValue(key, out int grantDamage);

            string escapeType = null;
            int val = 0;
            foreach (var et in unit.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type is "EscapeLevelUp" or "EscapeDraw" or "EscapeOpponentDeployPunish"
                    or "EscapeDebuffEnemiesByPower" or "EscapeDiscardTrashLowerPowerEnemy" or "EscapeGrantNextDeployPenetration"
                    or "EscapeDeployEscapeUnit")
                {
                    escapeType = type; val = value; break;
                }
                if (type == "LevelLinkEscapeDeploy" && owner.LeaderLevel >= value)
                {
                    escapeType = type; val = value; break;
                }
            }

            if (escapeType == null && !granted) continue;

            // BT05 파워 참조 이스케이프용 — 제거 전 파워 캡처
            int escPower = GetEffectivePower(isPlayer, lane, unit);

            // 이스케이프: 이 유닛을 주인의 덱 맨 아래로 (엑시트 미발동 — 트래시가 아님)
            FieldManager.Instance.RemoveUnit(isPlayer, lane);
            owner.AddToDeckBottom(unit);
            Debug.Log($"[이스케이프] {unit.CardName} 덱 맨 아래로");
            NotifyAllyMovedToDeckBottom(isPlayer, lane, unit); // ST09 아비게일(유닛) 패시브

            switch (escapeType)
            {
                case "EscapeLevelUp": // ST08 레니
                    EffectActions.LevelUp(isPlayer);
                    break;
                case "EscapeDraw": // ST09 선샤인 마린 일레븐, 해변가 키아라
                    EffectActions.Draw(isPlayer, val);
                    break;
                case "EscapeOpponentDeployPunish": // ST08 요밀로
                    _escapeDeployPunishThreshold[Idx(isPlayer)] = val;
                    Debug.Log($"[이스케이프] 상대 파워{val} 이하 유닛 배치 감시 시작");
                    break;
                case "LevelLinkEscapeDeploy": // ST08 1st Anniversary 수아 (레벨링크 8)
                    ProcessLevelLinkEscapeDeploy(isPlayer);
                    break;
                case "EscapeDebuffEnemiesByPower": // BT05-005: 상대 유닛 val장까지 파워-escPower
                {
                    var enemies = Enumerable.Range(0, 3)
                        .Where(i => FieldManager.Instance.GetUnit(!isPlayer, i) != null)
                        .OrderByDescending(i => GetEffectivePower(!isPlayer, i, FieldManager.Instance.GetUnit(!isPlayer, i)))
                        .Take(Mathf.Max(val, 1)).ToList();
                    foreach (var i in enemies) AddTurnBoost(!isPlayer, i, -escPower);
                    break;
                }
                case "EscapeDiscardTrashLowerPowerEnemy": // BT05-023: 패1 트래시 가능 → escPower보다 낮은 상대 유닛 트래시
                    EffectActions.DiscardHandOptional(isPlayer, 1, d =>
                    {
                        if (d < 1) return;
                        var target = Enumerable.Range(0, 3)
                            .Select(i => (i, u: FieldManager.Instance.GetUnit(!isPlayer, i)))
                            .Where(t => t.u != null && GetEffectivePower(!isPlayer, t.i, t.u) < escPower)
                            .OrderBy(t => GetEffectivePower(!isPlayer, t.i, t.u)).FirstOrDefault();
                        if (target.u != null) EffectActions.TrashUnit(!isPlayer, target.i);
                    });
                    break;
                case "EscapeGrantNextDeployPenetration": // BT05-004: 다음 배치 유닛에 관통 부여 (근사: 약탈로 대체)
                    _nextDeployPlunder[Idx(isPlayer)] = Mathf.Max(val, 1);
                    break;
                case "EscapeDeployEscapeUnit": // SB02-029: 이스케이프 유닛 배치
                    SB02_DeployEscapeUnit(isPlayer);
                    break;
            }

            if (granted) // ST09 아비게일 각성 액티브로 부여된 이스케이프
            {
                _grantedEscapeDamage.Remove(key);
                EffectActions.DamageOpponent(isPlayer, grantDamage);
                Debug.Log($"[이스케이프-부여] 상대에게 {grantDamage}대미지");
            }

            EffectActions.RefreshUI();
        }
    }

    // ST08 수아: 덱 위 3장 공개 → 유닛 1장 선택해 사이즈 무시 배치 (파워+5000/히트+1, 이번 턴)
    // 나머지 트래시. ※ "이번 턴 0코스트" 조건은 배치 이후 재계산 없어 근사 생략

    // ── 공용: "덱 맨 아래에 원하는 순서대로 놓는다" ────────────────────────────
    // 카드를 1장씩 골라 덱 맨 아래로 놓는다. 고른 순서 = 쌓이는 순서라 "원하는 순서대로"를 그대로 만족한다.
    //  · pool: 후보 목록(호출부가 넘긴 사본 — 여기서 소모됨) / fromZone: 카드가 현재 들어있는 존(트래시 등)
    //  · 플레이어가 취소하거나 후보가 떨어지면 종료하고 onDone(실제로 놓은 장수) 호출
    //  · budget >= 0 이면 남은 예산 이하 코스트 카드만 후보 (SB02-036: 상대 유닛 코스트만큼)
    //  · SB02_CardToDeckBottom 경유라 "이 턴 덱 맨 아래에 놓인 수" 카운터가 세트 무관하게 항상 갱신됨
    public void PickCardsToDeckBottom(bool isPlayer, List<CardData> pool, CardZone fromZone,
                                      int remaining, System.Action<int> onDone = null,
                                      int moved = 0, int budget = -1)
    {
        var sel = budget >= 0 ? pool.Where(c => c.Cost <= budget).ToList() : new List<CardData>(pool);
        if (remaining <= 0 || sel.Count == 0) { onDone?.Invoke(moved); return; }

        EffectActions.PickFromCards(isPlayer,
            $"덱 맨 아래에 놓을 카드를 선택하세요 (남은 {remaining}장{(budget >= 0 ? $", 코스트 {budget} 이하" : "")})",
            sel,
            picked =>
            {
                pool.Remove(picked);
                SB02_CardToDeckBottom(isPlayer, picked, fromZone); // 존 이동 + 덱밑 카운터 증가
                PickCardsToDeckBottom(isPlayer, pool, fromZone, remaining - 1, onDone,
                                      moved + 1, budget >= 0 ? budget - picked.Cost : -1);
            },
            onCancel: () => onDone?.Invoke(moved),
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }
}
