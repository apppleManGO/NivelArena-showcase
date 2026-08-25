// Assets/Scripts/UI/LobbyManager.cs
// ─────────────────────────────────────────────────────────────────────────
// 온라인 로비 (3단계-a 마무리). DeckSelect 씬의 "온라인 패널"에 붙인다.
//  흐름: 내 덱 고름(DeckSelect) → [입장] → 방 코드로 접속 → 서로 덱 교환
//        → 2명 모이면 호스트가 게임 씬 동시 로드 → 대전 시작.
//
//  UI 연결(에디터):
//   · RoomCodeInput : 방 코드 입력칸 (TMP_InputField)
//   · StatusText    : 상태 표시 (TMP_Text)
//   · [입장] 버튼 OnClick → LobbyManager.OnCreateOrJoinClicked  (없으면 만들고, 있으면 참가)
//   ※ 같은 GameObject(또는 씬)에 PhotonNetManager 도 있어야 함(채널 연결용).
//
//  ── 주의 ──
//   실제 대전 진행(행동 릴레이/상태 복제)은 3b·3c에서. 지금은 "두 사람이 같은 방에
//   들어가 게임 씬까지 함께 진입 + 덱 세팅"까지 동작한다.
// ─────────────────────────────────────────────────────────────────────────
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI 슬롯 (에디터에서 연결)")]
    public TMP_InputField RoomCodeInput;
    public TMP_Text       StatusText;

    [Header("설정")]
    public string GameSceneName = "SampleScene";

    private const string DeckKey = "deck"; // 플레이어 커스텀 속성 키

    // 전송 payload: 커스텀덱이면 "C|리더ID|카드ID들(콤마)", 스타터면 "S|덱ID".
    //  커스텀은 cardIds 실물을 보내야 호스트가 상대 덱을 정확히 재구성(+검증)할 수 있음.
    private string MyDeckPayload()
    {
        var gs = GameSettings.Instance;
        if (gs != null && !string.IsNullOrEmpty(gs.PlayerCustomDeck))
        {
            var save = DeckSaveLoad.Load(gs.PlayerCustomDeck);
            if (save != null)
                return "C|" + (save.leaderId ?? "") + "|" + string.Join(",", save.cardIds);
        }
        return "S|" + (gs != null ? gs.PlayerDeckId : "ST01");
    }

    // [입장] 버튼 — 방이 없으면 만들고, 있으면 참가 (JoinOrCreate)
    public void OnCreateOrJoinClicked()
    {
        // 최소 요구 버전 미만이면 온라인 차단 (버전 체크 결과)
        if (VersionChecker.OnlineBlocked)
        {
            SetStatus(VersionChecker.BlockReason);
            return;
        }

        string code = RoomCodeInput != null ? RoomCodeInput.text.Trim() : "";
        if (string.IsNullOrEmpty(code)) { SetStatus("방 코드를 입력하세요"); return; }

        if (PhotonNetManager.Instance == null)
        {
            SetStatus("PhotonNetManager가 씬에 없습니다");
            return;
        }

        // 호스트가 LoadLevel 하면 클라도 같은 씬 자동 로드
        PhotonNetwork.AutomaticallySyncScene = true;
        // 내 덱을 플레이어 속성에 실어 보냄 (상대가 읽어 자기 AI슬롯에 설정)
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { DeckKey, MyDeckPayload() } });

        SetStatus("접속 중...");
        PhotonNetManager.Instance.JoinRoom(code); // 연결+입장 (NetHub 채널도 여기서 꽂힘)
    }

    // ── PUN 콜백 ──────────────────────────────────────────────────

    public override void OnJoinedRoom()
    {
        SetStatus($"방 입장. 상대 대기 중... ({PhotonNetwork.CurrentRoom.PlayerCount}/2)");
        TryStartIfReady();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        SetStatus("상대 입장!");
        TryStartIfReady();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        SetStatus("상대가 나갔습니다. 대기 중...");
    }

    // 상대 덱 속성이 늦게 도착할 수 있어, 속성 갱신 때마다 재확인
    public override void OnPlayerPropertiesUpdate(Player target, Hashtable changedProps)
    {
        TryStartIfReady();
    }

    // ── 시작 조건 확인 ────────────────────────────────────────────

    private void TryStartIfReady()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return;

        var opponent = GetOpponent();
        if (opponent == null) return;
        if (!opponent.CustomProperties.ContainsKey(DeckKey)) return; // 상대 덱 아직 미도착

        // 상대 덱을 내 "AI(상대) 슬롯"에 설정 — SampleScene의 AIInstance가 이 덱을 씀
        string oppDeck = (string)opponent.CustomProperties[DeckKey];
        if (oppDeck != null && oppDeck.StartsWith("C|"))
        {
            // 커스텀덱: "C|리더ID|카드ID들" → cardIds 실물로 상대 덱 구성 (호스트가 검증)
            var parts = oppDeck.Split('|');
            string leaderId = parts.Length > 1 ? parts[1] : "";
            var cardIds = parts.Length > 2 && parts[2].Length > 0
                ? new System.Collections.Generic.List<string>(parts[2].Split(','))
                : new System.Collections.Generic.List<string>();
            GameSettings.Instance?.SetAINetDeck(leaderId, cardIds);
        }
        else
        {
            // 스타터: "S|덱ID" (구버전 순수 ID도 허용)
            string id = (oppDeck != null && oppDeck.StartsWith("S|")) ? oppDeck.Substring(2) : oppDeck;
            GameSettings.Instance?.SetAIDeck(id);
        }
        Debug.Log($"[Lobby] 상대 덱 payload: {oppDeck}");

        // 호스트만 씬 로드 → AutomaticallySyncScene로 클라도 함께 로드
        if (PhotonNetwork.IsMasterClient)
        {
            SetStatus("게임 시작!");
            PhotonNetwork.LoadLevel(GameSceneName);
        }
        else
        {
            SetStatus("게임 시작을 기다리는 중...");
        }
    }

    private Player GetOpponent()
    {
        foreach (var p in PhotonNetwork.PlayerList)
            if (p != PhotonNetwork.LocalPlayer) return p;
        return null;
    }

    private void SetStatus(string msg)
    {
        if (StatusText != null) StatusText.text = msg;
        Debug.Log($"[Lobby] {msg}");
    }
}
