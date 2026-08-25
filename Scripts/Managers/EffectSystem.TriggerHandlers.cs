// EffectSystem.TriggerHandlers.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 트리거(OnTriggerEffect) 효과 핸들러 레지스트리 (Dictionary dispatch)
// 시그니처에 lane 없음 (트리거는 대미지존 발동이라 레인 개념 없음). card.TriggerEffectTypes 순회.
// 새 트리거 effectType: H_Tr_XXX 핸들러 작성 + InitTriggerHandlers()에 등록.
public partial class EffectSystem : MonoBehaviour
{
    private delegate void TriggerHandler(bool isPlayer, CardData card, int value, string et);
    private Dictionary<string, TriggerHandler> _triggerHandlers;

    private void InitTriggerHandlers()
    {
        _triggerHandlers = new Dictionary<string, TriggerHandler>
        {
            ["TriggerReturnToHand"] = H_Tr_TriggerReturnToHand,
            ["TriggerLevelUp"] = H_Tr_TriggerLevelUp,
            ["TriggerForceDiscard"] = H_Tr_TriggerForceDiscard,
            ["TriggerTrashEnemyLow"] = H_Tr_TriggerTrashEnemyLow,
            ["TriggerDebuffEnemy"] = H_Tr_TriggerDebuffEnemy,
            ["TriggerRecoverExitUnit"] = H_Tr_TriggerRecoverExitUnit,
            ["TriggerRecoverFromTrash"] = H_Tr_TriggerRecoverFromTrash,
            ["TriggerTrashSelfDraw"] = H_Tr_TriggerTrashSelfDraw,
            ["TriggerTrashSelfDrawDiscard"] = H_Tr_TriggerTrashSelfDrawDiscard,
            ["TriggerTrashSelfMill"] = H_Tr_TriggerTrashSelfMill,
            ["TriggerTrashSelfSearchItem"] = H_Tr_TriggerTrashSelfSearchItem,
            ["TriggerRecoverFactionUnit"] = H_Tr_TriggerRecoverFactionUnit,
            ["TriggerTrashSelfRecoverFactionUnit"] = H_Tr_TriggerTrashSelfRecoverFactionUnit,
            ["TriggerTrashSelfRecoverSkillByLevel"] = H_Tr_TriggerTrashSelfRecoverSkillByLevel,
            ["TriggerTrashSelfRecoverUnitByLevel"] = H_Tr_TriggerTrashSelfRecoverUnitByLevel,
            ["TriggerTrashSelfDebuff"] = H_Tr_TriggerTrashSelfDebuff,
            ["TriggerTrashSelfActivateExit"] = H_Tr_TriggerTrashSelfActivateExit,
            ["TriggerTrashSelfRevealActivateSkill"] = H_Tr_TriggerTrashSelfRevealActivateSkill,
            ["TriggerReturnLowestCostEnemyToHand"] = H_Tr_TriggerReturnLowestCostEnemyToHand,
            ["TriggerTrashSelfReturnLowestCostEnemy"] = H_Tr_TriggerTrashSelfReturnLowestCostEnemy,
            ["TriggerTrashSelfLevelUp"] = H_Tr_TriggerTrashSelfLevelUp,
            ["TriggerTrashSelfRecoverUnitByCost"] = H_Tr_TriggerTrashSelfRecoverUnitByCost,
            ["TriggerTrashSelfRecoverExitUnit"] = H_Tr_TriggerTrashSelfRecoverExitUnit,
            ["TriggerTrashSelfConditionalForceDiscard"] = H_Tr_TriggerTrashSelfConditionalForceDiscard,
            ["TriggerTrashSelfTrashEnemyByCost"] = H_Tr_TriggerTrashSelfTrashEnemyByCost,
            ["TriggerTrashSelfSearchItemByCost"] = H_Tr_TriggerTrashSelfSearchItemByCost,
            ["TriggerTrashOrLevelUp"] = H_Tr_TriggerTrashOrLevelUp,
        };
    }

