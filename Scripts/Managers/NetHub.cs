// Assets/Scripts/Managers/NetHub.cs
// ─────────────────────────────────────────────────────────────────────────
// 네트워크 허브 — 현재 활성 채널을 보관하는 단일 접근점.
//  · 게임 코드(GameActionGateway / ChoiceBroker)는 NetHub만 참조한다.
//  · 기본값은 LocalChannel(오프라인) → 현재 동작.
//  · 온라인 대전 시작 시(3단계 후반) NetHub.Channel = new PhotonChannel(...) 한 줄로 교체.
// ─────────────────────────────────────────────────────────────────────────
public static class NetHub
{
    // 현재 채널 (기본: 오프라인). 온라인 진입/종료 시 교체.
    public static INetChannel Channel = new LocalChannel();

    public static bool IsOnline    => Channel.IsOnline;
    // UI 표시용 상대 명칭 (온라인=상대, 오프라인=AI)
    public static string OpponentLabel => IsOnline ? "상대" : "AI";
    public static bool IsAuthority => Channel.IsAuthority;
    public static bool IsLocalHuman(bool chooserIsPlayer) => Channel.IsLocalHuman(chooserIsPlayer);
    public static void SendAction(GameAction action) => Channel.SendAction(action);
    public static void RequestDefense(bool defenderIsPlayer, CardData attacker, CardData defender, System.Action<bool> onDecided,
                                      int attackerPower = -1, int defenderPower = -1)
        => Channel.RequestDefense(defenderIsPlayer, attacker, defender, onDecided, attackerPower, defenderPower);
    public static void BroadcastState() => Channel.BroadcastState();
    public static void SendEndPhase(int which) => Channel.SendEndPhase(which);
    public static void RequestCard(bool chooserIsPlayer, string title, System.Collections.Generic.List<CardData> candidates,
        System.Action<CardData> onPicked, System.Action onCancel)
        => Channel.RequestCard(chooserIsPlayer, title, candidates, onPicked, onCancel);
    public static void RequestMulligan(System.Action<bool> onDecided) => Channel.RequestMulligan(onDecided);
    public static void BroadcastGameOver(bool loserIsHostPlayer) => Channel.BroadcastGameOver(loserIsHostPlayer);
    public static void RelayToast(string message) => Channel.RelayToast(message);
    public static void RelayTrigger(string cardId) => Channel.RelayTrigger(cardId);
}
