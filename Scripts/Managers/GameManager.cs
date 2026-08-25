// Assets/Scripts/Managers/GameManager.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public bool IsGameOver { get; private set; } = false;

    // 결과 화면 UI 연결
    public ResultView ResultPanel;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // GameSettings에서 덱 로드 (덱 선택 화면에서 넘어온 경우)
        if (GameSettings.Instance != null)
        {
            var playerDeck = !string.IsNullOrEmpty(GameSettings.Instance.PlayerCustomDeck)
                ? LoadCustomDeck(GameSettings.Instance.PlayerCustomDeck)
                : LoadDeck(GameSettings.Instance.PlayerDeckId);

            // 상대 덱: 네트워크로 받은 커스텀덱(cardIds)이 있으면 그걸로, 없으면 로컬 커스텀/스타터
            DeckData aiDeck;
            if (GameSettings.Instance.AINetCardIds != null && GameSettings.Instance.AINetCardIds.Count > 0)
                aiDeck = BuildDeck("상대(네트워크)", GameSettings.Instance.AINetLeaderId, GameSettings.Instance.AINetCardIds);
            else if (!string.IsNullOrEmpty(GameSettings.Instance.AICustomDeck))
                aiDeck = LoadCustomDeck(GameSettings.Instance.AICustomDeck);
            else
                aiDeck = LoadDeck(GameSettings.Instance.AIDeckId);

            if (playerDeck != null) PlayerController.PlayerInstance.DeckAsset = playerDeck;
            if (aiDeck     != null) PlayerController.AIInstance.DeckAsset     = aiDeck;

            // 덱 합법성 검증 (치트 방지). 온라인은 호스트가 상대 덱까지 검증 → 위반 시 대전 취소.
            if (!ValidateDeck(playerDeck, "플레이어") || !ValidateDeck(aiDeck, NetHub.OpponentLabel))
            {
                if (NetHub.IsOnline)
                {
                    ToastView.Show("덱 규칙 위반 — 대전을 취소합니다");
                    if (PhotonNetManager.Instance != null) PhotonNetManager.Instance.LeaveRoom();
                    SceneManager.LoadScene("DeckSelect");
                    return;
                }
                // 오프라인은 로그만 (싱글 플레이 차단하지 않음)
            }
        }

        PlayerController.PlayerInstance.InitializeDeck();
        PlayerController.AIInstance.InitializeDeck();

        if (ResultPanel != null)
            ResultPanel.Hide();

        StartCoroutine(MulliganThenStart());
    }

    // 룰북 5.1.5: 랜덤으로 선후공 결정 후 룰북 5.1.6: 선공 → 후공 순으로 멀리건
    private IEnumerator MulliganThenStart()
    {
        bool playerGoesFirst = UnityEngine.Random.value >= 0.5f;
        string firstLabel = playerGoesFirst ? "플레이어 선공!" : $"{NetHub.OpponentLabel} 선공!";
        ToastView.Show(firstLabel);
        Debug.Log($"[선후공] {firstLabel}");

        yield return new WaitForSeconds(1.5f);

        // 선공 플레이어부터 멀리건 (룰북 5.1.6)
        if (playerGoesFirst)
        {
            yield return StartCoroutine(DoMulligan(PlayerController.PlayerInstance, "플레이어", isPlayer: true));
            yield return StartCoroutine(DoMulligan(PlayerController.AIInstance, "AI", isPlayer: false));
        }
        else
        {
            yield return StartCoroutine(DoMulligan(PlayerController.AIInstance, "AI", isPlayer: false));
            yield return StartCoroutine(DoMulligan(PlayerController.PlayerInstance, "플레이어", isPlayer: true));
        }

        TurnManager.Instance.StartGame(playerGoesFirst);
    }

    private IEnumerator DoMulligan(PlayerController pc, string label, bool isPlayer)
    {
        // 온라인 멀리건:
        if (NetHub.IsOnline)
        {
            if (!NetHub.IsAuthority) yield break; // 클라는 시작 시뮬 안 함(호스트가 구동)

            if (!isPlayer)
            {
                // 호스트가 클라 슬롯 멀리건을 "원격"으로 진행: 손패 복제 → 요청 → 응답 → 교체
                NetHub.BroadcastState(); // 클라가 자기 손패 보도록 먼저 복제
                bool decidedR = false, doMulR = false;
                NetHub.RequestMulligan(c => { doMulR = c; decidedR = true; });
                yield return new WaitUntil(() => decidedR);
                if (doMulR) ApplyMulligan(pc);
                NetHub.BroadcastState(); // 교체 결과 복제
                yield break;
            }
            // isPlayer=true(호스트 자신)면 아래 로컬 멀리건 팝업으로 진행
        }

        if (isPlayer)
        {
            bool decided = false;
            bool doMulligan = false;

            HandPickPopup.Instance?.ShowMulligan(label, new System.Collections.Generic.List<CardData>(pc.Hand), choice =>
            {
                doMulligan = choice;
                decided    = true;
            });

            yield return new WaitUntil(() => decided);

            if (doMulligan)
                ApplyMulligan(pc);
        }
        else
        {
            // AI: 패 평균 코스트가 3 초과면 교체
            float avg = 0f;
            foreach (var c in pc.Hand) avg += c.Cost;
            avg /= Mathf.Max(1, pc.Hand.Count);
            if (avg > 3f)
            {
                Debug.Log($"[멀리건] AI 교체 (평균코스트 {avg:F1})");
                ApplyMulligan(pc);
            }
            else
            {
                Debug.Log($"[멀리건] AI 유지 (평균코스트 {avg:F1})");
            }
            yield return new WaitForSeconds(0.3f);
        }
    }

    private void ApplyMulligan(PlayerController pc)
    {
        // 패 전부 덱으로 반환 → 셔플 → 5장 재드로우
        foreach (var card in pc.Hand)
            pc.AddToDeckBottom(card);
        pc.ClearHand();

        pc.ShuffleDeck();

        pc.DrawCard(5);
        HandView.Instance?.RefreshHand();
        HUDView.NotifyStatusChanged();
        Debug.Log($"[멀리건] {pc.name} 패 교체 완료");
    }

    private DeckData LoadDeck(string deckId)
    {
        if (string.IsNullOrEmpty(deckId)) return null;
        var deck = Resources.Load<DeckData>($"Decks/{deckId}");
        if (deck == null)
            Debug.LogWarning($"[GameManager] 덱 로드 실패: Decks/{deckId}");
        return deck;
    }

    // 덱 합법성 검증 (치트 방지). 위반 시 false + 콘솔 경고.
    private bool ValidateDeck(DeckData deck, string label)
    {
        if (deck == null) return true; // 로드 실패는 별도 처리 — 검증에선 통과 취급
        if (DeckValidator.Validate(deck, out string reason)) return true;
        Debug.LogWarning($"[덱검증] {label} 덱 규칙 위반: {reason}");
        return false;
    }

    private DeckData LoadCustomDeck(string deckName)
    {
        var save = DeckSaveLoad.Load(deckName);
        if (save == null)
        {
            Debug.LogWarning($"[GameManager] 커스텀 덱 로드 실패: {deckName}");
            return null;
        }
        return BuildDeck(deckName, save.leaderId, save.cardIds);
    }

    // leaderId + cardIds로 런타임 DeckData 생성 (로컬 커스텀 덱 / 네트워크 수신 덱 공용)
    public DeckData BuildDeck(string name, string leaderId, System.Collections.Generic.List<string> cardIds)
    {
        var cardMap   = new System.Collections.Generic.Dictionary<string, CardData>();
        var leaderMap = new System.Collections.Generic.Dictionary<string, LeaderData>();
        foreach (var c in Resources.LoadAll<CardData>("Cards"))
            if (c != null && !cardMap.ContainsKey(c.CardId)) cardMap[c.CardId] = c;
        foreach (var l in Resources.LoadAll<LeaderData>("Leaders"))
            if (l != null && !leaderMap.ContainsKey(l.CardId)) leaderMap[l.CardId] = l;

        var deck      = ScriptableObject.CreateInstance<DeckData>();
        deck.DeckId   = name;
        deck.DeckName = name;
        leaderMap.TryGetValue(leaderId ?? "", out deck.Leader);

        var cards = new System.Collections.Generic.List<CardData>();
        foreach (var id in cardIds ?? new System.Collections.Generic.List<string>())
        {
            if (cardMap.TryGetValue(id, out var card)) cards.Add(card);
            else Debug.LogWarning($"[GameManager] 카드 로드 실패: {id}");
        }
        deck.Cards = cards;

        Debug.Log($"[GameManager] 덱 '{name}' 생성 — 리더:{deck.Leader?.CardId} 카드:{cards.Count}장");
        return deck;
    }

    public void OnPlayerDefeated(PlayerController loser)
    {
        if (IsGameOver) return; // 중복 호출 방지
        IsGameOver = true;

        bool playerLost = loser == PlayerController.PlayerInstance;
        Debug.Log(playerLost ? "[결과] 플레이어 패배" : "[결과] AI 패배");

        // 온라인 호스트: 클라에도 결과 전송 (미러 관점으로 표시됨)
        if (NetHub.IsOnline && NetHub.IsAuthority)
            NetHub.BroadcastGameOver(playerLost);

        StartCoroutine(ShowResult(playerLost));
    }

    // [클라] 호스트가 보낸 게임오버 결과를 이 머신 관점(clientLost)으로 표시.
    public void ApplyNetworkGameOver(bool clientLost)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        Debug.Log(clientLost ? "[결과] (온라인) 패배" : "[결과] (온라인) 승리");
        StartCoroutine(ShowResult(clientLost));
    }

    private IEnumerator ShowResult(bool playerLost)
    {
        // 잠깐 딜레이 후 결과 화면 표시
        yield return new WaitForSeconds(1f);

        if (ResultPanel != null)
            ResultPanel.Show(playerLost ? "DEFEAT" : "VICTORY");
    }
}
