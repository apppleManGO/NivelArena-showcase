// Assets/Scripts/Managers/EffectSystem.EntryHandlers.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────
// 엔트리 효과 핸들러 레지스트리 (Dictionary dispatch)
//
// ★ 새 엔트리 effectType 추가 방법:
//    1) 아래에 H_XXX 핸들러 메서드 1개 작성 (파라미터 이름 isPlayer/lane/card/value/et 유지)
//    2) InitEntryHandlers()의 딕셔너리에 ["XXX"] = H_XXX 한 줄 등록
//    → OnUnitPlaced는 이 테이블만 조회함 (엔트리 switch 전면 폐지 완료 — 2026-07-19).
//      등록 안 된 effectType은 무시됨(무동작).
// ─────────────────────────────────────────────────────────────────────────
public partial class EffectSystem : MonoBehaviour
{
    // et = 원본 effectType 문자열 전체 (예: "EntryRevealSearchFaction:계승자:3") — 다중 파라미터 파싱용
    private delegate void EntryHandler(bool isPlayer, int lane, CardData card, int value, string et);

    private Dictionary<string, EntryHandler> _entryHandlers;

    private void InitEntryHandlers()
    {
        _entryHandlers = new Dictionary<string, EntryHandler>
        {
            ["EntryLevelUp"]            = H_EntryLevelUp,
            ["EntryEncounterDebuff"]    = H_EntryEncounterDebuff,
            ["EntryTrashWeakEncounter"] = H_EntryTrashWeakEncounter,
            ["EntryDraw"]               = H_EntryDraw,
            ["EntryMassDiscard"]        = H_EntryMassDiscard,
            ["EntryDrawThenDiscard"]    = H_EntryDrawThenDiscard,
            ["EntryDiscardDraw"]        = H_EntryDiscardDraw,
            ["EntrySearchItemCost"]     = H_EntrySearchItemCost,
            ["EntryBuffAllyTemp"] = H_EntryBuffAllyTemp,
            ["EntryRevealSearchFaction"] = H_EntryRevealSearchFaction,
            ["EntryRevealSearchKeyword"] = H_EntryRevealSearchKeyword,
            ["EntrySelfCannotAttack"] = H_EntrySelfCannotAttack,
            ["EntryDisableEncounterAttack"] = H_EntryDisableEncounterAttack,
            ["CreditDrawDiscard"] = H_CreditDrawDiscard,
            ["EntryGrantAttackerBoost"] = H_EntryGrantAttackerBoost,
            ["EntryCopyFactionAllyEntry"] = H_EntryCopyFactionAllyEntry,
            ["EntryGrantDuelistAttackerBoost"] = H_EntryGrantDuelistAttackerBoost,
            ["EntryDiscardGrantPenetration"] = H_EntryDiscardGrantPenetration,
            ["EntryGrantAllAttackerBoost"] = H_EntryGrantAllAttackerBoost,
            ["EntryReturnTrashCardsTrashEncounter"] = H_EntryReturnTrashCardsTrashEncounter,
            ["EntryTrashBothByCost"] = H_EntryTrashBothByCost,
            ["EntryForceAttack"] = H_EntryForceAttack,
            ["EntryDiscardTrashEncounter"] = H_EntryDiscardTrashEncounter,
            ["EntrySetEncounterPower"] = H_EntrySetEncounterPower,
            ["EntrySelfBoostUntilOpponentTurn"] = H_EntrySelfBoostUntilOpponentTurn,
            ["EntrySelfHitBoost"] = H_EntrySelfHitBoost,
            ["EntryGrantPenetrationAll"] = H_EntryGrantPenetrationAll,
            ["EntryTrashAllyDraw"] = H_EntryTrashAllyDraw,
            ["EntryOptionalDiscardTrashEncounter"] = H_EntryOptionalDiscardTrashEncounter,
            ["EntryGrantBreakthroughToBaseAlly"] = H_EntryGrantBreakthroughToBaseAlly,
            ["EntryDiscardHighCostTrashEncounter"] = H_EntryDiscardHighCostTrashEncounter,
            ["EntrySelfGrantAttackerBoost"] = H_EntrySelfGrantAttackerBoost,
            ["EntryRecoverUnitByCost"] = H_EntryRecoverUnitByCost,
            ["EntryReturnAllyItem"] = H_EntryReturnAllyItem,
            ["EntryDiscardItemsTrashEncounter"] = H_EntryDiscardItemsTrashEncounter,
            ["EntryRevealSearchItemToDeckBottom"] = H_EntryRevealSearchItemToDeckBottom,
            ["EntryTrashItemsToDeckTrashEncounter"] = H_EntryTrashItemsToDeckTrashEncounter,
            ["EntryRecoverSkillToDeckTop"] = H_EntryRecoverSkillToDeckTop,
            ["EntryGuardianAllyHitBoost"] = H_EntryGuardianAllyHitBoost,
            ["EntryDrawBoth"] = H_EntryDrawBoth,
            ["EntryMillActivateSkillOrDrawBoth"] = H_EntryMillActivateSkillOrDrawBoth,
            ["EntryRevealSearchUnitTrashRest"] = H_EntryRevealSearchUnitTrashRest,
            ["EntryRevealSearchSkillTrashRest"] = H_EntryRevealSearchSkillTrashRest,
            ["EntrySkillZoneTrashDraw"] = H_EntrySkillZoneTrashDraw,
            ["EntryLevelOrDraw"] = H_EntryLevelOrDraw,
            ["FrontlineEntryLevelUp"] = H_FrontlineEntryLevelUp,
            ["EntryDiscardLockHighCostDeploy"] = H_EntryDiscardLockHighCostDeploy,
            ["EntryNoAttackDamageByOpponentHandOver"] = H_EntryNoAttackDamageByOpponentHandOver,
            ["EntryDiscardActivateSkillZone"] = H_EntryDiscardActivateSkillZone,
            ["EntryUpgradePenetrationAlly"] = H_EntryUpgradePenetrationAlly,
            ["EntryRevealSearchItemTrashRestIfArmedAlly"] = H_EntryRevealSearchItemTrashRestIfArmedAlly,
            ["EntryReturnItemsTrashEnemiesByCost"] = H_EntryReturnItemsTrashEnemiesByCost,
            ["EntryTrashAllyGrantExitReturn"] = H_EntryTrashAllyGrantExitReturn,
            ["EntryTrashEncounterIfOpponentLowHand"] = H_EntryTrashEncounterIfOpponentLowHand,
            ["EntryGrantDefenderFinisherToLowCostEncounter"] = H_EntryGrantDefenderFinisherToLowCostEncounter,
            ["EntryEncounterDebuffIfTrashDraw"] = H_EntryEncounterDebuffIfTrashDraw,
            ["EntryTrashItemsToDeckBottomTrashEncounterByCost"] = H_EntryTrashItemsToDeckBottomTrashEncounterByCost,
        };
    }

