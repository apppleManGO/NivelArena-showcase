// Assets/Scripts/UI/PhaseBarView.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class PhaseBarView : MonoBehaviour
{
    [System.Serializable]
    public class PhaseSlot
    {
        public GameObject Root;       // 슬롯 루트 오브젝트 — 여기에 Button 붙임
        public Image      Background;
        public Image      Icon;
        public TMP_Text   Label;
        [HideInInspector] public Button Button;
    }

    [Header("페이즈 슬롯 (LevelUp/Draw/Main/Attack/End 순)")]
    public PhaseSlot[] Slots = new PhaseSlot[5];

    [Header("턴 표시")]
    public TMP_Text TurnText;

    [Header("색상")]
    public Color ColorDone     = new Color(0.29f, 0.69f, 0.49f);
    public Color ColorActive   = new Color(1f,    0.85f, 0.2f);
    public Color ColorInactive = new Color(0.7f,  0.7f,  0.7f);
    public Color ColorAI       = new Color(0.91f, 0.30f, 0.24f);
    public Color BgActive      = new Color(1f, 1f, 1f, 0.12f);
    public Color BgInactive    = new Color(0f, 0f, 0f, 0f);
    public Color BgClickable   = new Color(1f, 0.85f, 0.2f, 0.2f);

    private static readonly PhaseType[] PhaseOrder =
    {
        PhaseType.LevelUp,
        PhaseType.Draw,
        PhaseType.Main,
        PhaseType.Attack,
        PhaseType.End
    };

    private const int MainIndex   = 2;
    private const int AttackIndex = 3;
    private const int EndIndex    = 4;

    private void Start()
    {
        // 팝업들(200+)보다 낮은 sortingOrder로 고정
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = 10;
        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        TurnManager.Instance.OnPhaseChanged += Refresh;
        TurnManager.Instance.OnTurnChanged  += RefreshTurn;

        // 각 슬롯 루트에 SlotClickHandler 붙이기
        for (int i = 0; i < Slots.Length; i++)
        {
            var slot = Slots[i];
            if (slot == null) continue;

            var target = slot.Root != null ? slot.Root : slot.Background?.gameObject;
            if (target == null) continue;

            // 자식 포함 모든 Image/Text Raycast Target 활성화
            foreach (var img in target.GetComponentsInChildren<Image>(true))
                img.raycastTarget = true;
            foreach (var txt in target.GetComponentsInChildren<TMP_Text>(true))
                txt.raycastTarget = false; // 텍스트는 끄고 Image로만 받음

            var handler = target.GetComponent<SlotClickHandler>()
                       ?? target.AddComponent<SlotClickHandler>();
            int captured = i;
            handler.Init(captured, OnSlotClicked);

            // Button은 제거 (충돌 방지)
            var oldBtn = target.GetComponent<Button>();
            if (oldBtn != null) Destroy(oldBtn);

            slot.Button = null;
        }

        // 현재 상태로 초기화
        Refresh(TurnManager.Instance.CurrentPhase);
        RefreshTurn(TurnManager.Instance.IsPlayerTurn);
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance == null) return;
        TurnManager.Instance.OnPhaseChanged -= Refresh;
        TurnManager.Instance.OnTurnChanged  -= RefreshTurn;
    }

    private void OnSlotClicked(int index)
    {
        if (!TurnManager.Instance.IsPlayerTurn) return;

        var phase = TurnManager.Instance.CurrentPhase;

        switch (index)
        {
            case AttackIndex when phase == PhaseType.Main:
                // 메인 → 어택 페이즈로 진행
                TurnManager.Instance.EndMainPhase();
                break;
            case EndIndex when phase == PhaseType.Main:
                // 메인 → 어택 스킵 후 엔드
                TurnManager.Instance.SkipToEndPhase();
                break;
            case EndIndex when phase == PhaseType.Attack:
                // 어택 → 엔드
                TurnManager.Instance.EndAttackPhase();
                break;
        }
    }

    private void RefreshTurn(bool isPlayer)
    {
        if (TurnText == null) return;
        int turn = TurnManager.Instance.TurnNumber;
        TurnText.text  = isPlayer ? $"플레이어 턴  |  Turn {turn}" : $"{NetHub.OpponentLabel} 턴  |  Turn {turn}";
        // 내 턴=초록 / 상대 턴=빨강 (온라인에서 서로 누구 턴인지 색으로 구분)
        TurnText.color = isPlayer ? new Color(0.40f, 0.90f, 0.45f) : new Color(1f, 0.42f, 0.42f);
    }

    public void Refresh(PhaseType current)
    {
        bool isPlayer    = TurnManager.Instance.IsPlayerTurn;
        int  currentIdx  = System.Array.IndexOf(PhaseOrder, current);

        for (int i = 0; i < Slots.Length; i++)
        {
            var slot = Slots[i];
            if (slot == null) continue;

            bool isClickable = isPlayer && (
                (i == AttackIndex && current == PhaseType.Main)   ||  // 메인 → 어택
                (i == EndIndex    && current == PhaseType.Main)   ||  // 메인 → 엔드 스킵
                (i == EndIndex    && current == PhaseType.Attack));    // 어택 → 엔드

            if (i < currentIdx)
            {
                SetSlot(slot, ColorDone, BgInactive, false);
            }
            else if (i == currentIdx)
            {
                Color activeColor = isPlayer ? ColorActive : ColorAI;
                Color bgColor     = isClickable ? BgClickable : BgActive;
                SetSlot(slot, activeColor, bgColor, isClickable);
            }
            else
            {
                SetSlot(slot, ColorInactive, BgInactive, false);
            }

        }
    }

    private void SetSlot(PhaseSlot slot, Color iconColor, Color bgColor, bool clickable)
    {
        if (slot.Icon       != null) slot.Icon.color       = iconColor;
        if (slot.Label      != null) slot.Label.color      = Color.white;
        if (slot.Background != null) slot.Background.color = bgColor;
        if (slot.Button     != null) slot.Button.interactable = clickable;
    }
}
