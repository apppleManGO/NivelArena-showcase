// Assets/Scripts/UI/DeckBuilder/CardDragHandler.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 카드 그리드 아이템에 붙여서 드래그앤드롭 지원
public class CardDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    CardListItem       _item;
    DeckBuilderManager _manager;

    // 드래그 중 따라다니는 고스트 이미지
    static GameObject _ghost;
    static Canvas     _rootCanvas;

    CanvasGroup _canvasGroup;

    void Awake()
    {
        _item        = GetComponent<CardListItem>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Init(DeckBuilderManager manager)
    {
        _manager = manager;
    }

    // ── 클릭: 상세 표시 ──────────────────────────────────────────
    public void OnPointerClick(PointerEventData e)
    {
        if (_item?.Data != null)
            _manager?.ShowDetail(_item.Data);
    }

    // ── 드래그 시작 ──────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData e)
    {
        if (_item?.Data == null) return;

        // 루트 캔버스 찾기
        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

        // 고스트 생성
        _ghost = new GameObject("DragGhost");
        _ghost.transform.SetParent(_rootCanvas.transform, false);
        _ghost.transform.SetAsLastSibling();

        var rt = _ghost.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80, 112);

        var img = _ghost.AddComponent<Image>();
        if (_item.Data.Artwork != null)
            img.sprite = _item.Data.Artwork;
        else
            img.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        var cg = _ghost.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.alpha = 0.8f;

        _canvasGroup.alpha = 0.5f;

        UpdateGhostPosition(e);
    }

    public void OnDrag(PointerEventData e)
    {
        UpdateGhostPosition(e);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (_ghost != null) { Destroy(_ghost); _ghost = null; }
        _canvasGroup.alpha = 1f;
    }

    void UpdateGhostPosition(PointerEventData e)
    {
        if (_ghost == null || _rootCanvas == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rootCanvas.transform as RectTransform,
            e.position, _rootCanvas.worldCamera,
            out Vector2 pos);
        (_ghost.transform as RectTransform).anchoredPosition = pos;
    }

    public CardData GetCardData() => _item?.Data;
}
