// Assets/Scripts/Data/UgsDeckStore.cs
// ─────────────────────────────────────────────────────────────────────────
// UGS Cloud Save 덱 저장 구현 (IDeckStore).
//
//  구조: 로컬 캐시(LocalDeckStore) = 동기 소스 / 클라우드 = 백그라운드 동기화
//    · 읽기(Load/GetSavedDeckNames)는 항상 로컬 캐시에서 즉시 답한다
//      → 덱빌더·DeckSelect가 기다리지 않고, 오프라인에서도 그대로 동작한다.
//    · 쓰기(Save/Delete)는 로컬에 먼저 반영하고, 클라우드 전송은 뒤따라간다(실패해도 게임은 진행).
//    · 로그인 직후 SyncAsync()로 한 번 합친다.
//
//  저장 형태: 키 하나("custom_decks")에 덱 전체 + 삭제 표식을 JSON으로 넣는다.
//    덱 하나가 수백 바이트라 수십 개여도 수십 KB 수준이고, 키를 나누지 않으면
//    ① 한글 덱 이름을 키로 쓸 때의 문자 제약을 피하고 ② 부분 동기화로 인한
//    불일치가 생기지 않는다.
//
//  ★ 삭제 표식(tombstone)이 필요한 이유
//    덱을 그냥 지우면, 그 덱을 아직 갖고 있는 다른 기기가 동기화할 때
//    "저쪽에 없네, 내가 올려주자"며 되살려 버린다(합집합 병합의 숙명).
//    그래서 지우는 대신 "이 이름은 언제 삭제됐다"는 표식을 남기고, 그 표식도
//    덱과 똑같이 동기화한다. 삭제 이후에 다시 만든 덱은 savedAtUtc가 더 크므로
//    표식을 이기고 살아남는다.
//    표식은 무한정 쌓이면 안 되므로 TombstoneRetentionDays 지나면 버린다.
//
//  ★ 병합 규칙
//    1) 덱: 이름이 같으면 savedAtUtc가 큰 쪽(최신)이 이긴다
//    2) 표식: 이름이 같으면 deletedAtUtc가 큰 쪽이 이긴다
//    3) 표식 적용: 덱.savedAtUtc <= 표식.deletedAtUtc 면 그 덱은 삭제된 것
//                  (덱이 더 최신이면 = 지운 뒤 다시 만든 것 → 덱이 살고 표식을 버린다)
//    양쪽에만 있는 덱은 둘 다 남긴다 → 어느 쪽 덱도 조용히 사라지지 않는다.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;

public class UgsDeckStore : IDeckStore
{
    const string CloudKey = "custom_decks";
    const int    TombstoneRetentionDays = 90;  // 이보다 오래된 표식은 버린다

    [Serializable]
    class DeckTombstone
    {
        public string deckName;
        public long   deletedAtUtc;
    }

    // JsonUtility는 리스트를 단독으로 직렬화하지 못해 감싸는 타입이 필요하다
    [Serializable]
    class DeckCollection
    {
        public List<CustomDeckSave> decks   = new();
        public List<DeckTombstone>  deleted = new();   // ※ 예전 저장분엔 이 필드가 없다(빈 값으로 읽힘)
    }

    readonly LocalDeckStore _cache;
    readonly string         _accountId;

    public UgsDeckStore(string accountId)
    {
        _accountId = accountId;
        _cache     = new LocalDeckStore(accountId);
    }

    // ── 읽기 = 로컬 캐시 (즉시, 오프라인 가능) ───────────────────
    public CustomDeckSave Load(string deckName)  => _cache.Load(deckName);
    public List<string>   GetSavedDeckNames()    => _cache.GetSavedDeckNames();

