// Assets/Scripts/Data/DeckValidator.cs
// ─────────────────────────────────────────────────────────────────────────
// 덱 합법성 검증 — 게임 시작 시(특히 온라인 호스트) 덱이 룰을 지키는지 확인.
//  · 덱빌더 저장 시점 검증(Validate)은 JSON 직접 수정으로 우회 가능하므로,
//    "실제 대전에 들어가는 덱"을 런타임에 한 번 더 검사(치트 방지).
//  · 카드 스탯은 빌드 에셋에서 오므로 변조 불가 — 여기선 "덱 구성"의 합법성만 본다.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;

public static class DeckValidator
{
    public const int DeckSize = 40;
    public const int MaxCopies = 3; // DeckSaveLoad.MaxCopies와 동일

    // 유효하면 true. 위반 시 false + reason에 사유.
    public static bool Validate(DeckData deck, out string reason)
    {
        reason = "";
        if (deck == null) { reason = "덱이 없습니다"; return false; }
        if (deck.Leader == null) { reason = "리더가 없습니다"; return false; }

        var cards = deck.Cards;
        if (cards == null || cards.Count != DeckSize)
        {
            reason = $"덱은 정확히 {DeckSize}장이어야 합니다 (현재 {cards?.Count ?? 0}장)";
            return false;
        }

        // null 카드 방어 + 카드당 매수 집계
        var counts = new Dictionary<string, int>();
        foreach (var c in cards)
        {
            if (c == null) { reason = "덱에 잘못된 카드가 있습니다"; return false; }
            counts.TryGetValue(c.CardId, out int n);
            counts[c.CardId] = n + 1;
        }

        // 카드당 최대 3장
        foreach (var kv in counts)
            if (kv.Value > MaxCopies)
            {
                reason = $"카드 '{kv.Key}'가 {kv.Value}장입니다 (최대 {MaxCopies}장)";
                return false;
            }

        // 트리거 카드 최대 8장 (룰북 5.1.2.3)
        int triggers = cards.Count(c => c.IsTrigger);
        if (triggers > DeckSaveLoad.MaxTriggerCards)
        {
            reason = $"트리거 카드가 {triggers}장입니다 (최대 {DeckSaveLoad.MaxTriggerCards}장)";
            return false;
        }

        // 속성 서약: 리더 속성과 다른 속성 카드는 넣을 수 없음 (빈 속성=중립은 허용)
        string leaderAttr = deck.Leader.Attribute;
        if (!string.IsNullOrEmpty(leaderAttr))
            foreach (var c in cards)
                if (!string.IsNullOrEmpty(c.Attribute) && c.Attribute != leaderAttr)
                {
                    reason = $"속성 위반: '{c.CardName}'({c.Attribute})는 리더 속성({leaderAttr})과 다릅니다";
                    return false;
                }

        return true;
    }
}
