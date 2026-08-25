// Assets/Scripts/UI/DeckBuilder/CardListItem.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardListItem : MonoBehaviour
{
    [Header("UI")]
    public Image    CardImage;

    CardData            _data;
    DeckBuilderManager  _manager;

    public CardData Data => _data;

    public void Setup(CardData data, DeckBuilderManager manager)
    {
        _data    = data;
        _manager = manager;

        if (CardImage != null)
        {
            if (data.Artwork != null)
            {
                CardImage.sprite  = data.Artwork;
                CardImage.enabled = true;
            }
            else
            {
                CardImage.enabled = false;
            }
        }

        // 드래그 핸들러 초기화
        var drag = GetComponent<CardDragHandler>();
        if (drag != null) drag.Init(manager);

        RefreshCount();
    }

    public void RefreshCount()
    {
        if (_data == null) return;
        int cur = _manager.CountInDeck(_data.CardId);
        int max = _data.Type == CardType.Leader ? 1 : DeckSaveLoad.MaxCopies(_data.Rarity);

        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = (cur >= max) ? 0.5f : 1f;
    }

    public static Color AttributeColor(string attr) => attr switch
    {
        "화염" => new Color(0.9f, 0.3f, 0.2f),
        "대지" => new Color(0.3f, 0.7f, 0.3f),
        "파도" => new Color(0.2f, 0.5f, 0.9f),
        "폭풍" => new Color(0.6f, 0.2f, 0.8f),
        "번개" => new Color(0.9f, 0.8f, 0.1f),
        _      => Color.gray
    };
}
