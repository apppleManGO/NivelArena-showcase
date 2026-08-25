// EffectSystem.SkillHandlers.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 스킬(ExecuteSkillEffect) 효과 핸들러 레지스트리 (Dictionary dispatch)
// 시그니처: skill(카드)/targetLane. *Step 등 재귀 헬퍼는 EffectSystem.Skill.cs에 유지.
// 새 스킬 effectType: H_Sk_XXX 핸들러 작성 + InitSkillHandlers()에 등록.
public partial class EffectSystem : MonoBehaviour
{
    private delegate void SkillHandler(bool isPlayer, CardData skill, int targetLane, int value, string et);
    private Dictionary<string, SkillHandler> _skillHandlers;

    private void InitSkillHandlers()
    {
        _skillHandlers = new Dictionary<string, SkillHandler>
        {
            ["DebuffEnemyUnit"] = H_Sk_DebuffEnemyUnit,
            ["BuffAllyUnit"] = H_Sk_BuffAllyUnit,
            ["BuffAllAllies"] = H_Sk_BuffAllAllies,
            ["LevelUp"] = H_Sk_LevelUp,
            ["RecoverFromTrash"] = H_Sk_RecoverFromTrash,
            ["TopDeckSearch"] = H_Sk_TopDeckSearch,
            ["ForceDiscardExchange"] = H_Sk_ForceDiscardExchange,
            ["TrashAllySelfDraw"] = H_Sk_TrashAllySelfDraw,
            ["TrashBothUnits"] = H_Sk_TrashBothUnits,
            ["TrashWeakestInLane"] = H_Sk_TrashWeakestInLane,
            ["TrashFieldByHandCost"] = H_Sk_TrashFieldByHandCost,
            ["Draw"] = H_Sk_Draw,
            ["BuffGuardianAlly"] = H_Sk_BuffGuardianAlly,
            ["BuffGuardianAllyHit"] = H_Sk_BuffGuardianAllyHit,
            ["BuffGuardianAllyBreakthrough"] = H_Sk_BuffGuardianAllyBreakthrough,
            ["RecoverUnitFromTrash"] = H_Sk_RecoverUnitFromTrash,
            ["RecoverItemFromTrash"] = H_Sk_RecoverItemFromTrash,
            ["BuffAllyUntilOpponentTurn"] = H_Sk_BuffAllyUntilOpponentTurn,
            ["TrashEnemyByTotalPower"] = H_Sk_TrashEnemyByTotalPower,
            ["MillRecoverFactionUnit"] = H_Sk_MillRecoverFactionUnit,
            ["MillAddDamageZoneDraw"] = H_Sk_MillAddDamageZoneDraw,
            ["DrawPerEquippedItem"] = H_Sk_DrawPerEquippedItem,
            ["TrashAllyDamageMixDraw"] = H_Sk_TrashAllyDamageMixDraw,
            ["TrashBothLowerCost"] = H_Sk_TrashBothLowerCost,
            ["TrashArmedAllyTrashEnemy"] = H_Sk_TrashArmedAllyTrashEnemy,
            ["MillActivateSkill"] = H_Sk_MillActivateSkill,
            ["EffectDamageDrawBuff"] = H_Sk_EffectDamageDrawBuff,
            ["CovenantZeroCostDraw"] = H_Sk_CovenantZeroCostDraw,
            ["DisableDefend"] = H_Sk_DisableDefend,
            ["DisableEnemyAttack"] = H_Sk_DisableEnemyAttack,
            ["GrantExtraAttackBuff"] = H_Sk_GrantExtraAttackBuff,
            ["RevealDeployIgnoreSize"] = H_Sk_RevealDeployIgnoreSize,
            ["BuffAllyRecoverSkill"] = H_Sk_BuffAllyRecoverSkill,
            ["TrashSkillZoneDamage"] = H_Sk_TrashSkillZoneDamage,
            ["SkillForceAttackFactionUnit"] = H_Sk_SkillForceAttackFactionUnit,
            ["DebuffEnemyTrashAlly"] = H_Sk_DebuffEnemyTrashAlly,
            ["RecoverByHandCount"] = H_Sk_RecoverByHandCount,
            ["RecoverByDamageCount"] = H_Sk_RecoverByDamageCount,
            ["GrantAttackerTrashEncounter"] = H_Sk_GrantAttackerTrashEncounter,
            ["DebuffAllEnemies"] = H_Sk_DebuffAllEnemies,
            ["DebuffAllEnemiesOpponentDraw"] = H_Sk_DebuffAllEnemiesOpponentDraw,
            ["DebuffEnemyUnits"] = H_Sk_DebuffEnemyUnits,
            ["BuffAttackerAllies"] = H_Sk_BuffAttackerAllies,
            ["GrantAttackerPenetration"] = H_Sk_GrantAttackerPenetration,
            ["RecoverAttackerFromTrash"] = H_Sk_RecoverAttackerFromTrash,
            ["DebuffEnemyIfTrashDraw"] = H_Sk_DebuffEnemyIfTrashDraw,
            ["BuffAllAlliesUntilOpponentTurn"] = H_Sk_BuffAllAlliesUntilOpponentTurn,
            ["FrontlineBuffAll"] = H_Sk_FrontlineBuffAll,
            ["TrashAllyDrawPerHit"] = H_Sk_TrashAllyDrawPerHit,
            ["TrashAllyByHitDiscard"] = H_Sk_TrashAllyByHitDiscard,
            ["DiscardTrashEnemySameCost"] = H_Sk_DiscardTrashEnemySameCost,
            ["BuffMutualDestructionAlly"] = H_Sk_BuffMutualDestructionAlly,
            ["DrawPerBaseAlly"] = H_Sk_DrawPerBaseAlly,
            ["BuffBaseAlliesHit"] = H_Sk_BuffBaseAlliesHit,
            ["SetBaseOneHit"] = H_Sk_BuffBaseAlliesHit,
            ["TrashEnemiesByCostLimit"] = H_Sk_TrashEnemiesByCostLimit,
            ["RecoverExitUnitsByCost"] = H_Sk_RecoverExitUnitsByCost,
            ["RevealSearchByCostAll"] = H_Sk_RevealSearchByCostAll,
            ["GrantAllAttackerPlunder"] = H_Sk_GrantAllAttackerPlunder,
            ["RecoverHighCostFromTrash"] = H_Sk_RecoverHighCostFromTrash,
            ["FrontlineLevelUp"] = H_Sk_FrontlineLevelUp,
            ["TrashItem"] = H_Sk_TrashItem,
            ["BuffBaseAlliesUntilOpponentTurn"] = H_Sk_BuffBaseAlliesUntilOpponentTurn,
            ["BuffDefenderAlly"] = H_Sk_BuffDefenderAlly,
            ["BuffGuardianAllyAndHitIfHand"] = H_Sk_BuffGuardianAllyAndHitIfHand,
            ["BuffNonGuardianByGuardianPower"] = H_Sk_BuffNonGuardianByGuardianPower,
            ["DefenderTwoDamageDisableAttack"] = H_Sk_DefenderTwoDamageDisableAttack,
            ["DiscardDamage"] = H_Sk_DiscardDamage,
            ["DiscardUnitDrawPerHit"] = H_Sk_DiscardUnitDrawPerHit,
            ["SearchUniqueItem"] = H_Sk_SearchUniqueItem,
            ["ReturnItemToDeckBottom"] = H_Sk_ReturnItemToDeckBottom,
            ["RecoverItemByCost"] = H_Sk_RecoverItemByCost,
            ["RevealSearchItemsToDeckBottom"] = H_Sk_RevealSearchItemsToDeckBottom,
            ["DamageZoneItemExchange"] = H_Sk_DamageZoneItemExchange,
            ["KeepHandsDrawToThree"] = H_Sk_KeepHandsDrawToThree,
            ["DebuffEnemyByHandDiff"] = H_Sk_DebuffEnemyByHandDiff,
            ["GrantAttackerDuelist"] = H_Sk_GrantAttackerDuelist,
            ["DebuffEnemyByDiscardedUnitPower"] = H_Sk_DebuffEnemyByDiscardedUnitPower,
            ["DrawPerEntryAllyLockOpponentEntry"] = H_Sk_DrawPerEntryAllyLockOpponentEntry,
            ["ConditionalDiscardDamage"] = H_Sk_ConditionalDiscardDamage,
            ["TrashLowCostAllyDraw"] = H_Sk_TrashLowCostAllyDraw,
            ["BuffExitAlliesPower"] = H_Sk_BuffExitAlliesPower,
            ["BuffHighCostAllyHit"] = H_Sk_BuffHighCostAllyHit,
            ["BuffLowCostAlliesHitDraw"] = H_Sk_BuffLowCostAlliesHitDraw,
            ["BuffLowCostAlliesPowerHit"] = H_Sk_BuffLowCostAlliesPowerHit,
            ["GrantPassiveDrawOnEquip"] = H_Sk_GrantPassiveDrawOnEquip,
            ["MillRecoverUnit"] = H_Sk_MillRecoverUnit,
            ["RecoverUnitByCostRange"] = H_Sk_RecoverUnitByCostRange,
            ["DisableEnemyUnitAttack"] = H_Sk_DisableEnemyUnitAttack,
            ["DefenderAllyHitDiscardReturnEncounter"] = H_Sk_DefenderAllyHitDiscardReturnEncounter,
            ["ShareItemEffectToAllAllies"] = H_Sk_ShareItemEffectToAllAllies,
            ["TrashAllyBuffOthersByPower"] = H_Sk_TrashAllyBuffOthersByPower,
            ["LowCostAllyTrashEncounterIfWinning"] = H_Sk_LowCostAllyTrashEncounterIfWinning,
        };
    }

