// Assets/Scripts/UI/CardZoomView.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// 카드를 2초 이상 누르면 화면 왼쪽에 크게 띄우는 확대 뷰.
// 트리거 발동 시에는 ShowTimed()로 자동 닫힘.
public class CardZoomView : MonoBehaviour, IPointerClickHandler
{
    public static CardZoomView Instance { get; private set; }

    [Header("UI")]
    public Image    ZoomImage;
    public TMP_Text TriggerLabel;  // "TRIGGER!" 라벨
    public TMP_Text CardNameText;  // 카드 이름
    public TMP_Text CardCostText;  // 코스트
    public TMP_Text CardInfoText;  // 파워/히트 등 수치
    public TMP_Text CardEffectText; // 효과 텍스트

    private Coroutine _autoCloseCoroutine;

    private bool _canvasReady;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        gameObject.SetActive(false);
    }

    private void ReparentToRootCanvas()
    {
        var root = GetComponentInParent<Canvas>();
        if (root == null) return;
        while (root.transform.parent != null &&
               root.transform.parent.GetComponentInParent<Canvas>() != null)
            root = root.transform.parent.GetComponentInParent<Canvas>();
        transform.SetParent(root.transform, true);
        transform.SetAsLastSibling();
    }

    private void EnsureTopCanvas()
    {
        if (_canvasReady) return;
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = 199;
        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        _canvasReady = true;
    }

    // 리더 데이터로 확대
    public void Show(LeaderData leader, bool awakened)
    {
        if (leader == null) return;
        var sprite = (awakened && leader.AwakenArtwork != null)
            ? leader.AwakenArtwork
            : leader.BaseArtwork;
        Show(sprite);
        if (CardNameText   != null) CardNameText.text   = leader.LeaderName;
        if (CardCostText   != null) CardCostText.text   = "LEADER";
        if (CardInfoText   != null) CardInfoText.text   = "";
        if (CardEffectText != null)
            CardEffectText.text = awakened ? leader.AwakenEffectText : leader.BaseEffectText;
    }

    // 카드 데이터로 확대 (이름·효과 포함)
    public void Show(CardData card)
    {
        if (card == null) return;
        EnsureTopCanvas();
        Show(card.Artwork);
        ApplyCardInfo(card);
    }

    // 이미지만 확대 (기존 호환)
    public void Show(Sprite sprite)
    {
        if (sprite == null || ZoomImage == null) return;
        ReparentToRootCanvas();
        EnsureTopCanvas();
        StopAutoClose();
        if (TriggerLabel  != null) TriggerLabel.gameObject.SetActive(false);
        ZoomImage.sprite = sprite;
        ClearCardInfo();
        gameObject.SetActive(true);
    }

    // 트리거 발동 시 — duration초 후 자동으로 닫힘, 클릭으로도 닫힘
    public void ShowTimed(Sprite sprite, float duration = 2f)
    {
        if (ZoomImage == null) return;
        StopAutoClose();
        if (TriggerLabel != null) TriggerLabel.gameObject.SetActive(true);
        ZoomImage.sprite = sprite;
        ClearCardInfo();
        gameObject.SetActive(true);
        _autoCloseCoroutine = StartCoroutine(AutoClose(duration));
    }

    private void ApplyCardInfo(CardData card)
    {
        if (CardNameText   != null) CardNameText.text   = card.CardName;
        if (CardCostText   != null) CardCostText.text   = $"Cost {card.Cost}";

        if (CardInfoText != null)
        {
            string info = card.Type == CardType.Unit
                ? $"Power {card.AttackPower}  Hit {card.Hit}"
                : "";
            CardInfoText.text = info;
        }

        if (CardEffectText != null)
            CardEffectText.text = string.Join("\n", card.Effects);
    }

    private void ClearCardInfo()
    {
        if (CardNameText   != null) CardNameText.text   = "";
        if (CardCostText   != null) CardCostText.text   = "";
        if (CardInfoText   != null) CardInfoText.text   = "";
        if (CardEffectText != null) CardEffectText.text = "";
    }

    private IEnumerator AutoClose(float duration)
    {
        yield return new WaitForSeconds(duration);
        gameObject.SetActive(false);
        _autoCloseCoroutine = null;
    }

    private void StopAutoClose()
    {
        if (_autoCloseCoroutine != null)
        {
            StopCoroutine(_autoCloseCoroutine);
            _autoCloseCoroutine = null;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        StopAutoClose();
        gameObject.SetActive(false);
    }
}
