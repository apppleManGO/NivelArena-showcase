// Assets/Scripts/Managers/EffectActions.cs
// 효과 원자 액션(블록) 라이브러리
//
// 카드 효과는 이 블록들을 조립해서 구현한다 (EffectSystem의 case당 1~5줄 목표).
// 규칙:
//  - 선택(UI)이 필요한 블록은 콜백(then/onDone)을 받는다.
//    플레이어 = HandPickPopup/레인 타겟팅 UI, AI = 자동 선택이 블록 안에 내장되어 있어
//    호출부에서 인간/AI를 구분할 필요가 없다.
//  - 동기 블록(드로우, 트래시, 버프 등)은 즉시 실행하고 UI 갱신까지 책임진다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 버프 지속 기간
public enum BoostUntil
{
    EndOfTurn,          // 이 턴이 끝날 때까지
    EndOfOpponentTurn,  // 상대의 턴이 끝날 때까지
    EndOfAttack         // 이 공격이 끝날 때까지
}

// CardZone(카드 영역) enum은 Enums/CardType.cs에 정의되어 있음

public static class EffectActions
{
    private static PlayerController Owner(bool isPlayer)
        => isPlayer ? PlayerController.PlayerInstance : PlayerController.AIInstance;

    public static void RefreshUI()
    {
        HandView.Instance?.RefreshHand();
        HUDView.NotifyStatusChanged();
        LaneSlot.RefreshAllLanes();
    }

    // ── 자원 블록 (동기) ─────────────────────────────────────────

    // 드로우N
    public static void Draw(bool isPlayer, int count)
    {
        if (count <= 0) return;
        Owner(isPlayer).DrawCard(count);
        RefreshUI();
        Debug.Log($"[블록] {(isPlayer ? "플레이어" : "AI")} 드로우{count}");
        EffectSystem.Instance?.NotifyEffectDraw(isPlayer, count); // BT06 풀 파티 세헤라자드: 상대 비트리거 드로우 감지
    }

    // 리더 레벨+N
    public static void LevelUp(bool isPlayer, int count = 1)
    {
        for (int i = 0; i < count; i++)
            Owner(isPlayer).LevelUpLeader();
        RefreshUI();
    }

    // 상대에게 N대미지 (isPlayer = 효과 발동자)
    public static void DamageOpponent(bool isPlayer, int amount)
    {
        if (amount <= 0) return;
        Owner(!isPlayer).TakeDamage(amount);
        EffectSystem.Instance.NotifyEffectDamage(isPlayer); // 효과 대미지 훅 (몽환 나비 등)
        RefreshUI();
        Debug.Log($"[블록] {(isPlayer ? "플레이어" : "AI")} → 상대에게 {amount}대미지");
    }

    // 덱 위 N장 트래시 (밀). 트래시된 카드 목록 반환
    public static List<CardData> Mill(bool isPlayer, int count)
    {
        var owner = Owner(isPlayer);
        var milled = new List<CardData>();
        for (int i = 0; i < count && owner.DrawPile.Count > 0; i++)
        {
            var c = owner.DrawPile[0];
            owner.RemoveFromDeckAt(0);
            owner.AddToTrash(c);
            milled.Add(c);
        }
        RefreshUI();
        Debug.Log($"[블록] 덱 위 {milled.Count}장 트래시");
        return milled;
    }

    // 카드를 한 영역에서 다른 영역으로 이동 (회수/덱밑/대미지존 등 만능 프리미티브)
    public static void MoveCard(bool isPlayer, CardData card, CardZone from, CardZone to)
    {
        var o = Owner(isPlayer);

        bool removed = from switch
        {
            CardZone.Hand                       => o.RemoveFromHand(card),
            CardZone.DeckTop or CardZone.DeckBottom => o.RemoveFromDeck(card),
            CardZone.Trash                      => o.RemoveFromTrash(card),
            CardZone.Damage                     => o.RemoveFromDamageZone(card),
            _ => false
        };
        if (!removed)
        {
            Debug.LogWarning($"[블록] MoveCard: {card.CardName}이(가) {from}에 없음");
            return;
        }

        switch (to)
        {
            case CardZone.Hand:       o.AddToHand(card); break;
            case CardZone.DeckTop:    o.AddToDeckTop(card); break;
            case CardZone.DeckBottom: o.AddToDeckBottom(card); break;
            case CardZone.Trash:      o.AddToTrash(card); break;
            case CardZone.Damage:     o.AddToDamageZone(card); break;
        }
        RefreshUI();
        Debug.Log($"[블록] {card.CardName}: {from} → {to}");
    }

    // ── 카드 선택 블록 (비동기 — 콜백) ────────────────────────────

    // 후보 중 1장 선택. 플레이어=팝업, AI=aiPick(기본: 첫 장).
    // 후보가 없으면 then을 호출하지 않는다. onCancel: 플레이어가 취소했을 때 (AI는 취소 없음)
    public static void PickFromCards(bool isPlayer, string title, List<CardData> candidates,
        Action<CardData> then, Func<List<CardData>, CardData> aiPick = null, Action onCancel = null)
    {
        // 2단계: 선택 소유권 결정을 ChoiceBroker 한 곳으로 (동작 동일 — 로컬 팝업 / AI 자동)
        ChoiceBroker.PickCard(isPlayer, title, candidates, then, aiPick, onCancel);
    }

