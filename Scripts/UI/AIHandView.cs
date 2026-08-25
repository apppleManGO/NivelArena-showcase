// Assets/Scripts/UI/AIHandView.cs
// AI 손패를 카드 뒷면으로 표시
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AIHandView : MonoBehaviour
{
    public static AIHandView Instance { get; private set; }

    [Header("UI")]
    public Transform  CardContainer;   // Horizontal Layout Group
    public TMP_Text   HandCountText;   // "Hand: N" 텍스트 (선택)

    [Header("카드 크기 (플레이어 카드와 동일하게 맞출 것)")]
    public Vector2 CardSize = new Vector2(100, 140); // Inspector에서 조절

    [Header("카드 뒷면")]
    public Sprite CardBackSprite;      // Inspector에서 직접 연결 시 사용 (없으면 자동 생성)

    private readonly List<GameObject> _slots = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        TurnManager.Instance.OnPhaseChanged += _ => Refresh();
        Refresh();
    }

    public void Refresh()
    {
        if (PlayerController.AIInstance == null) return;

        int count = PlayerController.AIInstance.Hand.Count;

        // 슬롯 수 맞추기
        while (_slots.Count < count)
        {
            var go = new GameObject("AICard", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(CardContainer, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = CardSize;

            var img = go.GetComponent<Image>();
            img.sprite = GetCardBackSprite();

            // 중앙 로고 텍스트 (TMP)
            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "N";
            tmp.fontSize = CardSize.x * 0.2f; // 카드 크기에 비례
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.85f, 0.75f, 0.3f, 1f); // 금색

            _slots.Add(go);
        }

        while (_slots.Count > count)
        {
            Destroy(_slots[_slots.Count - 1]);
            _slots.RemoveAt(_slots.Count - 1);
        }

        if (HandCountText != null)
            HandCountText.text = $"Hand {count}";
    }

    private Sprite GetCardBackSprite()
    {
        if (CardBackSprite != null) return CardBackSprite;
        if (CardBackGenerator.Instance != null)
            return CardBackGenerator.Instance.GetCardBackSprite((int)CardSize.x, (int)CardSize.y);
        return null;
    }

    public static void NotifyHandChanged()
    {
        if (Instance != null) Instance.Refresh();
    }
}
