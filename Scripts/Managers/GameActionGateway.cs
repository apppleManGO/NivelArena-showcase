// Assets/Scripts/Managers/GameActionGateway.cs
// ─────────────────────────────────────────────────────────────────────────
// 행동 실행 관문 (교통정리 창구).
//  · 플레이어/AI의 모든 행동이 반드시 이 Submit()을 통과한다.
//  · 관문은 "직접 로직을 구현"하지 않는다 — 기존 매니저(FieldManager/EffectSystem 등)를
//    순서대로 호출만 한다. (얇게 유지 — 갓 오브젝트 금지)
//  · 검증(치트 방지의 씨앗)도 흩어져 있던 것을 여기로 모은다.
//
//  ★ 3단계(서버 대전) 전환 시: Submit() 안쪽에만 "서버로 보낼지 로컬 실행할지" 분기를 넣는다.
//    UI/AI 호출부는 하나도 안 바뀐다.
// ─────────────────────────────────────────────────────────────────────────
using UnityEngine;

public static class GameActionGateway
{
    // 모든 행동의 유일한 입구. 반환: 성공 여부.
    // (지금은 로컬 즉시 실행. 3단계에선 서버 왕복 때문에 async/이벤트 방식으로 바뀔 자리)
    public static bool Submit(GameAction action)
    {
        // 권위(오프라인 or 온라인 호스트)면 직접 검증·실행.
        // 온라인 클라면 행동을 호스트에게 전송(호스트가 검증·실행 후 복제로 돌려줌).
        if (NetHub.IsAuthority)
        {
            bool ok = Execute(action);
            if (ok && NetHub.IsOnline)
            {
                Narrate(action);               // 온라인 커멘터리 (오프라인은 AIController가 담당)
                NetHub.BroadcastState();        // 3c: 실행 성공 시 클라에 상태 복제
            }
            return ok;
        }

        NetHub.SendAction(action);
        return true; // 전송 접수 (실제 반영은 호스트의 복제로 도착)
    }

    // 온라인 커멘터리: 행동한 쪽의 "상대"에게 토스트 1개를 띄운다(뷰어 입장에선 항상 "상대: ~").
    //  · a.IsPlayer=true(호스트 행동)  → 클라(호스트의 상대)가 봐야 함 → RelayToast.
    //  · a.IsPlayer=false(클라 행동)   → 호스트(클라의 상대)가 봐야 함 → 로컬 표시.
    //     단, 공격은 클라 행동일 때 호스트에서 전투 토스트가 이미 상세히 뜨므로 중복 방지로 생략.
    //  · 오프라인은 여기 안 옴(호출 조건이 NetHub.IsOnline) → 기존 AIController "AI: ~" 유지.
    private static void Narrate(GameAction a)
    {
        string body = DescribeAction(a);
        if (body == null) return;
        string msg = $"{NetHub.OpponentLabel}: {body}"; // 온라인이므로 OpponentLabel="상대"
        if (a.IsPlayer) NetHub.RelayToast(msg);
        else            ToastView.Show(msg);
    }

    private static string DescribeAction(GameAction a)
    {
        string n = a.Card != null ? a.Card.CardName : "";
        switch (a.Type)
        {
            case GameActionType.PlaceUnit:    return $"{n} → 레인{a.Lane + 1} 배치";
            case GameActionType.Upgrade:      return $"{n} → 레인{a.Lane + 1} 업그레이드";
            case GameActionType.EquipItem:    return $"{n} → 레인{a.Lane + 1} 장착";
            case GameActionType.PlaySkill:    return $"스킬 {n} 사용";
            case GameActionType.LeaderActive: return "리더 액티브 발동";
            // 공격: 호스트 공격만 릴레이(클라는 전투 토스트 못 봄). 클라 공격은 호스트 전투 토스트가 담당 → null.
            case GameActionType.DeclareAttack: return a.IsPlayer ? $"레인{a.Lane + 1} 공격!" : null;
            default: return null;
        }
    }

    // 종류에 따라 올바른 처리로 라우팅 (교통정리만)
    private static bool Execute(GameAction a)
    {
        switch (a.Type)
        {
            case GameActionType.PlaceUnit:     return ExecutePlaceUnit(a);
            case GameActionType.Upgrade:       return ExecuteUpgrade(a);
            case GameActionType.EquipItem:     return ExecuteEquipItem(a);
            case GameActionType.DeclareAttack: return ExecuteDeclareAttack(a);
            case GameActionType.PlaySkill:     return ExecutePlaySkill(a);
            case GameActionType.LeaderActive:  return ExecuteLeaderActive(a);
            default:
                Debug.LogWarning($"[Gateway] 미지원 행동: {a.Type}");
                return false;
        }
    }

    // 이 행동 주체(isPlayer)의 턴 + 요구 페이즈인지 검증 (상대 턴 배치/공격 차단 — 온라인 치트 방지 겸용).
    //  · TurnManager.IsPlayerTurn: true=플레이어(true 슬롯)의 턴. 즉 "IsPlayerTurn == a.IsPlayer"면 그 슬롯의 턴.
    //  · 오프라인도 동일하게 동작(플레이어=자기 턴 메인, AI=AI 턴 메인).
    private static bool IsTurnPhase(bool isPlayer, PhaseType required)
    {
        var tm = TurnManager.Instance;
        return tm != null && tm.IsPlayerTurn == isPlayer && tm.CurrentPhase == required;
    }

