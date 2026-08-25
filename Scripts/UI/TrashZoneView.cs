// Assets/Scripts/UI/TrashZoneView.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 트래시 존 카드 목록을 보여주는 팝업. PlayerTrashText/AITrashText 클릭 시 열림(TrashZoneButton 참고).
// 카드는 CardLongPressZoom이 붙어서 1초 누르면 CardZoomView로 확대됨.
public class TrashZoneView : MonoBehaviour
{
    public static TrashZoneView Instance { get; private set; }

    [Header("UI")]
    public Transform  CardContainer;
    public GameObject CardSlotPrefab;
    public Button     CloseButton;

    [Header("레이어")]
    public Canvas RootCanvas; // Inspector에서 씬의 최상위 Canvas 연결

    private readonly List<GameObject> _slots = new();
    private Canvas _canvas;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (CloseButton != null)
            CloseButton.onClick.AddListener(Close);

        // Canvas를 Awake에서 미리 세팅 (DefensePopup과 동일 방식)
        _canvas = GetComponent<Canvas>();
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.overrideSorting = true;
        _canvas.sortingOrder    = 200;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        gameObject.SetActive(false);
    }

    public void Show(bool isPlayer)
    {
        // 루트 Canvas 직접 자식으로 이동
        if (RootCanvas != null)
        {
            transform.SetParent(RootCanvas.transform, true);
        }
        transform.SetAsLastSibling();

        PlayerController pc = isPlayer
            ? PlayerController.PlayerInstance
            : PlayerController.AIInstance;

        if (pc == null) return;

        while (_slots.Count < pc.TrashPile.Count)
            _slots.Add(Instantiate(CardSlotPrefab, CardContainer));

        while (_slots.Count > pc.TrashPile.Count)
        {
            Destroy(_slots[_slots.Count - 1]);
            _slots.RemoveAt(_slots.Count - 1);
        }

        for (int i = 0; i < pc.TrashPile.Count; i++)
        {
            CardData card = pc.TrashPile[i];
            GameObject slot = _slots[i];

            Image cardImage = slot.GetComponent<Image>();
            if (cardImage != null && card.Artwork != null)
                cardImage.sprite = card.Artwork;

            var zoom = slot.GetComponent<CardLongPressZoom>();
            if (zoom == null) zoom = slot.AddComponent<CardLongPressZoom>();
            zoom.Data = card;
        }

        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
