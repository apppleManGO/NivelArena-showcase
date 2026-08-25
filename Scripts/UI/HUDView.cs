// Assets/Scripts/UI/HUDView.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDView : MonoBehaviour
{
    [Header("페이즈 / 턴")]
    public TMP_Text PhaseText;
    public TMP_Text TurnText;

    [Header("플레이어 상태")]
    public TMP_Text PlayerLevelText;
    public TMP_Text PlayerDamageText;
    public TMP_Text PlayerDeckText;
    public TMP_Text PlayerHandText;
    public TMP_Text PlayerSizeText;      // 사이즈 표시
    public TMP_Text PlayerCostLeftText;  // 남은 코스트 표시

    [Header("플레이어 리더")]
    public Image PlayerLeaderImage;  // 좌하단 리더 이미지

    [Header("AI 리더")]
    public Image AILeaderImage;      // 좌상단 리더 이미지

    [Header("AI 상태")]
    public TMP_Text AILevelText;
    public TMP_Text AIDamageText;
    public TMP_Text AISizeText;
    public TMP_Text AIDeckText;
    public TMP_Text AITrashText;

    [Header("플레이어 트래시")]
    public TMP_Text PlayerTrashText;

    private void Start()
    {
        TurnManager.Instance.OnPhaseChanged += RefreshPhase;
        TurnManager.Instance.OnTurnChanged  += RefreshTurn;

        // 리더 이미지 클릭 시 확대
        AddLeaderClickZoom(PlayerLeaderImage, () => PlayerController.PlayerInstance?.Leader);
        AddLeaderClickZoom(AILeaderImage,     () => PlayerController.AIInstance?.Leader);
    }

    private void AddLeaderClickZoom(Image img, System.Func<LeaderData> getLeader)
    {
        if (img == null) return;
        var btn = img.gameObject.GetComponent<UnityEngine.UI.Button>()
               ?? img.gameObject.AddComponent<UnityEngine.UI.Button>();
        btn.transition = UnityEngine.UI.Selectable.Transition.None;
        btn.onClick.AddListener(() =>
        {
            var leader = getLeader();
            if (leader == null || CardZoomView.Instance == null) return;

            bool isPlayerLeader = (img == PlayerLeaderImage);

            // 자신의 메인 페이즈에 리더 액티브 발동 가능하면 클릭 = 발동 (턴당 1회) — 관문 경유
            if (isPlayerLeader && EffectSystem.Instance.CanUseLeaderActive(true))
            {
                GameActionGateway.Submit(new GameAction {
                    Type = GameActionType.LeaderActive, IsPlayer = true,
                });
                ToastView.Show($"리더 액티브 발동: {leader.LeaderName}");
                return;
            }

            // 발동할 수 없다면 왜인지 알려준다 — 아무 반응이 없으면 "액티브가 고장났다"로 보인다.
            // (액티브가 없는 리더면 null이 와서 조용히 확대만 된다)
            if (isPlayerLeader)
            {
                string why = EffectSystem.Instance.LeaderActiveBlockReason(true);
                if (why != null) ToastView.Show(why);
            }

            bool awakened = isPlayerLeader
                ? (PlayerController.PlayerInstance?.IsAwakened ?? false)
                : (PlayerController.AIInstance?.IsAwakened     ?? false);

            CardZoomView.Instance.Show(leader, awakened);
        });
    }

    private void RefreshPhase(PhaseType phase)
    {
        if (PhaseText != null)
            PhaseText.text = phase switch
            {
                PhaseType.LevelUp => "LevelUp",
                PhaseType.Draw    => "Draw",
                PhaseType.Main    => "Main",
                PhaseType.Attack  => "Attack",
                PhaseType.End     => "End",
                _                 => ""
            };

        RefreshStatus();
    }

    private void RefreshTurn(bool isPlayer)
    {
        if (TurnText != null)
        {
            TurnText.text = isPlayer ? "Your Turn" : (NetHub.IsOnline ? "Opponent Turn" : "AI Turn");
            // 내 턴=초록 / 상대 턴=빨강
            TurnText.color = isPlayer ? new Color(0.40f, 0.90f, 0.45f) : new Color(1f, 0.42f, 0.42f);
        }
    }

    public void RefreshStatus()
    {
        var p = PlayerController.PlayerInstance;
        var a = PlayerController.AIInstance;
        if (p == null || a == null) return;

        // 플레이어 기본 상태
        if (PlayerLevelText  != null) PlayerLevelText.text  = $"Lv {p.LeaderLevel}";
        if (PlayerDamageText != null) PlayerDamageText.text = $"DMG {p.DamageCount}/10";
        if (PlayerDeckText   != null) PlayerDeckText.text   = $"Deck {p.DrawPile.Count}";
        if (PlayerHandText   != null) PlayerHandText.text   = $"Hand {p.Hand.Count}";

        // 플레이어 사이즈 / 남은 코스트
        int fieldCost = FieldManager.Instance != null
            ? FieldManager.Instance.GetFieldCostSum(true) + FieldManager.Instance.GetSkillZoneCostSum(true)
            : 0;
        int costLeft = p.Size - fieldCost;

        if (PlayerSizeText     != null)
            PlayerSizeText.text = $"Size {p.Size}";

        if (PlayerCostLeftText != null)
        {
            PlayerCostLeftText.text  = $"Cost {costLeft}/{p.Size}";
            // 코스트 부족하면 빨간색으로 표시
            PlayerCostLeftText.color = costLeft <= 0
                ? new Color(1f, 0.3f, 0.3f)
                : Color.white;
        }

        // AI 상태
        if (AILevelText   != null) AILevelText.text   = $"Lv {a.LeaderLevel}";
        if (AIDamageText  != null) AIDamageText.text  = $"DMG {a.DamageCount}/10";
        if (AISizeText    != null) AISizeText.text    = $"Size {a.Size}";
        if (AIDeckText    != null) AIDeckText.text    = $"Deck {a.DrawPile.Count}";
        // 플레이어 트래시

        // 플레이어 리더 이미지 (각성 시 AwakenArtwork로 전환)
        if (PlayerLeaderImage != null && p.Leader != null)
        {
            var sprite = (p.IsAwakened && p.Leader.AwakenArtwork != null)
                ? p.Leader.AwakenArtwork
                : p.Leader.BaseArtwork;
            if (sprite != null) PlayerLeaderImage.sprite = sprite;
        }

        // AI 리더 이미지 (각성 시 AwakenArtwork로 전환)
        if (AILeaderImage != null && a.Leader != null)
        {
            var sprite = (a.IsAwakened && a.Leader.AwakenArtwork != null)
                ? a.Leader.AwakenArtwork
                : a.Leader.BaseArtwork;
            if (sprite != null) AILeaderImage.sprite = sprite;
        }
    }

    // 메인 페이즈 중 카드 배치할 때마다 UI 갱신 (외부에서 호출)
    public static void NotifyStatusChanged()
    {
        if (FindFirstObjectByType<HUDView>() is HUDView hud)
            hud.RefreshStatus();
    }

}
