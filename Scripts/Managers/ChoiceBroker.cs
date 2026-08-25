// Assets/Scripts/Managers/ChoiceBroker.cs
// ─────────────────────────────────────────────────────────────────────────
// 선택 중개소 — "이 선택을 누가 하는가"를 결정하는 단일 지점 (2단계: 선택 async).
//  · 행동은 GameActionGateway 한 곳으로 모았듯, "선택(입력)"은 여기 한 곳으로 모은다.
//  · 지금은 [로컬 인간 → 팝업] / [그 외 → 자동선택] 둘뿐 (기존 동작과 100% 동일).
//  · 콜백(onPicked) 구조라 이미 비동기 형태 — 팝업이 뜨고, 나중에 클릭하면 콜백이 불림.
//
//  ★ 3단계(서버 대전) 전환 시: PickCard 안에 "원격 인간 → 네트워크 선택요청 → 응답 시 onPicked"
//    분기 한 개만 추가하면 된다. 이 선택을 부르는 수십 개 효과 핸들러는 하나도 안 바뀐다.
// ─────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using UnityEngine;

public static class ChoiceBroker
{
    // 이 선택을 "로컬 인간"이 하는가? — 판정을 NetHub(현재 채널)에 위임.
    //  · 오프라인: chooserIsPlayer 그대로 (기존 동작).
    //  · 온라인: 이 클라의 플레이어 슬롯과 일치하는지로 판정 (PhotonChannel이 구현).
    public static bool IsLocalHuman(bool chooserIsPlayer) => NetHub.IsLocalHuman(chooserIsPlayer);

    // 카드 후보 중 1장 선택. 로컬 인간이면 팝업, 아니면 autoPick 자동.
    //  · onCancel 은 로컬 인간만 의미 (자동선택은 취소 없음).
    //  · 후보가 없으면 아무것도 안 함(onPicked 미호출) — 기존 동작 유지.
    public static void PickCard(
        bool chooserIsPlayer,
        string title,
        List<CardData> candidates,
        Action<CardData> onPicked,
        Func<List<CardData>, CardData> autoPick = null,
        Action onCancel = null)
    {
        if (candidates == null || candidates.Count == 0) return;

        if (IsLocalHuman(chooserIsPlayer) && HandPickPopup.Instance != null)
        {
            HandPickPopup.Instance.Show(title, candidates, onPicked, onCancel);
        }
        else if (NetHub.IsOnline)
        {
            // 온라인: 선택 주체가 원격 클라 → 그 클라에 카드 선택 요청 → 응답 시 onPicked/onCancel
            NetHub.RequestCard(chooserIsPlayer, title, candidates, onPicked, onCancel);
        }
        else
        {
            onPicked((autoPick ?? (list => list[0]))(candidates));
        }
    }

    // 방어 결정 (블록/패스 불리언 선택). 방어자가 로컬 인간이면 DefensePopup, 아니면 autoDecide 자동.
    //  · onDecided(true=방어, false=패스). 호출부는 코루틴에서 WaitUntil로 결과를 기다린다.
    //  · attacker/defender 는 팝업 표시용(공격/방어 카드).
    //  · attackerPower/defenderPower: 권위 측이 계산한 실제 전투 수치(버프·디버프 반영). 팝업 표시용, -1이면 인쇄 파워로 폴백.
    public static void DecideDefense(
        bool defenderIsPlayer,
        CardData attacker,
        CardData defender,
        Func<bool> autoDecide,
        Action<bool> onDecided,
        int attackerPower = -1,
        int defenderPower = -1)
    {
        if (IsLocalHuman(defenderIsPlayer) && DefensePopup.Instance != null)
        {
            DefensePopup.Instance.Show(attacker, defender, onDecided, attackerPower, defenderPower);
        }
        else if (NetHub.IsOnline)
        {
            // 온라인: 방어자가 원격 클라 → 그 클라에 선택 요청 → 응답 시 onDecided
            // 수치는 호스트가 계산해 함께 보냄 (클라는 미러 상태라 재계산하면 호스트와 어긋날 수 있음)
            NetHub.RequestDefense(defenderIsPlayer, attacker, defender, onDecided, attackerPower, defenderPower);
        }
        else
        {
            onDecided(autoDecide != null && autoDecide());
        }
    }
}
