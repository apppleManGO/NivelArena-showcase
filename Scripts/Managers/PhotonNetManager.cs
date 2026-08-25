// Assets/Scripts/Managers/PhotonNetManager.cs
// ─────────────────────────────────────────────────────────────────────────
// PUN 2 연결 채널 (3단계-a: 연결 토대). INetChannel의 온라인 구현.
//  · 역할: 방 코드로 접속/입장, 호스트(권위) 판정, NetHub에 자신을 꽂음.
//  · 방에 들어가면 NetHub.Channel = this → 관문/중개소가 자동으로 온라인 경로 사용.
//  · 방을 나가면 다시 LocalChannel(오프라인)로 복귀.
//
//  ※ 사용법: 씬에 빈 GameObject 하나 만들어 이 스크립트를 붙이고,
//    로비 UI 버튼에서 PhotonNetManager.Instance.JoinRoom("방코드") 호출.
//  ※ 접속 전 PhotonServerSettings에 App ID를 넣어야 실제 연결됨(현재 공란).
//
//  ── 남은 작업(다음 단계) ──
//   3b: SendAction RPC 릴레이 + 원격 선택 요청/응답 (ChoiceBroker 원격 분기)
//   3c: 상태 복제 (필드/존/HUD를 클라에 동기화)
// ─────────────────────────────────────────────────────────────────────────
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Linq;
using UnityEngine;

public class PhotonNetManager : MonoBehaviourPunCallbacks, INetChannel, IOnEventCallback
{
    public static PhotonNetManager Instance { get; private set; }

    private const byte EV_ACTION       = 1; // 클라→호스트 행동 전송
    private const byte EV_DEFENSE_REQ  = 2; // 호스트→클라 방어 요청
    private const byte EV_DEFENSE_RESP = 3; // 클라→호스트 방어 응답
    private const byte EV_STATE        = 4; // 호스트→클라 상태 복제 (3c)
    private const byte EV_ENDPHASE     = 5; // 클라→호스트 페이즈 종료 릴레이
    private const byte EV_CARD_REQ     = 6; // 호스트→클라 카드 선택 요청
    private const byte EV_CARD_RESP    = 7; // 클라→호스트 카드 선택 응답
    private const byte EV_MULL_REQ     = 8; // 호스트→클라 멀리건 요청
    private const byte EV_MULL_RESP    = 9; // 클라→호스트 멀리건 응답
    private const byte EV_GAMEOVER     = 10; // 호스트→클라 승패 결과
    private const byte EV_TOAST        = 11; // 호스트→클라 커멘터리 토스트
    private const byte EV_TRIGGER      = 12; // 호스트→클라 트리거 공개

    private string _pendingRoom;
    private System.Action<bool> _pendingDefense; // 호스트: 클라 방어 응답 대기 콜백 (전투는 순차라 1개면 충분)
    private System.Action<CardData> _pendingCardPick;   // 호스트: 클라 카드선택 응답 대기
    private System.Action _pendingCardCancel;
    private System.Collections.Generic.List<CardData> _pendingCardCandidates;
    private System.Action<bool> _pendingMulligan;       // 호스트: 클라 멀리건 응답 대기

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 접속/방 입장 ──────────────────────────────────────────────

