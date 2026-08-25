// Assets/Scripts/UI/ResultView.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class ResultView : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text ResultText;     // VICTORY / DEFEAT
    public TMP_Text SubText;        // 부가 메시지
    public Button   RetryButton;    // 다시하기
    public Button   MenuButton;     // 메인으로 (DeckSelect)
    public Button   QuitButton;     // 게임 종료
    public Image    Background;     // 반투명 배경

    [Header("레이어")]
    public Canvas RootCanvas;

    public Color VictoryColor = new Color(1f, 0.85f, 0f);   // 금색
    public Color DefeatColor  = new Color(0.8f, 0.1f, 0.1f); // 빨간색

    private void Start()
    {
        if (RetryButton != null) RetryButton.onClick.AddListener(OnRetry);
        if (MenuButton  != null) MenuButton.onClick.AddListener(OnMenu);
        if (QuitButton  != null) QuitButton.onClick.AddListener(OnQuit);

        // Canvas 최상위 레이어 설정
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = 300;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    public void Show(string result)
    {
        if (RootCanvas != null)
        {
            transform.SetParent(RootCanvas.transform, true);
        }
        transform.SetAsLastSibling();
        gameObject.SetActive(true);

        bool isVictory = result == "VICTORY";

        if (ResultText != null)
        {
            ResultText.text  = result;
            ResultText.color = isVictory ? VictoryColor : DefeatColor;
        }

        if (SubText != null)
            SubText.text = isVictory ? "Well Done!" : "Try Again!";

        // 게임 일시정지
        Time.timeScale = 0f;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f;
    }

    private void OnRetry()
    {
        Time.timeScale = 1f;
        LeaveRoomIfOnline(); // 온라인이면 방을 떠나 desync 방지 (재시작은 로컬 씬 리로드)
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnMenu()
    {
        Time.timeScale = 1f;
        LeaveRoomIfOnline();
        SceneManager.LoadScene("DeckSelect");
    }

    private void OnQuit()
    {
        Time.timeScale = 1f;
        LeaveRoomIfOnline();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // 온라인 대전 중이었다면 Photon 방에서 나가 오프라인으로 복귀
    private static void LeaveRoomIfOnline()
    {
        if (NetHub.IsOnline && PhotonNetManager.Instance != null)
            PhotonNetManager.Instance.LeaveRoom();
    }
}
