// Assets/Scripts/Data/AuthManager.cs
// ─────────────────────────────────────────────────────────────────────────
// 인증 파사드 — 게임 코드(UI 등)는 여기만 부른다. 실제 구현은 IAuthProvider.
//
//  ★ 로그인은 "선택"이다. 로그인하지 않아도 게임은 지금까지처럼 전부 동작하고,
//    덱은 이 기기에 저장된다(= 기존 사용자의 덱이 그대로 보인다).
//    로그인하면 그 계정 전용 덱 목록으로 바뀐다.
//
//  ★ 계정이 바뀌면 덱 저장 위치도 함께 바뀌어야 한다. 그 연결을 이 파일 한 곳에서만
//    한다(ApplyDeckScope) — 호출부가 로그인 상태를 신경 쓸 필요가 없다.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using UnityEngine;

public static class AuthManager
{
    // 현재 인증 백엔드.
    //  · UgsAuthProvider  = 진짜 계정(다른 기기에서도 로그인, 비밀번호는 서버 보관)
    //  · LocalAuthProvider = 이 기기 전용 프로필(백엔드 없이 테스트할 때만)
    // 오프라인이면 로그인만 실패하고 게임은 그대로 동작한다(로그인은 선택이므로).
    public static IAuthProvider Provider = new UgsAuthProvider();

    // 로그인/로그아웃으로 상태가 바뀔 때마다 발행 (UI 갱신용)
    public static event Action OnAuthChanged;

    public static bool   IsSignedIn  => Provider != null && Provider.IsSignedIn;
    public static string UserId      => Provider?.UserId;
    public static string DisplayName => Provider?.DisplayName;

    public static void SignUp(string userName, string password, Action onSuccess, Action<string> onError)
    {
        if (Provider == null) { onError?.Invoke("인증 백엔드가 없습니다."); return; }
        Provider.SignUp(userName, password,
            () => { ApplyDeckScope(); OnAuthChanged?.Invoke(); onSuccess?.Invoke(); },
            onError);
    }

    public static void SignIn(string userName, string password, Action onSuccess, Action<string> onError)
    {
        if (Provider == null) { onError?.Invoke("인증 백엔드가 없습니다."); return; }
        Provider.SignIn(userName, password,
            () => { ApplyDeckScope(); OnAuthChanged?.Invoke(); onSuccess?.Invoke(); },
            onError);
    }

    public static void SignOut()
    {
        Provider?.SignOut();
        ApplyDeckScope();
        OnAuthChanged?.Invoke();
    }

    // 저장된 세션이 있으면 비밀번호 없이 다시 로그인 (앱 시작 시 1회 — LoginView가 호출).
    // 성공했을 때만 콜백이 오므로, 로그인한 적 없는 사용자에겐 아무 일도 일어나지 않는다.
    public static void TryAutoSignIn()
    {
        Provider?.TryAutoSignIn(() => { ApplyDeckScope(); OnAuthChanged?.Invoke(); });
    }

    // ── 덱 저장 범위 전환 ────────────────────────────────────────
    // 로그아웃 = 기존 폴더(CustomDecks/) 그대로 → 예전에 만든 덱이 계속 보인다.
    // 로그인   = 계정 전용 폴더(CustomDecks/u_{uid}/) + 클라우드 동기화.
    //
    // ★ 동기화는 기다리지 않는다. UgsDeckStore의 읽기는 로컬 캐시라 바로 답하므로
    //   화면은 즉시 뜨고, 클라우드에만 있던 덱은 동기화가 끝나면 목록에 추가된다.
    static void ApplyDeckScope()
    {
        if (!IsSignedIn)
        {
            DeckSaveLoad.Store = new LocalDeckStore(null);
            return;
        }

        var store = new UgsDeckStore(UserId);
        DeckSaveLoad.Store = store;
        SyncDecks(store);
    }

    static async void SyncDecks(UgsDeckStore store)
    {
        await store.SyncAsync();
        OnDecksSynced?.Invoke();   // 목록이 늘었을 수 있으니 화면을 다시 그리게 알린다
    }

    // 클라우드 덱 동기화가 끝났을 때 발행 (DeckSelect 드롭다운 갱신용)
    public static event Action OnDecksSynced;

    // ── 로컬 덱 → 계정으로 올리기 ────────────────────────────────
    // 로그인 직후, 로그아웃 상태에서 만들어둔 덱을 이 계정으로 복사할지 물어볼 때 사용.
    // ※ 복사이므로 로컬 덱은 그대로 남는다(로그아웃하면 다시 보인다).

    // 아직 이 계정에 없는 로컬 덱 이름들 — 없으면 물어볼 필요도 없다
    public static List<string> GetLocalOnlyDeckNames()
    {
        var result = new List<string>();
        if (!IsSignedIn) return result;

        var accountDecks = new HashSet<string>(new LocalDeckStore(UserId).GetSavedDeckNames());
        foreach (var name in new LocalDeckStore(null).GetSavedDeckNames())
            if (!accountDecks.Contains(name)) result.Add(name);

        return result;
    }

    // 이름을 지정해 이 계정으로 복사. 반환 = 실제로 복사된 개수
    public static int ImportLocalDecks(IEnumerable<string> deckNames)
    {
        if (!IsSignedIn || deckNames == null) return 0;

        var from = new LocalDeckStore(null);
        var to   = new LocalDeckStore(UserId);

        int n = 0;
        foreach (var name in deckNames)
        {
            var deck = from.Load(name);
            if (deck == null) continue;
            to.Save(deck);
            n++;
        }

        if (n > 0) Debug.Log($"[Auth] 로컬 덱 {n}개를 계정({DisplayName})으로 복사");
        return n;
    }
}