    // 방 코드로 접속 시작 (로비 UI에서 호출)
    public void JoinRoom(string roomCode)
    {
        _pendingRoom = roomCode;
        if (PhotonNetwork.IsConnected)
        {
            TryJoinPending();
        }
        else
        {
            // 버전이 다르면 방 매칭 자체가 안 되도록 GameVersion 고정 (구·신버전 대전 차단)
            PhotonNetwork.GameVersion = Application.version;
            // 고유 UserId 부여 — MPPM 가상 플레이어가 PlayerPrefs를 공유해 UserId가 겹치는 문제 방지
            PhotonNetwork.AuthValues = new AuthenticationValues(System.Guid.NewGuid().ToString());
            PhotonNetwork.NickName = "P" + Random.Range(1000, 9999);
            PhotonNetwork.ConnectUsingSettings(); // App ID(PhotonServerSettings) 필요
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[Net] 마스터 서버 접속됨");
        TryJoinPending();
    }

    private void TryJoinPending()
    {
        if (string.IsNullOrEmpty(_pendingRoom)) return;
        Debug.Log($"[Net] 방 참가/생성 시도: '{_pendingRoom}'");
        var opts = new RoomOptions { MaxPlayers = 2 }; // 1:1 대전
        PhotonNetwork.JoinOrCreateRoom(_pendingRoom, opts, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        NetHub.Channel = this; // ★ 콘센트에 온라인 채널 꽂기
        Debug.Log($"[Net] ★ 방 입장 성공: {PhotonNetwork.CurrentRoom.Name} " +
                  $"(인원 {PhotonNetwork.CurrentRoom.PlayerCount}/2, 내가호스트={PhotonNetwork.IsMasterClient})");
    }

    // ── 진단 콜백 (원인 파악용) ──
    public override void OnCreatedRoom()
        => Debug.Log($"[Net] 방 생성됨(내가 호스트): {_pendingRoom}");

    public override void OnJoinRoomFailed(short returnCode, string message)
        => Debug.LogWarning($"[Net] 방 참가 실패 코드{returnCode}: {message}");

    public override void OnCreateRoomFailed(short returnCode, string message)
        => Debug.LogWarning($"[Net] 방 생성 실패 코드{returnCode}: {message}");

    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        NetHub.Channel = new LocalChannel(); // 오프라인 복귀
        Debug.Log("[Net] 방 퇴장 → 오프라인 채널로 복귀");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        NetHub.Channel = new LocalChannel();
        Debug.LogWarning($"[Net] 연결 끊김: {cause} → 오프라인 복귀");
        ResolvePendingChoicesFallback();
        ToastView.Show("서버 연결이 끊겼습니다");
    }

    // 게임 중 상대가 이탈(퇴장/네트워크 끊김 — Photon이 ~10초 후 감지)했을 때.
    //  · 호스트가 상대의 선택(방어/카드/멀리건)을 기다리며 무한 대기(행)하는 것을 방지.
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.LogWarning("[Net] 상대 이탈 감지 → 대기 중인 원격 선택 폴백 해소");
        ToastView.Show("상대 연결이 끊겼습니다");
        ResolvePendingChoicesFallback();
    }

    // 대기 중인 모든 원격 선택을 안전한 기본값으로 즉시 해소(무한 대기 방지).
    //  · 방어=패스(false) / 멀리건=유지(false) / 카드=취소 또는 첫 후보.
    private void ResolvePendingChoicesFallback()
    {
        if (_pendingDefense != null)
        {
            var cb = _pendingDefense; _pendingDefense = null; cb(false);
        }
        if (_pendingMulligan != null)
        {
            var cb = _pendingMulligan; _pendingMulligan = null; cb(false);
        }
        if (_pendingCardPick != null || _pendingCardCancel != null)
        {
            var pick = _pendingCardPick; var cancel = _pendingCardCancel; var cands = _pendingCardCandidates;
            _pendingCardPick = null; _pendingCardCancel = null; _pendingCardCandidates = null;
            if (cancel != null) cancel();
            else if (pick != null) pick(cands != null && cands.Count > 0 ? cands[0] : null);
        }
    }

    // ── INetChannel 구현 ─────────────────────────────────────────

    public bool IsOnline    => PhotonNetwork.InRoom;
    public bool IsAuthority => PhotonNetwork.IsMasterClient; // 마스터 클라 = 호스트 = 게임 로직 권위

    // 플레이어 슬롯 매핑: 호스트가 isPlayer=true(자신의 플레이어) 슬롯, 상대 클라가 isPlayer=false 슬롯.
    //  → 이 클라가 소유한 슬롯의 선택만 "로컬 인간".
    //  (호스트: IsLocalHuman(true)=true / 상대선택(false)=false=원격)
    //  (클라 : IsLocalHuman(false)=true / 호스트선택(true)=false=원격)
    public bool IsLocalHuman(bool chooserIsPlayer)
        => chooserIsPlayer == PhotonNetwork.IsMasterClient;

