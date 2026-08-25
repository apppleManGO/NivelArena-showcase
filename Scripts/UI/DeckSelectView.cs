// Assets/Scripts/UI/DeckSelectView.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class DeckSelectView : MonoBehaviour
{
    [Header("플레이어 덱 드롭다운")]
    public TMP_Dropdown PlayerDeckDropdown;
    public Image        PlayerLeaderPreview;

    [Header("AI 덱 드롭다운")]
    public TMP_Dropdown AIDeckDropdown;
    public Image        AILeaderPreview;

    [Header("선택 확인")]
    public Button    StartButton;
    public TMP_Text  SelectedDeckText;

    [Header("덱 빌더")]
    public Button NewDeckButton;
    public Button EditDeckButton;
    public Button DeleteDeckButton;

    [Header("삭제 확인 팝업")]
    public GameObject DeleteConfirmPopup;   // 평소 비활성화
    public TMP_Text   DeleteConfirmText;    // "'{덱이름}'을 삭제하시겠습니까?"
    public Button     DeleteConfirmYes;
    public Button     DeleteConfirmNo;

    // 드롭다운 항목 데이터 (스타터 + 커스텀)
    // isCustom=false → deckId로 Resources.Load, isCustom=true → deckName으로 JSON 로드
    struct DeckEntry
    {
        public string displayName;
        public string deckId;      // 스타터용
        public string deckName;    // 커스텀용
        public bool   isCustom;
    }

    readonly List<DeckEntry> _entries = new();

    static readonly string[] StarterIds = {
        "ST01","ST02","ST03","ST04","ST05","ST06",
        "ST07","ST08","ST09","ST10","ST11"
    };
    static readonly string[] StarterNames = {
        "ST01 - 카운터스", "ST02 - 리얼 카인드니스", "ST03 - 헬레틱",
        "ST04 - 인헤르트", "ST05 - 메이드 포 유", "ST06 - 계승자",
        "ST07 - 호문클루스", "ST08 - 애니버서리", "ST09 - 트로피컬",
        "ST10 - 베이룬", "ST11 - 코퀴토스"
    };

    void Start()
    {
        if (GameSettings.Instance == null)
            new GameObject("GameSettings").AddComponent<GameSettings>();

        BuildEntries();
        SetupDropdown(PlayerDeckDropdown, 0, idx => { UpdatePreview(PlayerLeaderPreview, idx); UpdateEditDeleteButtons(); ApplySelectedPlayerDeck(); });
        SetupDropdown(AIDeckDropdown,     1, idx => UpdatePreview(AILeaderPreview, idx));

        ApplySelectedPlayerDeck(); // 초기 선택도 즉시 반영 (드롭다운을 안 건드려도 온라인에서 올바른 덱)

        if (StartButton       != null) StartButton.onClick.AddListener(OnStartGame);
        if (NewDeckButton     != null) NewDeckButton.onClick.AddListener(GoToDeckBuilder);
        if (EditDeckButton    != null) EditDeckButton.onClick.AddListener(EditSelectedDeck);
        if (DeleteDeckButton  != null) DeleteDeckButton.onClick.AddListener(OnDeleteClick);

        if (DeleteConfirmYes  != null) DeleteConfirmYes.onClick.AddListener(OnDeleteConfirmed);
        if (DeleteConfirmNo   != null) DeleteConfirmNo.onClick.AddListener(CloseDeletePopup);
        if (DeleteConfirmPopup != null) DeleteConfirmPopup.SetActive(false);

        UpdateEditDeleteButtons();
    }

    void BuildEntries()
    {
        _entries.Clear();

        // 스타터 덱
        for (int i = 0; i < StarterIds.Length; i++)
            _entries.Add(new DeckEntry { displayName = StarterNames[i], deckId = StarterIds[i], isCustom = false });

        // 커스텀 덱 (JSON 저장된 덱 목록)
        foreach (var name in DeckSaveLoad.GetSavedDeckNames())
            _entries.Add(new DeckEntry { displayName = $"[커스텀] {name}", deckName = name, isCustom = true });
    }

    void SetupDropdown(TMP_Dropdown dropdown, int defaultIndex, System.Action<int> onChanged)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        var options = new List<string>();
        foreach (var e in _entries) options.Add(e.displayName);
        dropdown.AddOptions(options);

        // ★ 이 메서드는 목록이 바뀔 때마다 다시 불린다(삭제/로그인 전환).
        //   지우지 않으면 리스너가 계속 쌓여 한 번의 선택에 콜백이 여러 번 돈다.
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(idx => onChanged(idx));

        dropdown.SetValueWithoutNotify(Mathf.Clamp(defaultIndex, 0, Mathf.Max(0, _entries.Count - 1)));
        onChanged(dropdown.value);
    }

    void UpdatePreview(Image preview, int entryIdx)
    {
        if (preview == null || entryIdx >= _entries.Count) return;
        var e = _entries[entryIdx];

        Sprite artwork = null;
        if (!e.isCustom)
        {
            var deck = Resources.Load<DeckData>($"Decks/{e.deckId}");
            if (deck?.Leader?.BaseArtwork != null)
                artwork = deck.Leader.BaseArtwork;
        }
        else
        {
            var save = DeckSaveLoad.Load(e.deckName);
            if (save != null)
            {
                var leader = Resources.Load<LeaderData>($"Leaders/{save.leaderId}");
                if (leader?.BaseArtwork != null)
                    artwork = leader.BaseArtwork;
            }
        }

        preview.sprite  = artwork;
        preview.enabled = artwork != null;
    }

    // 편집/삭제 버튼은 커스텀 덱이 선택됐을 때만 활성화
    void UpdateEditDeleteButtons()
    {
        bool isCustom = IsPlayerSelectionCustom();
        if (EditDeckButton   != null) EditDeckButton.interactable   = isCustom;
        if (DeleteDeckButton != null) DeleteDeckButton.interactable = isCustom;
    }

    bool IsPlayerSelectionCustom()
    {
        if (PlayerDeckDropdown == null) return false;
        int idx = PlayerDeckDropdown.value;
        return idx < _entries.Count && _entries[idx].isCustom;
    }

    void GoToDeckBuilder()
    {
        if (GameSettings.Instance != null) GameSettings.Instance.EditDeckName = "";
        SceneManager.LoadScene("DeckBuilder");
    }

    void EditSelectedDeck()
    {
        int idx = PlayerDeckDropdown != null ? PlayerDeckDropdown.value : 0;
        if (idx < _entries.Count && _entries[idx].isCustom)
        {
            if (GameSettings.Instance != null)
                GameSettings.Instance.EditDeckName = _entries[idx].deckName;
        }
        SceneManager.LoadScene("DeckBuilder");
    }

    void OnDeleteClick()
    {
        int idx = PlayerDeckDropdown != null ? PlayerDeckDropdown.value : 0;
        if (idx >= _entries.Count || !_entries[idx].isCustom) return;

        string name = _entries[idx].deckName;
        if (DeleteConfirmText != null)
            DeleteConfirmText.text = $"'{name}'\n을 삭제하시겠습니까?";
        if (DeleteConfirmPopup != null)
            DeleteConfirmPopup.SetActive(true);
    }

    void OnDeleteConfirmed()
    {
        int idx = PlayerDeckDropdown != null ? PlayerDeckDropdown.value : 0;
        if (idx < _entries.Count && _entries[idx].isCustom)
        {
            DeckSaveLoad.Delete(_entries[idx].deckName);
            Debug.Log($"[DeckSelect] '{_entries[idx].deckName}' 삭제");
        }

        CloseDeletePopup();

        // 드롭다운 재구성
        BuildEntries();
        SetupDropdown(PlayerDeckDropdown, 0, i => { UpdatePreview(PlayerLeaderPreview, i); UpdateEditDeleteButtons(); });
        SetupDropdown(AIDeckDropdown,     1, i => UpdatePreview(AILeaderPreview, i));
        UpdateEditDeleteButtons();
    }

    void CloseDeletePopup()
    {
        if (DeleteConfirmPopup != null) DeleteConfirmPopup.SetActive(false);
    }

    // 덱 목록을 다시 읽어 드롭다운을 재구성 (로그인/로그아웃으로 저장 위치가 바뀔 때 — LoginView가 호출).
    //  · 선택 중이던 덱은 이름으로 다시 찾아 복원하고, 없어졌으면 0번으로 되돌린다.
    public void RebuildDeckList()
    {
        string prevPlayer = (PlayerDeckDropdown != null && PlayerDeckDropdown.value < _entries.Count)
            ? _entries[PlayerDeckDropdown.value].displayName : null;
        string prevAI = (AIDeckDropdown != null && AIDeckDropdown.value < _entries.Count)
            ? _entries[AIDeckDropdown.value].displayName : null;

        BuildEntries();

        SetupDropdown(PlayerDeckDropdown, IndexOf(prevPlayer, 0),
            i => { UpdatePreview(PlayerLeaderPreview, i); UpdateEditDeleteButtons(); ApplySelectedPlayerDeck(); });
        SetupDropdown(AIDeckDropdown, IndexOf(prevAI, 1),
            i => UpdatePreview(AILeaderPreview, i));

        UpdateEditDeleteButtons();
        ApplySelectedPlayerDeck();
    }

    int IndexOf(string displayName, int fallback)
    {
        if (!string.IsNullOrEmpty(displayName))
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].displayName == displayName) return i;
        return Mathf.Min(fallback, Mathf.Max(0, _entries.Count - 1));
    }

    // 현재 드롭다운에서 선택한 플레이어 덱을 GameSettings에 반영.
    //  · 드롭다운 변경 시 + 시작 시 호출 → 온라인 [입장]도 이 값을 그대로 사용 (기본 ST01로 안 빠짐).
    public void ApplySelectedPlayerDeck()
    {
        if (PlayerDeckDropdown == null || GameSettings.Instance == null) return;
        if (PlayerDeckDropdown.value >= _entries.Count) return;
        var e = _entries[PlayerDeckDropdown.value];
        if (e.isCustom) GameSettings.Instance.SetPlayerCustomDeck(e.deckName);
        else            GameSettings.Instance.SetPlayerDeck(e.deckId);
    }

    void OnStartGame()
    {
        ApplySelectedPlayerDeck();

        if (AIDeckDropdown != null)
        {
            var e = _entries[AIDeckDropdown.value];
            if (e.isCustom)
                GameSettings.Instance.SetAICustomDeck(e.deckName);
            else
                GameSettings.Instance.SetAIDeck(e.deckId);
        }

        SceneManager.LoadScene("SampleScene");
    }
}
