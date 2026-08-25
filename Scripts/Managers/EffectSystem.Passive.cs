// Assets/Scripts/Managers/EffectSystem.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public partial class EffectSystem : MonoBehaviour
{
    private int GetPassiveBonus(bool isPlayer, int lane, CardData card)
    {
        int bonus = 0;
        for (int i = 0; i < 3; i++)
        {
            if (i == lane) continue;
            var ally = FieldManager.Instance.GetUnit(isPlayer, i);
            if (ally == null) continue;

            foreach (var et in ally.EffectTypes)
            {
                var (type, value) = Parse(et);
                switch (type)
                {
                    // 블랑: 어태커 가진 유닛 파워+1000
                    case "PassiveAttackerAlliesBoost":
                        if (card.Keywords.Contains("어태커"))
                            bonus += value;
                        break;
                    // 엑시아: 엑시트 가진 유닛 파워+1000
                    case "PassiveExitAlliesBoost":
                        if (card.Keywords.Contains("엑시트"))
                            bonus += value;
                        break;
                    // 헬름: 가디언 가진 유닛 파워+N
                    case "PassiveGuardianAlliesBoost":
                        if (card.Keywords.Contains("가디언"))
                            bonus += value;
                        break;
                    // ST05 리타: 암드 가진 유닛 파워+N
                    case "PassiveArmedAlliesBoost":
                        if (card.Keywords.Contains("암드"))
                            bonus += value;
                        break;
                    // ST11 로엔: 디펜더 가진 유닛 파워+N
                    case "PassiveDefenderAlliesBoost":
                        if (card.Keywords.Contains("디펜더"))
                            bonus += value;
                        break;
                    // ST08 수아: 레벨링크:N — 리더 레벨 N 이상이면 다른 아군 전체 파워+M ("LevelLinkAlliesBoost:N:M")
                    case "LevelLinkAlliesBoost":
                    {
                        var parts = et.Split(':');
                        if (parts.Length >= 3
                            && int.TryParse(parts[1], out int reqLv)
                            && int.TryParse(parts[2], out int boostVal)
                            && GetOwner(isPlayer).LeaderLevel >= reqLv)
                            bonus += boostVal;
                        break;
                    }

                    // BT03-022 플로라: 레벨링크N + 전선구축(3칸 참) → 다른 자신 유닛 중 maxCost 이하 파워+M
                    case "LevelLinkFrontlineLowCostAlliesBoost":
                    {
                        var p = et.Split(':');
                        if (p.Length >= 4
                            && int.TryParse(p[1], out int lv) && int.TryParse(p[2], out int mc) && int.TryParse(p[3], out int pw)
                            && GetOwner(isPlayer).LeaderLevel >= lv && AllLanesFilled(isPlayer) && card.Cost <= mc)
                            bonus += pw;
                        break;
                    }
                }
            }

            // 디젤: 파워 = 리더레벨 × 1000
            if (ally.EffectTypes.Contains("PassivePowerScaleLevel") && i == lane)
            {
                bonus += GetOwner(isPlayer).LeaderLevel * 1000;
            }
        }

        // 디젤 본인
        if (card.EffectTypes.Any(e => e.StartsWith("PassivePowerScaleLevel")))
            bonus += GetOwner(isPlayer).LeaderLevel * 1000;

        // ── 자기 자신 참조 패시브 ──
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            switch (type)
            {
                // ST05 맥스웰/센티: 암드 — 아이템 장착 시 파워+N
                case "ArmedPowerBoost":
                    if (FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count > 0)
                        bonus += value;
                    break;

                // ST05 소다: 암드 — 장착 아이템 1장마다 파워+N
                case "ArmedPowerPerItem":
                    bonus += FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count * value;
                    break;

                // BT02 암드 유니크: 유니크 아이템 장착 시 파워+N
                case "ArmedUniqueBoost":
                    if (FieldManager.Instance.GetEquippedItems(isPlayer, lane).Any(i => i.Keywords.Contains("유니크")))
                        bonus += value;
                    break;

                // BT03-020 트리나: 레벨링크N + 전선구축(3칸 참) → 자기 파워+M (히트+K는 GetEffectiveHit)
                case "LevelLinkFrontlinePowerHitBoost":
                {
                    var p = et.Split(':');
                    if (p.Length >= 3 && int.TryParse(p[1], out int lv) && int.TryParse(p[2], out int pw)
                        && GetOwner(isPlayer).LeaderLevel >= lv && AllLanesFilled(isPlayer))
                        bonus += pw;
                    break;
                }

                // SB01-021 일레그: 아이템 장착 자신 유닛 3장↑ → 자기 파워+N (히트+1은 GetEffectiveHit)
                case "ArmedPowerHitIfThreeEquipped":
                    if (CountUnitsWithItems(isPlayer) >= 3) bonus += value;
                    break;

                // BT03-033 레어메탈자켓: 사이즈+1 — 사이즈는 이 엔진 전투와 무관(무동작)
                case "ItemSizeBoost":
                    break;

                // BT07-055 릴리: 사이드보드 이브 부활 — 복합 기믹(사이드 존/이브 업그레이드), 현재 근사(미동작)
                case "PassiveSideEveRevive":
                    break;

                // ST06 심판자 키세: 스킬존에 스킬 1장 이상이면 파워+N
                case "PassiveSkillZonePowerBoost":
                    if (GetOwner(isPlayer).SkillZone.Count >= 1)
                        bonus += value;
                    break;

                // ST08 타락의 유열 샬럿: 자신 패 1장마다 파워-N
                case "PassivePowerPerHandPenalty":
                    bonus -= GetOwner(isPlayer).Hand.Count * value;
                    break;

                // ST07 비르기타: 이 턴 트래시된 자신 유닛 1장 이상이면 파워+N (히트+1은 GetEffectiveHit에서)
                case "PassiveAllyTrashedBoost":
                    if (_unitsTrashedThisTurn[Idx(isPlayer)] >= 1)
                        bonus += value;
                    break;

                // ST06 계승자 유닛들: 《계승자》/《과거 혹은 미래》 가진 다른 아군 1장마다 파워+N
                case "PassivePowerPerFactionAlly":
                {
                    int count = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == lane) continue;
                        var other = FieldManager.Instance.GetUnit(isPlayer, i);
                        if (other?.Faction != null &&
                            (other.Faction.Contains("계승자") || other.Faction.Contains("과거 혹은 미래")))
                            count++;
                    }
                    bonus += count * value;
                    break;
                }

                // BT01 아군 관통 유닛이 있으면 파워+N
                case "PassivePenetrationAlliesBoost":
                {
                    bool hasAny = false;
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == lane) continue;
                        var other = FieldManager.Instance.GetUnit(isPlayer, i);
                        if (other == null) continue;
                        if (other.EffectTypes.Any(e => e.StartsWith("AttackerPenetration")) ||
                            _grantedPenetration.ContainsKey(Key(isPlayer, i)))
                            hasAny = true;
                    }
                    if (hasAny) bonus += value;
                    break;
                }

                // BT01 베이스 아군이 있으면 파워+N (리더 등)
                case "PassiveBaseAlliesBoost":
                {
                    bool hasBase = false;
                    for (int i = 0; i < 3; i++)
                    {
                        var other = FieldManager.Instance.GetUnit(isPlayer, i);
                        if (other?.Faction != null && other.Faction.StartsWith("베이스"))
                            hasBase = true;
                    }
                    if (hasBase) bonus += value;
                    break;
                }

                // BT01 코스트 1 아군이 있으면 파워+N
                case "PassiveCostOneAlliesBoost":
                {
                    bool has1 = false;
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == lane) continue;
                        var other = FieldManager.Instance.GetUnit(isPlayer, i);
                        if (other?.Cost == 1) has1 = true;
                    }
                    if (has1) bonus += value;
                    break;
                }

                // BT01 공멸 아군이 있으면 파워+N
                case "PassiveMutualDestructionAlliesBoost":
                {
                    bool hasMutual = false;
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == lane) continue;
                        var other = FieldManager.Instance.GetUnit(isPlayer, i);
                        if (other?.EffectTypes != null && other.EffectTypes.Contains("ExitMutualDestruction"))
                            hasMutual = true;
                    }
                    if (hasMutual) bonus += value;
                    break;
                }

                // BT01 전선(같은 레인에 상대 유닛 존재) 시 파워+N
                case "PassiveFrontlinePowerBoost":
                    if (FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
                        bonus += value;
                    break;

                // BT01 베이스 아군 수만큼 파워+N
                case "PassivePowerPerBaseAlly":
                {
                    int baseCount = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        var other = FieldManager.Instance.GetUnit(isPlayer, i);
                        if (other?.Faction != null && other.Faction.StartsWith("베이스"))
                            baseCount++;
                    }
                    bonus += baseCount * value;
                    break;
                }

                // BT01 리더 레벨 1당 파워+N
                case "PassivePowerBoostPerLevel":
                    bonus += GetOwner(isPlayer).LeaderLevel * value;
                    break;

                // BT02 일레그: 아이템 장착 아군 1장마다 파워+N
                case "PassivePowerPerEquippedAlly":
                {
                    int eq = 0;
                    for (int i = 0; i < 3; i++)
                        if (FieldManager.Instance.GetUnit(isPlayer, i) != null
                            && FieldManager.Instance.GetEquippedItems(isPlayer, i).Count > 0)
                            eq++;
                    bonus += eq * value;
                    break;
                }

                // BT02 크라운-네이키드 킹 리더: 아이템 장착 아군 전체 파워+N
                case "PassiveEquippedAlliesBoost":
                    if (FieldManager.Instance.GetUnit(isPlayer, lane) != null
                        && FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count > 0)
                        bonus += value;
                    break;

                // BT02 루피: 베이스 아군 1장마다 히트+1 → GetEffectiveHit에서 처리, 여기선 파워 없음
                // (아래 PassiveHitPerBaseAlly는 GetEffectiveHit에 추가)
                case "PassiveHitPerBaseAlly":
                    break; // 파워 보너스 없음

                // BT02 길로틴: 필드에서 다른 유닛이 효과로 트래시될 때마다 파워+N (이번 턴)
                case "PassiveEffectTrashBoost":
                    bonus += _effectTrashedThisTurn[Idx(isPlayer)] * value;
                    break;

                // BT03 홍련: 아군 히트 합산만큼 파워+N × hit합
                case "PassivePowerPerAllyHit":
                {
                    int hitSum3 = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == lane) continue;
                        var u3 = FieldManager.Instance.GetUnit(isPlayer, i);
                        if (u3 != null) hitSum3 += GetEffectiveHit(isPlayer, i, u3);
                    }
                    bonus += hitSum3 * value;
                    break;
                }

                // BT03 마나: 이 턴 패 트래시 발생 시 파워+N
                case "PassivePowerIfHandTrashedThisTurn":
                    if (_handTrashedThisTurn[Idx(isPlayer)])
                        bonus += value;
                    break;

                // BT03 PassiveItemEquipRestriction: 상대가 아이템을 장착할 수 없음
                // FieldManager.EquipItem에서 별도 체크 — 파워 보너스 없음
                case "PassiveItemEquipRestriction":
                    break;

                // BT03 PassiveOpponentNonTriggerDrawPunish: 상대 비트리거 드로우 시 패 트래시
                // 완전 구현은 드로우 훅 필요 — 근사: 파워 보너스 없음, 효과는 OnOpponentDraw 참조
                case "PassiveOpponentNonTriggerDrawPunish":
                    break;
            }
        }

        bonus += GetBT04PassiveBonus(isPlayer, lane, card); // BT04 대미지존/트래시 참조 패시브
        bonus += GetBT05PassiveBonus(isPlayer, lane, card); // BT05 레벨링크/믹스/이스케이프 패시브
        bonus += GetBT06PassiveBonus(isPlayer, lane, card); // BT06 광전사/체인/버프 키워드 참조 패시브
        bonus += GetBT07PassiveBonus(isPlayer, lane, card); // BT07 레벨/포지션/이브 아이템 참조 패시브
        bonus += GetSB01PassiveBonus(isPlayer, lane, card); // SB01 디펜더 수/전선구축 참조 패시브
        bonus += GetSB02PassiveBonus(isPlayer, lane, card); // SB02 히트임계값/패스케일링/오라
        return bonus;
    }

    // 전선구축: 자신의 3개 유닛 존이 모두 차 있는가
    private bool AllLanesFilled(bool isPlayer)
    {
        for (int i = 0; i < 3; i++)
            if (FieldManager.Instance.GetUnit(isPlayer, i) == null) return false;
        return true;
    }

    // 아이템을 장착한 자신 유닛의 수
    private int CountUnitsWithItems(bool isPlayer)
    {
        int n = 0;
        for (int i = 0; i < 3; i++)
            if (FieldManager.Instance.GetUnit(isPlayer, i) != null
                && FieldManager.Instance.GetEquippedItems(isPlayer, i).Count > 0) n++;
        return n;
    }
}