    // ── 3b: 행동 릴레이 ──────────────────────────────────────────

    // 온라인 클라가 자신의 행동을 호스트(마스터)에게 전송.
    //  직렬화: type(byte) / lane(int) / CardId(string). IsPlayer는 안 보냄
    //  — 호스트는 "받은 행동 = 클라(상대)의 것" 이라 자기 관점에서 isPlayer=false로 실행.
    public void SendAction(GameAction action)
    {
        object[] data =
        {
            (byte)action.Type,
            action.Lane,
            action.Card != null ? action.Card.CardId : "",
        };
        PhotonNetwork.RaiseEvent(
            EV_ACTION, data,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            SendOptions.SendReliable);
        Debug.Log($"[Net] 행동 전송 → 호스트: {action.Type} lane{action.Lane} {(action.Card != null ? action.Card.CardId : "")}");
    }

    // 호스트가 "원격 클라의 방어 선택" 필요 → 클라에 요청. 응답 오면 onDecided 호출.
    public void RequestDefense(bool defenderIsPlayer, CardData attacker, CardData defender, System.Action<bool> onDecided,
                               int attackerPower, int defenderPower)
    {
        _pendingDefense = onDecided;
        object[] data = {
            attacker != null ? attacker.CardId : "",
            defender != null ? defender.CardId : "",
            attackerPower,  // 호스트가 계산한 실제 전투 수치 — 클라는 미러 상태라 재계산하면 어긋날 수 있어 그대로 전송
            defenderPower,
        };
        PhotonNetwork.RaiseEvent(
            EV_DEFENSE_REQ, data,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others }, // 상대(클라)에게
            SendOptions.SendReliable);
        Debug.Log("[Net] 방어 요청 → 클라 (응답 대기)");
    }

    // ── 3c: 상태 복제 (v1 — 필드 유닛) ──────────────────────────

    // [호스트] 현재 상태(필드 + 턴/페이즈)를 클라에 전송. 호스트 관점 절대좌표.
    public void BroadcastState()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        var fm = FieldManager.Instance;
        var f = new string[6];
        for (int i = 0; i < 3; i++) f[i]     = fm.GetUnit(true,  i)?.CardId ?? ""; // 호스트 플레이어(true)
        for (int i = 0; i < 3; i++) f[3 + i] = fm.GetUnit(false, i)?.CardId ?? ""; // 호스트 상대(false)

        // 레인별 장착 아이템 (카드ID를 '|'로 묶어 6칸 문자열로 — 빈 레인은 "")
        var items = new string[6];
        for (int i = 0; i < 3; i++) items[i]     = string.Join("|", fm.GetEquippedItems(true,  i).Select(c => c.CardId));
        for (int i = 0; i < 3; i++) items[3 + i] = string.Join("|", fm.GetEquippedItems(false, i).Select(c => c.CardId));

        var tm = TurnManager.Instance;
        var pP = PlayerController.PlayerInstance; // 호스트 자신
        var pO = PlayerController.AIInstance;      // 상대(클라)

        // 손패: 클라의 손패(호스트 AIInstance=권위)는 카드ID로 → 클라 자기 손패 복원.
        //       호스트 자신 손패는 "개수만" → 클라엔 상대 뒷면 개수 (내용 숨김=히든 정보 보호).
        var clientHand   = pO.Hand.Select(c => c.CardId).ToArray();
        int oppHandCount = pP.Hand.Count;

        // 존: 대미지존·트래시는 앞면 공개라 내용(카드ID)까지, 덱은 순서 숨김이라 개수만.
        var pDmg   = pP.DamageZone.Select(c => c.CardId).ToArray();
        var oDmg   = pO.DamageZone.Select(c => c.CardId).ToArray();
        var pTrash = pP.TrashPile.Select(c => c.CardId).ToArray();
        var oTrash = pO.TrashPile.Select(c => c.CardId).ToArray();

        object[] payload = {
            f,
            tm != null && tm.IsPlayerTurn,
            (byte)(tm != null ? tm.CurrentPhase : PhaseType.Main),
            clientHand,
            oppHandCount,
            // 호스트 자신(player) 존: 5~9
            pP.LeaderLevel, pP.IsAwakened, pDmg, pP.DrawPile.Count, pTrash,
            // 상대(클라) 존: 10~14
            pO.LeaderLevel, pO.IsAwakened, oDmg, pO.DrawPile.Count, oTrash,
            items, // 15: 레인별 장착 아이템
        };
        PhotonNetwork.RaiseEvent(
            EV_STATE, payload,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    // 상대 손패 개수 표시용 플레이스홀더 (내용 안 보이는 뒷면이라 빈 카드로 충분)
    private static CardData _handPlaceholder;
    private static CardData HandPlaceholder()
        => _handPlaceholder != null ? _handPlaceholder : (_handPlaceholder = ScriptableObject.CreateInstance<CardData>());

    // [클라] 호스트 상태를 화면에 반영 (미러 관점: 호스트의 상대=클라의 나).
    private void OnStateReceived(EventData e)
    {
        if (PhotonNetwork.IsMasterClient) return;
        var payload = (object[])e.CustomData;
        var f       = (string[])payload[0];
        bool hostIsPlayerTurn = (bool)payload[1];
        var phase   = (PhaseType)(byte)payload[2];

        var fm = FieldManager.Instance;
        // 아이템 먼저 세팅 (미러) — 뒤이은 ReplicateSetUnit이 RefreshUI로 아이템까지 그림
        var items = (string[])payload[15];
        for (int i = 0; i < 3; i++) fm.ReplicateSetItems(false, i, ParseItems(items[i]));
        for (int i = 0; i < 3; i++) fm.ReplicateSetItems(true,  i, ParseItems(items[3 + i]));

        // 필드 미러: 호스트 플레이어(f[0..2]) → 클라의 상대(false) / 호스트 상대(f[3..5]) → 클라의 나(true)
        for (int i = 0; i < 3; i++) fm.ReplicateSetUnit(false, i, CardLookup.ById(f[i]));
        for (int i = 0; i < 3; i++) fm.ReplicateSetUnit(true,  i, CardLookup.ById(f[3 + i]));

        // 손패: 내 손패(권위)는 카드ID로 복원, 상대 손패는 개수만(플레이스홀더)
        var clientHand = (string[])payload[3];
        int oppCount   = (int)payload[4];

        var myPc = PlayerController.PlayerInstance;   // 클라 자신
        myPc.ClearHand();
        foreach (var cid in clientHand) { var c = CardLookup.ById(cid); if (c != null) myPc.AddToHand(c); }
        HandView.Instance?.RefreshHand();

        var oppPc = PlayerController.AIInstance;       // 상대(호스트) — 개수만
        oppPc.ClearHand();
        for (int i = 0; i < oppCount; i++) oppPc.AddToHand(HandPlaceholder());
        AIHandView.NotifyHandChanged();

        // 존/HUD 미러: 호스트 자신(5~9) → 클라의 상대(AIInstance), 호스트 상대(10~14) → 클라의 나(PlayerInstance)
        var ph = HandPlaceholder();
        myPc.ReplicateZones(
            (int)payload[10], (bool)payload[11], IdsToCards((string[])payload[12]), (int)payload[13], IdsToCards((string[])payload[14]), ph);
        oppPc.ReplicateZones(
            (int)payload[5],  (bool)payload[6],  IdsToCards((string[])payload[7]),  (int)payload[8],  IdsToCards((string[])payload[9]),  ph);

        DamageZoneView.NotifyDamageChanged();
        HUDView.NotifyStatusChanged();

        // 턴 미러: 호스트의 턴(true) = 클라 입장에선 상대 턴(false)
        TurnManager.Instance?.ApplyNetworkTurn(!hostIsPlayerTurn, phase);
    }

    private static System.Collections.Generic.List<CardData> IdsToCards(string[] ids)
    {
        var list = new System.Collections.Generic.List<CardData>();
        if (ids != null)
            foreach (var id in ids) { var c = CardLookup.ById(id); if (c != null) list.Add(c); }
        return list;
    }

    // "id1|id2" → 카드 리스트 (빈 문자열이면 빈 리스트)
    private static System.Collections.Generic.List<CardData> ParseItems(string packed)
    {
        var list = new System.Collections.Generic.List<CardData>();
        if (string.IsNullOrEmpty(packed)) return list;
        foreach (var id in packed.Split('|')) { var c = CardLookup.ById(id); if (c != null) list.Add(c); }
        return list;
    }

    // [클라→호스트] 페이즈 종료 릴레이
    public void SendEndPhase(int which)
    {
        PhotonNetwork.RaiseEvent(
            EV_ENDPHASE, (byte)which,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            SendOptions.SendReliable);
        Debug.Log($"[Net] 페이즈 종료 릴레이 → 호스트: {which}");
    }

    // [호스트] 클라의 페이즈 종료 수신 → TurnManager 플래그 세팅 (대기 중인 코루틴 재개)
    private void OnEndPhaseReceived(EventData e)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        int which = (byte)e.CustomData;
        var tm = TurnManager.Instance;
        if (tm == null) return;
        if      (which == 0) tm.NetworkEndMainPhase();
        else if (which == 1) tm.NetworkEndAttackPhase();
        else if (which == 2) tm.NetworkSkipToEnd();
        Debug.Log($"[Net] 페이즈 종료 수신(클라): {which} → 진행");
    }

    // 이벤트 수신 (행동/방어요청/방어응답/상태복제)
    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case EV_ACTION:       OnActionReceived(photonEvent); break;
            case EV_DEFENSE_REQ:  OnDefenseRequested(photonEvent); break;
            case EV_DEFENSE_RESP: OnDefenseResponded(photonEvent); break;
            case EV_STATE:        OnStateReceived(photonEvent); break;
            case EV_ENDPHASE:     OnEndPhaseReceived(photonEvent); break;
            case EV_CARD_REQ:     OnCardRequested(photonEvent); break;
            case EV_CARD_RESP:    OnCardResponded(photonEvent); break;
            case EV_MULL_REQ:     OnMulliganRequested(photonEvent); break;
            case EV_MULL_RESP:    OnMulliganResponded(photonEvent); break;
            case EV_GAMEOVER:     OnGameOverReceived(photonEvent); break;
            case EV_TOAST:        OnToastReceived(photonEvent); break;
            case EV_TRIGGER:      OnTriggerReceived(photonEvent); break;
        }
    }

    // ── 커멘터리/트리거 릴레이 (호스트→클라, 화면 피드백만) ──────────

    // [호스트] 커멘터리 토스트를 클라에 전송.
    public void RelayToast(string message)
    {
        PhotonNetwork.RaiseEvent(
            EV_TOAST, message,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    // [클라] 커멘터리 토스트 수신 → 그대로 표시 (문구는 이미 "상대: ~" = 클라 관점).
    private void OnToastReceived(EventData e)
    {
        if (PhotonNetwork.IsMasterClient) return;
        ToastView.Show((string)e.CustomData);
    }

    // [호스트] 트리거 공개(cardId)를 클라에 전송.
    public void RelayTrigger(string cardId)
    {
        PhotonNetwork.RaiseEvent(
            EV_TRIGGER, cardId,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    // [클라] 트리거 공개 수신 → 카드 이미지를 잠시 크게 표시 (관점 무관 = 안전).
    private void OnTriggerReceived(EventData e)
    {
        if (PhotonNetwork.IsMasterClient) return;
        var card = CardLookup.ById((string)e.CustomData);
        if (card != null)
            CardZoomView.Instance?.ShowTimed(card.Artwork, 3f);
    }

    // ── 승패 결과 동기화 ─────────────────────────────────────────

    // [호스트] 승패 결과를 클라에 전송.
    public void BroadcastGameOver(bool loserIsHostPlayer)
    {
        PhotonNetwork.RaiseEvent(
            EV_GAMEOVER, loserIsHostPlayer,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
        Debug.Log($"[Net] 게임오버 전송 (호스트 패배={loserIsHostPlayer})");
    }

    // [클라] 호스트 결과 수신 → 미러 관점으로 결과 화면 표시.
    //  · 호스트의 플레이어(true) = 클라의 상대. 호스트 플레이어가 졌으면 클라는 승리.
    private void OnGameOverReceived(EventData e)
    {
        if (PhotonNetwork.IsMasterClient) return;
        bool loserIsHostPlayer = (bool)e.CustomData;
        bool clientLost = !loserIsHostPlayer; // 미러: 호스트 상대가 나(클라)
        GameManager.Instance?.ApplyNetworkGameOver(clientLost);
    }

    // ── 원격 멀리건 (게임 시작 시) ────────────────────────────────

    // [호스트] 클라에게 멀리건 여부 요청. 응답 시 onDecided(true=교체).
    public void RequestMulligan(System.Action<bool> onDecided)
    {
        _pendingMulligan = onDecided;
        PhotonNetwork.RaiseEvent(
            EV_MULL_REQ, (byte)0,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
        Debug.Log("[Net] 멀리건 요청 → 클라 (응답 대기)");
    }

    // [클라] 멀리건 요청 → 자기 손패로 멀리건 팝업 (손패는 직전 상태복제로 갱신됨)
    private void OnMulliganRequested(EventData e)
    {
        if (PhotonNetwork.IsMasterClient) return;
        // 씬/손패가 아직 준비 안 됐으면 안전하게 "유지"로 응답 (호스트 무한대기 방지)
        var pc = PlayerController.PlayerInstance;
        if (pc == null || pc.Hand == null || pc.Hand.Count == 0)
        {
            PhotonNetwork.RaiseEvent(EV_MULL_RESP, false,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
            return;
        }
        var hand = new System.Collections.Generic.List<CardData>(pc.Hand);
        if (HandPickPopup.Instance != null)
            HandPickPopup.Instance.ShowMulligan("멀리건 — 손패를 교체하시겠습니까?", hand, choice =>
                PhotonNetwork.RaiseEvent(EV_MULL_RESP, choice,
                    new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable));
        else
            PhotonNetwork.RaiseEvent(EV_MULL_RESP, false,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    // [호스트] 클라의 멀리건 응답 → 대기 콜백 호출 (교체 실행은 GameManager)
    private void OnMulliganResponded(EventData e)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        bool choice = (bool)e.CustomData;
        var cb = _pendingMulligan; _pendingMulligan = null;
        Debug.Log($"[Net] 멀리건 응답 수신: {(choice ? "교체" : "유지")}");
        cb?.Invoke(choice);
    }

    // ── 원격 카드 선택 (효과 중 클라가 하는 선택) ────────────────

    // [호스트] 클라의 카드 선택 필요 → 요청. 응답 오면 onPicked/onCancel.
    public void RequestCard(bool chooserIsPlayer, string title,
        System.Collections.Generic.List<CardData> candidates,
        System.Action<CardData> onPicked, System.Action onCancel)
    {
        _pendingCardPick       = onPicked;
        _pendingCardCancel     = onCancel;
        _pendingCardCandidates = candidates;
        // 후보 없음 → 즉시 취소 처리 (원격 왕복 없이)
        if (candidates == null || candidates.Count == 0)
        {
            _pendingCardPick = null; _pendingCardCancel = null; _pendingCardCandidates = null;
            onCancel?.Invoke();
            return;
        }
        var ids = candidates.Select(c => c.CardId).ToArray();
        object[] data = { title, ids };
        PhotonNetwork.RaiseEvent(
            EV_CARD_REQ, data,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
        Debug.Log($"[Net] 카드 선택 요청 → 클라 ({candidates.Count}장)");
    }

    // [클라] 호스트의 카드 선택 요청 → 로컬 팝업 → 선택/취소 응답
    private void OnCardRequested(EventData e)
    {
        if (PhotonNetwork.IsMasterClient) return;
        var data  = (object[])e.CustomData;
        string title = (string)data[0];
        var cands    = IdsToCards((string[])data[1]);
        if (cands.Count == 0) { RespondCard(""); return; }

        if (HandPickPopup.Instance != null)
            HandPickPopup.Instance.Show(title, cands,
                picked => RespondCard(picked != null ? picked.CardId : ""),
                onCancel: () => RespondCard(""));
        else
            RespondCard(cands[0].CardId);
    }

    private void RespondCard(string cardId)
    {
        PhotonNetwork.RaiseEvent(
            EV_CARD_RESP, cardId ?? "",
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            SendOptions.SendReliable);
    }

    // [호스트] 클라의 카드 선택 응답 → 대기 콜백 호출 (빈 문자열=취소)
    private void OnCardResponded(EventData e)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        string cid = (string)e.CustomData;
        var pick   = _pendingCardPick;
        var cancel = _pendingCardCancel;
        var cands  = _pendingCardCandidates;
        _pendingCardPick = null; _pendingCardCancel = null; _pendingCardCandidates = null;

        if (string.IsNullOrEmpty(cid)) { cancel?.Invoke(); return; }
        var chosen = cands?.Find(c => c.CardId == cid) ?? CardLookup.ById(cid); // 동명카드 fungible
        pick?.Invoke(chosen);
        Debug.Log($"[Net] 카드 선택 응답 수신: {cid}");
    }

    // [호스트] 클라의 행동 → 재구성 후 실행 (권위 실행)
    private void OnActionReceived(EventData e)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        var data   = (object[])e.CustomData;
        var type   = (GameActionType)(byte)data[0];
        int lane   = (int)data[1];
        string cid = (string)data[2];

        // 클라(상대) = 호스트 관점 isPlayer=false 슬롯(AIInstance)
        var card = string.IsNullOrEmpty(cid) ? null : PlayerController.AIInstance.Hand.FirstOrDefault(c => c.CardId == cid);
        Debug.Log($"[Net] 행동 수신(클라): {type} lane{lane} {cid} → 호스트 실행");
        GameActionGateway.Submit(new GameAction { Type = type, IsPlayer = false, Lane = lane, Card = card });
        HandView.Instance?.RefreshHand();
        HUDView.NotifyStatusChanged();
    }

    // [클라] 호스트의 방어 요청 → 로컬 팝업 표시 → 선택하면 응답 전송
    private void OnDefenseRequested(EventData e)
    {
        if (PhotonNetwork.IsMasterClient) return; // 클라만
        var data = (object[])e.CustomData;
        var attacker = CardLookup.ById((string)data[0]);
        var defender = CardLookup.ById((string)data[1]);
        // 호스트가 계산한 실제 전투 수치 (구버전 페이로드 호환: 없으면 -1 = 인쇄 파워로 폴백)
        int atkPower = data.Length > 2 ? System.Convert.ToInt32(data[2]) : -1;
        int defPower = data.Length > 3 ? System.Convert.ToInt32(data[3]) : -1;
        Debug.Log("[Net] 방어 요청 수신 → 팝업 표시");

        if (DefensePopup.Instance != null)
            DefensePopup.Instance.Show(attacker, defender, choice =>
            {
                PhotonNetwork.RaiseEvent(
                    EV_DEFENSE_RESP, choice,
                    new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
                    SendOptions.SendReliable);
            }, atkPower, defPower);
        else
            PhotonNetwork.RaiseEvent(EV_DEFENSE_RESP, false,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }

    // [호스트] 클라의 방어 응답 → 대기 콜백 호출 (전투 코루틴 재개)
    private void OnDefenseResponded(EventData e)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        bool choice = (bool)e.CustomData;
        Debug.Log($"[Net] 방어 응답 수신: {(choice ? "방어" : "패스")}");
        var cb = _pendingDefense;
        _pendingDefense = null;
        cb?.Invoke(choice);
    }
}
