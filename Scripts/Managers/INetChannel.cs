// Assets/Scripts/Managers/INetChannel.cs
// ─────────────────────────────────────────────────────────────────────────
// 네트워크 추상화 (3단계) — 게임 코드가 Photon에 직접 의존하지 않게 하는 경계.
//  · 게임 코드(관문/중개소)는 이 인터페이스만 안다. Photon SDK 이름은 여기 안 나옴.
//  · 지금은 LocalChannel(오프라인)만 존재 → 현재 동작 그대로.
//  · 나중에 Photon Fusion 2 SDK 임포트 후, 이 인터페이스를 구현하는 PhotonChannel만
//    추가하면 게임 코드는 하나도 안 바뀌고 온라인 전환됨.
// ─────────────────────────────────────────────────────────────────────────
public interface INetChannel
{
    // 온라인 세션인가 (false = 싱글/AI 대전)
    bool IsOnline { get; }

    // 이 머신이 게임 로직을 실제로 실행하는 "권위(호스트)"인가.
    //  · 오프라인이면 항상 true (내가 곧 심판).
    //  · 온라인 호스트면 true, 온라인 클라면 false(→ 행동을 호스트에 전송).
    bool IsAuthority { get; }

    // 이 선택의 주체(chooserIsPlayer)가 "이 머신의 로컬 인간"인가.
    //  · 오프라인: chooserIsPlayer 그대로 (true=플레이어=로컬 인간, false=AI).
    //  · 온라인: 이 클라의 플레이어 슬롯과 일치하는지로 판정.
    bool IsLocalHuman(bool chooserIsPlayer);

    // 권위가 아닐 때(온라인 클라), 행동을 권위(호스트)에게 전송.
    //  · 오프라인/권위면 호출되지 않음.
    void SendAction(GameAction action);

    // 호스트가 "원격 클라의 방어 선택"이 필요할 때 요청 → 클라가 팝업으로 고르고 응답하면 onDecided 호출.
    //  · 오프라인이면 호출되지 않음(로컬 인간=팝업으로 처리).
    void RequestDefense(bool defenderIsPlayer, CardData attacker, CardData defender, System.Action<bool> onDecided,
                        int attackerPower, int defenderPower);

    // 호스트가 현재 게임 상태를 클라에 복제 전송 (3c). 오프라인/클라면 무동작.
    void BroadcastState();

    // 온라인 클라가 자기 턴의 페이즈 종료를 호스트에 릴레이 (0=메인, 1=어택, 2=어택스킵). 오프라인/호스트 무동작.
    void SendEndPhase(int which);

    // 호스트가 "원격 클라의 카드 선택"이 필요할 때 요청 → 클라 팝업 → 응답 시 onPicked(취소 시 onCancel).
    void RequestCard(bool chooserIsPlayer, string title, System.Collections.Generic.List<CardData> candidates,
        System.Action<CardData> onPicked, System.Action onCancel);

    // 게임 시작 시 호스트가 클라의 멀리건 여부를 요청 → 클라가 자기 손패 보고 결정 → onDecided(true=교체).
    void RequestMulligan(System.Action<bool> onDecided);

    // 게임 종료 시 호스트가 승패 결과를 클라에 전송. loserIsHostPlayer=호스트 관점 패배자가 호스트 자신인가.
    //  · 클라는 미러 관점으로 결과 화면 표시. 오프라인/클라면 무동작.
    void BroadcastGameOver(bool loserIsHostPlayer);

    // 온라인 커멘터리: 호스트가 자기 행동을 클라에 토스트로 알림(클라 입장에선 "상대"의 행동). 오프라인/클라 무동작.
    void RelayToast(string message);

    // 트리거 공개 릴레이: 호스트에서 발동한 트리거 카드(cardId)를 클라도 보게 함. 오프라인/클라 무동작.
    void RelayTrigger(string cardId);
}