    // 패에서 N장 순차 선택 트래시 (강제). 끝나면 onDone(실제 트래시한 장수).
    // AI는 랜덤 트래시.
    public static void DiscardHand(bool isPlayer, int count, Action<int> onDone = null)
        => DiscardInternal(isPlayer, count, 0, optional: false, onDone);

    // 패에서 N장 트래시 "할 수 있다" (선택적 — 플레이어는 취소 가능, 취소 시 중단).
    // AI는 패가 있으면 항상 트래시. 끝나면 onDone(실제 트래시한 장수).
    public static void DiscardHandOptional(bool isPlayer, int count, Action<int> onDone = null)
        => DiscardInternal(isPlayer, count, 0, optional: true, onDone);

    private static void DiscardInternal(bool isPlayer, int remaining, int done, bool optional, Action<int> onDone)
    {
        var owner = Owner(isPlayer);
        if (remaining <= 0 || owner.Hand.Count == 0)
        {
            onDone?.Invoke(done);
            return;
        }

        // 2단계: 패 트래시 선택도 ChoiceBroker 경유 (로컬=팝업 / AI=랜덤). 동작 동일.
        ChoiceBroker.PickCard(isPlayer,
            optional ? $"트래시할 패를 선택하세요 (취소 가능, {remaining}장 남음)"
                     : $"트래시할 패를 선택하세요 ({remaining}장 남음)",
            new List<CardData>(owner.Hand),
            onPicked: picked =>
            {
                owner.RemoveFromHand(picked);
                owner.AddToTrash(picked);
                RefreshUI();
                DiscardInternal(isPlayer, remaining - 1, done + 1, optional, onDone);
            },
            autoPick: list => list[UnityEngine.Random.Range(0, list.Count)], // AI: 랜덤 트래시(기존과 동일)
            onCancel: optional ? () => onDone?.Invoke(done) : (Action)null);
    }

