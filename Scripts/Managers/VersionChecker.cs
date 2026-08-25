// Assets/Scripts/Managers/VersionChecker.cs
// ─────────────────────────────────────────────────────────────────────────
// 버전 체크 — 실행 시 원격 매니페스트(JSON)로 최신 버전을 확인하고,
//  구버전이면 업데이트 안내 팝업을 띄운다. 최소 요구 버전 미만이면 온라인 차단.
//  · 씬 배치 불필요: RuntimeInitializeOnLoadMethod로 자동 생성.
//  · 팝업 UI는 코드로 생성(에디터 작업 없음). 폰트는 씬의 TMP에서 가져와 한글 렌더.
//  · Photon GameVersion(구·신버전 매칭 차단)은 PhotonNetManager에서 별도로 설정.
//
//  ★ 사용법: 아래 ManifestUrl에 본인이 호스팅한 JSON 주소를 넣으면 동작(비우면 스킵).
//    JSON 예:
//    { "latest":"1.3", "minimum":"1.3",
//      "url":"https://.../NivelArena-Win.zip", "notes":"카드 3종 추가, 버그 수정" }
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class VersionChecker : MonoBehaviour
{
    // ★ 여기에 버전 매니페스트 JSON 주소 입력. 비우면 체크 전체를 건너뛴다(게임은 정상 동작).
    //
    //  현재 방식 = 공개 Gist. 형식은 Docs/version.json.template 참고.
    //  ※ 소스 저장소가 비공개라 raw.githubusercontent.com은 쓸 수 없다(인증 필요 → 404).
    //
    //  ★★ Gist 주소를 넣을 때 반드시 리비전 해시를 빼고 넣을 것 ★★
    //    [Raw] 버튼이 주는 주소 = .../raw/<40자리해시>/version.json  ← 그 시점에 고정됨.
    //      이걸 그대로 쓰면 Gist를 수정해도 게임은 영원히 옛 내용만 본다.
    //    올바른 형태  = .../raw/version.json                          ← 항상 최신
    //  ※ 파일명이 URL 경로에 들어간다 → Gist에서 파일 이름을 바꾸면 이 주소가 죽는다.
    //    이미 배포된 빌드는 404를 받고 조용히 체크를 건너뛴다(아무도 눈치 못 챔). 바꾸지 말 것.
    const string ManifestUrl =
        "https://gist.githubusercontent.com/apppleManGO/8d239fadaf1e5df04ce503240132d409/raw/version.json";

    // 최소 요구 버전 미만이면 온라인 대전 차단 (LobbyManager가 참조)
    public static bool   OnlineBlocked { get; private set; }
    public static string BlockReason   { get; private set; } = "";

    [Serializable]
    class Manifest { public string latest; public string minimum; public string url; public string notes; }

    TMP_FontAsset _font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (string.IsNullOrWhiteSpace(ManifestUrl)) return; // 미설정 시 스킵(게임 정상 동작)
        WarnIfPinnedRevision();
        var go = new GameObject("VersionChecker");
        DontDestroyOnLoad(go);
        go.AddComponent<VersionChecker>();
    }

    // Gist [Raw] 버튼 주소는 리비전 해시가 박혀 있어 그 시점 내용에 고정된다.
    // 그대로 쓰면 Gist를 아무리 고쳐도 게임은 옛 내용만 보고, 아무도 이유를 모른다 —
    // 조용히 잘못 동작하는 종류라 실행 시 한 번 경고해 둔다.
    static void WarnIfPinnedRevision()
    {
        foreach (var seg in ManifestUrl.Split('/'))
        {
            if (seg.Length != 40) continue;
            bool allHex = true;
            foreach (char c in seg)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) { allHex = false; break; }

            if (allHex)
            {
                Debug.LogWarning("[Version] 매니페스트 주소에 리비전 해시가 들어 있습니다. "
                               + "그 시점 내용에 고정되어 Gist를 수정해도 반영되지 않습니다. "
                               + $"해시 부분('{seg}/')을 빼고 .../raw/version.json 형태로 넣으세요.");
                return;
            }
        }
    }

    void Start() => StartCoroutine(Check());

    IEnumerator Check()
    {
        using var req = UnityWebRequest.Get(ManifestUrl);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[Version] 매니페스트 로드 실패: {req.error}");
            yield break;
        }

        Manifest m;
        try { m = JsonUtility.FromJson<Manifest>(req.downloadHandler.text); }
        catch (Exception e) { Debug.LogWarning($"[Version] 매니페스트 파싱 실패: {e.Message}"); yield break; }
        if (m == null || string.IsNullOrEmpty(m.latest)) yield break;

        string cur = Application.version;
        bool outdated = Compare(cur, m.latest) < 0;
        bool belowMin = !string.IsNullOrEmpty(m.minimum) && Compare(cur, m.minimum) < 0;

        if (belowMin)
        {
            OnlineBlocked = true;
            BlockReason = $"온라인 대전은 버전 {m.minimum} 이상이 필요합니다 (현재 {cur}).";
        }
        Debug.Log($"[Version] 현재 {cur} / 최신 {m.latest} / 최소 {m.minimum} — outdated={outdated} belowMin={belowMin}");

        if (outdated) ShowPopup(cur, m, forced: belowMin);
    }

    // "1.2" vs "1.10"도 올바르게 비교 (숫자 파트별). a<b → -1
    static int Compare(string a, string b)
    {
        var pa = (a ?? "").Split('.');
        var pb = (b ?? "").Split('.');
        int n = Mathf.Max(pa.Length, pb.Length);
        for (int i = 0; i < n; i++)
        {
            int va = i < pa.Length && int.TryParse(pa[i], out int x) ? x : 0;
            int vb = i < pb.Length && int.TryParse(pb[i], out int y) ? y : 0;
            if (va != vb) return va < vb ? -1 : 1;
        }
        return 0;
    }

    // ── 코드로 만드는 업데이트 팝업 (씬 UI 불필요) ──────────────────
    void ShowPopup(string cur, Manifest m, bool forced)
    {
        var anyTmp = FindFirstObjectByType<TMP_Text>();
        _font = anyTmp != null ? anyTmp.font : null; // 씬의 한글 폰트 재사용

        var canvasGo = new GameObject("VersionPopupCanvas");
        DontDestroyOnLoad(canvasGo);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        var dim = NewImage(canvasGo.transform, new Color(0, 0, 0, 0.75f));
        Stretch(dim.rectTransform);

        var panel = NewImage(canvasGo.transform, new Color(0.11f, 0.13f, 0.16f, 1f));
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(760, 400);
        prt.anchoredPosition = Vector2.zero;

        var title = NewTmp(panel.transform, $"새 버전 {m.latest} 이(가) 있습니다", 46, TextAlignmentOptions.Top, FontStyles.Bold);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(680, 70), new Vector2(0, -34));
        title.color = Color.white;

        string bodyText = $"현재 버전: {cur}\n\n" +
                          (string.IsNullOrEmpty(m.notes) ? "새 버전으로 업데이트해 주세요." : m.notes);
        var body = NewTmp(panel.transform, bodyText, 28, TextAlignmentOptions.Top, FontStyles.Normal);
        Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(660, 190), new Vector2(0, -120));
        body.color = new Color(0.82f, 0.85f, 0.88f);

        // 강제(최소버전 미만)면 [다운로드]만 가운데, 아니면 [다운로드]/[나중에] 둘.
        if (forced)
        {
            MakeButton(panel.transform, "다운로드", new Vector2(0, 48), new Color(0.16f, 0.56f, 0.5f),
                () => { if (!string.IsNullOrEmpty(m.url)) Application.OpenURL(m.url); });
        }
        else
        {
            MakeButton(panel.transform, "다운로드", new Vector2(-150, 48), new Color(0.16f, 0.56f, 0.5f),
                () => { if (!string.IsNullOrEmpty(m.url)) Application.OpenURL(m.url); });
            MakeButton(panel.transform, "나중에", new Vector2(150, 48), new Color(0.28f, 0.30f, 0.34f),
                () => Destroy(canvasGo));
        }
    }

    // ── UI 생성 헬퍼 ─────────────────────────────────────────────
    static Image NewImage(Transform parent, Color c)
    {
        var go = new GameObject("img", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
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
        t.textWrappingMode = TextWrappingModes.Normal; // enableWordWrapping은 폐기됨
        t.color = Color.white;
        return t;
    }

    void MakeButton(Transform parent, string label, Vector2 anchoredPos, Color bg, Action onClick)
    {
        var go = new GameObject("btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(260, 76);
        rt.anchoredPosition = anchoredPos;

        var t = NewTmp(go.transform, label, 30, TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(t.rectTransform);

        go.GetComponent<Button>().onClick.AddListener(() => onClick());
    }
}
