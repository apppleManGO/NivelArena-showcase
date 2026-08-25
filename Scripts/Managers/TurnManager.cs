// Assets/Scripts/Managers/TurnManager.cs
using System;
using System.Collections;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public PhaseType CurrentPhase { get; private set; }
    public int TurnNumber { get; private set; } = 1;
    public bool IsPlayerTurn { get; private set; } = true;

    // 다른 시스템이 페이즈 변화를 구독할 수 있게 이벤트로 노출
    public event Action<PhaseType> OnPhaseChanged;
    public event Action<bool> OnTurnChanged; // true = 플레이어 턴

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // 게임 시작 진입점
    public void StartGame(bool playerGoesFirst = true)
    {
        TurnNumber   = 1;
        IsPlayerTurn = playerGoesFirst;

        // 온라인 클라는 턴 루프를 돌리지 않음 — 호스트가 구동하고 클라는 상태만 수신 (3c)
        if (NetHub.IsOnline && !NetHub.IsAuthority)
        {
            Debug.Log("[Turn] 온라인 클라 — 턴 루프 미실행(호스트 권위, 상태 수신만)");
            return;
        }
        StartCoroutine(RunTurn());
    }

    // 3c: 클라가 호스트로부터 받은 턴 상태를 화면에 반영 (시뮬 없이 UI 이벤트만).
    public void ApplyNetworkTurn(bool isPlayerTurn, PhaseType phase)
    {
        bool turnChanged = IsPlayerTurn != isPlayerTurn;
        IsPlayerTurn = isPlayerTurn;
        CurrentPhase = phase;
        if (turnChanged) OnTurnChanged?.Invoke(isPlayerTurn);
        OnPhaseChanged?.Invoke(phase);
    }

    private IEnumerator RunTurn()
    {
        while (true)
        {
            OnTurnChanged?.Invoke(IsPlayerTurn);

            yield return StartCoroutine(LevelUpPhase());
            yield return StartCoroutine(DrawPhase());
            yield return StartCoroutine(MainPhase());
            yield return StartCoroutine(AttackPhase());
            yield return StartCoroutine(EndPhase());

            // 턴 교대
            IsPlayerTurn = !IsPlayerTurn;
            if (IsPlayerTurn) TurnNumber++;
        }
    }

    // ── 페이즈별 코루틴 ──────────────────────────────────────

    private IEnumerator LevelUpPhase()
    {
        ChangePhase(PhaseType.LevelUp);
        // 1턴은 레벨업 없음 (니벨아레나 룰)
        if (TurnNumber > 1 || !IsPlayerTurn)
        {
            GetCurrentPlayer().LevelUpLeader();
        }
        yield return new WaitForSeconds(0.5f); // 연출용 딜레이
    }

    private IEnumerator DrawPhase()
    {
        ChangePhase(PhaseType.Draw);

        // 룰북 6.3.1.1: 선공 첫 턴은 드로우 없음
        bool isFirstTurnPlayer = TurnNumber == 1 && IsPlayerTurn;
        if (!isFirstTurnPlayer)
            GetCurrentPlayer().DrawCard(1);
        else
            Debug.Log("[드로우] 선공 첫 턴 — 드로우 없음");

        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator MainPhase()
    {
        ChangePhase(PhaseType.Main);

        // 자신의 메인 페이즈가 시작할 때 — 이스케이프 유닛 처리
        if (EffectSystem.Instance != null)
            EffectSystem.Instance.OnMainPhaseStart(IsPlayerTurn);

        if (IsPlayerTurn)
        {
            // 플레이어 입력 대기 — UI에서 EndMainPhase() 호출하면 풀림
            yield return new WaitUntil(() => _mainPhaseEnded);
            _mainPhaseEnded = false;
        }
        else if (NetHub.IsOnline)
        {
            // 온라인: 상대 슬롯 = 원격 인간 → AI 안 돌림.
            // 상대의 행동/턴종료는 네트워크로 옴(호스트 권위 턴 구동은 3c 과제).
            // ※ 지금은 턴 진행 동기화(3c) 전이라 이 대기가 자동으로 안 풀림 — 3c에서 원격 종료신호로 세팅.
            yield return new WaitUntil(() => _mainPhaseEnded);
            _mainPhaseEnded = false;
        }
        else
        {
            // 오프라인 AI 턴: AIController가 처리 후 완료 신호를 보냄
            yield return StartCoroutine(AIController.Instance.PlayTurn());
        }
    }

    private IEnumerator AttackPhase()
    {
        // 메인에서 스킵 요청 시 어택 페이즈 건너뜀
        if (_skipAttackPhase)
        {
            _skipAttackPhase = false;
            yield break;
        }

        ChangePhase(PhaseType.Attack);
        EffectSystem.Instance.OnAttackPhaseStart(IsPlayerTurn); // BT07-024: 어택 페이즈 시작 시 이동 기회

        if (IsPlayerTurn)
        {
            // 플레이어 어택 페이즈 시작 — 레인 클릭으로 개별 공격
            CombatManager.Instance.BeginAttackPhase();
            yield return new WaitUntil(() => _attackPhaseEnded);
            _attackPhaseEnded = false;
        }
        else if (NetHub.IsOnline)
        {
            // 온라인: 상대 어택 페이즈 = 원격 인간 → AI 안 돌림. (3c에서 원격 종료신호로 진행)
            yield return new WaitUntil(() => _attackPhaseEnded);
            _attackPhaseEnded = false;
        }
        else
        {
            // 오프라인 AI 공격 — 플레이어 유닛 있으면 방어 팝업 표시
            yield return StartCoroutine(AIController.Instance.AttackTurn());
            yield return StartCoroutine(CombatManager.Instance.ProcessAIAttackPhase());
        }
    }

    private IEnumerator EndPhase()
    {
        ChangePhase(PhaseType.End);
        GetCurrentPlayer().OnEndPhase();

        // 턴 종료 — 임시 효과 제거
        if (EffectSystem.Instance != null)
            EffectSystem.Instance.OnTurnEnd(IsPlayerTurn);

        yield return new WaitForSeconds(0.5f);
    }

    // ── 플레이어 입력 신호 ────────────────────────────────────

    private bool _mainPhaseEnded;
    private bool _attackPhaseEnded;
    private bool _skipAttackPhase;

    // 메인 → 어택 페이즈로 진행
    public void EndMainPhase()
    {
        if (NetHub.IsOnline && !NetHub.IsAuthority) { NetHub.SendEndPhase(0); return; } // 클라: 호스트에 릴레이
        if (CurrentPhase == PhaseType.Main && IsPlayerTurn)
            _mainPhaseEnded = true;
    }

    // 메인 → 어택 스킵하고 엔드 페이즈로
    public void SkipToEndPhase()
    {
        if (NetHub.IsOnline && !NetHub.IsAuthority) { NetHub.SendEndPhase(2); return; }
        if (CurrentPhase == PhaseType.Main && IsPlayerTurn)
        {
            _skipAttackPhase = true;
            _mainPhaseEnded  = true;
        }
    }

    // 어택 → 엔드 페이즈로
    public void EndAttackPhase()
    {
        if (NetHub.IsOnline && !NetHub.IsAuthority) { NetHub.SendEndPhase(1); return; }
        if (CurrentPhase == PhaseType.Attack && IsPlayerTurn)
            _attackPhaseEnded = true;
    }

    // ── 3c: 호스트가 클라의 턴종료 릴레이를 받아 플래그 세팅 (클라 턴 중 호스트는 WaitUntil 대기) ──
    public void NetworkEndMainPhase()   { _mainPhaseEnded  = true; }
    public void NetworkEndAttackPhase() { _attackPhaseEnded = true; }
    public void NetworkSkipToEnd()      { _skipAttackPhase = true; _mainPhaseEnded = true; }

    // ── 유틸 ─────────────────────────────────────────────────

    private void ChangePhase(PhaseType phase)
    {
        CurrentPhase = phase;
        OnPhaseChanged?.Invoke(phase);
        Debug.Log($"[Turn {TurnNumber}] {(IsPlayerTurn ? "Player" : "AI")} — {phase}");

        // 3c: 호스트는 페이즈/턴이 바뀔 때마다 클라에 상태 복제
        if (NetHub.IsOnline && NetHub.IsAuthority) NetHub.BroadcastState();
    }

    private PlayerController GetCurrentPlayer()
    {
        return IsPlayerTurn
            ? PlayerController.PlayerInstance
            : PlayerController.AIInstance;
    }
}