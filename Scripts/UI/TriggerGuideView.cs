// Assets/Scripts/UI/TriggerGuideView.cs
using UnityEngine;
using TMPro;

// 트리거 발동 시 프라이즈(타겟) 선택 안내 배너.
// SkillZoneSlot.EnterTriggerTargeting / CancelTriggerTargeting에서 자동으로 표시/숨김 처리됨.
public class TriggerGuideView : MonoBehaviour
{
    public static TriggerGuideView Instance { get; private set; }

    [Header("UI")]
    public TMP_Text GuideText;

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
        canvas.sortingOrder    = 201;
        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        _canvasReady = true;
    }

    public void Show(string message)
    {
        ReparentToRootCanvas();
        EnsureTopCanvas();
        gameObject.SetActive(true);
        if (GuideText != null) GuideText.text = message;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
