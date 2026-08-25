// Assets/Scripts/UI/SkillZoneSlot.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SkillZoneSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static SkillZoneSlot Instance { get; private set; }

    [Header("UI")]
    public Image    Background;
    public TMP_Text GuideText;      // "Drop Skill Here" 안내 텍스트
    public Image    SkillCardImage; // 사용된 스킬 카드 이미지 (턴 종료까지 표시)

    public Color NormalColor    = new Color(0.3f, 0.3f, 0.8f, 0.3f);
    public Color HighlightColor = new Color(0.3f, 0.8f, 0.3f, 0.5f);
    public Color TargetingColor = new Color(1f,   0.6f, 0f,   0.5f); // 타겟 선택 중

    // ── 타겟 선택 상태 ──────────────────────────────────────────
    public static CardData     PendingSkill    { get; private set; }
    public static TargetMode   CurrentMode     { get; private set; } = TargetMode.None;
    public static bool         IsTargeting     => CurrentMode != TargetMode.None;

    // ── 트리거(프라이즈) 타겟 선택 상태 ──────────────────────────
    // 스킬과 별개로, 트리거 효과(약화/트래시 대상 선택)에도 같은 레인 클릭 UI를 재사용
    public static bool   IsTriggerTargeting      { get; private set; }
    public static int    PendingCostLimit         { get; private set; } = -1; // -1 = 제한 없음
    public static bool   TriggerOwnerIsPlayer     { get; private set; } = true; // 트리거 발동자
    private static System.Action<int> _onTriggerLaneSelected;

    public static void EnterTriggerTargeting(TargetMode mode, int costLimit, System.Action<int> onLaneSelected, string guideMessage = null, bool triggerOwnerIsPlayer = true)
    {
        IsTriggerTargeting     = true;
        CurrentMode            = mode;
        PendingCostLimit       = costLimit;
        TriggerOwnerIsPlayer   = triggerOwnerIsPlayer;
        _onTriggerLaneSelected = onLaneSelected;
        Debug.Log($"[트리거] 타겟 선택 모드: {mode}, 발동자: {(triggerOwnerIsPlayer ? "플레이어" : "AI")}");
        TriggerGuideView.Instance?.Show(guideMessage ?? "레인을 선택하세요");
        NotifyTargetingChanged();
    }

    public static void CancelTriggerTargeting()
    {
        IsTriggerTargeting     = false;
        CurrentMode            = TargetMode.None;
        PendingCostLimit       = -1;
        TriggerOwnerIsPlayer   = true;
        _onTriggerLaneSelected = null;
        TriggerGuideView.Instance?.Hide();
        NotifyTargetingChanged();
    }

    public enum TargetMode
    {
        None,
        EnemyLane,  // 상대 레인 선택 (약점 간파, 미사일)
        AllyLane,   // 아군 레인 선택 (크레센도, 센스 쉐어링, 다 덤벼!)
        AnyLane,    // 아무 레인 선택 (엑셀러레이션)
    }

    // 타겟 불필요한 effectType 목록
    private static readonly HashSet<string> NoTargetEffects = new()
    {
        "LevelUp", "BuffAllAllies", "ForceDiscardExchange",
        "RecoverFromTrash", "TopDeckSearch", "TrashFieldByHandCost",
        "RecoverUnitFromTrash", "RecoverItemFromTrash",
        "MillRecoverFactionUnit", "MillAddDamageZoneDraw", "Draw"
    };

    // effectType → TargetMode 매핑
    private static readonly Dictionary<string, TargetMode> TargetModeMap = new()
    {
        { "DebuffEnemyUnit",   TargetMode.EnemyLane },
        { "BuffAllyUnit",      TargetMode.AllyLane  },
        { "TrashAllySelfDraw", TargetMode.AllyLane  },
        { "TrashBothUnits",    TargetMode.AllyLane  },
        { "TrashWeakestInLane",TargetMode.AnyLane   },
        { "BuffAllyUntilOpponentTurn", TargetMode.AllyLane  }, // ST11 테라 드레인
        { "TrashEnemyByTotalPower",    TargetMode.EnemyLane }, // ST06 은밀한 손길
        { "DrawPerEquippedItem",       TargetMode.AllyLane  }, // ST05 리타 부스트
        { "TrashAllyDamageMixDraw",    TargetMode.AllyLane  }, // ST09 운명의 인도
        { "DisableDefend",             TargetMode.EnemyLane }, // ST11 블랙 오더
        { "GrantExtraAttackBuff",      TargetMode.AllyLane  }, // ST06 사랑해, 기억해, 영원히.
        { "DisableEnemyAttack",        TargetMode.EnemyLane }, // BT06 킥킥, 타냐 등장!
    };

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (Background == null) Background = GetComponent<Image>();
        RefreshUI();
        TurnManager.Instance.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnDestroy()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(PhaseType phase)
    {
        // 페이즈 바뀌면 타겟 선택 취소
        CancelTargeting();
        CancelTriggerTargeting();

        // 엔드페이즈에 스킬 카드 이미지 숨김 (카드는 PlayerController.OnEndPhase에서 트래시로 이동)
        if (phase == PhaseType.End)
            ClearSkillCardImage();

        gameObject.SetActive(
            phase == PhaseType.Main && TurnManager.Instance.IsPlayerTurn);
    }

    public void ShowSkillCard(CardData card)
    {
        if (SkillCardImage == null || card.Artwork == null) return;
        SkillCardImage.sprite = card.Artwork;
        SkillCardImage.gameObject.SetActive(true);
        if (GuideText != null) GuideText.gameObject.SetActive(false);
    }

    private void ClearSkillCardImage()
    {
        if (SkillCardImage != null) SkillCardImage.gameObject.SetActive(false);
        if (GuideText      != null) GuideText.gameObject.SetActive(true);
    }

    // ── 드롭 처리 ──────────────────────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        CardView cardView = eventData.pointerDrag?.GetComponent<CardView>();
        if (cardView == null) return;

        CardData card = cardView.Data;

        // 스킬 카드만 허용
        if (card.Type != CardType.Skill)
        {
            cardView.ReturnToHand();
            return;
        }

        // 메인 페이즈 & 플레이어 턴 확인
        if (!TurnManager.Instance.IsPlayerTurn ||
            TurnManager.Instance.CurrentPhase != PhaseType.Main)
        {
            cardView.ReturnToHand();
            return;
        }

        // 스킬 발동 — 관문 경유 (검증 + 코스트 소비)
        var player = PlayerController.PlayerInstance;
        bool cast = GameActionGateway.Submit(new GameAction {
            Type = GameActionType.PlaySkill, IsPlayer = true, Lane = -1, Card = card,
        });
        if (!cast)
        {
            Debug.Log($"[스킬 거부] {card.CardName} (코스트/성약 금지)");
            cardView.ReturnToHand();
            return;
        }

        // 성공 시 UI 갱신 (효과 해결은 아래에서 — 2단계에 관문으로 이전 예정)
        Destroy(cardView.gameObject);
        HandView.Instance.RefreshHand();
        HUDView.NotifyStatusChanged();
        ShowSkillCard(card);

        Debug.Log($"[스킬] {card.CardName} 발동");

        // 타겟 필요 여부 확인
        TargetMode mode = GetTargetMode(card);

        if (mode == TargetMode.None)
        {
            // 즉시 발동
            EffectSystem.Instance.ExecuteSkillEffect(true, card, -1);
        }
        else
        {
            // 타겟 선택 모드 진입
            EnterTargeting(card, mode);
        }

        RefreshUI();
    }

    // ── 타겟 선택 ──────────────────────────────────────────────

    public static void EnterTargeting(CardData skill, TargetMode mode)
    {
        PendingSkill  = skill;
        CurrentMode   = mode;
        Debug.Log($"[스킬] 타겟 선택 모드: {mode} ({skill.CardName})");
        // LaneSlot들이 OnAttackStateChanged 대신 별도 이벤트로 갱신
        NotifyTargetingChanged();
    }

    public static void OnLaneClicked(int lane)
    {
        if (IsTriggerTargeting)
        {
            _onTriggerLaneSelected?.Invoke(lane);
            CancelTriggerTargeting();
            return;
        }

        if (!IsTargeting || PendingSkill == null) return;

        EffectSystem.Instance.ExecuteSkillEffect(true, PendingSkill, lane);
        CancelTargeting();
    }

    public static void CancelTargeting()
    {
        PendingSkill = null;
        CurrentMode  = TargetMode.None;
        NotifyTargetingChanged();
    }

    private static void NotifyTargetingChanged()
    {
        // LaneSlot들에 갱신 알림
        foreach (var slot in FindObjectsByType<LaneSlot>(FindObjectsSortMode.None))
            slot.RefreshTargetingUI();
    }

    // ── 유틸 ───────────────────────────────────────────────────

    private TargetMode GetTargetMode(CardData card)
    {
        foreach (var et in card.EffectTypes)
        {
            int idx = et.IndexOf(':');
            string type = idx < 0 ? et : et.Substring(0, idx);

            if (NoTargetEffects.Contains(type)) continue;
            if (TargetModeMap.TryGetValue(type, out var mode)) return mode;
        }
        return TargetMode.None;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Background != null) Background.color = HighlightColor;
    }

    public void OnPointerExit(PointerEventData eventData) => RefreshUI();

    private void RefreshUI()
    {
        if (Background == null) return;
        Background.color = IsTargeting ? TargetingColor : NormalColor;
        if (GuideText != null)
            GuideText.text = IsTargeting ? "Select Lane" : "Drop Skill";
    }
}