    // 트래시에서 조건에 맞는 카드 1장을 패로 회수. AI는 코스트 높은 순.
    public static void RecoverFromTrash(bool isPlayer, Func<CardData, bool> filter, string title,
        Action<CardData> then = null, Action onCancel = null)
    {
        var owner = Owner(isPlayer);
        var candidates = owner.TrashPile.Where(filter).ToList();
        PickFromCards(isPlayer, title, candidates,
            picked =>
            {
                MoveCard(isPlayer, picked, CardZone.Trash, CardZone.Hand);
                then?.Invoke(picked);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First(),
            onCancel: onCancel);
    }

    // 덱을 섞는다 (Fisher-Yates)
    public static void ShuffleDeck(bool isPlayer)
    {
        Owner(isPlayer).ShuffleDeck();
    }

    // 덱에서 조건에 맞는 카드 1장 서치 → 패. 이후 덱을 섞는다. AI는 코스트 높은 순.
    public static void SearchDeck(bool isPlayer, Func<CardData, bool> filter, string title,
        Action<CardData> then = null)
    {
        var owner = Owner(isPlayer);
        var candidates = owner.DrawPile.Where(filter).ToList();
        if (candidates.Count == 0)
        {
            Debug.Log("[블록] 서치 — 조건에 맞는 카드 없음");
            return;
        }
        PickFromCards(isPlayer, title, candidates,
            picked =>
            {
                owner.RemoveFromDeck(picked);
                owner.AddToHand(picked);
                ShuffleDeck(isPlayer);
                RefreshUI();
                Debug.Log($"[블록] 덱 서치 → {picked.CardName} 패로, 덱 셔플");
                then?.Invoke(picked);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // 덱 위 N장 공개 → 조건에 맞는 카드 1장 패로, 나머지는 모두 트래시.
    // 조건 일치가 없으면 전부 트래시.
    public static void RevealPickToHand(bool isPlayer, int revealCount, Func<CardData, bool> pickFilter, string title)
    {
        var owner = Owner(isPlayer);
        int take = Mathf.Min(revealCount, owner.DrawPile.Count);
        if (take == 0) return;

        var revealed = owner.PeekDeckTop(take);
        owner.RemoveDeckTop(take);

        var pickable = revealed.Where(pickFilter).ToList();
        if (pickable.Count == 0)
        {
            owner.AddToTrash(revealed);
            RefreshUI();
            Debug.Log($"[블록] 덱 {take}장 공개 — 조건 일치 없음, 전부 트래시");
            return;
        }

        PickFromCards(isPlayer, title, pickable,
            picked =>
            {
                revealed.Remove(picked);
                owner.AddToHand(picked);
                owner.AddToTrash(revealed);
                RefreshUI();
                Debug.Log($"[블록] 공개 {take}장 중 {picked.CardName} 패로, 나머지 {revealed.Count}장 트래시");
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // 필드 유닛 1장을 선택 (카드 팝업 방식 — 레인 클릭 UI 불필요).
    // targetIsPlayer: 어느 쪽 필드에서 고를지. then(lane)으로 레인 번호 전달.
    // AI는 aiPick(기본: 파워 높은 순).
    public static void SelectUnit(bool pickerIsPlayer, bool targetIsPlayer, Func<int, CardData, bool> filter,
        string title, Action<int> then, Func<List<int>, int> aiPick = null, Action onCancel = null)
    {
        var lanes = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(targetIsPlayer, i);
            if (u != null && filter(i, u)) lanes.Add(i);
        }
        if (lanes.Count == 0) return;
        if (lanes.Count == 1) { then(lanes[0]); return; }

        if (!pickerIsPlayer)
        {
            then(aiPick != null
                ? aiPick(lanes)
                : lanes.OrderByDescending(l => FieldManager.Instance.GetUnit(targetIsPlayer, l).AttackPower).First());
            return;
        }

        // 플레이어: 후보 유닛 카드를 팝업으로 보여주고 선택 → 레인으로 역매핑
        var cards = lanes.Select(l => FieldManager.Instance.GetUnit(targetIsPlayer, l)).ToList();
        PickFromCards(true, title, cards, picked =>
        {
            foreach (int l in lanes)
                if (FieldManager.Instance.GetUnit(targetIsPlayer, l) == picked)
                {
                    then(l);
                    return;
                }
        }, onCancel: onCancel);
    }

    // ── 필드 블록 ────────────────────────────────────────────────

    // 해당 레인 유닛 트래시 (엑시트 효과 발동 포함). 트래시한 유닛 반환.
    public static CardData TrashUnit(bool targetIsPlayer, int lane)
    {
        var unit = FieldManager.Instance.GetUnit(targetIsPlayer, lane);
        if (unit == null) return null;

        // SB01-020: 효과로 트래시될 때 패1 트래시로 생존 가능 (근사: 원인 무관 전체 효과-트래시 대상)
        if (EffectSystem.Instance.TryEffectTrashSurvive(targetIsPlayer, lane, unit)) return null;

        var equippedItems = new List<CardData>(FieldManager.Instance.GetEquippedItems(targetIsPlayer, lane));
        FieldManager.Instance.RemoveUnit(targetIsPlayer, lane, willResolveExit: true);
        Owner(targetIsPlayer).AddToTrash(unit);
        EffectSystem.Instance.OnUnitTrashed(targetIsPlayer, lane, unit);
        EffectSystem.Instance.OnItemsTrashedWithUnit(targetIsPlayer, equippedItems, unit); // 장착 아이템 엑시트(ST09 생사부, ST06 흑요석 반지 등)
        RefreshUI();
        Debug.Log($"[블록] {unit.CardName} 트래시 (레인{lane})");
        return unit;
    }

    // 해당 레인 유닛+장착 아이템을 주인의 패로 (바운스)
    public static CardData BounceUnit(bool targetIsPlayer, int lane)
    {
        var unit = FieldManager.Instance.GetUnit(targetIsPlayer, lane);
        if (unit == null) return null;

        var items = new List<CardData>(FieldManager.Instance.GetEquippedItems(targetIsPlayer, lane));
        FieldManager.Instance.RemoveUnit(targetIsPlayer, lane);
        var owner = Owner(targetIsPlayer);
        owner.AddToHand(unit);
        foreach (var item in items)
            owner.AddToHand(item);
        RefreshUI();
        Debug.Log($"[블록] {unit.CardName}(+아이템 {items.Count}개) 패로 복귀");
        return unit;
    }

    // ── 레인 선택 블록 (비동기 — 콜백) ────────────────────────────

    // 조건에 맞는 레인 선택. 후보 1개면 즉시, 플레이어+복수면 클릭 선택 UI, AI는 aiPick(기본: 첫 후보).
    // 후보가 없으면 then을 호출하지 않는다.
    public static void SelectLane(bool pickerIsPlayer, Func<int, bool> eligible, string guide,
        Action<int> then,
        SkillZoneSlot.TargetMode mode = SkillZoneSlot.TargetMode.EnemyLane,
        Func<List<int>, int> aiPick = null)
    {
        var candidates = Enumerable.Range(0, 3).Where(eligible).ToList();
        if (candidates.Count == 0) return;

        if (!pickerIsPlayer)
        {
            then((aiPick ?? (list => list[0]))(candidates));
            return;
        }
        if (candidates.Count == 1)
        {
            then(candidates[0]);
            return;
        }
        SkillZoneSlot.EnterTriggerTargeting(mode, -1, then, guide, triggerOwnerIsPlayer: pickerIsPlayer);
    }

    // ── 스탯 버프 블록 (동기) ────────────────────────────────────

    // 해당 레인 유닛에게 파워/히트 버프를 기간 지정으로 부여 (음수 = 디버프)
    public static void Buff(bool targetIsPlayer, int lane, int power, int hit, BoostUntil until)
    {
        EffectSystem.Instance.AddBoost(targetIsPlayer, lane, power, hit, until);
        HUDView.NotifyStatusChanged();
        Debug.Log($"[블록] 레인{lane} 파워{power:+#;-#;+0}/히트{hit:+#;-#;+0} ({until})");
    }
}
