// Assets/Scripts/Managers/CardLookup.cs
// ─────────────────────────────────────────────────────────────────────────
// CardId → CardData 조회 (네트워크로 받은 카드ID를 실제 에셋으로 복원).
//  · 모든 카드는 Resources/Cards 에셋이라, 어느 클라에서든 CardId로 되찾을 수 있다.
//  · 원격 선택 팝업 표시 등에 사용. 최초 1회 캐시.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;

public static class CardLookup
{
    private static Dictionary<string, CardData> _byId;

    private static void EnsureLoaded()
    {
        if (_byId != null) return;
        _byId = new Dictionary<string, CardData>();
        foreach (var c in Resources.LoadAll<CardData>("Cards"))
            if (c != null && !_byId.ContainsKey(c.CardId)) _byId[c.CardId] = c;
    }

    public static CardData ById(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return null;
        EnsureLoaded();
        return _byId.TryGetValue(cardId, out var c) ? c : null;
    }
}
