// Assets/Scripts/Managers/EffectSystem.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public partial class EffectSystem : MonoBehaviour
{
    // 엑시트 해결 진입점. 본체는 중간 return이 여러 개라, 어느 경로로 끝나든
    // 그 레인에 남은 "엑시트 타이밍 부여"가 정리되도록 finally로 감싼다.
    // (RemoveUnit은 keepExitGrants:true로 이 부여들을 남겨두고 오므로 여기서 마무리)
    public void OnUnitTrashed(bool isPlayer, int lane, CardData card)
    {
        try { OnUnitTrashedCore(isPlayer, lane, card); }
        finally { ClearExitGrants(isPlayer, lane); }
    }

    private void OnUnitTrashedCore(bool isPlayer, int lane, CardData card)
    {
        // 이 턴 트래시된 자신 유닛 수 (비르기타 등 조건용 — 전투/효과 모두 포함)
        _unitsTrashedThisTurn[Idx(isPlayer)]++;

        // SB01: 부여형/오라형 트래시 반응 (대미지존행/페인이터 연동/전선구축 재배치/암드 강제 트래시)
        SB01_OnUnitTrashedHook(isPlayer, lane, card);

        // BT04 EntryGrantDamageZoneOnTrash: 부여된 유닛은 트래시 대신 자신 대미지 존으로
        {
            string dzKey = Key(isPlayer, lane);
            if (_grantDamageZoneOnTrash.Contains(dzKey))
            {
                _grantDamageZoneOnTrash.Remove(dzKey);
                GetOwner(isPlayer).RemoveFromTrash(card);
                PlaceToDamageZone(isPlayer, card);
                return;
            }
        }

        // BT03 D: EntryTrashAllyGrantExitReturn — 트래시 시 패로 귀환
        {
            string exitRetKey = Key(isPlayer, lane);
            if (_grantedExitReturn.Contains(exitRetKey))
            {
                _grantedExitReturn.Remove(exitRetKey);
                GetOwner(isPlayer).RemoveFromTrash(card);
                GetOwner(isPlayer).AddToHand(card);
                HandView.Instance?.RefreshHand();
                Debug.Log($"[부여 엑시트] {card.CardName} → 패로 귀환");
                return; // 패로 갔으므로 이후 엑시트 효과 불필요
            }
        }

        // ST07: 이 유닛에게 부여된 「엑시트: 호문클루스 공격 수만큼 X」 실행
        string exitKey = Key(isPlayer, lane);
        if (_grantedExit.TryGetValue(exitKey, out var grant))
        {
            _grantedExit.Remove(exitKey);
            ExecuteGrantedExit(isPlayer, card, grant.type, grant.value);
        }

        // BT04 EntryLockExitByTotalDamageZone: 이 플레이어의 엑시트 효과 발동 잠금 (이번 턴)
        if (!_exitDisabledThisTurn[Idx(isPlayer)])
        {
            foreach (var et in card.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (_exitHandlers != null && _exitHandlers.TryGetValue(type, out var handler))
                    handler(isPlayer, lane, card, value, et);
            }
        }

        // BT02 효과로 트래시된 유닛 카운터 (길로틴, 그레이브용 — 근사: 전투 포함 전체 카운트)
        if (card.Type == CardType.Unit && lane >= 0)
            _effectTrashedThisTurn[Idx(isPlayer)]++;

        // BT01 신데렐라 리더: 패시브 — 5코스트 이상 자신 유닛 트래시 시 드로우1 (각자 턴당 1회)
        if (card.Type == CardType.Unit && card.Cost >= 0)
        {
            var owner0 = GetOwner(isPlayer);
            if (owner0.Leader != null)
            {
                var (ltype0, lval0) = Parse(owner0.Leader.BaseEffectType ?? "");
                if (ltype0 == "PassiveExitHighCostDraw" && card.Cost >= lval0)
                {
                    string lkey = $"leaderExitHighCostDraw_{(isPlayer?"P":"A")}";
                    if (!_selfLockedSkillsThisTurn.Contains(lkey))
                    {
                        _selfLockedSkillsThisTurn.Add(lkey);
                        EffectActions.Draw(isPlayer, 1);
                        Debug.Log($"[리더 패시브] {owner0.Leader.LeaderName} → {card.CardName}({card.Cost}코) 트래시 드로우1");
                    }
                }
            }
        }

        // BT01 모더니아: 패시브 — 아군의 엑시트 효과 발동 시 드로우N
        bool hadExitEffect = card.EffectTypes.Any(e => e.StartsWith("Exit"));
        if (hadExitEffect && lane >= 0)
        {
            for (int i3 = 0; i3 < 3; i3++)
            {
                var watcher = FieldManager.Instance.GetUnit(isPlayer, i3);
                if (watcher == null) continue;
                foreach (var wet in watcher.EffectTypes)
                {
                    var (wt, wv) = Parse(wet);
                    if (wt == "PassiveGrantExitDraw")
                        EffectActions.Draw(isPlayer, wv);
                }
            }
        }
    }

}
