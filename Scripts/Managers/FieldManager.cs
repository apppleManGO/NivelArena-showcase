// Assets/Script/Managers/FieldManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class FieldManager : MonoBehaviour
{
    public static FieldManager Instance { get; private set; }

    private const int LANE_COUNT = 3;

    // [플레이어여부(0=플레이어, 1=AI)][레인번호(0~2)]
    private CardData[,] _field = new CardData[2, LANE_COUNT];
    // 레인별 장착 아이템 (여러 개 장착 가능)
    private List<CardData>[,] _items;

    // 카드 배치/제거 시 UI가 구독할 이벤트
    public event Action<bool, int, CardData> OnUnitPlaced;  // isPlayer, lane, card
    public event Action<bool, int>           OnUnitRemoved; // isPlayer, lane

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        _items = new List<CardData>[2, LANE_COUNT];
        for (int p = 0; p < 2; p++)
            for (int l = 0; l < LANE_COUNT; l++)
                _items[p, l] = new List<CardData>();
    }

    // 카드 배치 가능 여부 확인 (빈 레인)
    public bool CanPlace(bool isPlayer, int lane)
    {
        if (lane < 0 || lane >= LANE_COUNT) return false;
        return GetUnit(isPlayer, lane) == null;
    }

    // 업그레이드 가능 여부 (기존 유닛보다 높은 코스트)
    public bool CanUpgrade(bool isPlayer, int lane, CardData newCard)
    {
        if (lane < 0 || lane >= LANE_COUNT) return false;
        var existing = GetUnit(isPlayer, lane);
        if (existing == null) return false;
        if (newCard.Cost > existing.Cost) return true; // 룰북 3.5.5.1: 기본은 "더 높은 코스트"만
        // BT07-045 이브(각성): 필드의 〈이브〉를 "코스트가 그 유닛의 코스트 이하인" 〈이브〉로도 업그레이드 가능
        return EffectSystem.Instance != null
            && EffectSystem.Instance.CanEveEqualOrLowerUpgrade(isPlayer, existing, newCard);
    }

    // 업그레이드 시 코스트 체크 — 기존 유닛+아이템 코스트를 필드합에서 제외
    public bool CanPlayCardAsUpgrade(bool isPlayer, int lane, CardData newCard, int playerSize)
    {
        var existing = GetUnit(isPlayer, lane);
        if (existing == null) return false;

        // 업그레이드 대상 유닛+아이템 코스트는 합산 제외 (룰북 6.4.1.1.2.1)
        int owner = isPlayer ? 0 : 1;
        int excludeCost = existing.Cost;
        foreach (var item in _items[owner, lane]) excludeCost += item.Cost;

        int total = newCard.Cost + GetFieldCostSum(isPlayer) - excludeCost + GetSkillZoneCostSum(isPlayer);
        return total <= playerSize;
    }

    // 유닛 배치
    public bool PlaceUnit(bool isPlayer, int lane, CardData card)
    {
        if (!CanPlace(isPlayer, lane)) return false;
        // BT03 EntryDiscardLockHighCostDeploy: 이번 턴 N코스트 이상 배치 불가
        if (EffectSystem.Instance != null && !EffectSystem.Instance.CanDeployCard(isPlayer, card)) return false;

        int owner = isPlayer ? 0 : 1;
        _field[owner, lane] = card;

        Debug.Log($"[Field] {(isPlayer ? "플레이어" : "AI")} 레인{lane}에 {card.CardName} 배치");
        OnUnitPlaced?.Invoke(isPlayer, lane, card);
        return true;
    }

    // 3c 상태 복제 전용: 레인의 장착 아이템을 직접 세팅(이벤트 없음 — 뒤이어 ReplicateSetUnit이 UI 갱신).
    public void ReplicateSetItems(bool isPlayer, int lane, List<CardData> items)
    {
        int owner = isPlayer ? 0 : 1;
        _items[owner, lane].Clear();
        if (items != null) _items[owner, lane].AddRange(items);
    }

    // 3c 상태 복제 전용: 필드를 직접 세팅(효과·트래시·검증 없이 UI 이벤트만 발생).
    //  · 온라인 클라가 호스트가 보낸 필드 상태를 화면에 반영할 때만 사용.
    public void ReplicateSetUnit(bool isPlayer, int lane, CardData card)
    {
        int owner = isPlayer ? 0 : 1;
        _field[owner, lane] = card;
        if (card == null) OnUnitRemoved?.Invoke(isPlayer, lane);
        else              OnUnitPlaced?.Invoke(isPlayer, lane, card);
    }

    // 사이즈 체크 없이 강제 배치 (효과에 의한 사이즈 무시 배치용)
    public bool ForcePlace(bool isPlayer, int lane, CardData card)
    {
        int owner = isPlayer ? 0 : 1;
        if (_field[owner, lane] != null) return false;
        _field[owner, lane] = card;
        Debug.Log($"[Field 강제배치] {(isPlayer ? "플레이어" : "AI")} 레인{lane}에 {card.CardName}");
        OnUnitPlaced?.Invoke(isPlayer, lane, card);
        return true;
    }

    // 업그레이드 배치 — 기존 유닛을 트래시(엑시트 효과 없음)하고 새 유닛 배치
    public bool UpgradeUnit(bool isPlayer, int lane, CardData newCard)
    {
        if (!CanUpgrade(isPlayer, lane, newCard)) return false;
        return ForceUpgradeUnit(isPlayer, lane, newCard);
    }

    // BT07 이브: 사이즈/코스트 조건을 완전히 무시하는 업그레이드 (기존 유닛이 반드시 존재해야 함)
    public bool ForceUpgradeUnit(bool isPlayer, int lane, CardData newCard)
    {
        int owner = isPlayer ? 0 : 1;
        var pc = isPlayer ? PlayerController.PlayerInstance : PlayerController.AIInstance;
        var old = _field[owner, lane];
        if (old == null) return false;

        // 아이템 트래시 (효과에 의한 트래시 아님)
        foreach (var item in _items[owner, lane])
        {
            pc.AddToTrash(item);
            Debug.Log($"[업그레이드] 아이템 {item.CardName} 트래시");
        }
        _items[owner, lane].Clear();

        // 기존 유닛 트래시 (엑시트 효과 발동하지 않음 — 룰북 10.1.7.2.2)
        pc.AddToTrash(old);
        _field[owner, lane] = null;
        // 업그레이드는 "다른 유닛"으로 교체하는 것 — 기존 유닛의 버프·부여를 새 유닛이 물려받으면 안 된다.
        // 엑시트가 아예 발동하지 않는 경로라 엑시트 부여까지 전부 정리(keepExitGrants: false).
        EffectSystem.Instance?.ClearLaneState(isPlayer, lane, keepExitGrants: false);
        OnUnitRemoved?.Invoke(isPlayer, lane);
        Debug.Log($"[업그레이드] {old.CardName} → {newCard.CardName} (엑시트 효과 없음)");

        // 새 유닛 배치
        _field[owner, lane] = newCard;
        OnUnitPlaced?.Invoke(isPlayer, lane, newCard);
        EffectSystem.Instance?.OnUnitUpgraded(isPlayer, lane, old, newCard); // BT07 스위치〈이브〉 감지용
        return true;
    }

    // BT07 이동(룰북 4.9): 인접한 자신의 유닛 존으로 유닛 이동. 목적지에 유닛이 있으면 서로 자리를 바꾼다(스왑).
    // 장착 아이템은 유닛을 따라간다. 성공 시 true.
    public bool MoveUnit(bool isPlayer, int fromLane, int toLane)
    {
        if (fromLane == toLane || Mathf.Abs(fromLane - toLane) != 1) return false;
        int owner = isPlayer ? 0 : 1;
        var moving = _field[owner, fromLane];
        if (moving == null) return false;

        var destUnit = _field[owner, toLane];
        var movingItems = _items[owner, fromLane];
        var destItems = _items[owner, toLane];

        _field[owner, toLane] = moving;
        _field[owner, fromLane] = destUnit; // 유닛 없으면 null — 정상 이동
        _items[owner, toLane] = movingItems;
        _items[owner, fromLane] = destItems ?? new List<CardData>();
        // 버프·부여 키가 레인 기준이라 그대로 두면 이동한 유닛이 버프를 두고 간다
        // (그리고 스왑된 상대 유닛이 그 버프를 가져간다). 아이템처럼 유닛을 따라가게 맞바꾼다.
        EffectSystem.Instance?.MoveLaneState(isPlayer, fromLane, toLane);

        Debug.Log($"[이동] {moving.CardName} 레인{fromLane}→{toLane}" + (destUnit != null ? $" ({destUnit.CardName}과 스왑)" : ""));
        EffectSystem.Instance?.NotifyUnitMoved(isPlayer, fromLane, toLane, moving);
        if (destUnit != null)
            EffectSystem.Instance?.NotifyUnitMoved(isPlayer, toLane, fromLane, destUnit); // 룰북 4.9.2.1: 스왑된 유닛도 "이동한 유닛"
        return true;
    }

    // 유닛 제거 — 장착 아이템 전부 함께 트래시
    //
    // willResolveExit = "이 호출 직후에 OnUnitTrashed를 부를 것인가?"
    //   true  → 엑시트 부여(_grantedExit 등)를 남겨둔다. OnUnitTrashed가 그걸 읽고 발동시킨 뒤
    //           스스로(ClearExitGrants) 정리한다.
    //   false → 지금 여기서 함께 정리한다. 바운스·덱 반환·대미지존행처럼 엑시트가 아예 돌지 않는
    //           경로에서 남겨두면, 같은 턴에 이 레인을 다시 채운 새 유닛이 이전 유닛의
    //           부여 엑시트를 물려받는다.
    // ★ 기본값 false = 안전한 쪽. 엑시트를 해결하는 호출부만 true를 넘긴다.
    public void RemoveUnit(bool isPlayer, int lane, bool willResolveExit = false)
    {
        int owner = isPlayer ? 0 : 1;
        var pc = isPlayer ? PlayerController.PlayerInstance : PlayerController.AIInstance;
        foreach (var item in _items[owner, lane])
        {
            pc.AddToTrash(item);
            Debug.Log($"[아이템] {item.CardName} 유닛과 함께 트래시");
        }
        _items[owner, lane].Clear();
        _field[owner, lane] = null;
        // 이 자리에 걸려 있던 버프·부여를 정리 — 안 하면 같은 턴에 이 레인을 다시 채운
        // 유닛이 이전 유닛의 버프를 물려받는다. (엑시트 부여는 위 willResolveExit 설명 참고)
        EffectSystem.Instance?.ClearLaneState(isPlayer, lane, keepExitGrants: willResolveExit);
        OnUnitRemoved?.Invoke(isPlayer, lane);
    }

    // 아이템 장착
    public bool EquipItem(bool isPlayer, int lane, CardData item)
    {
        int owner = isPlayer ? 0 : 1;
        var unit = _field[owner, lane];
        if (unit == null) return false;
        if (item.MinCostToEquip > 0 && unit.Cost < item.MinCostToEquip)
        {
            Debug.Log($"[아이템] 장착 조건 불만족: {unit.CardName}(코스트{unit.Cost}) < 필요{item.MinCostToEquip}");
            return false;
        }
        // BT05 MaxCostToEquip:N / BT06 EquipRequiresMaxCost:N — 유닛 코스트가 N 초과면 장착 불가
        foreach (var et in item.EffectTypes)
        {
            string prefix = et.StartsWith("MaxCostToEquip:") ? "MaxCostToEquip:"
                          : et.StartsWith("EquipRequiresMaxCost:") ? "EquipRequiresMaxCost:"
                          : null;
            if (prefix == null) continue;
            if (int.TryParse(et.Substring(prefix.Length), out int maxc) && unit.Cost > maxc)
            {
                Debug.Log($"[아이템] 장착 조건 불만족: {unit.CardName}(코스트{unit.Cost}) > 최대{maxc}");
                return false;
            }
        }
        // ST05 레어 메탈 건틀렛 등: "EquipRequiresKeyword:키워드" — 해당 키워드 가진 유닛만 장착 가능
        foreach (var et in item.EffectTypes)
        {
            if (!et.StartsWith("EquipRequiresKeyword:")) continue;
            string keyword = et.Substring("EquipRequiresKeyword:".Length);
            if (!unit.Keywords.Contains(keyword))
            {
                Debug.Log($"[아이템] 장착 조건 불만족: {unit.CardName}은(는) [{keyword}] 키워드 없음");
                return false;
            }
        }
        // BT04 EquipRequiresFaction:소속 — 해당 소속 유닛만 장착 가능
        foreach (var et in item.EffectTypes)
        {
            if (!et.StartsWith("EquipRequiresFaction:")) continue;
            string faction = et.Substring("EquipRequiresFaction:".Length);
            bool ok = (unit.Faction != null && unit.Faction.Contains(faction)) || unit.Keywords.Contains(faction);
            if (!ok)
            {
                Debug.Log($"[아이템] 장착 조건 불만족: {unit.CardName}은(는) 《{faction}》 아님");
                return false;
            }
        }
        // BT07 EquipRequiresName:이브 — 「이브」로 취급되는 유닛만 장착 가능
        foreach (var et in item.EffectTypes)
        {
            if (!et.StartsWith("EquipRequiresName:")) continue;
            string reqName = et.Substring("EquipRequiresName:".Length);
            bool ok = reqName == "이브" && EffectSystem.Instance != null && EffectSystem.Instance.IsEveCard(unit);
            if (!ok)
            {
                Debug.Log($"[아이템] 장착 조건 불만족: {unit.CardName}은(는) 《{reqName}》 아님");
                return false;
            }
        }
        // SB02 EquipRequiresBaseHit:N — 유닛의 기본 히트가 N 미만이면 장착 불가
        foreach (var et in item.EffectTypes)
        {
            if (!et.StartsWith("EquipRequiresBaseHit:")) continue;
            if (int.TryParse(et.Substring("EquipRequiresBaseHit:".Length), out int minh) && unit.Hit < minh)
            {
                Debug.Log($"[아이템] 장착 조건 불만족: {unit.CardName}(히트{unit.Hit}) < {minh}");
                return false;
            }
        }
        // BT03 PassiveItemEquipRestriction: 상대 유닛이 이 패시브를 가지면 장착 불가
        if (EffectSystem.Instance != null && EffectSystem.Instance.IsItemEquipRestricted(isPlayer))
        {
            Debug.Log($"[아이템] PassiveItemEquipRestriction — {unit.CardName}에 {item.CardName} 장착 불가");
            return false;
        }
        _items[owner, lane].Add(item);
        Debug.Log($"[아이템] {unit.CardName}에 {item.CardName} 장착 (총 {_items[owner,lane].Count}개)");
        EffectSystem.Instance?.OnItemEquipped(isPlayer, lane, item);
        return true;
    }

    public List<CardData> GetEquippedItems(bool isPlayer, int lane)
        => _items[isPlayer ? 0 : 1, lane];

    // 특정 아이템 1장 제거 (언장착 — 트래시에 추가하지 않음)
    public void UnequipItem(bool isPlayer, int lane, CardData item)
    {
        _items[isPlayer ? 0 : 1, lane].Remove(item);
    }

    // 하위 호환 — 첫 번째 아이템 반환 (단일 아이템 참조가 필요한 곳)
    public CardData GetEquippedItem(bool isPlayer, int lane)
    {
        var list = _items[isPlayer ? 0 : 1, lane];
        return list.Count > 0 ? list[0] : null;
    }

    // 특정 레인의 유닛 가져오기
    public CardData GetUnit(bool isPlayer, int lane)
    {
        int owner = isPlayer ? 0 : 1;
        return _field[owner, lane];
    }

    // 조우 가능한 레인 확인 (양쪽 모두 유닛 있음)
    public bool HasEncounter(int lane)
    {
        return GetUnit(true, lane) != null && GetUnit(false, lane) != null;
    }

    // 공격 가능한 레인 확인 (내 유닛 있고 상대 유닛 없음 = 다이렉트 어택 가능)
    public bool IsDirectAttackLane(bool isPlayer, int lane)
    {
        return GetUnit(isPlayer, lane) != null && GetUnit(!isPlayer, lane) == null;
    }

    // 룰북 6.4.1.1.2: 필드에 있는 모든 카드의 코스트 합
    public int GetFieldCostSum(bool isPlayer)
    {
        int sum = 0;
        for (int i = 0; i < LANE_COUNT; i++)
        {
            CardData unit = GetUnit(isPlayer, i);
            if (unit != null) sum += unit.Cost;
        }
        return sum;
    }

    // 카드를 낼 수 있는지 (카드 코스트 + 필드 유닛 코스트 합 + 스킬존 코스트 합 ≤ 사이즈)
    public bool CanPlayCard(bool isPlayer, CardData card, int playerSize)
    {
        return card.Cost + GetFieldCostSum(isPlayer) + GetSkillZoneCostSum(isPlayer) <= playerSize;
    }

    public int GetSkillZoneCostSum(bool isPlayer)
    {
        var owner = isPlayer ? PlayerController.PlayerInstance : PlayerController.AIInstance;
        int sum = 0;
        foreach (var c in owner.SkillZone)
            // BT06: 스킬존 카드가 이번 턴 0코스트화되었다면 사이즈 계산에서 제외
            sum += (EffectSystem.Instance != null && EffectSystem.Instance.IsSkillZeroCostGranted(c)) ? 0 : c.Cost;
        return sum;
    }

    // 플레이어의 전체 배치 현황
    public void DebugPrintField()
    {
        for (int lane = 0; lane < LANE_COUNT; lane++)
        {
            var playerUnit = GetUnit(true, lane);
            var aiUnit     = GetUnit(false, lane);
            Debug.Log($"레인{lane} | 플레이어: {playerUnit?.CardName ?? "없음"} | AI: {aiUnit?.CardName ?? "없음"}");
        }
    }
}