    private void H_Tr_TriggerReturnToHand(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            GetOwner(isPlayer).AddToHand(card);
            GetOwner(isPlayer).RemoveFromDamageZone(card);
            HandView.Instance?.RefreshHand();
            Debug.Log($"[트리거] {card.CardName} → 패로");
            break;

        } while (false);
    }

    private void H_Tr_TriggerLevelUp(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            GetOwner(isPlayer).LevelUpLeader();
            HUDView.NotifyStatusChanged();
            Debug.Log($"[트리거] {card.CardName} → 리더 레벨 {GetOwner(isPlayer).LeaderLevel}");
            break;

        } while (false);
    }

    private void H_Tr_TriggerForceDiscard(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            // value > 0 이면 상대 패가 value장 이상일 때만 발동
            if (value <= 0 || GetOwner(!isPlayer).Hand.Count >= value)
                ApplyForceDiscard(!isPlayer);
            else
                Debug.Log($"[트리거] {card.CardName} → 상대 패 {GetOwner(!isPlayer).Hand.Count}장 (조건 {value}장 미충족)");
            break;

        } while (false);
    }

    private void H_Tr_TriggerTrashEnemyLow(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            ResolveTriggerPick(isPlayer, value,
                eligible: lane => FieldManager.Instance.GetUnit(!isPlayer, lane) is { } u && u.Cost <= value,
                onPicked: lane => TrashTargetLane(!isPlayer, lane, value),
                guideMessage: $"[{card.CardName}] 트래시할 {value}코스트 이하 상대 유닛을 선택하세요");
            break;

        } while (false);
    }

    private void H_Tr_TriggerDebuffEnemy(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            ResolveTriggerPick(isPlayer, -1,
                eligible: lane => FieldManager.Instance.GetUnit(!isPlayer, lane) != null,
                onPicked: lane =>
                {
                    AddTurnBoost(!isPlayer, lane, -value);
                    Debug.Log($"[트리거] {card.CardName} → 레인{lane} 파워-{value}");
                },
                guideMessage: $"[{card.CardName}] 파워-{value}를 줄 상대 유닛을 선택하세요");
            break;

        } while (false);
    }

    private void H_Tr_TriggerRecoverExitUnit(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            RecoverExitUnitFromTrash(isPlayer);
            break;

        } while (false);
    }

    // ST01-013 전력 보강: 트래시존에서 N코스트 이하 유닛 1장을 골라 패로 (트리거 버전)
    private void H_Tr_TriggerRecoverFromTrash(bool isPlayer, CardData card, int value, string et)
    {
        EffectActions.RecoverFromTrash(isPlayer,
            c => c.Type == CardType.Unit && c.Cost <= value,
            $"트래시에서 회수할 {value}코스트 이하 유닛을 선택하세요");
    }

    private void H_Tr_TriggerTrashSelfDraw(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                // 이 카드를 트래시로, 드로우N
                GetOwner(isPlayer).RemoveFromDamageZone(card);
                GetOwner(isPlayer).AddToTrash(card);
                if (value > 0)
                {
                    GetOwner(isPlayer).DrawCard(value);
                    HandView.Instance?.RefreshHand();
                }
                HUDView.NotifyStatusChanged();
                Debug.Log($"[트리거] {card.CardName} → 트래시 후 드로우{value}");
                break;

            // ST05 원 포 올, ST07 스킨 슈트 등: 이 카드 트래시, 드로우N 후 패N장 트래시
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfDrawDiscard(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                EffectActions.Draw(isPlayer, value);
                EffectActions.DiscardHand(isPlayer, value);
                break;

            // ST07 망상 끝의 런웨이: 이 카드 트래시, 덱 위 N장 트래시
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfMill(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                EffectActions.Mill(isPlayer, value);
                break;

            // ST05 크라운/현장검토: 이 카드 트래시, 덱에서 N코스트 이하 아이템 서치
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfSearchItem(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                EffectActions.SearchDeck(isPlayer,
                    c => c.Type == CardType.Item && c.Cost <= value,
                    $"덱에서 가져올 {value}코스트 이하 아이템을 선택하세요");
                break;

            // ST06 빛의 루엘: 트래시에서 해당 소속 유닛 회수 — "TriggerRecoverFactionUnit:소속"
        } while (false);
    }

    private void H_Tr_TriggerRecoverFactionUnit(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                string faction = parts.Length > 1 ? parts[1] : "";
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Unit && c.Faction != null && c.Faction.Contains(faction),
                    $"트래시에서 회수할 《{faction}》 유닛을 선택하세요");
                break;
            }

            // ST07 페네/하르세티: 이 카드 트래시, 트래시에서 해당 소속 유닛 회수
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfRecoverFactionUnit(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                string faction = parts.Length > 1 ? parts[1] : "";
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Unit && c.Faction != null && c.Faction.Contains(faction),
                    $"트래시에서 회수할 《{faction}》 유닛을 선택하세요");
                break;
            }

            // ST10 다, 당당하게 노출을!: 이 카드 트래시, 트래시에서 리더레벨 이하 코스트 스킬 회수
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfRecoverSkillByLevel(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Skill && c.Cost <= GetOwner(isPlayer).LeaderLevel,
                    "트래시에서 회수할 스킬을 선택하세요 (리더 레벨 이하)");
                break;

            // ST11 블랙 오더: 이 카드 트래시, 트래시에서 리더레벨 이하 코스트 유닛 회수
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfRecoverUnitByLevel(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Unit && c.Cost <= GetOwner(isPlayer).LeaderLevel,
                    "트래시에서 회수할 유닛을 선택하세요 (리더 레벨 이하)");
                break;

            // ST06 심판자 키세/찬란한 영원: 이 카드 트래시, 유닛 1장 파워-N
            // (원문 "자신의 턴이 끝날 때까지" — 이번 턴 종료까지로 근사)
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfDebuff(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                EffectActions.SelectUnit(isPlayer, !isPlayer,
                    (l, u) => true,
                    $"파워-{value}를 줄 유닛을 선택하세요",
                    picked => EffectActions.Buff(!isPlayer, picked, -value, 0, BoostUntil.EndOfTurn));
                break;

            // ST09 프리즌 브레이크 캐시: 이 카드 트래시, 트래시의 엑시트(트리거 없는) 유닛 카드 효과 발동 → 덱 맨 아래로
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfActivateExit(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                var trashOwner = GetOwner(isPlayer);
                var exitCandidates = trashOwner.TrashPile
                    .Where(c => c.Type == CardType.Unit && c.Keywords.Contains("엑시트") && !c.IsTrigger)
                    .ToList();
                EffectActions.PickFromCards(isPlayer, "엑시트 효과를 발동할 카드를 선택하세요", exitCandidates,
                    picked =>
                    {
                        trashOwner.RemoveFromTrash(picked);
                        trashOwner.AddToDeckBottom(picked); // 덱 맨 아래
                        OnUnitTrashed(isPlayer, -1, picked); // 필드 밖 발동 — 레인 미사용 엑시트 타입만 정상 동작
                        EffectActions.RefreshUI();
                        Debug.Log($"[트리거] {card.CardName} → {picked.CardName} 엑시트 발동 후 덱 맨 아래로");
                    },
                    aiPick: list => list.OrderByDescending(c => c.Cost).First());
                break;
            }

            // ST09 제대로 그려볼까?: 이 카드 트래시, 덱 위 1장 공개 — 스킬이면 트래시하고 발동 가능, 나머지 패로
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfRevealActivateSkill(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                var revealOwner = GetOwner(isPlayer);
                if (revealOwner.DrawPile.Count == 0) break;
                var revealed = revealOwner.DrawPile[0];
                revealOwner.RemoveFromDeckAt(0);

                if (revealed.Type == CardType.Skill)
                {
                    EffectActions.PickFromCards(isPlayer,
                        $"{revealed.CardName}을(를) 트래시하고 효과를 발동하시겠습니까? (취소하면 패로)",
                        new List<CardData> { revealed },
                        picked =>
                        {
                            revealOwner.AddToTrash(picked);
                            ExecuteSkillEffect(isPlayer, picked, -1);
                            EffectActions.RefreshUI();
                        },
                        onCancel: () =>
                        {
                            revealOwner.AddToHand(revealed);
                            EffectActions.RefreshUI();
                        });
                }
                else
                {
                    revealOwner.AddToHand(revealed);
                    EffectActions.RefreshUI();
                }
                break;
            }

        } while (false);
    }

    private void H_Tr_TriggerReturnLowestCostEnemyToHand(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            {
                // 상대 필드 최저 코스트 유닛+장착 아이템을 패로
                CardData lowest = null;
                int lowestLane = -1;
                for (int i = 0; i < 3; i++)
                {
                    var u = FieldManager.Instance.GetUnit(!isPlayer, i);
                    if (u == null) continue;
                    if (lowest == null || u.Cost < lowest.Cost)
                    {
                        lowest = u;
                        lowestLane = i;
                    }
                }
                if (lowest != null && lowestLane >= 0)
                {
                    // 아이템을 먼저 복사 (RemoveUnit이 리스트를 초기화하므로)
                    var items = new List<CardData>(FieldManager.Instance.GetEquippedItems(!isPlayer, lowestLane));
                    FieldManager.Instance.RemoveUnit(!isPlayer, lowestLane);
                    GetOwner(!isPlayer).AddToHand(lowest);
                    foreach (var item in items)
                        GetOwner(!isPlayer).AddToHand(item);
                    HUDView.NotifyStatusChanged();
                    Debug.Log($"[트리거] {card.CardName} → {lowest.CardName}(+아이템 {items.Count}개) 패로 복귀");
                }
                break;
            }

            // ST11 사도 모르페아 / 독사의 손길: 이 카드 트래시, 상대 최저코스트 유닛+아이템 패로
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfReturnLowestCostEnemy(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                BounceLowestCostEnemy(isPlayer);
                break;

            // BT01 라푼젤: 이 카드를 트래시한다 + 리더 레벨+1
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfLevelUp(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                EffectActions.LevelUp(isPlayer);
                break;

            // BT01 홍련 : 흑영: 이 카드를 트래시한다 → 트래시에서 N코스트 이하 유닛 1장 패로
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfRecoverUnitByCost(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Unit && c.Cost <= value,
                    $"트래시에서 회수할 {value}코스트 이하 유닛을 선택하세요");
                break;

            // BT01 뷰티 풀 샷: 이 카드를 트래시한다 → 트래시에서 엑시트 유닛 1장 패로
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfRecoverExitUnit(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Unit && c.Keywords.Contains("엑시트"),
                    "트래시에서 회수할 엑시트 유닛을 선택하세요");
                break;

            // BT01 신데렐라: 이 카드를 트래시한다 → 상대 패 N장 이상이면 상대 패 1장 강제 트래시
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfConditionalForceDiscard(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                if (GetOwner(!isPlayer).Hand.Count >= value)
                    ApplyForceDiscard(!isPlayer);
                break;

            // BT01 VIP 기프트: 이 카드를 트래시한다 → N코스트 이하 상대 유닛 1장 트래시
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfTrashEnemyByCost(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                EffectActions.SelectUnit(isPlayer, !isPlayer,
                    (l, u) => u.Cost <= value,
                    $"{value}코스트 이하 상대 유닛을 선택하세요",
                    enemyLane => EffectActions.TrashUnit(!isPlayer, enemyLane));
                break;

            // BT02 트리거: 이 카드 트래시, 덱에서 N코스트 이하 아이템 서치
        } while (false);
    }

    private void H_Tr_TriggerTrashSelfSearchItemByCost(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            {
                EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
                var owner2 = GetOwner(isPlayer);
                var itemCandidates = owner2.DrawPile.Where(c => c.Type == CardType.Item && c.Cost <= value).ToList();
                if (itemCandidates.Count == 0) break;
                EffectActions.PickFromCards(isPlayer, $"덱에서 {value}코스트 이하 아이템을 선택하세요", itemCandidates,
                    picked2 =>
                    {
                        owner2.RemoveFromDeck(picked2);
                        owner2.AddToHand(picked2);
                        owner2.ShuffleDeck();
                        HandView.Instance?.RefreshHand();
                        Debug.Log($"[트리거] {card.CardName} → {picked2.CardName} 서치");
                    },
                    aiPick: list => list.OrderByDescending(c => c.Cost).First());
                break;
            }

            // ST08 1st Anniversary 수아(유닛): 이 카드를 트래시할 수 있다. 그렇지 않으면(대미지존에 남으면) 리더 레벨+1
        } while (false);
    }

    private void H_Tr_TriggerTrashOrLevelUp(bool isPlayer, CardData card, int value, string et)
    {
        do
        {
            if (!isPlayer)
            {
                // AI: 대미지존에 남겨 사이즈를 유지하며 레벨업 (더 안정적인 이득)
                EffectActions.LevelUp(isPlayer);
                break;
            }
            EffectActions.PickFromCards(true, "이 카드를 트래시하시겠습니까? (취소하면 리더 레벨+1)",
                new List<CardData> { card },
                picked => EffectActions.MoveCard(isPlayer, picked, CardZone.Damage, CardZone.Trash),
                onCancel: () => EffectActions.LevelUp(isPlayer));
            break;
        } while (false);
    }

}