    // ── 핸들러 (case 본문 그대로 — 파라미터명 동일하므로 로직 무변경) ──

    private void H_EntryLevelUp(bool isPlayer, int lane, CardData card, int value, string et)
    {
        GetOwner(isPlayer).LevelUpLeader();
        HUDView.NotifyStatusChanged();
        Debug.Log($"[앤트리] {card.CardName} → 리더 레벨 {GetOwner(isPlayer).LeaderLevel}");
    }

    private void H_EntryEncounterDebuff(bool isPlayer, int lane, CardData card, int value, string et)
    {
        AddTurnBoost(!isPlayer, lane, -value);
        Debug.Log($"[앤트리] {card.CardName} → 조우 유닛 파워-{value}");
    }

    private void H_EntryTrashWeakEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var weak = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (weak != null && weak.Cost <= value)
        {
            FieldManager.Instance.RemoveUnit(!isPlayer, lane);
            GetOwner(!isPlayer).AddToTrash(weak);
            Debug.Log($"[앤트리] {card.CardName} → {weak.CardName} 트래시 ({value}코이하)");
        }
    }

    private void H_EntryDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
        GetOwner(isPlayer).DrawCard(value);
        HandView.Instance?.RefreshHand();
        HUDView.NotifyStatusChanged();
        Debug.Log($"[엔트리] {card.CardName} → 드로우{value}");
    }

    private void H_EntryMassDiscard(bool isPlayer, int lane, CardData card, int value, string et)
    {
        ApplyMassDiscard(isPlayer, lane, card);
    }

    // ST05 슈가, ST11 레피테아: 드로우N 후 패N장 골라 트래시
    private void H_EntryDrawThenDiscard(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.Draw(isPlayer, value);
        EffectActions.DiscardHand(isPlayer, value);
    }

    // ST10 로빈 후드 제니스: 패N장 트래시할 수 있다 → 트래시한 만큼 드로우
    private void H_EntryDiscardDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.DiscardHandOptional(isPlayer, value,
            discarded => EffectActions.Draw(isPlayer, discarded));
    }

    // ST05 크라운: 덱에서 N코스트인 아이템 서치
    private void H_EntrySearchItemCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.SearchDeck(isPlayer,
            c => c.Type == CardType.Item && c.Cost == value,
            $"덱에서 가져올 {value}코스트 아이템을 선택하세요");
    }

    // ── 아래 case들은 점진적으로 EntryHandlers.cs 레지스트리로 이전 예정 ──
    // (EntryLevelUp/EntryEncounterDebuff/EntryTrashWeakEncounter/EntryDraw/
    //  EntryMassDiscard/EntryDrawThenDiscard/EntryDiscardDraw/EntrySearchItemCost
    //  → 이미 레지스트리로 이전됨)
    // ST06 소악마 루아, BT04 나탈론 학원 등: 다른 아군 1장 이번 턴 파워+N
    private void H_EntryBuffAllyTemp(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => l != lane,
                $"파워+{value}를 줄 유닛을 선택하세요",
                picked => EffectActions.Buff(isPlayer, picked, value, 0, BoostUntil.EndOfTurn));
            return;

        // ST06 실크/데스티나: 덱 위 N장 공개, 해당 소속 카드 1장 패, 나머지 트래시
        // 형식: "EntryRevealSearchFaction:소속:N"
    }

    private void H_EntryRevealSearchFaction(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var parts = et.Split(':');
            string faction = parts.Length > 1 ? parts[1] : "";
            int reveal = parts.Length > 2 && int.TryParse(parts[2], out int r) ? r : 3;
            EffectActions.RevealPickToHand(isPlayer, reveal,
                c => (c.Faction != null && c.Faction.Contains(faction)) || c.Keywords.Contains(faction),
                $"패에 넣을 《{faction}》 카드를 선택하세요");
            return;
        }

        // ST10 리베르타(체인), ST11 헬레나(버프): 덱 위 N장 공개, 해당 키워드 유닛 1장 패
        // 형식: "EntryRevealSearchKeyword:키워드:N"
    }

    private void H_EntryRevealSearchKeyword(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var parts = et.Split(':');
            string keyword = parts.Length > 1 ? parts[1] : "";
            int reveal = parts.Length > 2 && int.TryParse(parts[2], out int r) ? r : 3;
            EffectActions.RevealPickToHand(isPlayer, reveal,
                c => c.Type == CardType.Unit && c.Keywords.Contains(keyword),
                $"패에 넣을 [{keyword}] 유닛을 선택하세요");
            return;
        }

        // SB02 슈린, BT07 데모고르곤/기가스: 이 유닛은 이 턴 공격 불가
    }

    private void H_EntrySelfCannotAttack(bool isPlayer, int lane, CardData card, int value, string et)
    {
            DisableAttack(isPlayer, lane, BoostUntil.EndOfTurn);
            return;

        // BT06 소냐: 조우 유닛 상대 턴 끝까지 공격 불가
    }

    private void H_EntryDisableEncounterAttack(bool isPlayer, int lane, CardData card, int value, string et)
    {
            if (FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
                DisableAttack(!isPlayer, lane, BoostUntil.EndOfOpponentTurn);
            return;

        // ST08/ST09/SB02 크레딧:N — 배치되면 드로우N (트래시 시 패N장 트래시는 OnUnitTrashed에서)
    }

    private void H_CreditDrawDiscard(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.Draw(isPlayer, value);
            Debug.Log($"[크레딧] {card.CardName} → 드로우{value}");
            return;

        // ST06 유나: 엔트리 — 다른 아군 1장에 이번 턴 「어태커 파워+N」 부여
    }

    private void H_EntryGrantAttackerBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => l != lane,
                $"「어태커 파워+{value}」를 부여할 유닛을 선택하세요",
                picked => GrantAttackerBoost(isPlayer, picked, value));
            return;

        // ST06 이세리아: 엔트리 — 〈이세리아〉 이외의 《계승자》 아군 유닛 1장을 골라 그 유닛의 엔트리 효과를 발동
        // (근사: "하나 골라 발동"이지만 대상의 배치 트리거 전부를 재실행함 — ST06 카드는 대부분 엔트리 효과가 1개뿐이라 실질적으로 동일)
    }

    private void H_EntryCopyFactionAllyEntry(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => l != lane && u.CardName != card.CardName && u.Faction != null && u.Faction.Contains("계승자"),
                "엔트리 효과를 복사 발동할 《계승자》 유닛을 선택하세요",
                picked =>
                {
                    var target = FieldManager.Instance.GetUnit(isPlayer, picked);
                    if (target == null) return;
                    Debug.Log($"[엔트리 복사] {card.CardName} → {target.CardName}의 엔트리 재발동");
                    OnUnitPlaced(isPlayer, lane, target);
                });
            return;

        // ST06 리나크: 엔트리 — 다른 아군 1장에 이번 턴 듀얼리스트 + 「어태커 파워+N(이 공격이 끝날 때까지)」 부여
    }

    private void H_EntryGrantDuelistAttackerBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => l != lane,
                $"듀얼리스트 + 「어태커 파워+{value}」를 부여할 유닛을 선택하세요",
                picked =>
                {
                    GrantAttackerBoost(isPlayer, picked, value);
                    _grantedDuelistThisTurn.Add(Key(isPlayer, picked));
                    Debug.Log($"[엔트리] {card.CardName} → 레인{picked} 듀얼리스트+어태커 파워+{value} 부여");
                });
            return;

        // ST06 기원의 라스: 엔트리 — 패1 트래시(선택) → 다른 아군 1장에 이번 턴 「어태커 관통[N]」 부여
    }

    private void H_EntryDiscardGrantPenetration(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
            {
                if (discarded == 0) return;
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => l != lane,
                    $"「어태커 관통[{value}]」을 부여할 유닛을 선택하세요",
                    picked =>
                    {
                        _grantedPenetration[Key(isPlayer, picked)] = value;
                        Debug.Log($"[엔트리] {card.CardName} → 레인{picked} 어태커 관통[{value}] 부여");
                    });
            });
            return;

        // ST10 리아트리스: 엔트리 — 아군 전체에 이번 턴 「어태커 파워+N」 부여
    }

    private void H_EntryGrantAllAttackerBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
            for (int i = 0; i < 3; i++)
                if (FieldManager.Instance.GetUnit(isPlayer, i) != null)
                    GrantAttackerBoost(isPlayer, i, value);
            Debug.Log($"[엔트리] {card.CardName} → 아군 전체 「어태커 파워+{value}」 부여");
            return;

        // ST07 하르세티: 트래시의 호문클루스(트리거 없는) 카드를 조우 코스트만큼 골라 덱 밑으로 → 조우 트래시
    }

    private void H_EntryReturnTrashCardsTrashEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var encounter = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (encounter != null)
                RepeatReturnHomunculusToBottom(isPlayer, lane, encounter.Cost, 0);
            return;
        }

        // ST09 프리즌 브레이크 캐시: 다른 아군 1장 + 그 코스트 이하 상대 1장 선택해 함께 트래시 가능
    }

    private void H_EntryTrashBothByCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => l != lane,
                "함께 트래시할 아군 유닛을 선택하세요 (취소 가능)",
                allyLane =>
                {
                    int allyCost = FieldManager.Instance.GetUnit(isPlayer, allyLane)?.Cost ?? 0;
                    EffectActions.SelectUnit(isPlayer, !isPlayer,
                        (l2, u2) => u2.Cost <= allyCost,
                        $"함께 트래시할 {allyCost}코스트 이하 상대 유닛을 선택하세요",
                        enemyLane =>
                        {
                            EffectActions.TrashUnit(isPlayer, allyLane);
                            EffectActions.TrashUnit(!isPlayer, enemyLane);
                            NotifyEffectTrash(isPlayer, 2);
                        });
                });
            return;

        // ST10 사막의 꽃 실비아: 엔트리 — 조우 유닛이 있다면 이 유닛으로 즉시 공격
    }

    private void H_EntryForceAttack(bool isPlayer, int lane, CardData card, int value, string et)
    {
            if (FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
                StartCoroutine(CombatManager.Instance.ForceAttackLane(isPlayer, lane));
            return;

        // ST09 고스트헌터 유키: 조우 히트만큼 패 트래시할 수 있다 → 조우 유닛 트래시
    }

    private void H_EntryDiscardTrashEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var encounter = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (encounter == null) return;
            int need = GetEffectiveHit(!isPlayer, lane, encounter);
            if (GetOwner(isPlayer).Hand.Count < need) return;
            EffectActions.DiscardHandOptional(isPlayer, need, discarded =>
            {
                if (discarded >= need)
                    EffectActions.TrashUnit(!isPlayer, lane);
            });
            return;
        }

        // BT01 조우 유닛 파워를 N으로 고정 (이번 턴): 큰 음수 디버프로 근사
    }

    private void H_EntrySetEncounterPower(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var encounter = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (encounter != null)
            {
                int curPower = GetEffectivePower(!isPlayer, lane, encounter);
                int delta    = value - curPower;
                AddTurnBoost(!isPlayer, lane, delta);
                Debug.Log($"[엔트리] {card.CardName} → 조우 파워를 {value}로 고정 (델타:{delta})");
            }
            return;
        }

        // BT01 벨로타/스노우 화이트-화이트 나이트: 엔트리 — 이 유닛 상대 턴 끝까지 파워+N
    }

    private void H_EntrySelfBoostUntilOpponentTurn(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.Buff(isPlayer, lane, value, 0, BoostUntil.EndOfOpponentTurn);
            Debug.Log($"[엔트리] {card.CardName} → 파워+{value} (상대 턴까지)");
            return;

        // BT01 도라: 엔트리 — 이 유닛 히트+N (이번 턴)
    }

    private void H_EntrySelfHitBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
            AddTurnHitBoost(isPlayer, lane, value);
            Debug.Log($"[엔트리] {card.CardName} → 히트+{value}");
            return;

        // BT01 레드 후드: 엔트리 — 어태커 아군 전체에 관통[N] 부여
    }

    private void H_EntryGrantPenetrationAll(bool isPlayer, int lane, CardData card, int value, string et)
    {
            for (int i2 = 0; i2 < 3; i2++)
            {
                var ally2 = FieldManager.Instance.GetUnit(isPlayer, i2);
                if (ally2 != null && ally2.Keywords.Contains("어태커"))
                {
                    string k2 = Key(isPlayer, i2);
                    _grantedPenetration[k2] = _grantedPenetration.GetValueOrDefault(k2) + value;
                }
            }
            Debug.Log($"[엔트리] {card.CardName} → 어태커 아군 전체 관통[{value}] 부여");
            return;

        // BT01 로산나-시크 오션: 엔트리 — 아군 1장 트래시 → 드로우N
    }

    private void H_EntryTrashAllyDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => true,
                "트래시할 아군 유닛을 선택하세요",
                allyLane =>
                {
                    EffectActions.TrashUnit(isPlayer, allyLane);
                    EffectActions.Draw(isPlayer, value);
                });
            return;

        // BT01 프리바티-연회의 공주: 엔트리 — 패N장 트래시할 수 있다 → 조우 유닛 트래시
    }

    private void H_EntryOptionalDiscardTrashEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var enc2 = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (enc2 == null) return;
            EffectActions.DiscardHandOptional(isPlayer, value, discarded =>
            {
                if (discarded >= value)
                    EffectActions.TrashUnit(!isPlayer, lane);
            });
            return;
        }

        // BT01 홍련: 엔트리 — 베이스 아군 1장에 돌파[N코 이하] 부여
    }

    private void H_EntryGrantBreakthroughToBaseAlly(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => u.Faction != null && u.Faction.StartsWith("베이스"),
                "돌파를 부여할 베이스 유닛을 선택하세요",
                allyLane => GrantConditionalBreakthrough(isPlayer, allyLane, value));
            return;

        // BT01 신데렐라: 엔트리 — 패에서 조우보다 높은 코스트 카드 트래시 → 조우 트래시
    }

    private void H_EntryDiscardHighCostTrashEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var enc3 = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (enc3 == null) return;
            var handOpts = GetOwner(isPlayer).Hand.Where(c => c.Cost > enc3.Cost).ToList();
            if (handOpts.Count == 0) return;
            EffectActions.PickFromCards(isPlayer,
                $"트래시할 패를 선택하세요 ({enc3.CardName} 코스트 {enc3.Cost} 초과)",
                handOpts,
                picked =>
                {
                    GetOwner(isPlayer).RemoveFromHand(picked);
                    GetOwner(isPlayer).AddToTrash(picked);
                    EffectActions.RefreshUI();
                    EffectActions.TrashUnit(!isPlayer, lane);
                });
            return;
        }

        // BT02 볼륨-비트 더 건: 엔트리 — 이 턴이 끝날 때까지 「어태커 파워+N」 획득
    }

    private void H_EntrySelfGrantAttackerBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
            GrantAttackerBoost(isPlayer, lane, value);
            Debug.Log($"[엔트리] {card.CardName} → 어태커 파워+{value} 획득");
            return;

        // BT02 바이퍼-톡식 래빗: 엔트리 — 트래시에서 N코스트 이하 유닛 1장 패로
    }

    private void H_EntryRecoverUnitByCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.RecoverFromTrash(isPlayer,
                c => c.Type == CardType.Unit && c.Cost <= value,
                $"트래시에서 회수할 {value}코스트 이하 유닛을 선택하세요");
            return;

        // BT02 에이드: 엔트리 — 아군 유닛 1장의 장착 아이템 1장을 패로
    }

    private void H_EntryReturnAllyItem(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            // 아이템을 장착한 아군 레인 찾기
            var armedLanes = Enumerable.Range(0, 3)
                .Where(i => FieldManager.Instance.GetUnit(isPlayer, i) != null
                         && FieldManager.Instance.GetEquippedItems(isPlayer, i).Count > 0)
                .ToList();
            if (armedLanes.Count == 0) return;

            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => FieldManager.Instance.GetEquippedItems(isPlayer, l).Count > 0,
                "아이템을 회수할 아군 유닛 레인을 선택하세요",
                allyLane =>
                {
                    var items = FieldManager.Instance.GetEquippedItems(isPlayer, allyLane);
                    if (items.Count == 0) return;
                    var item = isPlayer
                        ? items[0] // 플레이어: 첫 번째 아이템 (UI 간소화)
                        : items[0]; // AI도 동일
                    FieldManager.Instance.UnequipItem(isPlayer, allyLane, item);
                    GetOwner(isPlayer).AddToHand(item);
                    HandView.Instance?.RefreshHand();
                    Debug.Log($"[엔트리] {card.CardName} → {item.CardName} 패로");
                });
            return;
        }

        // BT02 레오나: 엔트리 — 조우 히트만큼 패에서 아이템 트래시 가능 → 조우 트래시
    }

    private void H_EntryDiscardItemsTrashEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var enc4 = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (enc4 == null) return;
            int need4 = GetEffectiveHit(!isPlayer, lane, enc4);
            var handItems = GetOwner(isPlayer).Hand.Where(c => c.Type == CardType.Item).ToList();
            if (handItems.Count < need4 || need4 == 0) return;
            EffectActions.DiscardHandOptional(isPlayer, need4, discarded =>
            {
                // 정확히 need4장 트래시했을 때만 조우 트래시 — 간소화: 선택한 만큼 트래시하면 발동
                if (discarded >= need4)
                    EffectActions.TrashUnit(!isPlayer, lane);
            });
            return;
        }

        // BT02 리타-피쉬 가드: 엔트리 — 덱 위 N장 공개, 아이템 1장 패, 나머지 덱 맨 아래
    }

    private void H_EntryRevealSearchItemToDeckBottom(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var owner4 = GetOwner(isPlayer);
            var revealed4 = new List<CardData>();
            for (int i = 0; i < value && owner4.DrawPile.Count > 0; i++)
            {
                revealed4.Add(owner4.DrawPile[0]);
                owner4.RemoveFromDeckAt(0);
            }
            var items4 = revealed4.Where(c => c.Type == CardType.Item).ToList();
            var rest4  = revealed4.Where(c => c.Type != CardType.Item).ToList();
            EffectActions.PickFromCards(isPlayer, "패로 가져올 아이템을 선택하세요", items4,
                picked4 =>
                {
                    owner4.AddToHand(picked4);
                    items4.Remove(picked4);
                    foreach (var r in items4.Concat(rest4)) owner4.AddToDeckBottom(r);
                    // 셔플 없이 덱 맨 아래 추가 (원문: "원하는 순서대로")
                    HandView.Instance?.RefreshHand();
                    Debug.Log($"[엔트리] {card.CardName} → {picked4.CardName} 패로");
                },
                onCancel: () => { foreach (var r in revealed4) owner4.AddToDeckBottom(r); });
            return;
        }

        // BT02 소다 : 트윙클링 바니: 엔트리 — 트래시에서 아이템 N장 덱 맨 아래로 → 조우 트래시
    }

    private void H_EntryTrashItemsToDeckTrashEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var enc5 = FieldManager.Instance.GetUnit(!isPlayer, lane);
            var trashItems5 = GetOwner(isPlayer).TrashPile.Where(c => c.Type == CardType.Item).ToList();
            if (trashItems5.Count == 0) return;
            EntryTrashItemsToDeckStep(isPlayer, lane, card, value, trashItems5, enc5);
            return;
        }

        // BT02 루드밀라 : 윈터 오너: 엔트리 — 트래시에서 트리거 없는 스킬 1장 덱 맨 위로
    }

    private void H_EntryRecoverSkillToDeckTop(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var candidates5 = GetOwner(isPlayer).TrashPile
                .Where(c => c.Type == CardType.Skill && !c.IsTrigger)
                .ToList();
            if (candidates5.Count == 0) return;
            EffectActions.PickFromCards(isPlayer, "덱 맨 위에 놓을 스킬을 선택하세요", candidates5,
                picked =>
                {
                    GetOwner(isPlayer).RemoveFromTrash(picked);
                    GetOwner(isPlayer).AddToDeckTop(picked);
                    Debug.Log($"[엔트리] {card.CardName} → {picked.CardName} 덱 맨 위로");
                },
                aiPick: list => list.OrderByDescending(c => c.Cost).First());
            return;
        }

        // BT02 앨리스 : 원더랜드 바니: 엔트리 — 가디언 아군 1장 이번 턴 히트+N
    }

    private void H_EntryGuardianAllyHitBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => u.Keywords.Contains("가디언"),
                $"히트+{value}를 줄 가디언 유닛을 선택하세요",
                picked => AddTurnHitBoost(isPlayer, picked, value));
            return;

        // BT03 루드밀라: 엔트리 — 양쪽 모두 드로우N
    }

    private void H_EntryDrawBoth(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.Draw(isPlayer, value);
            EffectActions.Draw(!isPlayer, value);
            Debug.Log($"[엔트리] {card.CardName} → 양쪽 드로우{value}");
            return;

        // BT03 앨리스: 엔트리 — 덱 위 N장 트래시, 스킬이면 스킬존 발동 or 양쪽 드로우1
    }

    private void H_EntryMillActivateSkillOrDrawBoth(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var owner6 = GetOwner(isPlayer);
            if (owner6.DrawPile.Count == 0) return;
            var milled = owner6.DrawPile[owner6.DrawPile.Count - 1];
            owner6.RemoveFromDeckAt(owner6.DrawPile.Count - 1);
            owner6.AddToTrash(milled);
            if (milled.Type == CardType.Skill)
            {
                // 스킬존에 발동 (SkillZoneSlot.Drop 흐름과 동일하게)
                owner6.AddToSkillZone(milled);
                ExecuteSkillEffect(isPlayer, milled, lane);
                owner6.RemoveFromSkillZone(milled);
                owner6.AddToTrash(milled);
            }
            else
            {
                EffectActions.Draw(isPlayer, 1);
                EffectActions.Draw(!isPlayer, 1);
            }
            Debug.Log($"[엔트리] {card.CardName} → 밀 {milled.CardName}({milled.Type})");
            return;
        }

        // BT03 브리드: 엔트리 — 덱 위 N장 공개, M코스트 이하 유닛 1장 패, 나머지 트래시
    }

    private void H_EntryRevealSearchUnitTrashRest(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var parts6 = et.Split(':');
            int rev6 = value;
            int costCap6 = parts6.Length > 2 && int.TryParse(parts6[2], out int cc6) ? cc6 : 99;
            var owner6b = GetOwner(isPlayer);
            int cnt6 = Mathf.Min(rev6, owner6b.DrawPile.Count);
            // BT03-023: "덱 맨 위에서 N장 공개" — DrawPile[0]이 맨 위 (드로우가 index 0에서 가져감)
            var revealed6 = owner6b.PeekDeckTop(cnt6);
            owner6b.RemoveDeckTop(cnt6);
            var units6 = revealed6.Where(c => c.Type == CardType.Unit && c.Cost <= costCap6).ToList();
            var rest6 = revealed6.Where(c => !(c.Type == CardType.Unit && c.Cost <= costCap6)).ToList();
            foreach (var r in rest6) owner6b.AddToTrash(r);
            if (units6.Count == 0) return;
            EffectActions.PickFromCards(isPlayer, $"패로 가져올 {costCap6}코스트 이하 유닛을 선택하세요", units6,
                picked => { units6.Remove(picked); owner6b.AddToHand(picked); foreach (var r in units6) owner6b.AddToTrash(r); HandView.Instance?.RefreshHand(); },
                aiPick: list => list.OrderByDescending(c => c.Cost).First(),
                onCancel: () => { foreach (var r in units6) owner6b.AddToTrash(r); });
            return;
        }

        // BT03 메어리: 엔트리 — 덱 위 N장 공개, 스킬 1장 패, 나머지 트래시
    }

    private void H_EntryRevealSearchSkillTrashRest(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var owner6c = GetOwner(isPlayer);
            int cnt6c = Mathf.Min(value, owner6c.DrawPile.Count);
            // BT03-055: "덱 맨 위에서 N장 공개" — DrawPile[0]이 맨 위
            var revealed6c = owner6c.PeekDeckTop(cnt6c);
            owner6c.RemoveDeckTop(cnt6c);
            var skills6c = revealed6c.Where(c => c.Type == CardType.Skill).ToList();
            var rest6c = revealed6c.Where(c => c.Type != CardType.Skill).ToList();
            foreach (var r in rest6c) owner6c.AddToTrash(r);
            if (skills6c.Count == 0) return;
            EffectActions.PickFromCards(isPlayer, "패로 가져올 스킬을 선택하세요", skills6c,
                picked => { skills6c.Remove(picked); owner6c.AddToHand(picked); foreach (var r in skills6c) owner6c.AddToTrash(r); HandView.Instance?.RefreshHand(); },
                aiPick: list => list.OrderByDescending(c => c.Cost).First(),
                onCancel: () => { foreach (var r in skills6c) owner6c.AddToTrash(r); });
            return;
        }

        // BT03 홍련-연화: 엔트리 — 스킬존 스킬 1장 트래시 → 드로우N
    }

    private void H_EntrySkillZoneTrashDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var skillZone6 = GetOwner(isPlayer).SkillZone;
            if (skillZone6.Count == 0) return;
            EffectActions.PickFromCards(isPlayer, "트래시할 스킬존 스킬을 선택하세요", new List<CardData>(skillZone6),
                picked =>
                {
                    GetOwner(isPlayer).RemoveFromSkillZone(picked);
                    GetOwner(isPlayer).AddToTrash(picked);
                    EffectActions.Draw(isPlayer, value);
                },
                aiPick: list => list.OrderBy(c => c.Cost).First());
            return;
        }

        // BT03 디젤: 엔트리 — 리더 레벨 N 이상이면 레벨+1, 아니면 드로우M
    }

    private void H_EntryLevelOrDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var parts6d = et.Split(':');
            int lvReq6 = value;
            int draw6 = parts6d.Length > 2 && int.TryParse(parts6d[2], out int d6) ? d6 : 1;
            if (GetOwner(isPlayer).LeaderLevel >= lvReq6) EffectActions.LevelUp(isPlayer);
            else EffectActions.Draw(isPlayer, draw6);
            return;
        }

        // BT03 티아: 엔트리(전선 — 같은 레인에 상대 유닛 있을 때) — 리더 레벨+1
    }

    private void H_FrontlineEntryLevelUp(bool isPlayer, int lane, CardData card, int value, string et)
    {
            if (FieldManager.Instance.GetUnit(!isPlayer, lane) != null) EffectActions.LevelUp(isPlayer);
            return;

        // BT03 앵커: 엔트리 — 패 N장 트래시, 상대 M코스트 이상 유닛 이번 턴 배치 불가
    }

    private void H_EntryDiscardLockHighCostDeploy(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var parts6e = et.Split(':');
            int discard6 = value;
            int lockCost6 = parts6e.Length > 2 && int.TryParse(parts6e[2], out int lc6) ? lc6 : 5;
            for (int i = 0; i < discard6; i++) ApplyForceDiscard(isPlayer);
            _highCostDeployLocked[Idx(!isPlayer)] = lockCost6;
            Debug.Log($"[엔트리] {card.CardName} → 상대 {lockCost6}코스트 이상 배치 불가(이번 턴)");
            return;
        }

        // BT03 헬름: 엔트리 — 상대 패가 N장 이상이면 상대 1대미지
    }

    private void H_EntryNoAttackDamageByOpponentHandOver(bool isPlayer, int lane, CardData card, int value, string et)
    {
            if (GetOwner(!isPlayer).Hand.Count >= value)
            {
                GetOwner(!isPlayer).TakeDamage(1);
                Debug.Log($"[엔트리] {card.CardName} → 상대 패{GetOwner(!isPlayer).Hand.Count}장 ≥ {value} → 1대미지");
            }
            return;

        // BT03 메어리-의료: 엔트리 — 패 N장 트래시 → 스킬존 스킬 1장 효과 발동
    }

    private void H_EntryDiscardActivateSkillZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            for (int i = 0; i < value; i++) ApplyForceDiscard(isPlayer);
            var sz = GetOwner(isPlayer).SkillZone;
            if (sz.Count == 0) return;
            EffectActions.PickFromCards(isPlayer, "발동할 스킬존 스킬을 선택하세요", new List<CardData>(sz),
                picked => ExecuteSkillEffect(isPlayer, picked, lane),
                aiPick: list => list.OrderByDescending(c => c.Cost).First());
            return;
        }

        // BT03 아니스: 엔트리 — 업그레이드한 아군 1장에 관통[N] 부여, 파워+M
    }

    private void H_EntryUpgradePenetrationAlly(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var parts6f = et.Split(':');
            int pen6 = value;
            int boost6 = parts6f.Length > 2 && int.TryParse(parts6f[2], out int b6) ? b6 : 0;
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => true,
                "관통 부여할 아군 유닛을 선택하세요",
                picked =>
                {
                    _grantedPenetration[Key(isPlayer, picked)] = pen6;
                    if (boost6 > 0) AddTurnBoost(isPlayer, picked, boost6);
                });
            return;
        }

        // BT03 폴크방: 엔트리 — 덱 위 N장 공개, 조우 유닛과 파워 비교, 이기는 아이템이면 패/아니면 트래시
    }

    private void H_EntryRevealSearchItemTrashRestIfArmedAlly(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var owner6g = GetOwner(isPlayer);
            int cnt6g = Mathf.Min(value, owner6g.DrawPile.Count);
            // BT03-071: "덱 맨 위에서 N장 공개" — DrawPile[0]이 맨 위
            var revealed6g = owner6g.PeekDeckTop(cnt6g);
            owner6g.RemoveDeckTop(cnt6g);
            var items6g = revealed6g.Where(c => c.Type == CardType.Item).ToList();
            var rest6g = revealed6g.Where(c => c.Type != CardType.Item).ToList();
            foreach (var r in rest6g) owner6g.AddToTrash(r);
            // 암드 아군이 있으면 아이템 모두 패, 아니면 트래시
            bool hasArmed = Enumerable.Range(0, 3).Any(i =>
                FieldManager.Instance.GetUnit(isPlayer, i) != null
                && FieldManager.Instance.GetEquippedItems(isPlayer, i).Count > 0);
            foreach (var item in items6g)
            {
                if (hasArmed) owner6g.AddToHand(item);
                else owner6g.AddToTrash(item);
            }
            HandView.Instance?.RefreshHand();
            Debug.Log($"[엔트리] {card.CardName} → 공개{cnt6g}장, 아이템{items6g.Count}장 {(hasArmed ? "패" : "트래시")}");
            return;
        }

        // BT03 베히모스: 엔트리 — 패의 아이템 전부 트래시, 트래시한 코스트합 이하 상대 유닛들 트래시
    }

    private void H_EntryReturnItemsTrashEnemiesByCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var owner6h = GetOwner(isPlayer);
            var handItems6 = owner6h.Hand.Where(c => c.Type == CardType.Item).ToList();
            int costSum6 = 0;
            foreach (var hi in handItems6)
            {
                owner6h.RemoveFromHand(hi);
                owner6h.AddToTrash(hi);
                costSum6 += hi.Cost;
            }
            HandView.Instance?.RefreshHand();
            // 코스트합 이하 상대 유닛 전부 트래시
            for (int i = 0; i < 3; i++)
            {
                var eu = FieldManager.Instance.GetUnit(!isPlayer, i);
                if (eu != null && eu.Cost <= costSum6) EffectActions.TrashUnit(!isPlayer, i);
            }
            Debug.Log($"[엔트리] {card.CardName} → 패 아이템 {handItems6.Count}장 트래시, 코스트합{costSum6}");
            return;
        }

        // BT03 D: 엔트리 — 아군 1장 트래시 → 그 유닛에 엑시트: 패로 귀환 부여
    }

    private void H_EntryTrashAllyGrantExitReturn(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => l != lane,
                "트래시할 아군 유닛을 선택하세요",
                picked =>
                {
                    _grantedExitReturn.Add(Key(isPlayer, picked));
                    EffectActions.TrashUnit(isPlayer, picked);
                });
            return;

        // BT03 리틀 머메이드: 엔트리 — 상대 패 N장 이하면 조우 유닛 트래시
    }

    private void H_EntryTrashEncounterIfOpponentLowHand(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var enc6i = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (enc6i != null && GetOwner(!isPlayer).Hand.Count <= value)
                EffectActions.TrashUnit(!isPlayer, lane);
            return;
        }

        // BT03 메이든: 엔트리 — N코스트 이하 조우 유닛에 DefenderFinisher 부여
    }

    private void H_EntryGrantDefenderFinisherToLowCostEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var enc6j = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (enc6j != null && enc6j.Cost <= value)
            {
                _grantedDefenderFinisher.Add(Key(!isPlayer, lane));
                Debug.Log($"[엔트리] {card.CardName} → {enc6j.CardName}에 DefenderFinisher 부여");
            }
            return;
        }

        // BT03 홍련-연화 크로스: 엔트리 — 조우 유닛이 트래시 카드 있으면 파워-N → 드로우M
    }

    private void H_EntryEncounterDebuffIfTrashDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var parts6k = et.Split(':');
            int debuff6 = value;
            int draw6k = parts6k.Length > 2 && int.TryParse(parts6k[2], out int d6k) ? d6k : 1;
            var enc6k = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (enc6k != null && GetOwner(!isPlayer).TrashPile.Count > 0)
            {
                AddTurnBoost(!isPlayer, lane, -debuff6);
                EffectActions.Draw(isPlayer, draw6k);
                Debug.Log($"[엔트리] {card.CardName} → 조우 파워-{debuff6}, 드로우{draw6k}");
            }
            return;
        }

        // BT03 슈엔: 엔트리 — 아이템 N장 덱 맨 아래로 → 코스트합 이하 상대 유닛 트래시
    }

    private void H_EntryTrashItemsToDeckBottomTrashEncounterByCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var owner6l = GetOwner(isPlayer);
            var allItems6 = owner6l.Hand.Where(c => c.Type == CardType.Item).Concat(
                owner6l.TrashPile.Where(c => c.Type == CardType.Item)).ToList();
            if (allItems6.Count == 0) return;
            EntryTrashItemsToDeckBottomByCostStep(isPlayer, lane, card, value, allItems6, 0);
            return;
        }
    }

}
