// Assets/Scripts/Managers/EffectSystem.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public partial class EffectSystem : MonoBehaviour
{
    public void OnTriggerEffect(bool isPlayer, CardData card)
    {
        foreach (var et in card.TriggerEffectTypes)
        {
            var (type, value) = Parse(et);
            if (_triggerHandlers != null && _triggerHandlers.TryGetValue(type, out var handler))
                handler(isPlayer, card, value, et);
        }
    }

    public void OnAttackEnd(bool isPlayer, int lane)
    {
        _attackBoosts.Remove(Key(isPlayer, lane));
        _bt06ChainInfiltrateThisAttack.Remove(Key(isPlayer, lane)); // 방어당한 경우 등 소비되지 않았으면 정리
    }

    // ── 스킬 효과 실행 ───────────────────────────────────────────
    // targetLane: -1 = 타겟 없음
}
