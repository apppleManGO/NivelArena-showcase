// Assets/Scripts/Data/DeckStore.cs
// ─────────────────────────────────────────────────────────────────────────
// 덱 저장 백엔드 경계 (클라우드 동기화 준비 — 1단계 추상화).
//  · 게임 코드는 DeckSaveLoad(파사드)만 참조. 실제 저장은 IDeckStore 구현이 담당.
//  · 기본값 LocalDeckStore(persistentDataPath JSON) → 현재 동작 100% 동일.
//  · 나중에 Firebase 등 클라우드는 이 인터페이스를 구현하는 CloudDeckStore로 교체
//    (DeckSaveLoad.Store = new CloudDeckStore(...) 한 줄 — 게임 코드 무변경).
//    이는 멀티플레이의 NetHub/INetChannel/LocalChannel과 완전히 같은 패턴.
//
//  ※ 시그니처는 동기(sync) 유지 이유: 클라우드 구현도 "로컬 캐시"를 동기 소스로 두고,
//    실제 클라우드 push/pull은 로그인/저장 시 백그라운드로 처리한다. 그래야 UI/호출부가
//    async로 뒤엎이지 않고 그대로 동작한다(오프라인에서도 즉시 읽고 씀).
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public interface IDeckStore
{
    void Save(CustomDeckSave deck);
    CustomDeckSave Load(string deckName);
    List<string> GetSavedDeckNames();
    void Delete(string deckName);
}

// 로컬 저장 — persistentDataPath/CustomDecks/[u_{uid}/]*.json
//
//  · 로그아웃 상태(accountId = null) = CustomDecks/ 바로 아래 = **기존과 완전히 동일한 경로**.
//    ★ 이미 배포한 빌드에서 만든 덱이 그대로 보여야 하므로 이 경로는 절대 바꾸지 않는다.
//  · 로그인 상태 = CustomDecks/u_{uid}/ 하위 → 계정별로 덱 목록이 분리된다.
//
//  계정 전환은 AuthManager.ApplyDeckScope가 DeckSaveLoad.Store를 갈아끼우는 식으로 처리하고,
//  게임 코드(덱빌더·DeckSelect 등)는 여전히 DeckSaveLoad만 부른다.
public class LocalDeckStore : IDeckStore
{
    readonly string _accountId; // null = 로그아웃(공용 로컬 덱)

    public LocalDeckStore(string accountId = null) { _accountId = accountId; }

    string SaveDir
    {
        get
        {
            string root = Path.Combine(Application.persistentDataPath, "CustomDecks");
            return string.IsNullOrEmpty(_accountId)
                ? root
                : Path.Combine(root, "u_" + Sanitize(_accountId));
        }
    }

    public void Save(CustomDeckSave deck)
    {
        Directory.CreateDirectory(SaveDir);
        string path = Path.Combine(SaveDir, Sanitize(deck.deckName) + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(deck, prettyPrint: true));
    }

    public CustomDeckSave Load(string deckName)
    {
        string path = Path.Combine(SaveDir, Sanitize(deckName) + ".json");
        if (!File.Exists(path)) return null;
        return JsonUtility.FromJson<CustomDeckSave>(File.ReadAllText(path));
    }

    public List<string> GetSavedDeckNames()
    {
        var names = new List<string>();
        if (!Directory.Exists(SaveDir)) return names;
        foreach (var file in Directory.GetFiles(SaveDir, "*.json"))
            names.Add(Path.GetFileNameWithoutExtension(file));
        return names;
    }

    public void Delete(string deckName)
    {
        string path = Path.Combine(SaveDir, Sanitize(deckName) + ".json");
        if (File.Exists(path)) File.Delete(path);
    }

    // 파일명 안전화 (경로/특수문자 제거) — 공용으로 재사용 가능하게 public static
    public static string Sanitize(string name) =>
        string.IsNullOrEmpty(name) ? "unnamed" :
        name.Replace("/", "_").Replace("\\", "_").Replace(":", "_")
            .Replace("*", "_").Replace("?", "_").Replace("\"", "_")
            .Replace("<", "_").Replace(">", "_").Replace("|", "_");
}
