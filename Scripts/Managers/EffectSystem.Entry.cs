// Assets/Scripts/Managers/EffectSystem.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public partial class EffectSystem : MonoBehaviour
{
    public void OnUnitPlaced(bool isPlayer, int lane, CardData card)
    {
        // BT07-048: 이번 턴〈이브〉를 배치했는지 추적 (배치 종류 무관 — 정상 배치/사이즈 무시 배치 모두 포함)
        if (IsEveCard(card)) _bt07EveDeployedThisTurn[Idx(isPlayer)] = true;

        // ST08 요밀로 이스케이프: 상대가 파워 N 이하 유닛을 배치하면 감시자 드로우1+상대 1대미지
        int watcherIdx = Idx(!isPlayer);
        if (_escapeDeployPunishThreshold[watcherIdx] >= 0 && card.AttackPower <= _escapeDeployPunishThreshold[watcherIdx])
        {
            bool watcherIsPlayer = !isPlayer;
            EffectActions.Draw(watcherIsPlayer, 1);
            EffectActions.DamageOpponent(watcherIsPlayer, 1);
            Debug.Log($"[이스케이프 감시] {card.CardName}(파워{card.AttackPower}) 배치 → 드로우1+1대미지");
        }

        // BT03 폴크방 DrawPerEntryAllyLockOpponentEntry: 엔트리 잠금 중이면 이 유닛 엔트리 발동 안 함
        bool entryBlocked = _entryLocked[Idx(isPlayer)];
        if (entryBlocked)
        {
            Debug.Log($"[엔트리 잠금] {card.CardName} 엔트리 효과 발동 불가 (상대 폴크방 효과)");
            return;
        }

        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            // 모든 엔트리 effectType은 EffectSystem.EntryHandlers.cs 레지스트리에 등록됨
            if (_entryHandlers != null && _entryHandlers.TryGetValue(type, out var handler))
                handler(isPlayer, lane, card, value, et);
        }

        ApplyPendingNextDeployBuff(isPlayer, lane); // BT04 BuffNextDeployPlunderPower

        CheckLethalPower(); // 룰: 배치/엔트리효과(디버프 아우라 포함)로 파워 0 이하 유닛 사망
    }
}
