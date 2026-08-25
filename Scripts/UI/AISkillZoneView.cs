// Assets/Scripts/UI/AISkillZoneView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// AI 스킬존 표시 전용 뷰. 드롭 기능 없이 AI가 스킬 사용 시 카드 이미지만 표시.
public class AISkillZoneView : MonoBehaviour
{
    public static AISkillZoneView Instance { get; private set; }

    [Header("UI")]
    public Image    SkillCardImage;
    public TMP_Text GuideText;      // "AI Skill Zone" 안내 텍스트

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        TurnManager.Instance.OnPhaseChanged += OnPhaseChanged;
        ClearSkillCard();
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(PhaseType phase)
    {
        if (phase == PhaseType.End)
            ClearSkillCard();
    }

    public void ShowSkillCard(CardData card)
    {
        if (SkillCardImage == null || card == null) return;
        if (card.Artwork != null)
        {
            SkillCardImage.sprite = card.Artwork;
            SkillCardImage.gameObject.SetActive(true);
        }
        if (GuideText != null) GuideText.gameObject.SetActive(false);
    }

    private void ClearSkillCard()
    {
        if (SkillCardImage != null) SkillCardImage.gameObject.SetActive(false);
        if (GuideText      != null) GuideText.gameObject.SetActive(true);
    }
}
