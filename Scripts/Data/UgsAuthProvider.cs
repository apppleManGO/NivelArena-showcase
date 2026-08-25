// Assets/Scripts/Data/UgsAuthProvider.cs
// ─────────────────────────────────────────────────────────────────────────
// Unity Gaming Services 인증 구현 (IAuthProvider).
//
//  ★ LocalAuthProvider와의 결정적 차이 = 비밀번호가 이 PC에 남지 않는다.
//    · 비밀번호는 HTTPS로 Unity 서버에 보내지고, 서버가 해시로 보관한다.
//    · 이 PC에는 "세션 토큰"만 남는다(PlayerPrefs). 토큰으로는 재로그인만 되고
//      비밀번호를 되돌려 알아낼 수 없다.
//    · 따라서 다른 기기에서도 같은 계정으로 로그인된다(= 덱이 따라온다).
//
//  ※ 사전 준비(완료됨): UGS 콘솔에서 Username Password 공급자 활성화 +
//    com.unity.services.authentication 패키지.
//
//  ※ IAuthProvider는 콜백 방식인데 UGS는 async다. 경계에서만 async void로 받고
//    예외를 전부 잡아 onError로 넘긴다(호출부 UI는 콜백 그대로 사용).
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public class UgsAuthProvider : IAuthProvider
{
    public bool   IsSignedIn  => UnityServices.State == ServicesInitializationState.Initialized
                                 && AuthenticationService.Instance.IsSignedIn;
    public string UserId      => IsSignedIn ? AuthenticationService.Instance.PlayerId : null;

    // 표시용 이름 = 가입할 때 쓴 아이디. 서버가 아직 안 내려줬으면 PlayerId로 대체.
    public string DisplayName
    {
        get
        {
            if (!IsSignedIn) return null;
            var info = AuthenticationService.Instance.PlayerInfo;
            return !string.IsNullOrEmpty(info?.Username) ? info.Username
                                                         : AuthenticationService.Instance.PlayerId;
        }
    }

    public async void SignUp(string userName, string password, Action onSuccess, Action<string> onError)
    {
        try
        {
            await EnsureInitialized();
            await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(userName, password);
            Debug.Log($"[Auth] UGS 가입 성공: {userName} (PlayerId={AuthenticationService.Instance.PlayerId})");
            onSuccess?.Invoke();
        }
        catch (Exception e) { onError?.Invoke(Translate(e, signUp: true)); }
    }

    public async void SignIn(string userName, string password, Action onSuccess, Action<string> onError)
    {
        try
        {
            await EnsureInitialized();
            await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(userName, password);
            Debug.Log($"[Auth] UGS 로그인 성공: {userName} (PlayerId={AuthenticationService.Instance.PlayerId})");
            onSuccess?.Invoke();
        }
        catch (Exception e) { onError?.Invoke(Translate(e, signUp: false)); }
    }

    public void SignOut()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized) return;
        // clearCredentials: true = 세션 토큰까지 삭제.
        // 안 지우면 다음 실행에 자동 로그인돼 "로그아웃했는데 다시 로그인됨"이 된다.
        AuthenticationService.Instance.SignOut(clearCredentials: true);
        Debug.Log("[Auth] UGS 로그아웃");
    }

    // ── 초기화 ──────────────────────────────────────────────────
    // UnityServices.InitializeAsync는 한 번만. 여러 곳에서 동시에 불려도
    // 같은 Task를 기다리도록 캐시한다.
    static Task _initTask;

    static Task EnsureInitialized()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized) return Task.CompletedTask;
        return _initTask ??= UnityServices.InitializeAsync();
    }

    // ── 자동 로그인 ─────────────────────────────────────────────
    // 이전에 로그인한 적이 있으면 세션 토큰으로 다시 들어간다(비밀번호 불필요).
    // ※ 토큰이 있을 때 SignInAnonymouslyAsync는 새 익명 계정을 만드는 게 아니라
    //   그 토큰의 계정으로 복귀한다. 토큰이 없으면 아무것도 하지 않는다.
    //   ★ 그래서 SessionTokenExists 확인이 필수 — 이걸 빼면 로그인한 적 없는
    //     사용자에게 익명 계정이 새로 생겨 버린다.
    public async void TryAutoSignIn(Action onSignedIn)
    {
        try
        {
            await EnsureInitialized();
            if (!AuthenticationService.Instance.SessionTokenExists) return;
            if (AuthenticationService.Instance.IsSignedIn) return;

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"[Auth] 저장된 세션으로 자동 로그인 (PlayerId={AuthenticationService.Instance.PlayerId})");
            onSignedIn?.Invoke();
        }
        catch (Exception e)
        {
            // 토큰 만료·서버 문제 등 — 자동 로그인 실패는 조용히 넘긴다(수동 로그인 가능)
            Debug.LogWarning($"[Auth] 자동 로그인 실패(무시): {e.Message}");
        }
    }

    // ── 오류 문구 ───────────────────────────────────────────────
    // UGS 예외를 사용자에게 보여줄 한국어로 바꾼다.
    static string Translate(Exception e, bool signUp)
    {
        // ★ 서버가 보낸 원문을 먼저 남긴다 — 아래 한국어 문구는 요약이라, 어느 항목이
        //   왜 거부됐는지는 원문에만 들어 있다. (비밀번호 값은 절대 로그에 넣지 말 것)
        if (e is RequestFailedException req)
            Debug.LogWarning($"[Auth] 서버 응답 code={req.ErrorCode} msg=\"{req.Message}\"");
        else
            Debug.LogWarning($"[Auth] 예외 {e.GetType().Name}: {e.Message}");

        if (e is AuthenticationException ae)
        {
            // ★ 10002(InvalidParameters)는 형식 오류 전용이 아니다.
            //   서버가 "이 공급자를 못 쓴다"고 거부할 때도 같은 코드로 온다 —
            //   그걸 형식 오류로 번역하면 멀쩡한 아이디/비밀번호를 계속 바꿔보게 된다.
            //   실제 사례: usernamepassword external id provider is not available: PERMISSION_DENIED
            string raw = ae.Message ?? "";
            if (raw.Contains("provider is not available") || raw.Contains("PERMISSION_DENIED"))
                return "서버에서 아이디/비밀번호 로그인이 켜져 있지 않습니다.\n"
                     + "UGS 대시보드 > LiveOps > Authentication > Identity Providers 에서\n"
                     + "Username Password 를 추가하고, 게임이 쓰는 환경(Environment)과\n"
                     + "같은 환경에 설정됐는지 확인해 주세요.";

            // 여기까지 왔으면 진짜 형식 문제거나, SDK가 빈 값을 거른 경우.
            // ※ 원문(영문)은 위에서 콘솔에 남겼다 — 화면엔 안 띄운다(사용자에게 의미 없음).
            if (ae.ErrorCode == AuthenticationErrorCodes.InvalidParameters)
                return signUp
                    ? "형식이 올바르지 않습니다.\n- 아이디  3~20자, 영문/숫자와  .  -  @  _\n- 비밀번호  8~30자, 대문자/소문자/숫자/기호 각 1자 이상"
                    : "아이디 또는 비밀번호가 올바르지 않습니다.";
            if (ae.ErrorCode == AuthenticationErrorCodes.BannedUser)
                return "이용이 제한된 계정입니다.";
            if (ae.ErrorCode == AuthenticationErrorCodes.InvalidSessionToken)
                return "로그인 정보가 만료되었습니다. 다시 로그인해 주세요.";
            if (ae.ErrorCode == AuthenticationErrorCodes.EnvironmentMismatch)
                return "서버 환경 설정이 맞지 않습니다. (UGS 환경 확인 필요)";
        }

        // 서버가 돌려준 요청 실패 — 가입 시 중복 아이디, 로그인 시 자격 불일치 등이 여기로 온다.
        if (e is RequestFailedException)
            return signUp
                ? "가입에 실패했습니다. 이미 있는 아이디이거나 형식이 맞지 않습니다."
                : "아이디 또는 비밀번호가 올바르지 않습니다.";

        Debug.LogWarning($"[Auth] 예기치 못한 오류: {e}");
        return "네트워크 오류로 실패했습니다. 잠시 후 다시 시도해 주세요.";
    }
}
