// Assets/Scripts/UI/LoginView.cs
// ─────────────────────────────────────────────────────────────────────────
// 로그인/회원가입 화면.
//  · 씬 배치 불필요 — DeckSelect 씬에 자동으로 붙고 UI는 전부 코드로 생성한다.
//    (VersionChecker와 같은 방식. 폰트는 씬의 TMP에서 가져와 한글 렌더)
//  · 로그인은 "선택"이다. 화면 구석의 계정 버튼을 눌렀을 때만 창이 열리고,
//    로그인하지 않아도 게임은 지금까지처럼 전부 동작한다.
//  · 로그인/로그아웃 시 덱 목록이 그 계정 것으로 바뀐다(AuthManager가 처리) →
//    DeckSelect의 드롭다운을 다시 그려준다.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginView : MonoBehaviour
{
    // 우상단 계정 버튼 크기 (높이는 고정, 폭만 아이디 길이에 따라 왼쪽으로 늘어남)
    const float AccountH    = 64f;
    const float AccountMinW = 200f;  // "로그인"처럼 짧을 때의 최소 폭
    const float AccountMaxW = 560f;  // 이보다 길어지면 말줄임(…)
    const float AccountPadX = 28f;   // 글자 좌우 여백

    // 로그인 창 크기 — 오류 문구가 여러 줄이라 세로 여유가 필요하다.
    // 배치(위에서부터): 제목 32 / 안내 104 / 아이디 212 / 비밀번호 294 / 오류 376~546
    //        (아래에서): 닫기 40~104 / 로그인·회원가입 128~200  → 오류 끝(546)과 14px 여유
    const float PanelW   = 720f;
    const float PanelH   = 760f;
    const float MessageH = 170f;     // 22pt 기준 약 5줄까지 수용

    TMP_FontAsset _font;
    RectTransform _accountButton;
    TMP_Text      _hintLabel;

    GameObject   _dialog;       // 로그인 창 (평소 비활성)
    TMP_Text     _accountLabel; // 우상단 "로그인" / "홍길동 ▾"
    TMP_Text     _message;      // 창 안의 안내/오류 문구
    TMP_InputField _idField, _pwField;
    Button       _submitButton;
    TMP_Text     _submitLabel, _toggleLabel, _titleLabel;

    bool _signUpMode;   // false = 로그인, true = 회원가입
    bool _busy;         // 요청 진행 중 (중복 클릭 방지)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        // ★ 씬을 다시 들어올 때마다 확인해야 한다.
        //   이 오브젝트는 씬에 속하므로 DeckBuilder로 갔다 오면 파괴돼 있다 —
        //   최초 1회만 만들면 덱빌더에 다녀온 뒤 로그인 버튼이 사라진다.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureFor(scene.name);
        EnsureFor(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    static void EnsureFor(string sceneName)
    {
        if (sceneName != "DeckSelect") return;              // 대전 씬·덱빌더엔 불필요
        if (FindFirstObjectByType<LoginView>() != null) return;
        new GameObject("LoginView").AddComponent<LoginView>();
    }

    void Start()
    {
        var anyTmp = FindFirstObjectByType<TMP_Text>();
        _font = anyTmp != null ? anyTmp.font : null; // 씬의 한글 폰트 재사용

        BuildUI();
        RefreshAccountLabel();
        AuthManager.OnAuthChanged += OnAuthChanged;
        // 클라우드에만 있던 덱이 내려온 뒤에도 목록을 다시 그려야 한다
        AuthManager.OnDecksSynced += OnDecksSynced;

        // 저장된 세션이 있으면 자동 로그인 (비동기 — 끝나면 OnAuthChanged로 화면이 갱신된다).
        // ★ 반드시 위 구독 뒤에 호출할 것. 먼저 부르면 완료 알림을 놓친다.
        AuthManager.TryAutoSignIn();
    }

    void OnDestroy()
    {
        AuthManager.OnAuthChanged -= OnAuthChanged;
        AuthManager.OnDecksSynced -= OnDecksSynced;
    }

    void OnAuthChanged()
    {
        RefreshAccountLabel();
        // 덱 저장 위치가 바뀌었으므로 선택 화면을 다시 그린다
        FindFirstObjectByType<DeckSelectView>()?.RebuildDeckList();
    }

    // 동기화는 로그인보다 늦게 끝난다 — 그때 목록을 한 번 더 그려야 클라우드 덱이 보인다
    void OnDecksSynced() => FindFirstObjectByType<DeckSelectView>()?.RebuildDeckList();

    // ── 화면 구성 ────────────────────────────────────────────────
    void BuildUI()
    {
        var canvasGo = new GameObject("LoginCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000; // 덱 선택 UI 위, 버전 팝업(32000) 아래
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.transform.SetParent(transform, false);

        // 우상단 계정 버튼 — 이것만 평소에 보인다
        var accBtn = MakeButton(canvasGo.transform, "로그인", new Color(0.20f, 0.22f, 0.27f), OnAccountButton);
        _accountButton = (RectTransform)accBtn.transform;
        // ★ 앵커·피벗이 우상단(1,1) → 폭을 늘리면 오른쪽 끝은 그대로고 **왼쪽으로만** 자란다.
        _accountButton.anchorMin = _accountButton.anchorMax = _accountButton.pivot = new Vector2(1f, 1f);
        _accountButton.sizeDelta        = new Vector2(AccountMinW, AccountH);
        _accountButton.anchoredPosition = new Vector2(-24, -24);

        _accountLabel = accBtn.GetComponentInChildren<TMP_Text>();
        // ★ 줄바꿈을 끈다 — 켜져 있으면 긴 아이디가 두 줄로 접히면서 버튼 높이를 넘어간다.
        //   대신 폭이 글자에 맞춰 늘어나고, 한계를 넘으면 말줄임(…)으로 처리한다.
        _accountLabel.textWrappingMode = TextWrappingModes.NoWrap;
        // ★ Ellipsis를 쓰면 안 된다 — NotoSansKR SDF는 Static이라 말줄임표(U+2026)가 없어서
        //   TMP가 매 갱신마다 경고를 뱉고 알아서 Truncate로 바꾼다. 처음부터 Truncate로 둔다.
        _accountLabel.overflowMode     = TextOverflowModes.Truncate;

        // 라벨을 버튼보다 좌우로 조금 좁게 — 최대 폭에서 말줄임될 때 글자가 가장자리에 붙지 않는다
        var lrt = _accountLabel.rectTransform;
        lrt.offsetMin = new Vector2( AccountPadX * 0.5f, 0f);
        lrt.offsetMax = new Vector2(-AccountPadX * 0.5f, 0f);

        BuildDialog(canvasGo.transform);
    }

    void BuildDialog(Transform parent)
    {
        _dialog = new GameObject("LoginDialog", typeof(RectTransform));
        _dialog.transform.SetParent(parent, false);
        Stretch((RectTransform)_dialog.transform);

        var dim = NewImage(_dialog.transform, new Color(0, 0, 0, 0.75f));
        Stretch(dim.rectTransform);
        // 배경 클릭 = 닫기
        dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseDialog);

        // ── 세로 배치 ──────────────────────────────────────────
        // 위(제목/안내/입력/오류)는 패널 상단 기준, 아래(버튼)는 하단 기준으로 잡는다.
        // ★ 오류 문구는 최대 4줄까지 나올 수 있어(가입 형식 안내) 넉넉히 잡아야 한다.
        //   예전엔 76px이라 잘렸고, 게다가 아래 버튼과 영역이 겹쳐 있었다.
        var panel = NewImage(_dialog.transform, new Color(0.11f, 0.13f, 0.16f, 1f));
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(PanelW, PanelH), Vector2.zero);

        _titleLabel = NewTmp(panel.transform, "로그인", 44, TextAlignmentOptions.Top, FontStyles.Bold);
        Place(_titleLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(640, 60), new Vector2(0, -32));

        _hintLabel = NewTmp(panel.transform, "", 22, TextAlignmentOptions.Top, FontStyles.Normal);
        Place(_hintLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(620, 80), new Vector2(0, -104));
        _hintLabel.color = new Color(0.62f, 0.66f, 0.72f);

        _idField = MakeInput(panel.transform, "아이디", new Vector2(0, -212), false);
        _pwField = MakeInput(panel.transform, "비밀번호", new Vector2(0, -294), true);

        // 오류/진행 문구 — 상단 정렬이라 줄이 늘어나면 아래로 자란다
        _message = NewTmp(panel.transform, "", 22, TextAlignmentOptions.TopLeft, FontStyles.Normal);
        // 폭을 입력칸(560)과 맞춰 왼쪽 라인이 아이디/비밀번호 칸과 일직선이 되게 한다
        Place(_message.rectTransform, new Vector2(0.5f, 1f), new Vector2(560, MessageH), new Vector2(0, -376));
        _message.color = new Color(0.95f, 0.55f, 0.5f);

        _submitButton = MakeButton(panel.transform, "로그인", new Color(0.16f, 0.56f, 0.5f), OnSubmit);
        Place((RectTransform)_submitButton.transform, new Vector2(0.5f, 0f), new Vector2(300, 72), new Vector2(-160, 128));
        _submitLabel = _submitButton.GetComponentInChildren<TMP_Text>();

        var toggleBtn = MakeButton(panel.transform, "회원가입", new Color(0.28f, 0.30f, 0.34f), OnToggleMode);
        Place((RectTransform)toggleBtn.transform, new Vector2(0.5f, 0f), new Vector2(300, 72), new Vector2(160, 128));
        _toggleLabel = toggleBtn.GetComponentInChildren<TMP_Text>();

        var closeBtn = MakeButton(panel.transform, "닫기", new Color(0.24f, 0.25f, 0.29f), CloseDialog);
        Place((RectTransform)closeBtn.transform, new Vector2(0.5f, 0f), new Vector2(620, 64), new Vector2(0, 40));

        _dialog.SetActive(false);
    }

    // ── 동작 ────────────────────────────────────────────────────
    void OnAccountButton()
    {
        if (AuthManager.IsSignedIn) { AuthManager.SignOut(); return; } // 로그인 상태면 로그아웃
        OpenDialog();
    }

    void OpenDialog()
    {
        _signUpMode = false;
        ApplyMode();
        _idField.text = ""; _pwField.text = "";
        SetMessage("", false);
        _dialog.SetActive(true);
    }

    void CloseDialog() => _dialog.SetActive(false);

    void OnToggleMode()
    {
        _signUpMode = !_signUpMode;
        ApplyMode();
        SetMessage("", false);
    }

    void ApplyMode()
    {
        _titleLabel.text  = _signUpMode ? "회원가입" : "로그인";
        _submitLabel.text = _signUpMode ? "가입하기" : "로그인";
        _toggleLabel.text = _signUpMode ? "로그인으로" : "회원가입";

        // 가입 모드에서는 규칙을 미리 보여준다 — 틀리고 나서 빨간 글씨로 알려주는 것보다 낫다
        _hintLabel.text = _signUpMode
            ? "아이디  3~20자 - 영문/숫자와  .  -  @  _\n비밀번호  8~30자 - 대문자, 소문자, 숫자, 기호 각 1자 이상"
            : "로그인하면 덱이 계정에 저장돼 다른 기기에서도 쓸 수 있습니다.\n로그인하지 않아도 게임은 그대로 이용할 수 있습니다.";
    }

    void OnSubmit()
    {
        if (_busy) return;
        _busy = true;
        _submitButton.interactable = false;
        SetMessage(_signUpMode ? "가입 중..." : "로그인 중...", false);

        string id = _idField.text.Trim();
        string pw = _pwField.text;   // ★ 비밀번호는 Trim하지 않는다 — 입력을 몰래 바꾸면 안 된다

        // 대신 공백이 섞였으면 짚어준다. 비밀번호를 복사해 붙여넣을 때 끝에 공백이나
        // 줄바꿈이 딸려오는 일이 흔한데, 그러면 서버는 "형식 오류"라고만 답해서 원인을 알기 어렵다.
        foreach (char c in pw)
        {
            if (!char.IsWhiteSpace(c)) continue;
            _busy = false; _submitButton.interactable = true;
            SetMessage("비밀번호에 공백이 들어 있습니다.\n복사해 붙여넣으셨다면 앞뒤에 공백이 딸려왔는지 확인해 주세요.", true);
            return;
        }

        Action onOk    = () => { _busy = false; _submitButton.interactable = true; OnSignedIn(); };
        Action<string> onErr = msg =>
        {
            _busy = false; _submitButton.interactable = true;
            SetMessage(msg, true);
        };

        if (_signUpMode) AuthManager.SignUp(id, pw, onOk, onErr);
        else             AuthManager.SignIn(id, pw, onOk, onErr);
    }

    void OnSignedIn()
    {
        CloseDialog();

        // 로그아웃 상태에서 만들어둔 덱이 있으면 이 계정으로 올릴지 물어본다.
        // ※ 복사이므로 원본(로컬 덱)은 남는다 — 로그아웃하면 다시 보인다.
        var localOnly = AuthManager.GetLocalOnlyDeckNames();
        if (localOnly.Count > 0) ShowImportPrompt(localOnly);
    }

    void ShowImportPrompt(List<string> deckNames)
    {
        var canvasGo = new GameObject("ImportPromptCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var dim = NewImage(canvasGo.transform, new Color(0, 0, 0, 0.75f));
        Stretch(dim.rectTransform);

        var panel = NewImage(canvasGo.transform, new Color(0.11f, 0.13f, 0.16f, 1f));
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(720, 420), Vector2.zero);

        var title = NewTmp(panel.transform, "이 기기의 덱을 계정으로 가져올까요?", 34, TextAlignmentOptions.Top, FontStyles.Bold);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(640, 50), new Vector2(0, -30));

        string preview = string.Join(", ", deckNames.GetRange(0, Mathf.Min(5, deckNames.Count)));
        if (deckNames.Count > 5) preview += $" 외 {deckNames.Count - 5}개";

        var body = NewTmp(panel.transform,
            $"가져올 덱 {deckNames.Count}개\n{preview}\n\n복사되는 것이라 기존 덱은 그대로 남습니다.",
            24, TextAlignmentOptions.Top, FontStyles.Normal);
        Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(620, 180), new Vector2(0, -96));
        body.color = new Color(0.82f, 0.85f, 0.88f);

        var yes = MakeButton(panel.transform, "가져오기", new Color(0.16f, 0.56f, 0.5f), () =>
        {
            int n = AuthManager.ImportLocalDecks(deckNames);
            Destroy(canvasGo);
            FindFirstObjectByType<DeckSelectView>()?.RebuildDeckList();
            Debug.Log($"[Auth] 덱 {n}개 가져오기 완료");
        });
        Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(280, 72), new Vector2(-150, 40));

        var no = MakeButton(panel.transform, "나중에", new Color(0.28f, 0.30f, 0.34f), () => Destroy(canvasGo));
        Place((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(280, 72), new Vector2(150, 40));
    }

    void RefreshAccountLabel()
    {
        if (_accountLabel == null) return;
        // 구분자는 '|' — 가운뎃점(U+00B7)은 이 폰트에 없어 공백으로 치환된다(PhaseBar와 동일 규칙)
        _accountLabel.text = AuthManager.IsSignedIn
            ? $"{AuthManager.DisplayName} | 로그아웃"
            : "로그인";
        ResizeAccountButton();
    }

    // 계정 버튼 폭을 글자 길이에 맞춘다 (높이는 항상 고정).
    //  · 우상단 피벗이라 넓어지는 만큼 왼쪽으로만 뻗는다 → 다른 UI를 위아래로 밀지 않는다.
    //  · AccountMaxW를 넘는 긴 아이디는 라벨의 Ellipsis가 "홍길동…  · 로그아웃" 식으로 줄인다.
    void ResizeAccountButton()
    {
        if (_accountButton == null || _accountLabel == null) return;

        // 방금 바꾼 문자열 기준으로 계산되도록 갱신 후 측정
        _accountLabel.ForceMeshUpdate();
        float textW = _accountLabel.GetPreferredValues(_accountLabel.text).x;

        float w = Mathf.Clamp(textW + AccountPadX * 2f, AccountMinW, AccountMaxW);
        _accountButton.sizeDelta = new Vector2(w, AccountH);
    }

    void SetMessage(string msg, bool isError)
    {
        if (_message == null) return;
        _message.text  = msg;
        _message.color = isError ? new Color(0.95f, 0.55f, 0.5f) : new Color(0.7f, 0.78f, 0.85f);
    }

    // ── UI 생성 헬퍼 (VersionChecker와 동일 패턴) ──────────────────
    static Image NewImage(Transform parent, Color c)
    {
        var go = new GameObject("img", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = c;
        return go.GetComponent<Image>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    TMP_Text NewTmp(Transform parent, string text, float size, TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject("tmp", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text; t.fontSize = size; t.alignment = align; t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.color = Color.white;
        return t;
    }

    Button MakeButton(Transform parent, string label, Color bg, Action onClick)
    {
        var go = new GameObject("btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        ((RectTransform)go.transform).sizeDelta = new Vector2(260, 72);

        var t = NewTmp(go.transform, label, 28, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform);

        go.GetComponent<Button>().onClick.AddListener(() => onClick());
        return go.GetComponent<Button>();
    }

    TMP_InputField MakeInput(Transform parent, string placeholder, Vector2 pos, bool isPassword)
    {
        var go = new GameObject("input", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        // ★ 비활성 상태로 조립한다. TMP_InputField는 활성화되는 순간 textComponent/textViewport를
        //   참조하므로, 먼저 컴포넌트를 붙이면 아직 안 만든 자식을 찾다가 경고·오작동이 난다.
        go.SetActive(false);
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.18f, 0.20f, 0.24f, 1f);
        Place((RectTransform)go.transform, new Vector2(0.5f, 1f), new Vector2(560, 62), pos);

        // 텍스트 영역 (좌우 여백)
        var area = new GameObject("area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(go.transform, false);
        var art = (RectTransform)area.transform;
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
        art.offsetMin = new Vector2(16, 6); art.offsetMax = new Vector2(-16, -6);

        var text = NewTmp(area.transform, "", 26, TextAlignmentOptions.Left, FontStyles.Normal);
        Stretch(text.rectTransform);

        var ph = NewTmp(area.transform, placeholder, 26, TextAlignmentOptions.Left, FontStyles.Italic);
        Stretch(ph.rectTransform);
        ph.color = new Color(0.5f, 0.54f, 0.6f);

        var field = go.AddComponent<TMP_InputField>();
        field.targetGraphic = bg;
        field.textViewport  = art;
        field.textComponent = text;
        field.placeholder   = ph;
        field.lineType      = TMP_InputField.LineType.SingleLine;
        if (isPassword)
        {
            field.contentType  = TMP_InputField.ContentType.Password;
            field.asteriskChar = '*';
        }

        go.SetActive(true); // 조립이 끝난 뒤 활성화
        return field;
    }
}
