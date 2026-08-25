// EffectSystem.ExitHandlers.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 엑시트(OnUnitTrashed) 효과 핸들러 레지스트리 (Dictionary dispatch)
// 새 엑시트 effectType: H_Exit_XXX 핸들러 작성 + InitExitHandlers()에 등록.
// (전/후처리 — 트래시카운터·부여엑시트귀환·리더패시브·모더니아 — 는 OnUnitTrashed에 유지)
public partial class EffectSystem : MonoBehaviour
{
    private delegate void ExitHandler(bool isPlayer, int lane, CardData card, int value, string et);
    private Dictionary<string, ExitHandler> _exitHandlers;

    private void InitExitHandlers()
    {
        _exitHandlers = new Dictionary<string, ExitHandler>
        {
            ["ExitLevelUp"] = H_Exit_ExitLevelUp,
            ["ExitDraw"] = H_Exit_ExitDraw,
            ["ExitForceDiscard"] = H_Exit_ExitForceDiscard,
            ["ExitRecoverExitUnit"] = H_Exit_ExitRecoverExitUnit,
            ["ExitMutualDestruction"] = H_Exit_ExitMutualDestruction,
            ["ExitBuffAllyHit"] = H_Exit_ExitBuffAllyHit,
            ["CreditDrawDiscard"] = H_Exit_CreditDrawDiscard,
            ["ExitTrashLowCost"] = H_Exit_ExitTrashLowCost,
            ["ExitDebuffEnemy"] = H_Exit_ExitDebuffEnemy,
            ["ExitDrawThenDiscard"] = H_Exit_ExitDrawThenDiscard,
            ["ExitConditionalForceDiscard"] = H_Exit_ExitConditionalForceDiscard,
            ["ArmedOnTrashReturnItem"] = H_Exit_ArmedOnTrashReturnItem,
            ["ExitRecoverItemByCost"] = H_Exit_ExitRecoverItemByCost,
            ["ExitDamageZoneItemExchange"] = H_Exit_ExitDamageZoneItemExchange,
            ["ExitDrawPerExitAlly"] = H_Exit_ExitDrawPerExitAlly,
            ["ExitDebuffEnemyPerExitAlly"] = H_Exit_ExitDebuffEnemyPerExitAlly,
            ["ExitRecoverItemExactCost"] = H_Exit_ExitRecoverItemExactCost,
            ["ExitRecoverUnitExactCost"] = H_Exit_ExitRecoverUnitExactCost,
            ["ExitDamageZoneExchange"] = H_Exit_ExitDamageZoneExchange,
            ["ExitDeployFromHandIfOpponentTurn"] = H_Exit_ExitDeployFromHandIfOpponentTurn,
            ["ArmedExitTrashUnequippedUnit"] = H_Exit_ArmedExitTrashUnequippedUnit,
            ["ArmedSelfTrashReturnWithItems"] = H_Exit_ArmedSelfTrashReturnWithItems,
        };
    }

