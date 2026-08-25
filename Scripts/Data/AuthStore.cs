// Assets/Scripts/Data/AuthStore.cs
// ─────────────────────────────────────────────────────────────────────────
// 인증 백엔드 경계.
//  · 게임 코드는 AuthManager(파사드)만 참조. 실제 로그인은 IAuthProvider 구현이 담당.
//  · 기본값 LocalAuthProvider(이 기기 안에서만 통하는 프로필) → 백엔드 없이도 전 흐름 동작.
//  · UGS 패키지를 설치하면 UgsAuthProvider로 교체 (AuthManager.Provider = ... 한 줄).
//    이는 DeckSaveLoad/IDeckStore, NetHub/INetChannel과 완전히 같은 패턴.
//
//  ※ 시그니처가 콜백인 이유: 클라우드 구현은 비동기지만, 호출부(UI)를 async로 뒤엎지 않기
//    위해 프로젝트의 기존 콜백 체인 방식(EffectActions/ChoiceBroker)을 따른다.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public interface IAuthProvider
{
    bool   IsSignedIn  { get; }
    string UserId      { get; }   // ★ 덱 저장 폴더·클라우드 키의 기준이 되는 계정 식별자
    string DisplayName { get; }   // 화면 표시용 (보통 아이디)

    void SignUp (string userName, string password, Action onSuccess, Action<string> onError);
    void SignIn (string userName, string password, Action onSuccess, Action<string> onError);
    void SignOut();

    // 저장된 세션으로 자동 로그인 시도 (없으면 아무 일도 하지 않는다).
    // 성공 시에만 onSignedIn — 실패는 조용히 넘긴다(수동 로그인이 가능하므로).
    void TryAutoSignIn(Action onSignedIn);
}

// ─────────────────────────────────────────────────────────────────────────
// 이 기기 안에서만 통하는 프로필 (백엔드 없이 동작하는 기본 구현).
//
// ★★ 이것은 보안 경계가 아니다. ★★
//   계정 정보가 이 PC의 파일로만 존재하므로 파일을 지우거나 바꾸면 그만이다.
//   "한 PC를 여러 사람이 쓸 때 덱을 따로 관리한다" 정도의 용도이고,
//   진짜 계정(다른 기기에서 로그인, 남이 못 보게)은 UGS 구현이 담당한다.
//   비밀번호를 해시로 저장하는 것도 평문 노출을 피하려는 것일 뿐 보호 수단이 아니다.
// ─────────────────────────────────────────────────────────────────────────
public class LocalAuthProvider : IAuthProvider
{
    [Serializable]
    class Account
    {
        public string userName;
        public string userId;
        public string salt;
        public string hash;
    }

    static string AccountDir => Path.Combine(Application.persistentDataPath, "Accounts");

    string _userId, _userName;

    public bool   IsSignedIn  => !string.IsNullOrEmpty(_userId);
    public string UserId      => _userId;
    public string DisplayName => _userName;

    public void SignUp(string userName, string password, Action onSuccess, Action<string> onError)
    {
        string err = Validate(userName, password);
        if (err != null) { onError?.Invoke(err); return; }

        if (Load(userName) != null) { onError?.Invoke("이미 있는 아이디입니다."); return; }

        string salt = Guid.NewGuid().ToString("N");
        var acc = new Account
        {
            userName = userName,
            userId   = Guid.NewGuid().ToString("N"),
            salt     = salt,
            hash     = Hash(password, salt),
        };

        Directory.CreateDirectory(AccountDir);
        File.WriteAllText(PathOf(userName), JsonUtility.ToJson(acc, true));

        _userId = acc.userId; _userName = acc.userName;
        Debug.Log($"[Auth] 로컬 프로필 생성: {userName}");
        onSuccess?.Invoke();
    }

    public void SignIn(string userName, string password, Action onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
        {
            onError?.Invoke("아이디와 비밀번호를 입력하세요.");
            return;
        }

        var acc = Load(userName);
        // 아이디가 없는 경우와 비밀번호가 틀린 경우를 같은 문구로 — 계정 존재 여부를 흘리지 않는다
        if (acc == null || Hash(password, acc.salt) != acc.hash)
        {
            onError?.Invoke("아이디 또는 비밀번호가 올바르지 않습니다.");
            return;
        }

        _userId = acc.userId; _userName = acc.userName;
        Debug.Log($"[Auth] 로컬 프로필 로그인: {userName}");
        onSuccess?.Invoke();
    }

    public void SignOut()
    {
        _userId = null; _userName = null;
    }

    // 로컬 프로필엔 세션 개념이 없다. 비밀번호 없이 자동으로 들어가 버리면
    // "이 PC에서 사람별로 덱을 나눈다"는 목적 자체가 없어지므로 아무것도 하지 않는다.
    public void TryAutoSignIn(Action onSignedIn) { }

    // ── 내부 ──────────────────────────────────────────────────
    // 규칙은 UGS Authentication과 맞춰둔다 — 나중에 UGS로 바꿔도 사용자가 겪는 제약이 같도록.
    //  · 아이디: 3~20자, 영문/숫자와 . - @ _ 만. 대소문자 구분 없음(PathOf에서 소문자화)
    //  · 비밀번호: 8~30자, 대문자·소문자·숫자·기호 각 1자 이상. 대소문자 구분함
    // ★ 아이디에 한글을 허용하면 UGS로 전환할 때 그 계정이 못 넘어간다 — 지금부터 막는다.
    static string Validate(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName))            return "아이디를 입력하세요.";
        if (userName.Length < 3 || userName.Length > 20)    return "아이디는 3~20자여야 합니다.";

        foreach (char c in userName)
        {
            bool allowed = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                           || c == '.' || c == '-' || c == '@' || c == '_';
            if (!allowed) return "아이디는 영문/숫자와 . - @ _ 만 쓸 수 있습니다.";
        }

        if (string.IsNullOrEmpty(password))                 return "비밀번호를 입력하세요.";
        if (password.Length < 8 || password.Length > 30)    return "비밀번호는 8~30자여야 합니다.";

        bool upper = false, lower = false, digit = false, symbol = false;
        foreach (char c in password)
        {
            if      (char.IsUpper(c)) upper  = true;
            else if (char.IsLower(c)) lower  = true;
            else if (char.IsDigit(c)) digit  = true;
            else                      symbol = true;
        }
        if (!upper || !lower || !digit || !symbol)
            return "비밀번호는 대문자, 소문자, 숫자, 기호를 각각 1자 이상 포함해야 합니다.";

        return null;
    }

    static string PathOf(string userName)
        => Path.Combine(AccountDir, LocalDeckStore.Sanitize(userName.ToLowerInvariant()) + ".json");

    static Account Load(string userName)
    {
        string p = PathOf(userName);
        if (!File.Exists(p)) return null;
        try   { return JsonUtility.FromJson<Account>(File.ReadAllText(p)); }
        catch { return null; }
    }

    static string Hash(string password, string salt)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + "|" + password));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    // 이 기기에 만들어진 프로필 목록 (로그인 화면에서 참고용)
    public static List<string> GetLocalAccountNames()
    {
        var names = new List<string>();
        if (!Directory.Exists(AccountDir)) return names;
        foreach (var f in Directory.GetFiles(AccountDir, "*.json"))
        {
            try
            {
                var acc = JsonUtility.FromJson<Account>(File.ReadAllText(f));
                if (acc != null && !string.IsNullOrEmpty(acc.userName)) names.Add(acc.userName);
            }
            catch { /* 깨진 파일은 무시 */ }
        }
        return names;
    }
}
