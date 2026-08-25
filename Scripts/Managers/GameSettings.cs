// Assets/Scripts/Managers/GameSettings.cs
// 씬 전환 시 선택한 덱 정보를 유지하는 싱글톤
using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance { get; private set; }

    public string PlayerDeckId     { get; private set; } = "ST01";
    public string AIDeckId         { get; private set; } = "ST02";
    public string EditDeckName     { get; set; }          = "";

    // 커스텀 덱 (null이면 스타터 덱 사용)
    public string PlayerCustomDeck { get; private set; } = null;
    public string AICustomDeck     { get; private set; } = null;

    // 네트워크로 수신한 상대 커스텀 덱 (cardIds 실물). 있으면 GameManager가 이걸로 상대 덱 구성.
    public string AINetLeaderId { get; private set; } = null;
    public System.Collections.Generic.List<string> AINetCardIds { get; private set; } = null;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetPlayerDeck(string deckId)       { PlayerDeckId = deckId; PlayerCustomDeck = null; }
    public void SetAIDeck(string deckId)           { AIDeckId     = deckId; AICustomDeck = null; AINetLeaderId = null; AINetCardIds = null; }
    public void SetPlayerCustomDeck(string name)   { PlayerCustomDeck = name; }
    public void SetAICustomDeck(string name)       { AICustomDeck     = name; }

    // 네트워크 수신 상대 덱 설정 (LobbyManager가 상대 커스텀덱 payload를 파싱해 호출)
    public void SetAINetDeck(string leaderId, System.Collections.Generic.List<string> cardIds)
    {
        AINetLeaderId = leaderId;
        AINetCardIds  = cardIds;
        AICustomDeck  = null; // 로컬 커스텀 경로와 충돌 방지
    }
}