    // ── 쓰기 = 로컬 먼저, 클라우드는 뒤따라 ──────────────────────
    public void Save(CustomDeckSave deck)
    {
        if (deck == null) return;
        deck.savedAtUtc = DateTime.UtcNow.Ticks;   // 병합 시 최신 판단 기준
        _cache.Save(deck);

        // 지웠던 이름을 다시 만든 경우 — 표식을 치운다(안 치우면 다음 동기화에 또 지워진다)
        var tombs = LoadTombstones();
        if (tombs.RemoveAll(t => t.deckName == deck.deckName) > 0) SaveTombstones(tombs);

        PushAsync();
    }

    public void Delete(string deckName)
    {
        _cache.Delete(deckName);

        // ★ 표식을 남긴다 — 이게 없으면 다른 기기가 이 덱을 되살린다
        var tombs = LoadTombstones();
        tombs.RemoveAll(t => t.deckName == deckName);
        tombs.Add(new DeckTombstone { deckName = deckName, deletedAtUtc = DateTime.UtcNow.Ticks });
        SaveTombstones(tombs);

        PushAsync();
    }

    // ── 클라우드 전송 ───────────────────────────────────────────
    // 실패해도 로컬엔 이미 반영돼 있다. 다음 저장이나 로그인 때 다시 올라간다.
    async void PushAsync()
    {
        try
        {
            if (!Ready) return;
            await UploadAsync(LocalDecks(), LoadTombstones());
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Deck] 클라우드 저장 실패(로컬엔 저장됨): {e.Message}");
        }
    }

    Task UploadAsync(List<CustomDeckSave> decks, List<DeckTombstone> tombs)
    {
        var json = JsonUtility.ToJson(new DeckCollection { decks = decks, deleted = tombs });
        return CloudSaveService.Instance.Data.Player.SaveAsync(
            new Dictionary<string, object> { { CloudKey, json } });
    }

    // ── 로그인 직후 합치기 ──────────────────────────────────────
    public async Task SyncAsync()
    {
        try
        {
            if (!Ready) { Debug.Log("[Deck] 로그인 상태가 아니라 동기화 건너뜀"); return; }

            var cloud      = await FetchCloudAsync();
            var localDecks = LocalDecks();
            var localTombs = LoadTombstones();

            // 1) 덱 — 이름이 같으면 최신(savedAtUtc 큰 쪽)
            var decks = new Dictionary<string, CustomDeckSave>();
            foreach (var d in localDecks.Concat(cloud.decks))
            {
                if (d == null || string.IsNullOrEmpty(d.deckName)) continue;
                if (!decks.TryGetValue(d.deckName, out var cur) || d.savedAtUtc > cur.savedAtUtc)
                    decks[d.deckName] = d;
            }

            // 2) 표식 — 이름이 같으면 최신(deletedAtUtc 큰 쪽)
            var tombs = new Dictionary<string, DeckTombstone>();
            foreach (var t in localTombs.Concat(cloud.deleted))
            {
                if (t == null || string.IsNullOrEmpty(t.deckName)) continue;
                if (!tombs.TryGetValue(t.deckName, out var cur) || t.deletedAtUtc > cur.deletedAtUtc)
                    tombs[t.deckName] = t;
            }

            // 3) 표식 적용 — 삭제 이후에 다시 만든 덱은 살아남고 그 표식은 버린다
            int removed = 0;
            foreach (var t in tombs.Values.ToList())
            {
                if (!decks.TryGetValue(t.deckName, out var d)) continue;
                if (d.savedAtUtc <= t.deletedAtUtc) { decks.Remove(t.deckName); removed++; }
                else                                  tombs.Remove(t.deckName);   // 되살아난 덱
            }

            // 4) 오래된 표식 청소 (무한정 쌓이지 않게)
            long cutoff = DateTime.UtcNow.AddDays(-TombstoneRetentionDays).Ticks;
            foreach (var t in tombs.Values.ToList())
                if (t.deletedAtUtc < cutoff) tombs.Remove(t.deckName);

            ApplyToCache(decks);
            SaveTombstones(tombs.Values.ToList());

            Debug.Log($"[Deck] 동기화 완료 - 로컬 {localDecks.Count} + 클라우드 {cloud.decks.Count} "
                    + $"-> {decks.Count} (삭제 반영 {removed}, 표식 {tombs.Count})");

            // 5) 합친 결과를 다시 올려 양쪽을 같게 만든다
            await UploadAsync(decks.Values.ToList(), tombs.Values.ToList());
        }
        catch (Exception e)
        {
            // 동기화 실패 = 이 기기 덱으로 계속 진행 (게임은 막지 않는다)
            Debug.LogWarning($"[Deck] 동기화 실패(로컬 덱으로 진행): {e.Message}");
        }
    }

    // 병합 결과를 로컬 캐시에 반영. 살아남지 못한 덱의 파일은 지운다.
    // ★ 파일 이름은 Sanitize를 거친 형태라, 덱 이름을 그대로 비교하면 안 된다
    //   (특수문자가 든 이름이 다르게 저장돼 있어 멀쩡한 덱을 지울 수 있다).
    void ApplyToCache(Dictionary<string, CustomDeckSave> decks)
    {
        var keep = new HashSet<string>(decks.Keys.Select(LocalDeckStore.Sanitize));
        foreach (var fileName in _cache.GetSavedDeckNames())
            if (!keep.Contains(fileName)) _cache.Delete(fileName);

        foreach (var d in decks.Values) _cache.Save(d);
    }

    async Task<DeckCollection> FetchCloudAsync()
    {
        var empty = new DeckCollection();

        var result = await CloudSaveService.Instance.Data.Player.LoadAsync(
            new HashSet<string> { CloudKey });

        if (!result.TryGetValue(CloudKey, out var item) || item?.Value == null) return empty;

        string json = item.Value.GetAsString();
        if (string.IsNullOrWhiteSpace(json)) return empty;

        var col = JsonUtility.FromJson<DeckCollection>(json);
        if (col == null) return empty;

        col.decks   ??= new List<CustomDeckSave>();
        col.deleted ??= new List<DeckTombstone>();   // 표식 도입 전 저장분 대비
        return col;
    }

    // ── 삭제 표식 로컬 보관 ─────────────────────────────────────
    // ★ 덱 폴더 안에 두면 안 된다 — LocalDeckStore가 *.json을 전부 덱으로 읽어
    //   표식 파일이 덱 목록에 나타난다. 그래서 별도 폴더에 둔다.
    string TombstonePath
        => Path.Combine(Application.persistentDataPath, "DeckSync",
                        $"u_{LocalDeckStore.Sanitize(_accountId ?? "local")}.tombstones.json");

    List<DeckTombstone> LoadTombstones()
    {
        try
        {
            string p = TombstonePath;
            if (!File.Exists(p)) return new List<DeckTombstone>();
            var col = JsonUtility.FromJson<DeckCollection>(File.ReadAllText(p));
            return col?.deleted ?? new List<DeckTombstone>();
        }
        catch { return new List<DeckTombstone>(); }
    }

    void SaveTombstones(List<DeckTombstone> tombs)
    {
        try
        {
            string p = TombstonePath;
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllText(p, JsonUtility.ToJson(new DeckCollection { deleted = tombs }));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Deck] 삭제 표식 저장 실패: {e.Message}");
        }
    }

    // ── 유틸 ────────────────────────────────────────────────────
    List<CustomDeckSave> LocalDecks()
        => _cache.GetSavedDeckNames()
                 .Select(n => _cache.Load(n))
                 .Where(d => d != null)
                 .ToList();

    // 클라우드 호출이 가능한 상태인가 (미초기화·로그아웃 상태에서 호출해도 안전하도록)
    static bool Ready =>
        UnityServices.State == ServicesInitializationState.Initialized
        && AuthenticationService.Instance.IsSignedIn;
}
