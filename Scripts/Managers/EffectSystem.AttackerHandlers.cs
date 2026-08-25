// Assets/Scripts/Managers/EffectSystem.AttackerHandlers.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 어태커(OnAttackDeclared) 효과 핸들러 레지스트리 (Dictionary dispatch)
// 새 어태커 effectType: H_XXX 핸들러 작성 + InitAttackerHandlers()에 등록.
// (전/후처리 — 공격횟수·부여어태커·아이템어태커·PassiveDrawOnEnemyAttack — 는 OnAttackDeclared에 유지)
public partial class EffectSystem : MonoBehaviour
{
    private delegate void AttackerHandler(bool isPlayer, int lane, CardData card, int value, string et);
    private Dictionary<string, AttackerHandler> _attackerHandlers;

    private void InitAttackerHandlers()
    {
        _attackerHandlers = new Dictionary<string, AttackerHandler>
        {
            ["AttackerPowerBoost"] = H_AttackerPowerBoost,
            ["ArmedAttackerForceDiscard"] = H_ArmedAttackerForceDiscard,
            ["AttackerBuffAlly"] = H_AttackerBuffAlly,
            ["AttackerDiscardDebuffByTotalPower"] = H_AttackerDiscardDebuffByTotalPower,
            ["ChainAttackerDebuffEncounter"] = H_ChainAttackerDebuffEncounter,
            ["ChainAttackerHitBoost"] = H_ChainAttackerHitBoost,
            ["ChainAttackerBreakthrough"] = H_ChainAttackerBreakthrough,
            ["AttackerGainExitDrawPerAttack"] = H_AttackerGainExitDrawPerAttack,
            ["AttackerGainExitRevealPerAttack"] = H_AttackerGainExitDrawPerAttack,
            ["AttackerGainExitTrashEnemyPerAttack"] = H_AttackerGainExitDrawPerAttack,
            ["AttackerGainExitMillDamagePerAttack"] = H_AttackerGainExitDrawPerAttack,
            ["AttackerGainExitDebuffPerAttack"] = H_AttackerGainExitDrawPerAttack,
            ["AttackerBreakthroughHighCost"] = H_AttackerBreakthroughHighCost,
            ["AttackerConditionalBreakthroughHighCost"] = H_AttackerConditionalBreakthroughHighCost,
            ["LevelLinkAttackerBreakthrough"] = H_LevelLinkAttackerBreakthroughLowCost,
            ["ArmedAttackerPenetration"] = H_ArmedAttackerPenetration,
            ["ArmedUniqueBreakthroughByHit"] = H_ArmedUniqueBreakthroughByHit,
            ["ArmedThreeAttackerDraw"] = H_ArmedThreeAttackerDraw,
            ["AttackerEncounterDebuff"] = H_AttackerEncounterDebuff,
            ["AttackerDiscardDebuffEncounterPerCard"] = H_AttackerDiscardDebuffEncounterPerCard,
            ["AttackerConditionalHandRecoverSkill"] = H_AttackerConditionalHandRecoverSkill,
        };
    }

    private void H_AttackerPowerBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
            AddAttackBoost(isPlayer, lane, value);
            Debug.Log($"[어태커] {card.CardName} 파워+{value}");
            return;