    private void H_Exit_ExitLevelUp(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            GetOwner(isPlayer).LevelUpLeader();
            HUDView.NotifyStatusChanged();
            Debug.Log($"[엑시트] {card.CardName} → 리더 레벨 {GetOwner(isPlayer).LeaderLevel}");
            break;

        } while (false);
    }

    private void H_Exit_ExitDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            GetOwner(isPlayer).DrawCard(value);
            HandView.Instance?.RefreshHand();
            HUDView.NotifyStatusChanged();
            Debug.Log($"[엑시트] {card.CardName} → 드로우{value}");
            break;

        } while (false);
    }

    private void H_Exit_ExitForceDiscard(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            ApplyForceDiscard(!isPlayer);
            break;

        } while (false);
    }

    private void H_Exit_ExitRecoverExitUnit(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            RecoverExitUnitFromTrash(isPlayer);
            break;

        } while (false);
    }

    private void H_Exit_ExitMutualDestruction(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
                // 공멸은 CombatManager.ProcessMutualDestruction에서 처리 — "전투로 트래시한 상대 유닛"
                // 정보가 필요해서 엑시트 훅에서는 알 수 없음. 여기는 의도적으로 비어 있음(마커 역할).
                break;

            // ST09 구원받지 못한 이안: 엑시트 — 아군 1장 이번 턴 히트+N
        } while (false);
    }

    private void H_Exit_ExitBuffAllyHit(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => true,
                    $"히트+{value}를 줄 유닛을 선택하세요",
                    picked => EffectActions.Buff(isPlayer, picked, 0, value, BoostUntil.EndOfTurn));
                break;

            // ST08/ST09/SB02 크레딧:N — 트래시되면 자신의 패 N장 골라 트래시
        } while (false);
    }

    private void H_Exit_CreditDrawDiscard(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
                EffectActions.DiscardHand(isPlayer, value);
                Debug.Log($"[크레딧] {card.CardName} 트래시 → 패 {value}장 트래시");
                break;

            // ST09 프리즌 브레이크 캐시: 엑시트 — N코스트 이하 유닛 1장 트래시 가능
            // (원문은 양측 필드 대상 — 상대 유닛 대상으로 근사)
        } while (false);
    }

    private void H_Exit_ExitTrashLowCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, !isPlayer,
                    (l, u) => u.Cost <= value,
                    $"트래시할 {value}코스트 이하 유닛을 선택하세요",
                    picked =>
                    {
                        EffectActions.TrashUnit(!isPlayer, picked);
                        NotifyEffectTrash(isPlayer, 1);
                    });
                break;

            // BT01 에테르: 엑시트 — 상대 유닛 1장 파워-N (이번 턴)
        } while (false);
    }

    private void H_Exit_ExitDebuffEnemy(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, !isPlayer,
                    (l, u) => true,
                    $"파워-{value}를 줄 상대 유닛을 선택하세요",
                    picked => AddTurnBoost(!isPlayer, picked, -value));
                break;

            // BT01 D : 킬러 와이프: 엑시트 — 드로우N 후 패N장 골라 트래시
        } while (false);
    }

    private void H_Exit_ExitDrawThenDiscard(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
                EffectActions.Draw(isPlayer, value);
                EffectActions.DiscardHand(isPlayer, value);
                break;

            // BT01 노벨-펭귄 홈즈: 엑시트 — 상대 패 N장 이상이면 상대 패 1장 강제 트래시
        } while (false);
    }

    private void H_Exit_ExitConditionalForceDiscard(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
                if (GetOwner(!isPlayer).Hand.Count >= value)
                    ApplyForceDiscard(!isPlayer);
                break;

            // BT02 암드: 트래시 시 장착 아이템 회수 (패로)
        } while (false);
    }

    private void H_Exit_ArmedOnTrashReturnItem(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            {
                if (lane < 0) break;
                var armedItems = FieldManager.Instance.GetEquippedItems(isPlayer, lane).ToList();
                foreach (var ai in armedItems)
                {
                    FieldManager.Instance.UnequipItem(isPlayer, lane, ai);
                    GetOwner(isPlayer).AddToHand(ai);
                    Debug.Log($"[엑시트] {card.CardName} 암드 → {ai.CardName} 패 귀환");
                }
                HandView.Instance?.RefreshHand();
                break;
            }

            // BT02 트로니: 엑시트 — 트래시에서 N코스트 아이템 1장 패로
        } while (false);
    }

    private void H_Exit_ExitRecoverItemByCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Item && c.Cost == value,
                    $"트래시에서 회수할 {value}코스트 아이템을 선택하세요");
                break;

            // BT02 밀크: 엑시트 — 대미지존 아이템 1장 패로 → 패 1장 대미지존으로
        } while (false);
    }

    private void H_Exit_ExitDamageZoneItemExchange(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            {
                var dmgItems2 = GetOwner(isPlayer).DamageZone.Where(c => c.Type == CardType.Item).ToList();
                if (dmgItems2.Count == 0) break;
                EffectActions.PickFromCards(isPlayer, "패로 가져올 대미지존 아이템을 선택하세요", dmgItems2,
                    pickedItem2 =>
                    {
                        GetOwner(isPlayer).RemoveFromDamageZone(pickedItem2);
                        GetOwner(isPlayer).AddToHand(pickedItem2);
                        HandView.Instance?.RefreshHand();
                        HUDView.NotifyStatusChanged();
                        EffectActions.PickFromCards(isPlayer, "대미지존에 놓을 패를 선택하세요",
                            new List<CardData>(GetOwner(isPlayer).Hand),
                            toPlace2 =>
                            {
                                GetOwner(isPlayer).RemoveFromHand(toPlace2);
                                GetOwner(isPlayer).AddToDamageZone(toPlace2);
                                HandView.Instance?.RefreshHand();
                                HUDView.NotifyStatusChanged();
                            },
                            aiPick: list => list.OrderBy(c => c.Cost).First());
                    },
                    aiPick: list => list.OrderByDescending(c => c.Cost).First());
                break;
            }

            // BT03 로산나: 엑시트 — 다른 엑시트 아군 1장마다 드로우1
        } while (false);
    }

    private void H_Exit_ExitDrawPerExitAlly(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            {
                int exCnt = 0;
                for (int i = 0; i < 3; i++)
                {
                    var u3 = FieldManager.Instance.GetUnit(isPlayer, i);
                    if (u3 != null && u3 != card && u3.EffectTypes.Any(e => e.StartsWith("Exit"))) exCnt++;
                }
                if (exCnt > 0) EffectActions.Draw(isPlayer, exCnt);
                Debug.Log($"[엑시트] {card.CardName} → 엑시트 아군{exCnt}장 → 드로우{exCnt}");
                break;
            }

            // BT03 니힐리스타: 엑시트 — 다른 엑시트 아군 1장마다 상대 유닛 파워-N
        } while (false);
    }

    private void H_Exit_ExitDebuffEnemyPerExitAlly(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            {
                int exCnt3 = 0;
                for (int i = 0; i < 3; i++)
                {
                    var u3 = FieldManager.Instance.GetUnit(isPlayer, i);
                    if (u3 != null && u3 != card && u3.EffectTypes.Any(e => e.StartsWith("Exit"))) exCnt3++;
                }
                if (exCnt3 <= 0) break;
                int totalDebuff = exCnt3 * value;
                EffectActions.SelectUnit(isPlayer, !isPlayer,
                    (l, u) => true,
                    $"파워-{totalDebuff}를 줄 상대 유닛을 선택하세요",
                    picked => AddTurnBoost(!isPlayer, picked, -totalDebuff));
                break;
            }

            // BT03 크라운: 엑시트 — 트래시에서 정확히 N코스트 아이템 1장 패로
        } while (false);
    }

    private void H_Exit_ExitRecoverItemExactCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Item && c.Cost == value,
                    $"트래시에서 회수할 {value}코스트 아이템을 선택하세요");
                break;

            // BT03 신데렐라: 엑시트 — 트래시에서 정확히 N코스트 유닛 1장 패로
        } while (false);
    }

    private void H_Exit_ExitRecoverUnitExactCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Unit && c.Cost == value,
                    $"트래시에서 회수할 {value}코스트 유닛을 선택하세요");
                break;

            // BT03 레오나: 엑시트 — 양쪽 대미지존 카드 1장씩 교환
        } while (false);
    }

    private void H_Exit_ExitDamageZoneExchange(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            {
                var myDmg = GetOwner(isPlayer).DamageZone;
                var opDmg = GetOwner(!isPlayer).DamageZone;
                if (myDmg.Count == 0 || opDmg.Count == 0) break;
                EffectActions.PickFromCards(isPlayer, "상대에게 줄 내 대미지존 카드를 선택하세요", new List<CardData>(myDmg),
                    myPick =>
                    {
                        EffectActions.PickFromCards(isPlayer, "내가 가져올 상대 대미지존 카드를 선택하세요", new List<CardData>(opDmg),
                            opPick =>
                            {
                                GetOwner(isPlayer).RemoveFromDamageZone(myPick);
                                GetOwner(!isPlayer).AddToDamageZone(myPick);
                                GetOwner(!isPlayer).RemoveFromDamageZone(opPick);
                                GetOwner(isPlayer).AddToDamageZone(opPick);
                                Debug.Log($"[엑시트] {card.CardName} → 대미지존 교환");
                            },
                            aiPick: list => list.OrderBy(c => c.Cost).First());
                    },
                    aiPick: list => list.OrderBy(c => c.Cost).First());
                break;
            }

            // BT03 미하라: 엑시트 — 상대 턴이면 패에서 N코스트 이하 유닛 사이즈 무시 배치
        } while (false);
    }

    private void H_Exit_ExitDeployFromHandIfOpponentTurn(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            {
                // 상대 턴 = lane >= 0이고 현재 턴이 상대 것
                // TurnManager.Instance.IsPlayerTurn과 isPlayer 비교
                bool opponentTurn = TurnManager.Instance != null && TurnManager.Instance.IsPlayerTurn != isPlayer;
                if (!opponentTurn) break;
                var candidates3 = GetOwner(isPlayer).Hand
                    .Where(c => c.Type == CardType.Unit && c.Cost <= value)
                    .ToList();
                if (candidates3.Count == 0) break;
                EffectActions.PickFromCards(isPlayer, $"사이즈 무시 배치할 {value}코스트 이하 유닛을 선택하세요", candidates3,
                    picked =>
                    {
                        GetOwner(isPlayer).RemoveFromHand(picked);
                        for (int i = 0; i < 3; i++)
                        {
                            if (FieldManager.Instance.GetUnit(isPlayer, i) == null)
                            {
                                FieldManager.Instance.ForcePlace(isPlayer, i, picked);
                                break;
                            }
                        }
                        HandView.Instance?.RefreshHand();
                    },
                    aiPick: list => list.OrderByDescending(c => c.Cost).First());
                break;
            }

            // BT03 트로니-스위트: 암드 엑시트 — 비장착 아군 유닛 N장까지 트래시
        } while (false);
    }

    private void H_Exit_ArmedExitTrashUnequippedUnit(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            {
                if (lane < 0) break;
                bool wasArmed = FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count > 0;
                if (!wasArmed) break;
                ArmedExitTrashUnequippedStep(isPlayer, card, value, value);
                break;
            }

            // BT03 슈가: 암드 — 트래시 시 아이템과 함께 패로 귀환
        } while (false);
    }

    private void H_Exit_ArmedSelfTrashReturnWithItems(bool isPlayer, int lane, CardData card, int value, string et)
    {
        do
        {
            {
                if (lane < 0) break;
                var myItems3 = FieldManager.Instance.GetEquippedItems(isPlayer, lane).ToList();
                bool wasArmed3 = myItems3.Count > 0;
                if (!wasArmed3) break;
                // 카드+아이템 모두 트래시에서 제거 → 패로
                GetOwner(isPlayer).RemoveFromTrash(card);
                GetOwner(isPlayer).AddToHand(card);
                foreach (var ai3 in myItems3)
                {
                    FieldManager.Instance.UnequipItem(isPlayer, lane, ai3);
                    GetOwner(isPlayer).RemoveFromTrash(ai3);
                    GetOwner(isPlayer).AddToHand(ai3);
                }
                HandView.Instance?.RefreshHand();
                Debug.Log($"[엑시트] {card.CardName} 암드 → {card.CardName}+아이템{myItems3.Count}장 패 귀환");
                break;
            }
        } while (false);
    }

}