    // 유닛 배치: 검증 → 상태 변경. UI는 건드리지 않는다(호출부가 결과로 처리).
    private static bool ExecutePlaceUnit(GameAction a)
    {
        var pc = a.IsPlayer ? PlayerController.PlayerInstance : PlayerController.AIInstance;
        var card = a.Card;
        if (card == null) return false;

        // 자신의 메인 페이즈에만 배치 가능 (상대 턴 배치 차단)
        if (!IsTurnPhase(a.IsPlayer, PhaseType.Main)) return false;
        // 검증 (기존 LaneSlot/AIController에 중복돼 있던 것을 여기로 통합)
        if (!FieldManager.Instance.CanPlayCard(a.IsPlayer, card, pc.Size)) return false;
        if (!FieldManager.Instance.CanPlace(a.IsPlayer, a.Lane)) return false;
        if (EffectSystem.Instance.IsLaneDeployBlocked(a.IsPlayer, a.Lane, card)) return false;

        // 상태 변경 (실무는 기존 매니저들이)
        FieldManager.Instance.PlaceUnit(a.IsPlayer, a.Lane, card);
        pc.RemoveFromHand(card);
        EffectSystem.Instance.OnUnitPlaced(a.IsPlayer, a.Lane, card);
        return true;
    }

    // 업그레이드: 기존 유닛보다 높은 코스트로 교체 (엑시트 미발동은 UpgradeUnit 내부 처리)
    private static bool ExecuteUpgrade(GameAction a)
    {
        var pc = a.IsPlayer ? PlayerController.PlayerInstance : PlayerController.AIInstance;
        var card = a.Card;
        if (card == null) return false;

        if (!IsTurnPhase(a.IsPlayer, PhaseType.Main)) return false; // 자신의 메인 페이즈만
        if (!FieldManager.Instance.CanUpgrade(a.IsPlayer, a.Lane, card)) return false;
        if (!FieldManager.Instance.CanPlayCardAsUpgrade(a.IsPlayer, a.Lane, card, pc.Size)) return false;

        FieldManager.Instance.UpgradeUnit(a.IsPlayer, a.Lane, card);
        pc.RemoveFromHand(card);
        EffectSystem.Instance.OnUnitPlaced(a.IsPlayer, a.Lane, card);
        return true;
    }

    // 아이템 장착: 코스트 검증 + 장착(장착 조건은 EquipItem 내부 검증) + 코스트 차감용 스킬존 이동
    private static bool ExecuteEquipItem(GameAction a)
    {
        var pc = a.IsPlayer ? PlayerController.PlayerInstance : PlayerController.AIInstance;
        var card = a.Card;
        if (card == null) return false;

        if (!IsTurnPhase(a.IsPlayer, PhaseType.Main)) return false; // 자신의 메인 페이즈만
        if (!FieldManager.Instance.CanPlayCard(a.IsPlayer, card, pc.Size)) return false;
        if (!FieldManager.Instance.EquipItem(a.IsPlayer, a.Lane, card)) return false;

        pc.RemoveFromHand(card);
        pc.AddToSkillZone(card); // 코스트 차감용 (턴엔드에 트래시 이동)
        return true;
    }

    // 공격 선언: 검증 후 전투 코루틴 시작. (방어 선택 팝업은 코루틴 내부 — 2단계에서 원격 async화)
    // ※ 현재는 플레이어 공격만 관문 경유. AI 공격은 ProcessAIAttackPhase 코루틴이 자체 진행.
    private static bool ExecuteDeclareAttack(GameAction a)
    {
        if (!IsTurnPhase(a.IsPlayer, PhaseType.Attack)) return false; // 자신의 어택 페이즈만
        if (a.IsPlayer)
        {
            // 호스트/오프라인 플레이어 공격 (어택 페이즈 기록 포함)
            if (!CombatManager.Instance.CanAttack(a.Lane)) return false;
            CombatManager.Instance.StartCoroutine(CombatManager.Instance.PlayerAttackLane(a.Lane));
        }
        else
        {
            // 온라인: 상대(클라)가 릴레이한 공격 → 범용 경로 (방어 결정은 ChoiceBroker로 호스트 팝업).
            //  ForceAttackLane은 ST07 보너스공격용이라 1회 제한을 안 걸므로, 여기서 검증·기록을 직접 한다.
            //  (검증/기록을 submit 시점에 해야 클라가 코루틴 완료 전 연타해도 재공격이 막힌다)
            if (!CombatManager.Instance.CanAttack(false, a.Lane)) return false;
            CombatManager.Instance.MarkAttacked(false, a.Lane);
            CombatManager.Instance.StartCoroutine(CombatManager.Instance.ForceAttackLane(false, a.Lane));
        }
        return true;
    }

    // 스킬 발동: 검증 + 코스트 소비(패→스킬존)까지. 효과 해결(ExecuteSkillEffect)·타겟 선택은 호출부/2단계.
    private static bool ExecutePlaySkill(GameAction a)
    {
        var pc = a.IsPlayer ? PlayerController.PlayerInstance : PlayerController.AIInstance;
        var card = a.Card;
        if (card == null) return false;

        if (!FieldManager.Instance.CanPlayCard(a.IsPlayer, card, pc.Size)) return false;
        if (!EffectSystem.Instance.CanCastSkill(a.IsPlayer, card)) return false;

        pc.RemoveFromHand(card);
        pc.AddToSkillZone(card);
        return true;
    }

    // 리더 액티브: 각성면 액티브 발동 (검증은 CanUseLeaderActive, 내부 선택 팝업은 2단계 대상)
    private static bool ExecuteLeaderActive(GameAction a)
    {
        if (!EffectSystem.Instance.CanUseLeaderActive(a.IsPlayer)) return false;
        return EffectSystem.Instance.TriggerLeaderActive(a.IsPlayer);
    }
}