        // ST05 프리바티(유닛): 암드 어태커 — 아이템 장착 시 공격할 때 상대 패N장 트래시
    }

    private void H_ArmedAttackerForceDiscard(bool isPlayer, int lane, CardData card, int value, string et)
    {
            if (FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count > 0)
            {
                EffectActions.DiscardHand(!isPlayer, value);
                Debug.Log($"[암드 어태커] {card.CardName} → 상대 패 {value}장 트래시");
            }
            return;

        // ST10 블레이드: 어태커 — 다른 아군 1장 이번 턴 파워+N
    }

    private void H_AttackerBuffAlly(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.SelectUnit(isPlayer, isPlayer,
                (l, u) => l != lane,
                $"파워+{value}를 줄 유닛을 선택하세요",
                picked => EffectActions.Buff(isPlayer, picked, value, 0, BoostUntil.EndOfTurn));
            return;

        // ST06 심판자 키세: 어태커 — 상대 유닛 1장 선택, 그 유닛 히트만큼 패 트래시하면
        // 이 턴이 끝날 때까지 그 유닛 파워가 필드 아군 전체 현재 파워 합만큼 감소
    }

    private void H_AttackerDiscardDebuffByTotalPower(bool isPlayer, int lane, CardData card, int value, string et)
    {
            EffectActions.SelectUnit(isPlayer, !isPlayer,
                (l, u) => true,
                "파워를 감소시킬 상대 유닛을 선택하세요",
                enemyLane =>
                {
                    var target = FieldManager.Instance.GetUnit(!isPlayer, enemyLane);
                    if (target == null) return;
                    int need = GetEffectiveHit(!isPlayer, enemyLane, target);
                    if (need <= 0 || GetOwner(isPlayer).Hand.Count < need) return;
                    EffectActions.DiscardHandOptional(isPlayer, need, discarded =>
                    {
                        if (discarded < need) return;
                        int totalAllyPower = 0;
                        for (int i = 0; i < 3; i++)
                        {
                            var ally = FieldManager.Instance.GetUnit(isPlayer, i);
                            if (ally != null) totalAllyPower += GetEffectivePower(isPlayer, i, ally);
                        }
                        AddTurnBoost(!isPlayer, enemyLane, -totalAllyPower);
                        Debug.Log($"[어태커] {card.CardName} → 레인{enemyLane} 파워-{totalAllyPower}");
                    });
                });
            return;

        // ST10 유스티아: 체인:C 어태커 — 공격 횟수 C 이상이면 조우 유닛 이번 턴 파워-N
        // 형식: "ChainAttackerDebuffEncounter:C:N"
    }

    private void H_ChainAttackerDebuffEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var parts = et.Split(':');
            int need = int.Parse(parts[1]);
            int amount = int.Parse(parts[2]);
            if (GetChainCount(isPlayer, lane) >= need)
            {
                AddTurnBoost(!isPlayer, lane, -amount);
                Debug.Log($"[체인{need}] {card.CardName} → 조우 파워-{amount}");
            }
            return;
        }

        // ST10 테레제: 체인:C 어태커 — 이 공격 히트+N ("ChainAttackerHitBoost:C:N")
    }

    private void H_ChainAttackerHitBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var parts = et.Split(':');
            int need = int.Parse(parts[1]);
            int amount = int.Parse(parts[2]);
            if (GetChainCount(isPlayer, lane) >= need)
                EffectActions.Buff(isPlayer, lane, 0, amount, BoostUntil.EndOfAttack);
            return;
        }

        // ST10 빌헬미나: 체인:C 어태커 — 이 공격 돌파 획득 ("ChainAttackerBreakthrough:C")
    }

    private void H_ChainAttackerBreakthrough(bool isPlayer, int lane, CardData card, int value, string et)
    {
            if (GetChainCount(isPlayer, lane) >= value)
            {
                _breakthroughGrants.Add(Key(isPlayer, lane));
                Debug.Log($"[체인{value}] {card.CardName} → 돌파 획득");
            }
            return;

        // ST07 호문클루스 5종: 어태커 — 이 턴이 끝날 때까지 「엑시트: 호문클루스 공격 수만큼 X」를 얻는다
    }

    private void H_AttackerGainExitDrawPerAttack(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var (type, _) = Parse(et);
            _grantedExit[Key(isPlayer, lane)] = (type, value);
            Debug.Log($"[어태커] {card.CardName} → 엑시트 부여 ({type})");
            return;

        // BT02 하란/도로시: 어태커 돌파[N코스트 이상] — 조우 유닛 코스트가 N 이상이면 방어 불가
    }

    // BT03-028 나가: 레벨링크N 어태커 — 리더 레벨 N 이상이면 돌파[M코 이하](조우 M코 이하 상대는 방어 불가)
    //  형식 "LevelLinkAttackerBreakthrough:N:M" (value=N=레벨, parts[2]=M=코스트상한)
    private void H_LevelLinkAttackerBreakthroughLowCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var parts = et.Split(':');
        int maxCost = parts.Length > 2 && int.TryParse(parts[2], out int m) ? m : 4;
        if (GetOwner(isPlayer).LeaderLevel < value) return;
        var def = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (def != null && def.Cost <= maxCost)
            _breakthroughGrants.Add(Key(isPlayer, lane));
    }

    private void H_AttackerBreakthroughHighCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var defHigh = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (defHigh != null && defHigh.Cost >= value)
                _breakthroughGrants.Add(Key(isPlayer, lane));
            return;
        }

        // BT02 도로시: 어태커 — 패가 N장 이상이면 돌파[M코스트 이상] 획득 ("AttackerConditionalBreakthroughHighCost:N:M")
    }

    private void H_AttackerConditionalBreakthroughHighCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var parts = et.Split(':');
            int handReq = value;
            int costReq = parts.Length > 2 && int.TryParse(parts[2], out int m) ? m : 6;
            if (GetOwner(isPlayer).Hand.Count >= handReq)
            {
                var defCond = FieldManager.Instance.GetUnit(!isPlayer, lane);
                if (defCond != null && defCond.Cost >= costReq)
                    _breakthroughGrants.Add(Key(isPlayer, lane));
            }
            return;
        }

        // BT02 프림-씨 오브 슬로스: 암드 어태커 — 아이템 장착 시 관통[N]
    }

    private void H_ArmedAttackerPenetration(bool isPlayer, int lane, CardData card, int value, string et)
    {
            if (FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count > 0)
            {
                string pKey = Key(isPlayer, lane);
                _grantedPenetration[pKey] = _grantedPenetration.GetValueOrDefault(pKey) + value;
                Debug.Log($"[암드 어태커] {card.CardName} → 관통[{value}] 획득");
            }
            return;

        // BT02 크라운-네이키드 킹: 암드:유니크 — 유니크 아이템 장착 시 아이템 수가 조우 히트 이상이면 돌파
    }

    private void H_ArmedUniqueBreakthroughByHit(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var items2 = FieldManager.Instance.GetEquippedItems(isPlayer, lane);
            bool hasUnique = items2.Any(i => i.Keywords.Contains("유니크"));
            if (!hasUnique) return;
            var defUnit = FieldManager.Instance.GetUnit(!isPlayer, lane);
            int defHit  = defUnit != null ? GetEffectiveHit(!isPlayer, lane, defUnit) : 0;
            if (items2.Count >= defHit)
                _breakthroughGrants.Add(Key(isPlayer, lane));
            return;
        }

        // BT02 슈가: 암드 3장 이상 어태커 — 드로우N
    }

    private void H_ArmedThreeAttackerDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
            if (FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count >= 3)
                EffectActions.Draw(isPlayer, value);
            return;

        // BT03 바이퍼/홍련: 어태커 — 조우 유닛 파워-N
    }

    private void H_AttackerEncounterDebuff(bool isPlayer, int lane, CardData card, int value, string et)
    {
            AddTurnBoost(!isPlayer, lane, -value);
            Debug.Log($"[어태커] {card.CardName} → 조우 파워-{value}");
            return;

        // BT03 브래디: 어태커 — 패 1장마다 조우 유닛 파워-N
    }

    private void H_AttackerDiscardDebuffEncounterPerCard(bool isPlayer, int lane, CardData card, int value, string et)
    {
            if (GetOwner(isPlayer).Hand.Count > 0)
                AddTurnBoost(!isPlayer, lane, -GetOwner(isPlayer).Hand.Count * value);
            Debug.Log($"[어태커] {card.CardName} → 패{GetOwner(isPlayer).Hand.Count}장 × -{value}");
            return;

        // BT03 베스티: 어태커 — 패 N장 이상이면 조우 유닛 파워-M 후 트래시에서 스킬 회수
    }

    private void H_AttackerConditionalHandRecoverSkill(bool isPlayer, int lane, CardData card, int value, string et)
    {
        {
            var parts3a = et.Split(':');
            int handReq = value;
            int debuff3 = parts3a.Length > 2 && int.TryParse(parts3a[2], out int d3) ? d3 : 0;
            if (GetOwner(isPlayer).Hand.Count >= handReq)
            {
                if (debuff3 > 0) AddTurnBoost(!isPlayer, lane, -debuff3);
                var skills3 = GetOwner(isPlayer).TrashPile.Where(c => c.Type == CardType.Skill).ToList();
                if (skills3.Count > 0)
                    EffectActions.PickFromCards(isPlayer, "회수할 스킬을 선택하세요", skills3,
                        picked3 => { GetOwner(isPlayer).RemoveFromTrash(picked3); GetOwner(isPlayer).AddToHand(picked3); HandView.Instance?.RefreshHand(); },
                        aiPick: l => l.OrderByDescending(c => c.Cost).First());
            }
            return;
        }
    }

}
