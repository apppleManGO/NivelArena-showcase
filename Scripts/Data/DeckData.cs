// Assets/Scripts/Data/DeckData.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDeck", menuName = "NivelArena/DeckData")]
public class DeckData : ScriptableObject
{
    [Header("덱 정보")]
    public string DeckId;
    public string DeckName;

    [Header("구성")]
    public LeaderData Leader;
    public List<CardData> Cards = new();  // 40장 (동일 에셋이 count만큼 중복 포함)

    public bool IsValid(out string errorMessage)
    {
        if (Leader == null)
        {
            errorMessage = "리더 카드가 없습니다.";
            return false;
        }
        if (Cards.Count != 40)
        {
            errorMessage = $"덱은 40장이어야 합니다. (현재 {Cards.Count}장)";
            return false;
        }

        var grouped = Cards.GroupBy(c => c.CardId);
        foreach (var group in grouped)
        {
            int limit = group.First().IsTrigger ? 8 : 3;
            if (group.Count() > limit)
            {
                errorMessage = $"'{group.Key}' 카드가 {limit}장 초과입니다.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    public List<CardData> GetShuffledDeck()
    {
        var shuffled = new List<CardData>(Cards);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return shuffled;
    }
}
