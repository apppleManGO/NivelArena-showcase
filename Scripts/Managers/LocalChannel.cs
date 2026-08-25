// Assets/Scripts/Managers/LocalChannel.cs
// ─────────────────────────────────────────────────────────────────────────
// 오프라인 채널 — 싱글플레이/AI 대전. INetChannel의 기본 구현.
//  · 현재 동작을 100% 그대로 재현: 온라인 아님, 내가 곧 권위, isPlayer=로컬 인간.
//  · Photon 미설치 상태에서도 이 파일만으로 게임이 지금처럼 돌아간다.
// ─────────────────────────────────────────────────────────────────────────
public class LocalChannel : INetChannel
{
    public bool IsOnline    => false;
    public bool IsAuthority => true;   // 오프라인은 이 머신이 유일한 권위

    // 오프라인: 기존 규칙 그대로 (플레이어=로컬 인간, AI=false)
    public bool IsLocalHuman(bool chooserIsPlayer) => chooserIsPlayer;

    // 오프라인은 전송할 상대가 없음 — 아무것도 안 함 (권위가 직접 실행하므로 호출도 안 됨)
    public void SendAction(GameAction action) { }

    // 오프라인은 원격 요청 없음 (로컬 인간=팝업으로 처리되어 호출 안 됨). 안전 기본값.
    public void RequestDefense(bool defenderIsPlayer, CardData attacker, CardData defender, System.Action<bool> onDecided,
                               int attackerPower, int defenderPower)
        => onDecided?.Invoke(false);

    // 오프라인은 복제 대상 없음 — 무동작
    public void BroadcastState() { }

    // 오프라인은 릴레이 없음 — 무동작 (호출 안 됨)
    public void SendEndPhase(int which) { }

    // 오프라인은 원격 요청 없음 (로컬 인간=팝업). 안전 기본값 — 첫 후보 자동 (호출 안 됨)
    public void RequestCard(bool chooserIsPlayer, string title, System.Collections.Generic.List<CardData> candidates,
        System.Action<CardData> onPicked, System.Action onCancel)
        => onPicked?.Invoke(candidates != null && candidates.Count > 0 ? candidates[0] : null);

    // 오프라인은 원격 멀리건 없음 — 교체 안 함 (호출 안 됨)
    public void RequestMulligan(System.Action<bool> onDecided) => onDecided?.Invoke(false);

    // 오프라인은 복제 대상 없음 — 무동작 (결과는 로컬에서 직접 표시)
    public void BroadcastGameOver(bool loserIsHostPlayer) { }

    // 오프라인은 릴레이 대상 없음 — 무동작 (커멘터리는 AIController가 직접 표시)
    public void RelayToast(string message) { }
    public void RelayTrigger(string cardId) { }
}
