// Assets/Script/UI/CardView.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("UI 요소")]
    public Image CardImage; // 카드 이미지 하나만

    public CardData Data { get; private set; }

    private RectTransform _rect;
    private Canvas        _canvas;
    private CanvasGroup   _canvasGroup;
    private Transform     _originalParent;
    private Vector3       _originalPosition;
    private int           _originalSiblingIndex;

    private void Awake()
    {
        _rect        = GetComponent<RectTransform>();

        // CanvasGroup 없으면 자동 추가
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // CardImage 자동 연결 (Inspector에서 미연결 시)
        if (CardImage == null)
            CardImage = GetComponent<Image>();
    }

    // Canvas는 Instantiate 후 부모가 설정된 뒤 찾기
    private Canvas GetCanvas()
    {
        if (_canvas != null) return _canvas;
        _canvas = GetComponentInParent<Canvas>();
        return _canvas;
    }

    public void Setup(CardData data)
    {
        Data = data;

        if (CardImage == null)
            CardImage = GetComponent<Image>();

        if (CardImage != null && data.Artwork != null)
            CardImage.sprite = data.Artwork;
    }

    private Coroutine _holdCoroutine;
    private const float HoldDuration = 1f;

    public void OnPointerDown(PointerEventData eventData)
    {
        CancelHold();
        _holdCoroutine = StartCoroutine(HoldTimer());
    }

    public void OnPointerUp(PointerEventData eventData)   => CancelHold();
    public void OnPointerExit(PointerEventData eventData) => CancelHold();

    private void CancelHold()
    {
        if (_holdCoroutine != null)
        {
            StopCoroutine(_holdCoroutine);
            _holdCoroutine = null;
        }
    }

    private IEnumerator HoldTimer()
    {
        yield return new WaitForSeconds(HoldDuration);
        _holdCoroutine = null;
        if (Data != null)
            CardZoomView.Instance?.Show(Data);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        CancelHold(); // 드래그 시작하면 길게 누르기 취소

        if (!CanDrag()) return;

        _originalParent       = transform.parent;
        _originalPosition     = transform.localPosition;
        _originalSiblingIndex = transform.GetSiblingIndex();

        Canvas canvas = GetCanvas();
        if (canvas == null) return;

        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;
        if (_canvasGroup != null) _canvasGroup.alpha = 0.8f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!CanDrag()) return;
        Canvas canvas = GetCanvas();
        if (canvas == null) return;
        _rect.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.alpha = 1f;
        if (_canvas != null && transform.parent == _canvas.transform)
            ReturnToHand();
    }

    public void ReturnToHand()
    {
        transform.SetParent(_originalParent);
        transform.SetSiblingIndex(_originalSiblingIndex);
        transform.localPosition = _originalPosition;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging) return;
        // 클릭 시 카드 확대 표시 (추후 구현)
        Debug.Log($"[카드 클릭] {Data.CardName}");
    }

    private bool CanDrag() =>
        TurnManager.Instance.IsPlayerTurn &&
        TurnManager.Instance.CurrentPhase == PhaseType.Main;
}