    // 약점 간파, 미사일: 상대 유닛 파워 디버프
    private void H_Sk_DebuffEnemyUnit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0)
                {
                    AddTurnBoost(!isPlayer, targetLane, -value);
                    Debug.Log($"[스킬] {skill.CardName} → 레인{targetLane} 파워-{value}");
                }
                break;

            // 크레센도: 아군 유닛 파워 버프
        } while (false);
    }

    private void H_Sk_BuffAllyUnit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0)
                {
                    AddTurnBoost(isPlayer, targetLane, value);
                    Debug.Log($"[스킬] {skill.CardName} → 레인{targetLane} 파워+{value}");
                }
                break;

            // 오직 화력!: 모든 아군 유닛 파워 버프
        } while (false);
    }

    private void H_Sk_BuffAllAllies(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) != null)
                        AddTurnBoost(isPlayer, i, value);
                Debug.Log($"[스킬] {skill.CardName} → 전 유닛 파워+{value}");
                break;

            // 스승의 은혜: 리더 레벨+1
        } while (false);
    }

    private void H_Sk_LevelUp(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                GetOwner(isPlayer).LevelUpLeader();
                HUDView.NotifyStatusChanged();
                Debug.Log($"[스킬] {skill.CardName} → 리더 레벨 {GetOwner(isPlayer).LeaderLevel}");
                break;

            // 전력 보강: 트래시에서 N코스트 이하 유닛 회수
        } while (false);
    }

    private void H_Sk_RecoverFromTrash(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                var recoverOwner = GetOwner(isPlayer);
                var recoverCandidates = recoverOwner.TrashPile
                    .Where(c => c.Type == CardType.Unit && c.Cost <= value)
                    .ToList();
                if (recoverCandidates.Count == 0) break;
                if (isPlayer)
                {
                    HandPickPopup.Instance?.Show(
                        $"트래시에서 회수할 유닛을 선택하세요 ({value}코스트 이하)",
                        recoverCandidates,
                        picked =>
                        {
                            recoverOwner.RemoveFromTrash(picked);
                            recoverOwner.AddToHand(picked);
                            HandView.Instance?.RefreshHand();
                            Debug.Log($"[스킬] {skill.CardName} → {picked.CardName} 회수");
                        });
                }
                else
                {
                    var picked = recoverCandidates.OrderByDescending(c => c.AttackPower).First();
                    recoverOwner.RemoveFromTrash(picked);
                    recoverOwner.AddToHand(picked);
                    Debug.Log($"[스킬] {skill.CardName} → {picked.CardName} 회수 (AI)");
                }
                break;

            // 프라이즈: 덱 상위 N장 중 1장 패로
        } while (false);
    }

    private void H_Sk_TopDeckSearch(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                var owner = GetOwner(isPlayer);
                if (owner.DrawPile.Count > 0)
                {
                    int take = Mathf.Min(value, owner.DrawPile.Count);
                    var candidates = owner.PeekDeckTop(take);
                    owner.RemoveDeckTop(take);

                    if (isPlayer)
                    {
                        HandPickPopup.Instance?.Show(
                            $"패로 가져올 카드를 선택하세요 (덱 상위 {take}장)",
                            candidates,
                            chosen =>
                            {
                                owner.AddToHand(chosen);
                                candidates.Remove(chosen);
                                // 나머지는 덱 맨 아래로
                                owner.AddToDeckBottom(candidates);
                                HandView.Instance?.RefreshHand();
                                HUDView.NotifyStatusChanged();
                                Debug.Log($"[스킬] {skill.CardName} → {chosen.CardName} 패로 (선택)");
                            }
                        );
                    }
                    else
                    {
                        // AI는 첫 번째 카드 자동 선택
                        var chosen = candidates[0];
                        candidates.RemoveAt(0);
                        owner.AddToHand(chosen);
                        owner.AddToDeckBottom(candidates);
                        HUDView.NotifyStatusChanged();
                        Debug.Log($"[스킬] {skill.CardName} → {chosen.CardName} 패로 (AI 자동)");
                    }
                }
                break;

            // 기습: 내 패 1장 트래시 → 상대 패 1장 트래시
        } while (false);
    }

    private void H_Sk_ForceDiscardExchange(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                var me = GetOwner(isPlayer);
                if (me.Hand.Count > 0)
                {
                    // 발동자가 플레이어면 선택, AI면 랜덤
                    PickAndTrash(isPlayer, me, () => ApplyForceDiscard(!isPlayer));
                    Debug.Log($"[스킬] {skill.CardName} → 패 교환");
                }
                break;

            // 센스 쉐어링: 아군 유닛 트래시 → 드로우 N
        } while (false);
    }

    private void H_Sk_TrashAllySelfDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0)
                {
                    var unit = FieldManager.Instance.GetUnit(isPlayer, targetLane);
                    if (unit != null)
                    {
                        FieldManager.Instance.RemoveUnit(isPlayer, targetLane, willResolveExit: true);
                        GetOwner(isPlayer).AddToTrash(unit);
                        OnUnitTrashed(isPlayer, targetLane, unit);
                        GetOwner(isPlayer).DrawCard(value);
                        HandView.Instance?.RefreshHand();
                        HUDView.NotifyStatusChanged();
                        Debug.Log($"[스킬] {skill.CardName} → {unit.CardName} 트래시, 드로우{value}");
                    }
                }
                break;

            // 다 덤벼!: 아군+조우 유닛 트래시
        } while (false);
    }

    private void H_Sk_TrashBothUnits(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0)
                {
                    var ally  = FieldManager.Instance.GetUnit(isPlayer,  targetLane);
                    var enemy = FieldManager.Instance.GetUnit(!isPlayer, targetLane);
                    if (ally != null)
                    {
                        FieldManager.Instance.RemoveUnit(isPlayer, targetLane, willResolveExit: true);
                        GetOwner(isPlayer).AddToTrash(ally);
                        OnUnitTrashed(isPlayer, targetLane, ally);
                    }
                    if (enemy != null)
                    {
                        FieldManager.Instance.RemoveUnit(!isPlayer, targetLane, willResolveExit: true);
                        GetOwner(!isPlayer).AddToTrash(enemy);
                        OnUnitTrashed(!isPlayer, targetLane, enemy);
                    }
                    Debug.Log($"[스킬] {skill.CardName} → 레인{targetLane} 양쪽 트래시");
                }
                break;

            // 엑셀러레이션: 레인에서 파워 낮은 유닛 트래시
        } while (false);
    }

    private void H_Sk_TrashWeakestInLane(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0)
                {
                    var p = FieldManager.Instance.GetUnit(true,  targetLane);
                    var a = FieldManager.Instance.GetUnit(false, targetLane);
                    if (p != null && a != null)
                    {
                        int pPow = GetEffectivePower(true,  targetLane, p);
                        int aPow = GetEffectivePower(false, targetLane, a);
                        if (pPow < aPow)
                        {
                            FieldManager.Instance.RemoveUnit(true, targetLane, willResolveExit: true);
                            GetOwner(true).AddToTrash(p);
                            OnUnitTrashed(true, targetLane, p);
                        }
                        else if (aPow < pPow)
                        {
                            FieldManager.Instance.RemoveUnit(false, targetLane, willResolveExit: true);
                            GetOwner(false).AddToTrash(a);
                            OnUnitTrashed(false, targetLane, a);
                        }
                        else // 동점 — 양쪽 트래시
                        {
                            // 양패는 "동시에" 필드를 떠난다 — 두 유닛을 먼저 걷어낸 뒤 엑시트를 해결해야,
                            // 먼저 도는 엑시트가 아직 필드에 남아 있는 상대를 보고 오판하지 않는다.
                            // ※ 예전엔 여기서 OnUnitTrashed를 아예 안 불러 동점일 때만 엑시트가 통째로
                            //   무시됐다(승패가 갈리는 위 두 분기는 정상 발동).
                            FieldManager.Instance.RemoveUnit(true,  targetLane, willResolveExit: true);
                            FieldManager.Instance.RemoveUnit(false, targetLane, willResolveExit: true);
                            GetOwner(true).AddToTrash(p);
                            GetOwner(false).AddToTrash(a);
                            OnUnitTrashed(true,  targetLane, p);
                            OnUnitTrashed(false, targetLane, a);
                        }
                        Debug.Log($"[스킬] {skill.CardName} → 레인{targetLane} 약자 트래시");
                    }
                }
                break;

            // 흑화: 패 유닛 트래시 → 그 유닛보다 코스트 낮은 필드 유닛 트래시
        } while (false);
    }

    private void H_Sk_TrashFieldByHandCost(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                ApplyTrashFieldByHandCost(isPlayer);
                break;

            // 너싱: 드로우N
        } while (false);
    }

    private void H_Sk_Draw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                GetOwner(isPlayer).DrawCard(value);
                HandView.Instance?.RefreshHand();
                Debug.Log($"[스킬] {skill.CardName} → 드로우{value}");
                break;

            // 선배의 응원: 가디언 유닛 파워+N (상대 턴 끝날 때까지)
        } while (false);
    }

    private void H_Sk_BuffGuardianAlly(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                ApplyBuffGuardianAlly(isPlayer, skill, value, false);
                break;

            // 약오르죠?: 가디언 유닛 히트+N (상대 턴 끝날 때까지)
        } while (false);
    }

    private void H_Sk_BuffGuardianAllyHit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                ApplyBuffGuardianAlly(isPlayer, skill, value, true);
                break;

            // 실낙원: 가디언 유닛에게 이 턴 돌파 부여
        } while (false);
    }

    private void H_Sk_BuffGuardianAllyBreakthrough(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                ApplyBuffGuardianAllyBreakthrough(isPlayer, skill);
                break;

            // ST10 심연의 응시, ST11 월야호담: 트래시에서 유닛 회수 (코스트 무제한)
        } while (false);
    }

    private void H_Sk_RecoverUnitFromTrash(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Unit,
                    "트래시에서 회수할 유닛을 선택하세요");
                break;

            // ST05 현장검토: 트래시에서 아이템 회수
        } while (false);
    }

    private void H_Sk_RecoverItemFromTrash(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Item,
                    "트래시에서 회수할 아이템을 선택하세요");
                break;

            // ST11 테라 드레인: 아군 1장 상대 턴 끝까지 파워+N
        } while (false);
    }

    private void H_Sk_BuffAllyUntilOpponentTurn(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0)
                    EffectActions.Buff(isPlayer, targetLane, value, 0, BoostUntil.EndOfOpponentTurn);
                break;

            // ST06 은밀한 손길: 상대 유닛 파워 ≤ 아군 전체 파워 합이면 트래시
        } while (false);
    }

    private void H_Sk_TrashEnemyByTotalPower(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0)
                {
                    var target = FieldManager.Instance.GetUnit(!isPlayer, targetLane);
                    if (target == null) break;
                    int totalAllyPower = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        var ally = FieldManager.Instance.GetUnit(isPlayer, i);
                        if (ally != null) totalAllyPower += GetEffectivePower(isPlayer, i, ally);
                    }
                    if (GetEffectivePower(!isPlayer, targetLane, target) <= totalAllyPower)
                        EffectActions.TrashUnit(!isPlayer, targetLane);
                    else
                        Debug.Log($"[스킬] {skill.CardName} → 대상 파워가 아군 합({totalAllyPower})보다 높아 실패");
                }
                break;

            // ST07 망상 끝의 런웨이: 덱 위 4장 트래시 + 트래시에서 해당 소속 유닛 회수
            // 형식: "MillRecoverFactionUnit:소속"
        } while (false);
    }

    private void H_Sk_MillRecoverFactionUnit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                string faction = parts.Length > 1 ? parts[1] : "";
                EffectActions.Mill(isPlayer, 4);
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Unit && c.Faction != null && c.Faction.Contains(faction),
                    $"트래시에서 회수할 《{faction}》 유닛을 선택하세요");
                break;
            }

            // ST07 빛나는 자신감: 덱 위 N장 트래시 + 트래시 1장 대미지존에 + 대미지존 7장 이상이면 드로우1
        } while (false);
    }

    private void H_Sk_MillAddDamageZoneDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                EffectActions.Mill(isPlayer, value);
                var trashOwner = GetOwner(isPlayer);
                EffectActions.PickFromCards(isPlayer,
                    "대미지 존에 놓을 카드를 선택하세요",
                    new List<CardData>(trashOwner.TrashPile),
                    picked =>
                    {
                        EffectActions.MoveCard(isPlayer, picked, CardZone.Trash, CardZone.Damage);
                        if (trashOwner.DamageZone.Count >= 7)
                            EffectActions.Draw(isPlayer, 1);
                    });
                break;
            }

            // ST05 리타 부스트: 아군 유닛 1장의 N코스트 이상 장착 아이템 수만큼 드로우
        } while (false);
    }

    private void H_Sk_DrawPerEquippedItem(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0)
                {
                    int itemCount = FieldManager.Instance.GetEquippedItems(isPlayer, targetLane)
                        .Count(item => item.Cost >= value);
                    EffectActions.Draw(isPlayer, itemCount);
                    Debug.Log($"[스킬] {skill.CardName} → 아이템 {itemCount}개 → 드로우{itemCount}");
                }
                break;

            // ST09 운명의 인도: 아군 1장 트래시 → 상대 N대미지 + 믹스면 드로우1
        } while (false);
    }

    private void H_Sk_TrashAllyDamageMixDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0 && FieldManager.Instance.GetUnit(isPlayer, targetLane) != null)
                {
                    EffectActions.TrashUnit(isPlayer, targetLane);
                    NotifyEffectTrash(isPlayer, 1);
                    EffectActions.DamageOpponent(isPlayer, value);
                    if (HasMixCondition(isPlayer, skill.Attribute))
                        EffectActions.Draw(isPlayer, 1);
                }
                break;

            // ST09 과거는 중요하지 않아.: 아군 1장 + 그보다 코스트 낮은 상대 1장 선택해 함께 트래시
        } while (false);
    }

    private void H_Sk_TrashBothLowerCost(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => true,
                    "함께 트래시할 아군 유닛을 선택하세요",
                    allyLane =>
                    {
                        int allyCost = FieldManager.Instance.GetUnit(isPlayer, allyLane)?.Cost ?? 0;
                        EffectActions.SelectUnit(isPlayer, !isPlayer,
                            (l2, u2) => u2.Cost < allyCost,
                            $"함께 트래시할 {allyCost}코스트 미만 상대 유닛을 선택하세요",
                            enemyLane =>
                            {
                                EffectActions.TrashUnit(isPlayer, allyLane);
                                EffectActions.TrashUnit(!isPlayer, enemyLane);
                                NotifyEffectTrash(isPlayer, 2);
                            });
                    });
                break;

            // ST05 원 포 올: 아이템 N장 이상 장착한 아군 유닛 1장 트래시 → 상대 유닛 1장 트래시
        } while (false);
    }

    private void H_Sk_TrashArmedAllyTrashEnemy(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => FieldManager.Instance.GetEquippedItems(isPlayer, l).Count >= value,
                    $"아이템 {value}장 이상 장착한 아군 유닛을 선택하세요",
                    allyLane =>
                    {
                        EffectActions.TrashUnit(isPlayer, allyLane);
                        EffectActions.SelectUnit(isPlayer, !isPlayer,
                            (l2, u2) => true,
                            "트래시할 상대 유닛을 선택하세요",
                            enemyLane => EffectActions.TrashUnit(!isPlayer, enemyLane));
                    });
                break;

            // ST09 제대로 그려볼까?: 덱 위 N장 트래시, 그중 스킬 1장 효과 발동 가능
        } while (false);
    }

    private void H_Sk_MillActivateSkill(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var milled = EffectActions.Mill(isPlayer, value);
                var skillCandidates = milled.Where(c => c.Type == CardType.Skill).ToList();
                if (skillCandidates.Count == 0) break;
                // ※ 여기서 발동되는 스킬이 레인 타겟을 요구하는 타입이면 그 부분은 무시됨(targetLane=-1) — 무타겟 계열만 완전 동작
                EffectActions.PickFromCards(isPlayer, "발동할 스킬을 선택하세요 (선택 안 해도 됨)", skillCandidates,
                    picked => ExecuteSkillEffect(isPlayer, picked, -1));
                break;
            }

            // ST09 몽환 나비: 이번 턴 효과 대미지마다 드로우1
            // (원문의 "동명 카드 재발동 금지"는 미구현 — 중첩 발동 시 드로우 중복 없음)
        } while (false);
    }

    private void H_Sk_EffectDamageDrawBuff(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                _effectDamageDraw[Idx(isPlayer)] = true;
                Debug.Log($"[스킬] {skill.CardName} → 이번 턴 효과 대미지마다 드로우1");
                break;

            // ST06 성약 공통 꼬리(이계의 머시너리/은밀한 손길/사랑해 기억해 영원히.):
            // 이번 턴 《성약》 스킬 발동 금지 + 계승자 아군 1장 0코스트화 → 드로우1
        } while (false);
    }

    private void H_Sk_CovenantZeroCostDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                _covenantLocked[Idx(isPlayer)] = true;
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Faction != null && u.Faction.Contains("계승자"),
                    "0코스트로 만들 《계승자》 유닛을 선택하세요",
                    picked =>
                    {
                        _zeroCostGrants.Add(Key(isPlayer, picked));
                        EffectActions.Draw(isPlayer, 1);
                    });
                break;

            // ST11 블랙 오더: 상대 유닛 1장 이번 턴 방어 불가
        } while (false);
    }

    private void H_Sk_DisableDefend(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0)
                    DisableDefendLane(!isPlayer, targetLane);
                break;

            // BT06 킥킥, 타냐 등장!: 상대 유닛 1장 상대 턴 끝까지 공격 불가
        } while (false);
    }

    private void H_Sk_DisableEnemyAttack(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0)
                    DisableAttack(!isPlayer, targetLane, BoostUntil.EndOfOpponentTurn);
                break;

            // ST06 사랑해, 기억해, 영원히.: 조우 중인 아군 1장 — 어택 페이즈 추가 공격 1회 + 이번 턴 파워+N
        } while (false);
    }

    private void H_Sk_GrantExtraAttackBuff(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (targetLane >= 0 && FieldManager.Instance.GetUnit(isPlayer, targetLane) != null)
                {
                    AddExtraAttack(isPlayer, targetLane);
                    EffectActions.Buff(isPlayer, targetLane, value, 0, BoostUntil.EndOfTurn);
                }
                break;

            // ST08 같이 한잔하겠어?: 덱 위 N장 공개, 유닛 1장 사이즈 무시 배치, 나머지 트래시
        } while (false);
    }

    private void H_Sk_RevealDeployIgnoreSize(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                RevealAndDeployUnit(isPlayer, value);
                break;

            // ST08 전체 주목!: 아군 1장 파워+N(자신 턴 끝까지). 어태커 보유 시 트래시에서 2코 스킬 회수
        } while (false);
    }

    private void H_Sk_BuffAllyRecoverSkill(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => true,
                    $"파워+{value}를 줄 유닛을 선택하세요",
                    picked =>
                    {
                        var target = FieldManager.Instance.GetUnit(isPlayer, picked);
                        EffectActions.Buff(isPlayer, picked, value, 0, BoostUntil.EndOfTurn);
                        if (target != null && target.Keywords.Contains("어태커"))
                            EffectActions.RecoverFromTrash(isPlayer,
                                c => c.Type == CardType.Skill && c.Cost == 2 && !c.IsTrigger,
                                "트래시에서 회수할 2코스트 스킬을 선택하세요");
                    });
                break;

            // ST08 타락의 유열 속으로: 스킬존 전부 트래시, N장 이상이면 상대 1대미지. 이번 턴 이 스킬 재발동 금지
        } while (false);
    }

    private void H_Sk_TrashSkillZoneDamage(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var zoneOwner = GetOwner(isPlayer);
                int trashedCount = zoneOwner.SkillZone.Count;
                zoneOwner.AddToTrash(zoneOwner.SkillZone);
                zoneOwner.ClearSkillZone();
                if (trashedCount >= value)
                    EffectActions.DamageOpponent(isPlayer, 1);
                _selfLockedSkillsThisTurn.Add(SelfLockKey(isPlayer, skill.CardId));
                EffectActions.RefreshUI();
                Debug.Log($"[스킬] {skill.CardName} → 스킬존 {trashedCount}장 트래시" + (trashedCount >= value ? ", 상대 1대미지" : ""));
                break;
            }

            // ST07 사랑의 증거: 해당 소속 아군 1장 선택, 조우 유닛 있으면 그 유닛으로 즉시 공격
            // 형식: "SkillForceAttackFactionUnit:소속"
        } while (false);
    }

    private void H_Sk_SkillForceAttackFactionUnit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                string faction = parts.Length > 1 ? parts[1] : "";
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Faction != null && u.Faction.Contains(faction),
                    $"공격할 《{faction}》 유닛을 선택하세요",
                    picked =>
                    {
                        if (FieldManager.Instance.GetUnit(!isPlayer, picked) != null)
                            StartCoroutine(CombatManager.Instance.ForceAttackLane(isPlayer, picked));
                    });
                break;
            }

            // ST10 짐이 곧 베이룬이다(0코): 상대 유닛 1장 이번 턴 파워-N. 자신 유닛 1장 트래시할 수 있다(선택)
        } while (false);
    }

    private void H_Sk_DebuffEnemyTrashAlly(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, !isPlayer,
                    (l, u) => true,
                    $"파워-{value}로 만들 상대 유닛을 선택하세요",
                    enemyLane =>
                    {
                        AddTurnBoost(!isPlayer, enemyLane, -value);
                        Debug.Log($"[스킬] {skill.CardName} → 레인{enemyLane} 파워-{value}");
                    });
                // 자신 유닛 트래시는 순수 이득이 없는 선택지라 AI는 발동하지 않는다
                if (isPlayer)
                    EffectActions.SelectUnit(isPlayer, isPlayer,
                        (l, u) => true,
                        "트래시할 아군 유닛을 선택하세요 (취소 가능)",
                        allyLane => EffectActions.TrashUnit(isPlayer, allyLane));
                break;

            // ST10 다, 당당하게 노출을!: 코스트 합이 패 장수 이하가 되도록 트래시에서 이 카드 이외의 카드를 N장까지 회수
        } while (false);
    }

    private void H_Sk_RecoverByHandCount(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                RecoverByHandCountStep(isPlayer, skill, value, 0);
                break;

            // ST06 찬란한 영원: 코스트 합이 대미지 존 카드 수 이하가 되도록 트래시에서 이 카드 이외의 카드를 N장까지 회수
        } while (false);
    }

    private void H_Sk_RecoverByDamageCount(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                RecoverByDamageCountStep(isPlayer, skill, value, 0);
                break;

            // ST10 피의 기사: 이번 턴 이 스킬 재발동 금지 + 아군 1장 선택, 이번 턴 「어태커: 조우 유닛 트래시」 부여
        } while (false);
    }

    private void H_Sk_GrantAttackerTrashEncounter(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                _selfLockedSkillsThisTurn.Add(SelfLockKey(isPlayer, skill.CardId));
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => true,
                    "「어태커: 조우 유닛 트래시」를 부여할 유닛을 선택하세요",
                    picked =>
                    {
                        _grantedAttackerTrashEncounter.Add(Key(isPlayer, picked));
                        Debug.Log($"[스킬] {skill.CardName} → 레인{picked} 「어태커: 조우 유닛 트래시」 부여");
                    });
                break;

            // BT01 포메이션 F.F: 상대 유닛 전체 파워-N
        } while (false);
    }

    // BT06-081 허니문 패키지: 상대 전 유닛 파워-N(상대턴 끝까지) + 상대가 카드 1장 드로우
    private void H_Sk_DebuffAllEnemiesOpponentDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        for (int i = 0; i < 3; i++)
            if (FieldManager.Instance.GetUnit(!isPlayer, i) != null)
                EffectActions.Buff(!isPlayer, i, -value, 0, BoostUntil.EndOfOpponentTurn);
        EffectActions.Draw(!isPlayer, 1);
        Debug.Log($"[스킬] {skill.CardName} → 상대 전 유닛 파워-{value}(상대턴끝) + 상대 드로우1");
    }

    private void H_Sk_DebuffAllEnemies(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(!isPlayer, i) != null)
                        AddTurnBoost(!isPlayer, i, -value);
                Debug.Log($"[스킬] {skill.CardName} → 상대 전 유닛 파워-{value}");
                break;

            // BT01 압도: 상대 유닛 M장 각각 파워-N ("DebuffEnemyUnits:N:M")
        } while (false);
    }

    private void H_Sk_DebuffEnemyUnits(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                int debuffVal = value;
                int targets  = parts.Length > 2 && int.TryParse(parts[2], out int t) ? t : 1;
                DebuffEnemyUnitsStep(isPlayer, skill, debuffVal, targets);
                break;
            }

            // BT01 치얼업 투게더: 어태커 아군 전체 파워+N
        } while (false);
    }

    private void H_Sk_BuffAttackerAllies(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                for (int i = 0; i < 3; i++)
                {
                    var u = FieldManager.Instance.GetUnit(isPlayer, i);
                    if (u != null && u.Keywords.Contains("어태커"))
                        AddTurnBoost(isPlayer, i, value);
                }
                Debug.Log($"[스킬] {skill.CardName} → 어태커 아군 파워+{value}");
                break;

            // BT01 와일드 투스: 아군 1장에 「어태커 관통[N]」 부여
        } while (false);
    }

    private void H_Sk_GrantAttackerPenetration(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Keywords.Contains("어태커"),
                    $"관통[{value}]를 부여할 어태커 유닛을 선택하세요",
                    picked =>
                    {
                        string k = Key(isPlayer, picked);
                        _grantedPenetration[k] = _grantedPenetration.GetValueOrDefault(k) + value;
                        Debug.Log($"[스킬] {skill.CardName} → 레인{picked} 관통[{value}] 부여");
                    });
                break;

            // BT01 아군 합류: 트래시에서 어태커 유닛 회수
        } while (false);
    }

    private void H_Sk_RecoverAttackerFromTrash(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Unit && c.Keywords.Contains("어태커"),
                    "트래시에서 회수할 어태커 유닛을 선택하세요");
                break;

            // BT01 피날레: 상대 유닛 파워-N + 패 1장 트래시하면 드로우M ("DebuffEnemyIfTrashDraw:N:M")
        } while (false);
    }

    private void H_Sk_DebuffEnemyIfTrashDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                int debuff = value;
                int draw   = parts.Length > 2 && int.TryParse(parts[2], out int d) ? d : 1;
                if (targetLane >= 0) AddTurnBoost(!isPlayer, targetLane, -debuff);
                EffectActions.DiscardHandOptional(isPlayer, 1,
                    discarded => { if (discarded > 0) EffectActions.Draw(isPlayer, draw); });
                break;
            }

            // BT01 파란 나비의 꿈: 아군 전체 상대 턴 끝까지 파워+N
        } while (false);
    }

    private void H_Sk_BuffAllAlliesUntilOpponentTurn(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) != null)
                        EffectActions.Buff(isPlayer, i, value, 0, BoostUntil.EndOfOpponentTurn);
                Debug.Log($"[스킬] {skill.CardName} → 아군 전체 파워+{value} (상대 턴까지)");
                break;

            // BT01 설온제: 조우 중인(전선) 아군 전체 파워+N
        } while (false);
    }

    private void H_Sk_FrontlineBuffAll(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                for (int i = 0; i < 3; i++)
                {
                    if (FieldManager.Instance.GetUnit(isPlayer, i) != null &&
                        FieldManager.Instance.GetUnit(!isPlayer, i) != null)
                        AddTurnBoost(isPlayer, i, value);
                }
                Debug.Log($"[스킬] {skill.CardName} → 전선 아군 파워+{value}");
                break;

            // BT01 시크릿 코드: 아군 1장 트래시 → 그 히트만큼 드로우
        } while (false);
    }

    private void H_Sk_TrashAllyDrawPerHit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => true,
                    "트래시할 아군 유닛을 선택하세요",
                    allyLane =>
                    {
                        var unit2 = FieldManager.Instance.GetUnit(isPlayer, allyLane);
                        int hit2  = unit2 != null ? GetEffectiveHit(isPlayer, allyLane, unit2) : 0;
                        EffectActions.TrashUnit(isPlayer, allyLane);
                        if (hit2 > 0) EffectActions.Draw(isPlayer, hit2);
                    });
                break;

            // BT01 여긴 내가 맡는다!: 아군 1장 트래시 → 상대가 그 히트만큼 패 강제 트래시
        } while (false);
    }

    private void H_Sk_TrashAllyByHitDiscard(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => true,
                    "트래시할 아군 유닛을 선택하세요",
                    allyLane =>
                    {
                        var unit3 = FieldManager.Instance.GetUnit(isPlayer, allyLane);
                        int hit3  = unit3 != null ? GetEffectiveHit(isPlayer, allyLane, unit3) : 0;
                        EffectActions.TrashUnit(isPlayer, allyLane);
                        for (int i = 0; i < hit3; i++) ApplyForceDiscard(!isPlayer);
                    });
                break;

            // BT01 버닝 샷: 패 1장 트래시 → 같은 코스트 상대 유닛 트래시
        } while (false);
    }

    private void H_Sk_DiscardTrashEnemySameCost(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
                {
                    if (discarded < 1) return;
                    // AI/플레이어 모두 가장 최근 트래시된 카드 코스트 추적 (트래시파일 마지막 카드)
                    var trashPile = GetOwner(isPlayer).TrashPile;
                    if (trashPile.Count == 0) return;
                    int cost = trashPile[trashPile.Count - 1].Cost;
                    EffectActions.SelectUnit(isPlayer, !isPlayer,
                        (l, u) => u.Cost == cost,
                        $"코스트 {cost}인 상대 유닛을 선택하세요",
                        enemyLane => EffectActions.TrashUnit(!isPlayer, enemyLane));
                });
                break;

            // BT01 마계흑룡파: 공멸 가진 아군 1장 파워+N
        } while (false);
    }

    private void H_Sk_BuffMutualDestructionAlly(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.EffectTypes.Contains("ExitMutualDestruction"),
                    $"파워+{value}를 줄 공멸 유닛을 선택하세요",
                    picked => EffectActions.Buff(isPlayer, picked, value, 0, BoostUntil.EndOfTurn));
                break;

            // BT01 디저트 타임: 베이스 아군 수만큼 드로우
        } while (false);
    }

    private void H_Sk_DrawPerBaseAlly(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                int baseCnt = 0;
                for (int i = 0; i < 3; i++)
                {
                    var u = FieldManager.Instance.GetUnit(isPlayer, i);
                    if (u?.Faction != null && u.Faction.StartsWith("베이스")) baseCnt++;
                }
                if (baseCnt > 0) EffectActions.Draw(isPlayer, baseCnt);
                Debug.Log($"[스킬] {skill.CardName} → 베이스 아군 {baseCnt}장 → 드로우{baseCnt}");
                break;
            }

            // BT01 검신합일: 베이스 아군 전체 히트+N
            // BT01 순백의 의지 (SetBaseOneHit): 베이스 아군 히트+N으로 근사
        } while (false);
    }

    private void H_Sk_BuffBaseAlliesHit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                for (int i = 0; i < 3; i++)
                {
                    var u = FieldManager.Instance.GetUnit(isPlayer, i);
                    if (u?.Faction != null && u.Faction.StartsWith("베이스"))
                        AddTurnHitBoost(isPlayer, i, value);
                }
                Debug.Log($"[스킬] {skill.CardName} → 베이스 아군 히트+{value}");
                break;

            // BT01 뷰티 풀 샷: 코스트 합계 N 이하가 되도록 상대 유닛 최대 M장 트래시 ("TrashEnemiesByCostLimit:N:M")
        } while (false);
    }

    private void H_Sk_TrashEnemiesByCostLimit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                int costLimit = value;
                int maxTrash  = parts.Length > 2 && int.TryParse(parts[2], out int m) ? m : 1;
                TrashEnemiesByCostLimitStep(isPlayer, skill, costLimit, maxTrash, 0);
                break;
            }

            // BT01 EX 매거진: 트래시에서 엑시트 유닛 코스트합 N 이하로 최대 M장 회수 ("RecoverExitUnitsByCost:N:M")
        } while (false);
    }

    private void H_Sk_RecoverExitUnitsByCost(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                int budget2  = value;
                int maxPicks = parts.Length > 2 && int.TryParse(parts[2], out int m) ? m : 1;
                RecoverExitUnitsByCostStep(isPlayer, skill, budget2, maxPicks, 0);
                break;
            }

            // BT01 VIP 기프트: 덱 위 N장 공개, M코스트 이하 카드 전부 패, 나머지 덱 복귀+셔플 ("RevealSearchByCostAll:N:M")
        } while (false);
    }

    private void H_Sk_RevealSearchByCostAll(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                int reveal2  = value;
                int costMax  = parts.Length > 2 && int.TryParse(parts[2], out int m) ? m : 3;
                var ownerR   = GetOwner(isPlayer);
                var revealed = new List<CardData>();
                for (int i = 0; i < reveal2 && ownerR.DrawPile.Count > 0; i++)
                {
                    revealed.Add(ownerR.DrawPile[0]);
                    ownerR.RemoveFromDeckAt(0);
                }
                var kept    = revealed.Where(c => c.Cost <= costMax).ToList();
                var rest    = revealed.Where(c => c.Cost > costMax).ToList();
                foreach (var c in kept)  ownerR.AddToHand(c);
                foreach (var c in rest)  ownerR.AddToDeckBottom(c);
                ownerR.ShuffleDeck();
                HandView.Instance?.RefreshHand();
                Debug.Log($"[스킬] {skill.CardName} → 공개{reveal2}장, 패로:{kept.Count}장, 복귀:{rest.Count}장");
                break;
            }

            // BT02 글레링 아이즈: 이 턴 아군 전체 「어태커 약탈[N]」 획득
        } while (false);
    }

    private void H_Sk_GrantAllAttackerPlunder(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                for (int i2 = 0; i2 < 3; i2++)
                {
                    if (FieldManager.Instance.GetUnit(isPlayer, i2) != null)
                    {
                        var k2 = Key(isPlayer, i2);
                        _grantedAttackerPlunder[k2] = value;
                    }
                }
                Debug.Log($"[스킬] {skill.CardName} → 아군 전체 약탈[{value}] 부여");
                break;

            // BT02 더 게임 마스터: 트래시에서 N코스트 이상 유닛 1장 패로
        } while (false);
    }

    private void H_Sk_RecoverHighCostFromTrash(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Unit && c.Cost >= value,
                    $"트래시에서 회수할 {value}코스트 이상 유닛을 선택하세요");
                break;

            // BT02 딸기향 이끌림: 전 레인에 유닛이 있으면 리더 레벨+1
        } while (false);
    }

    private void H_Sk_FrontlineLevelUp(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                if (Enumerable.Range(0, 3).All(i => FieldManager.Instance.GetUnit(isPlayer, i) != null))
                    EffectActions.LevelUp(isPlayer);
                break;

            // BT02 위대한 도둑: 아이템 1장 트래시 (자신 or 상대)
        } while (false);
    }

    private void H_Sk_TrashItem(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer, // 아이템 장착 유닛 레인 선택
                    (l, u) => FieldManager.Instance.GetEquippedItems(isPlayer, l).Count > 0 ||
                              FieldManager.Instance.GetEquippedItems(!isPlayer, l).Count > 0,
                    "트래시할 아이템이 장착된 레인을 선택하세요",
                    picked =>
                    {
                        // 플레이어: 자신/상대 중 선택. 간소화: 아이템 있는 첫 레인
                        bool hasMine  = FieldManager.Instance.GetEquippedItems(isPlayer, picked).Count > 0;
                        bool hasEnemy = FieldManager.Instance.GetEquippedItems(!isPlayer, picked).Count > 0;
                        if (!hasMine && !hasEnemy) return;
                        bool trashEnemy = hasEnemy && (!hasMine || !isPlayer); // AI는 상대 아이템 트래시 우선
                        bool owner2 = trashEnemy ? !isPlayer : isPlayer;
                        var items2 = FieldManager.Instance.GetEquippedItems(owner2, picked);
                        if (items2.Count == 0) return;
                        var item2 = items2[0];
                        FieldManager.Instance.UnequipItem(owner2, picked, item2);
                        GetOwner(owner2).AddToTrash(item2);
                        Debug.Log($"[스킬] {skill.CardName} → {item2.CardName} 트래시");
                    });
                break;

            // BT02 양호 선생님: 베이스 아군 전체 상대 턴 끝까지 파워+N
        } while (false);
    }

    private void H_Sk_BuffBaseAlliesUntilOpponentTurn(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                for (int i2 = 0; i2 < 3; i2++)
                {
                    var u2 = FieldManager.Instance.GetUnit(isPlayer, i2);
                    if (u2?.Faction != null && u2.Faction.StartsWith("베이스"))
                        EffectActions.Buff(isPlayer, i2, value, 0, BoostUntil.EndOfOpponentTurn);
                }
                break;

            // BT02 폴리스 라인: 디펜더 아군 1장 이번 턴 파워+N
        } while (false);
    }

    private void H_Sk_BuffDefenderAlly(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Keywords.Contains("디펜더"),
                    $"파워+{value}를 줄 디펜더 유닛을 선택하세요",
                    picked => EffectActions.Buff(isPlayer, picked, value, 0, BoostUntil.EndOfTurn));
                break;

            // BT02 당근과 토끼 파티: 가디언 1장 파워+N [+ 패≥M이면 히트+1] ("BuffGuardianAllyAndHitIfHand:N:M:1")
        } while (false);
    }

    private void H_Sk_BuffGuardianAllyAndHitIfHand(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                int buffPow = value;
                int handReq = parts.Length > 2 && int.TryParse(parts[2], out int h) ? h : 5;
                bool addHit = parts.Length > 3 && parts[3] == "1";
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Keywords.Contains("가디언"),
                    "파워를 올릴 가디언 유닛을 선택하세요",
                    picked =>
                    {
                        EffectActions.Buff(isPlayer, picked, buffPow, 0, BoostUntil.EndOfTurn);
                        if (addHit && GetOwner(isPlayer).Hand.Count >= handReq)
                            AddTurnHitBoost(isPlayer, picked, 1);
                    });
                break;
            }

            // BT02 우리는 친구!: 가디언 1장 + 비가디언 1장, 비가디언 파워를 가디언 현재 파워만큼 증가
        } while (false);
    }

    private void H_Sk_BuffNonGuardianByGuardianPower(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Keywords.Contains("가디언"),
                    "기준이 될 가디언 유닛을 선택하세요",
                    guardianLane =>
                    {
                        var gUnit = FieldManager.Instance.GetUnit(isPlayer, guardianLane);
                        int gPower = gUnit != null ? GetEffectivePower(isPlayer, guardianLane, gUnit) : 0;
                        EffectActions.SelectUnit(isPlayer, isPlayer,
                            (l, u) => !u.Keywords.Contains("가디언"),
                            "파워를 올릴 비가디언 유닛을 선택하세요",
                            targetLane2 => EffectActions.Buff(isPlayer, targetLane2, gPower, 0, BoostUntil.EndOfTurn));
                    });
                break;

            // BT02 이지스 캐논 견제 사격: 디펜더 아군 2장 선택 → 상대 1대미지, 선택 유닛 이번 턴 공격 불가
        } while (false);
    }

    private void H_Sk_DefenderTwoDamageDisableAttack(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var defLanes = Enumerable.Range(0, 3)
                    .Where(i => FieldManager.Instance.GetUnit(isPlayer, i)?.Keywords.Contains("디펜더") == true)
                    .ToList();
                if (defLanes.Count < 2) break;
                // 첫 번째 선택
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Keywords.Contains("디펜더"),
                    "공격 불가로 만들 디펜더 유닛 1번을 선택하세요",
                    first =>
                    {
                        DisableAttack(isPlayer, first, BoostUntil.EndOfTurn);
                        EffectActions.SelectUnit(isPlayer, isPlayer,
                            (l, u) => u.Keywords.Contains("디펜더") && l != first,
                            "공격 불가로 만들 디펜더 유닛 2번을 선택하세요",
                            second =>
                            {
                                DisableAttack(isPlayer, second, BoostUntil.EndOfTurn);
                                EffectActions.DamageOpponent(isPlayer, 1);
                                Debug.Log($"[스킬] {skill.CardName} → 레인{first},{second} 공격 불가, 상대 1대미지");
                            });
                    });
                break;
            }

            // BT02 해적의 안목: 패 N장 트래시 → 상대 M대미지 ("DiscardDamage:N:M")
        } while (false);
    }

    private void H_Sk_DiscardDamage(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                int discardN = value;
                int damageM  = parts.Length > 2 && int.TryParse(parts[2], out int d) ? d : 1;
                EffectActions.DiscardHand(isPlayer, discardN, _ => EffectActions.DamageOpponent(isPlayer, damageM));
                break;
            }

            // BT02 신세계: 패에서 유닛 1장 트래시 → 그 히트만큼 드로우
        } while (false);
    }

    private void H_Sk_DiscardUnitDrawPerHit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var handUnits = GetOwner(isPlayer).Hand.Where(c => c.Type == CardType.Unit).ToList();
                if (handUnits.Count == 0) break;
                EffectActions.PickFromCards(isPlayer, "트래시할 유닛을 선택하세요", handUnits,
                    picked =>
                    {
                        int hitVal = picked.Hit;
                        GetOwner(isPlayer).RemoveFromHand(picked);
                        GetOwner(isPlayer).AddToTrash(picked);
                        HandView.Instance?.RefreshHand();
                        if (hitVal > 0) EffectActions.Draw(isPlayer, hitVal);
                        Debug.Log($"[스킬] {skill.CardName} → {picked.CardName} 트래시, 드로우{hitVal}");
                    },
                    aiPick: list => list.OrderByDescending(c => c.Hit).First());
                break;
            }

            // BT02 식사 시간입니다, 주인님: 덱에서 유니크 아이템 1장 서치 + 셔플
        } while (false);
    }

    private void H_Sk_SearchUniqueItem(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var deck2 = GetOwner(isPlayer).DrawPile;
                var uniqueItems = deck2.Where(c => c.Type == CardType.Item && c.Keywords.Contains("유니크")).ToList();
                if (uniqueItems.Count == 0) break;
                var picked2 = isPlayer ? uniqueItems[0] : uniqueItems[0]; // 항상 첫 번째 (유니크 아이템은 1장만)
                EffectActions.PickFromCards(isPlayer, "패로 가져올 유니크 아이템을 선택하세요", uniqueItems,
                    p2 =>
                    {
                        GetOwner(isPlayer).RemoveFromDeck(p2);
                        GetOwner(isPlayer).AddToHand(p2);
                        HandView.Instance?.RefreshHand();
                        GetOwner(isPlayer).ShuffleDeck();
                        Debug.Log($"[스킬] {skill.CardName} → {p2.CardName} 패로");
                    },
                    aiPick: list => list.First());
                break;
            }

            // BT02 비기너즈 베네핏: 필드 아이템 1장 → 주인 덱 맨 아래
        } while (false);
    }

    private void H_Sk_ReturnItemToDeckBottom(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer, // 레인 선택
                    (l, u) => FieldManager.Instance.GetEquippedItems(isPlayer, l).Count > 0 ||
                              FieldManager.Instance.GetEquippedItems(!isPlayer, l).Count > 0,
                    "아이템이 장착된 레인을 선택하세요",
                    picked =>
                    {
                        bool hasMine2  = FieldManager.Instance.GetEquippedItems(isPlayer, picked).Count > 0;
                        bool itemOwner = hasMine2 ? isPlayer : !isPlayer;
                        var items3 = FieldManager.Instance.GetEquippedItems(itemOwner, picked);
                        if (items3.Count == 0) return;
                        var item3 = items3[0];
                        FieldManager.Instance.UnequipItem(itemOwner, picked, item3);
                        GetOwner(itemOwner).AddToDeckBottom(item3);
                        Debug.Log($"[스킬] {skill.CardName} → {item3.CardName} 덱 맨 아래");
                    });
                break;

            // BT02 로얄 에타이어: 트래시에서 N코스트 이하 아이템 1장 패로
        } while (false);
    }

    private void H_Sk_RecoverItemByCost(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Item && c.Cost <= value,
                    $"트래시에서 회수할 {value}코스트 이하 아이템을 선택하세요");
                break;

            // BT02 고양이 보은: 덱 위 N장 공개, 아이템 최대 M장 패, 나머지 덱 맨 아래 ("RevealSearchItemsToDeckBottom:N:M")
        } while (false);
    }

    private void H_Sk_RevealSearchItemsToDeckBottom(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts = et.Split(':');
                int rev2 = value;
                int maxI = parts.Length > 2 && int.TryParse(parts[2], out int m2) ? m2 : 1;
                RevealSearchItemsToDeckBottomStep(isPlayer, skill, rev2, maxI);
                break;
            }

            // BT02 용기있는 시선: 대미지존 아이템 1장 패로 → 패 1장 대미지존으로
        } while (false);
    }

    private void H_Sk_DamageZoneItemExchange(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var dmgItems = GetOwner(isPlayer).DamageZone.Where(c => c.Type == CardType.Item).ToList();
                if (dmgItems.Count == 0) break;
                EffectActions.PickFromCards(isPlayer, "패로 가져올 대미지존 아이템을 선택하세요", dmgItems,
                    pickedItem =>
                    {
                        GetOwner(isPlayer).RemoveFromDamageZone(pickedItem);
                        GetOwner(isPlayer).AddToHand(pickedItem);
                        HandView.Instance?.RefreshHand();
                        HUDView.NotifyStatusChanged();
                        Debug.Log($"[스킬] {skill.CardName} → {pickedItem.CardName} 패로");
                        // 패 1장 대미지존으로
                        EffectActions.PickFromCards(isPlayer, "대미지존에 놓을 패를 선택하세요",
                            new List<CardData>(GetOwner(isPlayer).Hand),
                            toPlace =>
                            {
                                GetOwner(isPlayer).RemoveFromHand(toPlace);
                                GetOwner(isPlayer).AddToDamageZone(toPlace);
                                HandView.Instance?.RefreshHand();
                                HUDView.NotifyStatusChanged();
                                Debug.Log($"[스킬] {skill.CardName} → {toPlace.CardName} 대미지존으로");
                            },
                            aiPick: list => list.OrderBy(c => c.Cost).First());
                    },
                    aiPick: list => list.OrderByDescending(c => c.Cost).First());
                break;
            }

            // BT03 꽃구경: 패 N장 드로우 → 합계 N장이 되도록 맞춤 (패<N이면 드로우, 패>N이면 트래시)
        } while (false);
    }

    private void H_Sk_KeepHandsDrawToThree(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var owner3k = GetOwner(isPlayer);
                int target3 = value;
                int diff3k = target3 - owner3k.Hand.Count;
                if (diff3k > 0) EffectActions.Draw(isPlayer, diff3k);
                else if (diff3k < 0)
                    for (int i = 0; i < -diff3k; i++) ApplyForceDiscard(isPlayer);
                Debug.Log($"[스킬] {skill.CardName} → 패 {target3}장 조정 (차이{diff3k})");
                break;
            }

            // BT03 뉴 플레이버: 상대 유닛 파워-(양쪽 패 장수 차이 × N)
        } while (false);
    }

    private void H_Sk_DebuffEnemyByHandDiff(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                int handDiff3 = Mathf.Abs(GetOwner(isPlayer).Hand.Count - GetOwner(!isPlayer).Hand.Count);
                if (handDiff3 <= 0) break;
                EffectActions.SelectUnit(isPlayer, !isPlayer,
                    (l, u) => true,
                    $"파워-{handDiff3 * value}를 줄 상대 유닛을 선택하세요",
                    picked => AddTurnBoost(!isPlayer, picked, -handDiff3 * value));
                break;
            }

            // BT03 폭발은 예술: N코스트 이하 아군 1장에 어태커:듀얼리스트 부여
        } while (false);
    }

    private void H_Sk_GrantAttackerDuelist(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Cost <= value,
                    $"{value}코스트 이하 아군에 듀얼리스트 부여",
                    picked => _grantedDuelistThisTurn.Add(Key(isPlayer, picked)));
                break;

            // BT03 계승되는 힘: 이 턴 트래시한 유닛 코스트만큼 상대 유닛 파워감소
        } while (false);
    }

    private void H_Sk_DebuffEnemyByDiscardedUnitPower(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                // _unitsTrashedThisTurn 카운트 기반이 아니라 실제 트래시된 유닛 코스트 합산 필요
                // 근사: 트래시된 유닛의 평균 코스트(구조상 코스트 추적이 없어 턴 트래시 수 × 3으로 근사)
                int trashCostSum = _unitsTrashedThisTurn[Idx(isPlayer)] * 3;
                if (trashCostSum <= 0) break;
                EffectActions.SelectUnit(isPlayer, !isPlayer,
                    (l, u) => true,
                    $"파워-{trashCostSum}을 줄 상대 유닛을 선택하세요",
                    picked => AddTurnBoost(!isPlayer, picked, -trashCostSum));
                break;
            }

            // BT03 폴크방: 이 턴 아군 엔트리가 발동되면 상대 엔트리 잠금 + 드로우1
        } while (false);
    }

    private void H_Sk_DrawPerEntryAllyLockOpponentEntry(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                _entryLocked[Idx(!isPlayer)] = true;
                EffectActions.Draw(isPlayer, 1);
                Debug.Log($"[스킬] {skill.CardName} → 상대 엔트리 잠금 + 드로우1");
                break;

            // BT03 블루워터: 패 N장 이상이면 M대미지
        } while (false);
    }

    private void H_Sk_ConditionalDiscardDamage(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts3c = et.Split(':');
                int handReq3 = value;
                int dmg3 = parts3c.Length > 2 && int.TryParse(parts3c[2], out int d3c) ? d3c : 1;
                if (GetOwner(isPlayer).Hand.Count >= handReq3)
                {
                    GetOwner(!isPlayer).TakeDamage(dmg3);
                    Debug.Log($"[스킬] {skill.CardName} → 패{GetOwner(isPlayer).Hand.Count}장 ≥ {handReq3} → {dmg3}대미지");
                }
                break;
            }

            // BT03 킬 더 로드: N코스트 이하 아군 1장 트래시 → 드로우M
        } while (false);
    }

    private void H_Sk_TrashLowCostAllyDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts3d = et.Split(':');
                int maxCost3 = value;
                int draw3d = parts3d.Length > 2 && int.TryParse(parts3d[2], out int d3d) ? d3d : 1;
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Cost <= maxCost3,
                    $"{maxCost3}코스트 이하 아군 트래시 → 드로우{draw3d}",
                    picked => { EffectActions.TrashUnit(isPlayer, picked); EffectActions.Draw(isPlayer, draw3d); });
                break;
            }

            // BT03 M.M.R. 연구소: 엑시트 가진 아군 전체 파워+N
        } while (false);
    }

    private void H_Sk_BuffExitAlliesPower(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                for (int i3 = 0; i3 < 3; i3++)
                {
                    var u3 = FieldManager.Instance.GetUnit(isPlayer, i3);
                    if (u3 != null && u3.EffectTypes.Any(e => e.StartsWith("Exit")))
                        AddTurnBoost(isPlayer, i3, value);
                }
                Debug.Log($"[스킬] {skill.CardName} → 엑시트 아군 파워+{value}");
                break;

            // BT03 브랜드 뉴 이어: N코스트 이상 아군 1장 이번 턴 히트+M
        } while (false);
    }

    private void H_Sk_BuffHighCostAllyHit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts3e = et.Split(':');
                int costReq3 = value;
                int hitVal3 = parts3e.Length > 2 && int.TryParse(parts3e[2], out int h3) ? h3 : 1;
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Cost >= costReq3,
                    $"{costReq3}코스트 이상 아군에 히트+{hitVal3}",
                    picked => AddTurnHitBoost(isPlayer, picked, hitVal3));
                break;
            }

            // BT03 최적의 경로: N코스트 이하 아군 전체 히트+M → 드로우M
        } while (false);
    }

    private void H_Sk_BuffLowCostAlliesHitDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts3f = et.Split(':');
                int costCap3 = value;
                int hitV3 = parts3f.Length > 2 && int.TryParse(parts3f[2], out int h3f) ? h3f : 1;
                int drawV3 = parts3f.Length > 3 && int.TryParse(parts3f[3], out int dv3) ? dv3 : 1;
                for (int i3 = 0; i3 < 3; i3++)
                {
                    var u3 = FieldManager.Instance.GetUnit(isPlayer, i3);
                    if (u3 != null && u3.Cost <= costCap3) AddTurnHitBoost(isPlayer, i3, hitV3);
                }
                EffectActions.Draw(isPlayer, drawV3);
                break;
            }

            // BT03 시크릿 가든: N코스트 이하 아군 전체 파워+P, 히트+H
        } while (false);
    }

    private void H_Sk_BuffLowCostAlliesPowerHit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts3g = et.Split(':');
                int costCap3g = value;
                int powerV3 = parts3g.Length > 2 && int.TryParse(parts3g[2], out int pv3) ? pv3 : 0;
                int hitV3g   = parts3g.Length > 3 && int.TryParse(parts3g[3], out int hv3) ? hv3 : 0;
                for (int i3 = 0; i3 < 3; i3++)
                {
                    var u3 = FieldManager.Instance.GetUnit(isPlayer, i3);
                    if (u3 != null && u3.Cost <= costCap3g)
                    {
                        if (powerV3 > 0) AddTurnBoost(isPlayer, i3, powerV3);
                        if (hitV3g > 0) AddTurnHitBoost(isPlayer, i3, hitV3g);
                    }
                }
                break;
            }

            // BT03 죽으면 안 돼: 아군 전체에 이번 턴 아이템 장착 시 드로우N 부여
        } while (false);
    }

    private void H_Sk_GrantPassiveDrawOnEquip(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                for (int i3 = 0; i3 < 3; i3++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i3) != null)
                        _grantedPassiveDrawOnEquip.Add(Key(isPlayer, i3));
                Debug.Log($"[스킬] {skill.CardName} → 아군 전체 장착 시 드로우{value} 부여");
                break;

            // BT03 거대한 집게: 덱 위 N장 트래시, 유닛이면 패로 회수 선택
        } while (false);
    }

    private void H_Sk_MillRecoverUnit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var owner3m = GetOwner(isPlayer);
                var milled3 = new List<CardData>();
                for (int i = 0; i < value && owner3m.DrawPile.Count > 0; i++)
                {
                    var top = owner3m.DrawPile[owner3m.DrawPile.Count - 1];
                    owner3m.RemoveFromDeckAt(owner3m.DrawPile.Count - 1);
                    owner3m.AddToTrash(top);
                    milled3.Add(top);
                }
                var units3m = milled3.Where(c => c.Type == CardType.Unit).ToList();
                if (units3m.Count > 0)
                    EffectActions.PickFromCards(isPlayer, "패로 회수할 유닛을 선택하세요", units3m,
                        picked => { owner3m.RemoveFromTrash(picked); owner3m.AddToHand(picked); HandView.Instance?.RefreshHand(); },
                        aiPick: l => l.OrderByDescending(c => c.Cost).First(),
                        onCancel: () => { });
                break;
            }

            // BT03 궤도 엘리베이터: 트래시에서 N~M코스트 유닛 1장 패로
        } while (false);
    }

    private void H_Sk_RecoverUnitByCostRange(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                var parts3r = et.Split(':');
                int minCost3 = value;
                int maxCost3r = parts3r.Length > 2 && int.TryParse(parts3r[2], out int mc3) ? mc3 : 99;
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Unit && c.Cost >= minCost3 && c.Cost <= maxCost3r,
                    $"트래시에서 {minCost3}~{maxCost3r}코스트 유닛을 선택하세요");
                break;
            }

            // BT03 다시 하면 돼!: 상대 유닛 1장 이번 턴 공격 불가
        } while (false);
    }

    private void H_Sk_DisableEnemyUnitAttack(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, !isPlayer,
                    (l, u) => true,
                    "이번 턴 공격 불가로 만들 상대 유닛을 선택하세요",
                    picked => _attackDisabledTurn.Add(Key(!isPlayer, picked)));
                break;

            // BT03 로망틱 발렌타인: 아군 디펜더 유닛 히트만큼 패 트래시 → 조우 유닛 패로
        } while (false);
    }

    private void H_Sk_DefenderAllyHitDiscardReturnEncounter(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Keywords.Contains("디펜더"),
                    "패 트래시 기준이 될 디펜더 아군을 선택하세요",
                    allyLane =>
                    {
                        var ally3 = FieldManager.Instance.GetUnit(isPlayer, allyLane);
                        if (ally3 == null) return;
                        int hitCount3 = GetEffectiveHit(isPlayer, allyLane, ally3);
                        for (int i = 0; i < hitCount3; i++) ApplyForceDiscard(isPlayer);
                        // 조우 유닛 패로 바운스
                        var enc3 = FieldManager.Instance.GetUnit(!isPlayer, allyLane);
                        if (enc3 != null) EffectActions.BounceUnit(!isPlayer, allyLane);
                    });
                break;
            }

            // BT03 빨리 오라고오오!!!: 아군 전체에 장착 아이템 효과 공유
        } while (false);
    }

    private void H_Sk_ShareItemEffectToAllAllies(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                // 아이템이 있는 아군 1장의 아이템 효과를 다른 아군 전체에 복사 (이번 턴, 파워/히트 효과만)
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => FieldManager.Instance.GetEquippedItems(isPlayer, l).Count > 0,
                    "효과를 공유할 아이템 장착 아군을 선택하세요",
                    sourceLane =>
                    {
                        var srcItems = FieldManager.Instance.GetEquippedItems(isPlayer, sourceLane);
                        for (int i = 0; i < 3; i++)
                        {
                            if (i == sourceLane) continue;
                            if (FieldManager.Instance.GetUnit(isPlayer, i) == null) continue;
                            foreach (var item3 in srcItems)
                                foreach (var et3 in item3.EffectTypes)
                                {
                                    var (t3, v3) = Parse(et3);
                                    if (t3 == "ItemPowerBoost") AddTurnBoost(isPlayer, i, v3);
                                    else if (t3 == "ItemHitBoost") AddTurnHitBoost(isPlayer, i, v3);
                                }
                        }
                        Debug.Log($"[스킬] {skill.CardName} → 레인{sourceLane} 아이템 효과 공유");
                    });
                break;
            }

            // BT03 본딩 페인: 아군 1장 트래시 → 다른 아군 전체 코스트×1000 파워+N
        } while (false);
    }

    private void H_Sk_TrashAllyBuffOthersByPower(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => true,
                    "트래시할 아군 유닛을 선택하세요",
                    srcLane =>
                    {
                        var srcUnit = FieldManager.Instance.GetUnit(isPlayer, srcLane);
                        int srcPower = srcUnit != null ? srcUnit.AttackPower : 0;
                        EffectActions.TrashUnit(isPlayer, srcLane);
                        for (int i = 0; i < 3; i++)
                        {
                            if (FieldManager.Instance.GetUnit(isPlayer, i) != null)
                                AddTurnBoost(isPlayer, i, srcPower);
                        }
                        Debug.Log($"[스킬] {skill.CardName} → {srcPower} 파워 공유");
                    });
                break;

            // BT03 축제의 끝: N코스트 이하 아군 1장을 이기고 있는 레인으로 이동 → 조우 유닛 트래시
        } while (false);
    }

    private void H_Sk_LowCostAllyTrashEncounterIfWinning(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        do
        {
            {
                // 먼저 이기고 있는 레인(아군 파워 > 상대 파워)을 찾아 N코스트 이하 아군 선택 후 조우 트래시
                var winningLanes3 = new List<int>();
                for (int i = 0; i < 3; i++)
                {
                    var myU = FieldManager.Instance.GetUnit(isPlayer, i);
                    var opU = FieldManager.Instance.GetUnit(!isPlayer, i);
                    if (myU != null && opU != null)
                    {
                        // 리더 패시브는 GetEffectivePower에 이미 포함됨 (별도 가산 금지 — 이중 계산)
                        int myP = GetEffectivePower(isPlayer, i, myU);
                        int opP = GetEffectivePower(!isPlayer, i, opU);
                        if (myP > opP) winningLanes3.Add(i);
                    }
                }
                if (winningLanes3.Count == 0) break;
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Cost <= value && winningLanes3.Contains(l),
                    $"{value}코스트 이하 이기고 있는 아군을 선택하세요",
                    picked =>
                    {
                        var enc3l = FieldManager.Instance.GetUnit(!isPlayer, picked);
                        if (enc3l != null) EffectActions.TrashUnit(!isPlayer, picked);
                    });
                break;
            }
        } while (false);
    }

}
