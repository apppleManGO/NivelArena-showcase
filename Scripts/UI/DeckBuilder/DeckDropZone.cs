// Assets/Scripts/UI/DeckBuilder/DeckDropZone.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 중앙 덱 패널 위에 붙여두면 드래그한 카드를 드롭해서 덱에 추가
public class DeckDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image _highlightImage;

    DeckBuilderManager _manager;

    void Awake()
    {
        _manager = FindFirstObjectByType<DeckBuilderManager>();
        if (_highlightImage != null) _highlightImage.enabled = false;
    }

    public void OnDrop(PointerEventData e)
    {
        if (_highlightImage != null) _highlightImage.enabled = false;

        var drag = e.pointerDrag?.GetComponent<CardDragHandler>();
        if (drag == null) return;

        var card = drag.GetCardData();
        if (card != null) _manager?.AddCard(card);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (e.dragging && _highlightImage != null)
            _highlightImage.enabled = true;
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_highlightImage != null) _highlightImage.enabled = false;
    }
}
