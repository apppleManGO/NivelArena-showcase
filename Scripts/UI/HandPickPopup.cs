// Assets/Scripts/UI/HandPickPopup.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 패에서 카드 1장을 선택하는 팝업. 멀리건(Yes/No) 모드도 겸함.
public class HandPickPopup : MonoBehaviour
{
    public static HandPickPopup Instance { get; private set; }

    [Header("UI")]
    public TMP_Text  TitleText;
    public Transform CardContainer;
    public Button    CancelButton;   // 카드 선택 모드: 취소 / 멀리건 모드: 유지
    public Button    ConfirmButton;  // 멀리건 모드 전용: 교체 (평소 비활성)

    [Header("프리팹")]
    public GameObject CardButtonPrefab;

    [Header("레이어")]
    public Canvas RootCanvas;

    private Action<CardData> _callback;
    private Action           _onCancel;
    private Action<bool>     _mulliganCallback;
    private readonly List<GameObject> _items = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = 200;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        CancelButton?.onClick.AddListener(OnCancel);
        ConfirmButton?.onClick.AddListener(OnMulliganConfirm);
        if (ConfirmButton != null) ConfirmButton.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    public void Show(string title, List<CardData> hand, Action<CardData> callback, Action onCancel = null)
    {
        // 루트 Canvas 직접 자식으로 이동
        if (RootCanvas != null)
        {
            transform.SetParent(RootCanvas.transform, true);
        }
        transform.SetAsLastSibling();

        _callback = callback;
        _onCancel = onCancel;

        if (TitleText != null) TitleText.text = title;

        foreach (var go in _items) Destroy(go);
        _items.Clear();

        foreach (var card in hand)
        {
            var go = Instantiate(CardButtonPrefab, CardContainer);
            _items.Add(go);

            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(100, 140);

            var cardImageTransform = go.transform.Find("CardImage");
            if (cardImageTransform != null)
            {
                var img = cardImageTransform.GetComponent<Image>();
                if (img != null && card.Artwork != null)
                    img.sprite = card.Artwork;
            }

            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null)
                txt.text = $"{card.CardName}\n(Cost {card.Cost})";

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            var captured = card;
            btn.onClick.AddListener(() => OnPicked(captured));
        }

        gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    // 멀리건 모드: 카드 표시 전용 + 유지/교체 버튼
    public void ShowMulligan(string playerName, List<CardData> hand, Action<bool> callback)
    {
        if (RootCanvas != null) transform.SetParent(RootCanvas.transform, true);
        transform.SetAsLastSibling();

        _mulliganCallback = callback;
        _callback = null;
        _onCancel = null;

        if (TitleText != null)
            TitleText.text = $"{playerName}의 초기 패\n교체하시겠습니까?";

        if (CancelButton != null)
        {
            var txt = CancelButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "유지";
        }
        if (ConfirmButton != null)
        {
            ConfirmButton.gameObject.SetActive(true);
            var txt = ConfirmButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "교체";
        }

        foreach (var go in _items) Destroy(go);
        _items.Clear();

        foreach (var card in hand)
        {
            var go = Instantiate(CardButtonPrefab, CardContainer);
            _items.Add(go);

            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(100, 140);

            var cardImageTransform = go.transform.Find("CardImage");
            if (cardImageTransform != null)
            {
                var img = cardImageTransform.GetComponent<Image>();
                if (img != null && card.Artwork != null)
                    img.sprite = card.Artwork;
            }

            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = $"{card.CardName}\n(Cost {card.Cost})";

            // 멀리건 모드에서는 카드 클릭 비활성
            var btn = go.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }

        gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    private void OnMulliganConfirm()
    {
        var cb = _mulliganCallback;
        CloseMulligan();
        cb?.Invoke(true);
    }

    private void OnPicked(CardData card)
    {
        Close();
        _callback?.Invoke(card);
    }

    private void OnCancel()
    {
        if (_mulliganCallback != null)
        {
            var cb = _mulliganCallback;
            CloseMulligan();
            cb.Invoke(false);
            return;
        }
        Close();
        _onCancel?.Invoke();
    }

    private void CloseMulligan()
    {
        _mulliganCallback = null;
        if (ConfirmButton != null) ConfirmButton.gameObject.SetActive(false);
        if (CancelButton != null)
        {
            var txt = CancelButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "취소";
        }
        gameObject.SetActive(false);
        Time.timeScale = 1f;
        foreach (var go in _items) Destroy(go);
        _items.Clear();
    }

    private void Close()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f;
        foreach (var go in _items) Destroy(go);
        _items.Clear();
    }
}
