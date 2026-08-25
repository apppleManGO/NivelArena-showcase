// Assets/Scripts/Managers/EffectSystem.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public partial class EffectSystem : MonoBehaviour
{
    public void OnAttackDeclared(bool isPlayer, int lane, CardData card)
    {
        // 체인 조건용 공격 횟수 (이번 공격 포함)
        _attackCountThisTurn[Idx(isPlayer)]++;

        // ST07: 《호문클루스》 아군이 공격한 횟수 (이번 공격 포함)
        if (card.Faction != null && card.Faction.Contains("호문클루스"))
            _homunculusAttackCountThisTurn[Idx(isPlayer)]++;

        // 부여된 「어태커 파워+N」 (유나/리아트리스 등이 부여)
        if (_attackerGrants.TryGetValue(Key(isPlayer, lane), out int grant))
        {
            AddAttackBoost(isPlayer, lane, grant);
            Debug.Log($"[부여 어태커] {card.CardName} 파워+{grant}");
        }

        // BT01 PassiveAttackCostDiscard: 공격 선언 시 N장 패 트래시
        OnAttackCostDiscard(isPlayer, lane, card);

        // BT06: 부여된 「공격 시 상대 드로우1」 (상대 턴 끝까지)
        if (_bt06AttackerDrawPenalty.Contains(Key(isPlayer, lane)))
        {
            EffectActions.Draw(!isPlayer, 1);
            Debug.Log($"[부여] {card.CardName} 공격 → 상대 드로우1");
        }

        // BT06 DrawPerDefenderDisableAttackers: 상대 턴 끝까지 어태커 효과 발동 불가
        bool attackerEffectsDisabled = _bt06AttackerEffectsDisabled[Idx(isPlayer)];
        if (!attackerEffectsDisabled)
            foreach (var et in card.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (_attackerHandlers != null && _attackerHandlers.TryGetValue(type, out var handler))
                    handler(isPlayer, lane, card, value, et);
            }

        // ST10 피의 기사로 부여된 「어태커: 조우 유닛 트래시」
        if (!attackerEffectsDisabled
            && _grantedAttackerTrashEncounter.Contains(Key(isPlayer, lane))
            && FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
        {
            EffectActions.TrashUnit(!isPlayer, lane);
            Debug.Log($"[부여 어태커] {card.CardName} → 조우 유닛 트래시");
        }

        // BT07-072 스위치로 부여된 「어태커: 상대 1장 파워-N×(1+장착 아이템 수)」 (이번 턴)
        if (!attackerEffectsDisabled && _bt07EveAttackerDebuffPerItem.TryGetValue(Key(isPlayer, lane), out int perItemDebuff))
        {
            int repeats = 1 + FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count;
            EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
                "파워를 감소시킬 상대 유닛을 선택하세요",
                picked => AddTurnBoost(!isPlayer, picked, -perItemDebuff * repeats));
        }

        // BT07-077 스킬로 부여된 「어태커: 코스트≤장착 아이템 수인 상대 1장 트래시」 (이번 턴)
        if (!attackerEffectsDisabled && _bt07EveAttackerTrashByItems.Contains(Key(isPlayer, lane)))
        {
            int itemCount = FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count;
            EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => u.Cost <= itemCount,
                $"트래시할 {itemCount}코스트 이하 상대 유닛을 선택하세요 (선택 안 해도 됨)",
                picked => EffectActions.TrashUnit(!isPlayer, picked));
        }

        // 장착 아이템의 어태커 효과도 적용
        if (!attackerEffectsDisabled)
        foreach (var item in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
            foreach (var et in item.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "AttackerPowerBoost")
                {
                    AddAttackBoost(isPlayer, lane, value);
                    Debug.Log($"[아이템 어태커] {item.CardName} 파워+{value}");
                }
                // BT04-039 ItemAttackerPowerPerDamageZoneDuelist:N — DZ 1장마다 +1000, 양측 DZ≥N 듀얼리스트
                else if (type == "ItemAttackerPowerPerDamageZoneDuelist")
                {
                    AddAttackBoost(isPlayer, lane, GetDamageZoneCount(isPlayer) * 1000);
                    if (GetTotalDamageZoneCount(isPlayer) >= value)
                        _grantedDuelistThisTurn.Add(Key(isPlayer, lane));
                }
                // ST08 더 썬: 어태커 — 양측 패 장수가 다르면 그 차이 1장마다 조우 유닛 파워-N
                else if (type == "ItemAttackerDebuffPerHandDiff")
                {
                    int diff = Mathf.Abs(GetOwner(isPlayer).Hand.Count - GetOwner(!isPlayer).Hand.Count);
                    if (diff > 0)
                    {
                        AddTurnBoost(!isPlayer, lane, -diff * value);
                        Debug.Log($"[아이템 어태커] {item.CardName} → 패 차이 {diff}장, 조우 파워-{diff * value}");
                    }
                }
                // BT03 아이템 어태커: 패N장 트래시 → 조우 히트-1 (0이 되면 트래시)
                else if (type == "ItemAttackerEncounterDebuffSacrificeDraw")
                {
                    var partsAtk = et.Split(':');
                    int discardCost = partsAtk.Length >= 2 ? int.Parse(partsAtk[1]) : 1;
                    int drawN = partsAtk.Length >= 3 ? int.Parse(partsAtk[2]) : 0;
                    EffectActions.DiscardHandOptional(isPlayer, discardCost, discI =>
                    {
                        if (discI < discardCost) return;
                        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
                        if (enc != null)
                        {
                            AddTurnHitBoost(!isPlayer, lane, -1);
                            if (GetEffectiveHit(!isPlayer, lane, enc) <= 0)
                                EffectActions.TrashUnit(!isPlayer, lane);
                        }
                        if (drawN > 0) EffectActions.Draw(isPlayer, drawN);
                        Debug.Log($"[아이템 어태커] {item.CardName} → 패{discardCost}장 트래시, 조우 히트-1");
                    });
                }
                // BT03 아이템 어태커: 패N장 트래시 → 조우가 방어 못 하면 패N장 트래시, 그렇지 않으면 트래시
                else if (type == "ItemAttackerDiscardForceHitDiscardOrTrashEncounter")
                {
                    var partsAtk2 = et.Split(':');
                    int dcN = partsAtk2.Length >= 2 ? int.Parse(partsAtk2[1]) : 1;
                    EffectActions.DiscardHandOptional(isPlayer, dcN, discI2 =>
                    {
                        if (discI2 < dcN) return;
                        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
                        if (enc == null) return;
                        // 조우가 방어 불가(디펜드 불가 상태)면 트래시, 아니면 상대 패N장 강제 트래시
                        if (IsDefendDisabled(!isPlayer, lane))
                            EffectActions.TrashUnit(!isPlayer, lane);
                        else
                            for (int dj = 0; dj < dcN; dj++) ApplyForceDiscard(!isPlayer);
                        Debug.Log($"[아이템 어태커] {item.CardName} → 조우 방어불가:{IsDefendDisabled(!isPlayer, lane)}");
                    });
                }
                // BT06-041 천둥의 망치: 조우 파워-N(이번 공격), 트래시했다면(근사) 아이템 자기 트래시 가능 → 드로우M
                else if (type == "ItemAttackerDebuffTrashSelfDraw")
                    BT06_ItemAttackerDebuffTrashSelfDraw(isPlayer, lane, item, et);
                else BT05_ItemAttacker(isPlayer, lane, card, item, type, et); // BT05 아이템 어태커
            }

        // BT03 PassiveDrawOnEnemyAttack: 상대 공격 시 드로우 (방어 측 패시브)
        // isPlayer = 공격자, !isPlayer = 방어자
        for (int di = 0; di < 3; di++)
        {
            var defUnit = FieldManager.Instance.GetUnit(!isPlayer, di);
            if (defUnit == null) continue;
            foreach (var et in defUnit.EffectTypes)
            {
                var (dtype, dval) = Parse(et);
                if (dtype == "PassiveDrawOnEnemyAttack")
                {
                    EffectActions.Draw(!isPlayer, dval > 0 ? dval : 1);
                    Debug.Log($"[패시브] {defUnit.CardName} → 상대 공격 시 드로우{(dval > 0 ? dval : 1)}");
                    break; // 유닛당 1회
                }
            }
        }
    }

    public void OnAttackWon(bool isPlayer, int lane, CardData attacker, CardData defeatedUnit = null)
    {
        foreach (var et in attacker.EffectTypes)
        {
            var (type, value) = Parse(et);
            switch (type)
            {
                // SB01-003: 전투로 상대 유닛 트래시 시, 그 유닛 히트 이하로 패를 골라 트래시 가능 → 1장마다 상대에게 1대미지
                case "AttackerPowerPerDamageZoneDischargeDamage":
                    if (defeatedUnit != null) SB01_DischargeDamageStep(isPlayer, defeatedUnit.Hit, 0);
                    break;

                case "AttackerPenetration":
                    GetOwner(!isPlayer).TakeDamage(value);
                    Debug.Log($"[관통] {attacker.CardName} → {value}대미지");
                    break;
                case "AttackerPlunder":
                    GetOwner(isPlayer).DrawCard(value);
                    HandView.Instance?.RefreshHand();
                    Debug.Log($"[약탈] {attacker.CardName} → 드로우{value}");
                    break;
                // ST10 사도 블레이드: 체인:C — 공격 횟수 C 이상이면 이 공격 약탈[N]
                // 형식: "ChainAttackerPlunder:C:N"
                case "ChainAttackerPlunder":
                {
                    var parts = et.Split(':');
                    int need = int.Parse(parts[1]);
                    int amount = int.Parse(parts[2]);
                    if (GetChainCount(isPlayer, lane) >= need)
                    {
                        EffectActions.Draw(isPlayer, amount);
                        Debug.Log($"[체인{need} 약탈] {attacker.CardName} → 드로우{amount}");
                    }
                    break;
                }
                // BT06-020: 체인:C — 조우 유닛 파워-N(어태커 핸들러에서 처리). 승리(트래시)했다면 드로우1
                // 형식: "ChainAttackerDebuffEncounterDraw:C:N" — 여기서는 승리 시 드로우만 처리
                case "ChainAttackerDebuffEncounterDraw":
                {
                    var parts = et.Split(':');
                    int need = int.Parse(parts[1]);
                    if (GetChainCount(isPlayer, lane) >= need)
                    {
                        EffectActions.Draw(isPlayer, 1);
                        Debug.Log($"[체인{need}] {attacker.CardName} → 조우 트래시(승리), 드로우1");
                    }
                    break;
                }
            }
        }
        // 장착 아이템의 전투 승리 효과
        foreach (var item in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
            foreach (var et in item.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "AttackerPlunder")
                {
                    GetOwner(isPlayer).DrawCard(value);
                    HandView.Instance?.RefreshHand();
                    Debug.Log($"[아이템 약탈] {item.CardName} → 드로우{value}");
                }
                // BT04-040 ItemPassivePenetrationByDamageZone:N:M — DZ≥N 관통1, ≥M 관통2
                else if (type == "ItemPassivePenetrationByDamageZone")
                {
                    var p = et.Split(':');
                    int n1 = value, n2 = p.Length > 2 && int.TryParse(p[2], out int m) ? m : int.MaxValue;
                    int dz = GetDamageZoneCount(isPlayer);
                    int dmg = dz >= n2 ? 2 : (dz >= n1 ? 1 : 0);
                    if (dmg > 0) { GetOwner(!isPlayer).TakeDamage(dmg); Debug.Log($"[BT04 아이템 관통] {item.CardName} → {dmg}대미지"); }
                }
            }

        // ST06 기원의 라스로 부여된 「어태커 관통[N]」
        if (_grantedPenetration.TryGetValue(Key(isPlayer, lane), out int grantedPen))
        {
            GetOwner(!isPlayer).TakeDamage(grantedPen);
            Debug.Log($"[부여 관통] {attacker.CardName} → {grantedPen}대미지");
        }
        // BT02 글레링 아이즈로 부여된 「어태커 약탈[N]」
        if (_grantedAttackerPlunder.TryGetValue(Key(isPlayer, lane), out int grantedPlunder))
        {
            EffectActions.Draw(isPlayer, grantedPlunder);
            Debug.Log($"[부여 약탈] {attacker.CardName} → 드로우{grantedPlunder}");
        }
    }

    // BT06-059 시기의 밤 레비아: 디펜더 — 방어 확정 시 카드를 N장 드로우한다
    public void OnDefendDeclared(bool isPlayer, int lane, CardData defender)
    {
        foreach (var et in defender.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (type == "DefenderDraw")
            {
                EffectActions.Draw(isPlayer, value);
                Debug.Log($"[디펜더] {defender.CardName} → 드로우{value}");
            }
        }
        TrySB01DefendDeclared(isPlayer, lane, defender); // SB01 디펜더 트리거/오라
    }

    // DefenderFinisher 보유 여부 확인 (CombatManager에서 호출)
    public bool HasDefenderFinisher(bool isPlayer, int lane)
    {
        foreach (var item in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
            if (item.EffectTypes.Contains("DefenderFinisher")) return true;
        // BT01 DefenderFinisherSelf: 방어 승리 시 자기 자신도 트래시
        var unit = FieldManager.Instance.GetUnit(isPlayer, lane);
        if (unit?.EffectTypes != null && unit.EffectTypes.Contains("DefenderFinisherSelf"))
            return true;
        // BT03 EntryGrantDefenderFinisherToLowCostEncounter로 부여된 경우
        if (_grantedDefenderFinisher.Contains(Key(isPlayer, lane)))
            return true;
        return false;
    }

    // BT02 ArmedOnTrashSacrificeItemSurvive: 유닛이 트래시될 때, 장착 아이템 1장 트래시하면 생존
    // CombatManager.TrashUnit 전에 호출 — true 반환 시 트래시 취소
    public bool TryArmedSacrificeItemSurvive(bool isPlayer, int lane, CardData card)
    {
        // BT05-079 ItemPassiveMillPreventTrash:N — 장착 아이템이 이 패시브면 덱 N장 트래시로 생존 (턴당 각 1회)
        foreach (var it in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
        {
            var mp = it.EffectTypes.FirstOrDefault(e => e.StartsWith("ItemPassiveMillPreventTrash"));
            if (mp == null) continue;
            string mkey = $"millPrevent_{(isPlayer ? "P" : "A")}_{lane}";
            if (_selfLockedSkillsThisTurn.Contains(mkey)) break; // 턴당 1회
            _selfLockedSkillsThisTurn.Add(mkey);
            int n = Parse(mp).value; if (n <= 0) n = 3;
            EffectActions.Mill(isPlayer, n);
            Debug.Log($"[BT05] {it.CardName} → 덱 {n}장 트래시로 {card.CardName} 트래시 방지");
            return true;
        }

        // BT07-083 지속형 회복제: 상대 효과로 트래시될 때 이 아이템을 트래시하면 생존 (근사: 트래시 원인 무관 항상 체크)
        var protector = FieldManager.Instance.GetEquippedItems(isPlayer, lane)
            .FirstOrDefault(it => it.EffectTypes.Contains("ItemTrashProtection"));
        if (protector != null)
        {
            FieldManager.Instance.UnequipItem(isPlayer, lane, protector);
            GetOwner(isPlayer).AddToTrash(protector);
            EffectActions.RefreshUI();
            Debug.Log($"[BT07] {protector.CardName} 트래시 → {card.CardName} 생존");
            return true;
        }

        if (!card.EffectTypes.Contains("ArmedOnTrashSacrificeItemSurvive")) return false;
        var items = FieldManager.Instance.GetEquippedItems(isPlayer, lane);
        if (items.Count == 0) return false;

        if (!isPlayer)
        {
            // AI: 코스트 낮은 아이템 1장 희생
            var sacrifice = items.OrderBy(i => i.Cost).First();
            FieldManager.Instance.UnequipItem(isPlayer, lane, sacrifice);
            GetOwner(isPlayer).AddToTrash(sacrifice);
            Debug.Log($"[AI 희생] {sacrifice.CardName} 트래시 → {card.CardName} 생존");
            return true;
        }

        // 플레이어: 팝업으로 선택
        bool survived = false;
        EffectActions.PickFromCards(isPlayer, "희생할 장착 아이템을 선택하세요 (취소 시 유닛 트래시)", new List<CardData>(items),
            pickedItem =>
            {
                FieldManager.Instance.UnequipItem(isPlayer, lane, pickedItem);
                GetOwner(isPlayer).AddToTrash(pickedItem);
                Debug.Log($"[희생] {pickedItem.CardName} 트래시 → {card.CardName} 생존");
                survived = true;
            },
            onCancel: () => { survived = false; });
        return survived;
    }

    // BT01 PassiveAttackCostDiscard: 이 유닛이 공격 선언 시 N장 패 트래시
    public void OnAttackCostDiscard(bool isPlayer, int lane, CardData attacker)
    {
        foreach (var et in attacker.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (type != "PassiveAttackCostDiscard") continue;
            for (int i = 0; i < value; i++) ApplyForceDiscard(isPlayer);
        }
    }

}
