// Assets/Scripts/Managers/EffectSystem.BT04.cs
// ─────────────────────────────────────────────────────────────────────────
// BT04 (대미지존 조작 테마) effectType 구현 — 격리 파일.
//  · 대미지존을 "능동적으로 조작하는 자원"으로 사용: 패/필드/덱을 DZ에 놓기, DZ 수 참조,
//    DZ에서 회수/부활, 임계값(파워/DZ 수) 선택 효과.
//  · 핸들러는 기존 레지스트리(_entryHandlers/_attackerHandlers/_exitHandlers/_skillHandlers)에
//    InitBT04Handlers()로 등록 (Awake에서 호출). Active/Passive/Item은 각 switch/loop에 별도 추가.
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class EffectSystem : MonoBehaviour
{
    // ── BT04 턴 단위 상태 (OnTurnEnd에서 초기화) ──
    private int[]  _damageZoneTurnBonus = { 0, 0 };            // DrawIfDamageZoneBonusCount 등 이번 턴 DZ 수 가산
    private bool[] _cardPlacedToDamageZoneThisTurn = { false, false }; // 이 턴 효과로 DZ에 카드 놓임
    private HashSet<string> _endTurnTrashGrants = new();       // Key(isPlayer,lane) — 자신 턴 끝에 트래시
    private bool[] _exitDisabledThisTurn = { false, false };   // EntryLockExitByTotalDamageZone — 엑시트 잠금
    private int[]  _nextDeployPower   = { 0, 0 };              // BuffNextDeployPlunderPower — 다음 배치 유닛 파워
    private int[]  _nextDeployPlunder = { 0, 0 };              // BuffNextDeployPlunderPower — 다음 배치 유닛 약탈
    public  HashSet<string> _grantDamageZoneOnTrash = new();   // Key(isPlayer,lane) — 트래시 시 자신 DZ로

    // 레인 귀속 상태 정리/이동 훅 (ForEachLaneState에서 호출 — Boosts.cs 주석 참고)
    // ※ _grantDamageZoneOnTrash는 엑시트 타이밍에 읽히므로 ForEachExitGrant 쪽에 등록돼 있음
    private void ForEachLaneStateBT04(ILaneStateOp op)
    {
        op.Apply(_endTurnTrashGrants);
    }

    // 다음에 배치되는 유닛에 대기 중인 버프 적용 (OnUnitPlaced 끝에서 호출)
    private void ApplyPendingNextDeployBuff(bool isPlayer, int lane)
    {
        int idx = Idx(isPlayer);
        if (_nextDeployPower[idx] == 0 && _nextDeployPlunder[idx] == 0) return;
        if (_nextDeployPower[idx] != 0) AddTurnBoost(isPlayer, lane, _nextDeployPower[idx]);
        if (_nextDeployPlunder[idx] != 0)
            _grantedAttackerPlunder[Key(isPlayer, lane)] = _nextDeployPlunder[idx];
        Debug.Log($"[BT04] 다음 배치 버프 적용 → 레인{lane} 파워+{_nextDeployPower[idx]}, 약탈[{_nextDeployPlunder[idx]}]");
        _nextDeployPower[idx] = 0; _nextDeployPlunder[idx] = 0;
    }

    // 자신 턴 종료 시 「턴 끝 트래시」 부여된 유닛 처리 (OnTurnEnd에서 호출)
    private void ProcessEndTurnTrashGrants(bool isPlayerTurn)
    {
        foreach (var key in _endTurnTrashGrants.ToList())
        {
            // key = "p_2" / "a_1"
            bool kp = key.StartsWith("p");
            if (kp != isPlayerTurn) continue;
            int kl = int.Parse(key.Substring(2));
            if (FieldManager.Instance.GetUnit(kp, kl) != null)
                CombatManager.Instance?.TrashUnitPublic(kp, kl);
            _endTurnTrashGrants.Remove(key);
        }
    }

    // ── 대미지존 조작 인프라 ──

    // 자신의 대미지 존 참조 수 = 실제 장수 + PassiveDamageZoneBonus(리더/유닛) + 이번 턴 가산
    public int GetDamageZoneCount(bool isPlayer)
    {
        var owner = GetOwner(isPlayer);
        int c = owner.DamageZone.Count;
        if (owner.Leader != null)
        {
            var (lt, lv) = Parse(owner.Leader.BaseEffectType ?? "");
            if (lt == "PassiveDamageZoneBonus") c += lv;
        }
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u?.EffectTypes == null) continue;
            foreach (var et in u.EffectTypes)
            {
                var (t, v) = Parse(et);
                if (t == "PassiveDamageZoneBonus") c += v;
            }
        }
        c += _damageZoneTurnBonus[Idx(isPlayer)];
        return c;
    }

    // 자신과 상대의 대미지 존 카드 총합 (자신 쪽은 보너스 반영, 상대는 실제 수)
    public int GetTotalDamageZoneCount(bool isPlayer)
        => GetDamageZoneCount(isPlayer) + GetOwner(!isPlayer).DamageZone.Count;

    // 자신의 대미지 존에서 특정 소속을 가진 카드 수
    public int GetDamageZoneFactionCount(bool isPlayer, string faction)
        => GetOwner(isPlayer).DamageZone.Count(c =>
            (c.Faction != null && c.Faction.Contains(faction)) || c.Keywords.Contains(faction));

    // 카드를 자신의 대미지 존에 놓는다 (호출 전에 원래 존에서 제거해둘 것)
    public void PlaceToDamageZone(bool isPlayer, CardData card)
    {
        GetOwner(isPlayer).AddToDamageZone(card);
        _cardPlacedToDamageZoneThisTurn[Idx(isPlayer)] = true;
        DamageZoneView.NotifyDamageChanged();
        HUDView.NotifyStatusChanged();
        Debug.Log($"[BT04] {card.CardName} → 대미지 존 ({GetOwner(isPlayer).DamageZone.Count}장)");
    }

    // ── 핸들러 레지스트리 등록 (Awake에서 호출) ──
    private void InitBT04Handlers()
    {
        // 엔트리
        _entryHandlers["EntryHandToDamageZoneDraw"]       = BT04_EntryHandToDamageZoneDraw;
        _entryHandlers["EntryMillToDamageZoneDraw"]       = BT04_EntryMillToDamageZoneDraw;
        _entryHandlers["EntryTrashAlly"]                  = BT04_EntryTrashAlly;
        _entryHandlers["EntryTrashAllyHitBoost"]          = BT04_EntryTrashAllyHitBoost;
        _entryHandlers["EntryDrawPerFactionAlly"]         = BT04_EntryDrawPerFactionAlly;
        _entryHandlers["EntryDiscardTrashLowCostEncounter"] = BT04_EntryDiscardTrashLowCostEncounter;
        // 어태커
        _attackerHandlers["AttackerPowerPerDamageZone"]   = BT04_AttackerPowerPerDamageZone;
        _attackerHandlers["AttackerHitBoostIfDamageZone"] = BT04_AttackerHitBoostIfDamageZone;
        _attackerHandlers["AttackerDamageIfTotalDamageZone"] = BT04_AttackerDamageIfTotalDamageZone;
        _attackerHandlers["AttackerDrawIfTotalDamageZone"]   = BT04_AttackerDrawIfTotalDamageZone;
        // 엑시트
        _exitHandlers["ExitSelfToDamageZone"]             = BT04_ExitSelfToDamageZone;
        // 스킬
        _skillHandlers["HandToDamageZoneDraw"]            = BT04_HandToDamageZoneDraw;
        _skillHandlers["DrawIfTotalDamageZone"]           = BT04_DrawIfTotalDamageZone;
        _skillHandlers["DebuffEnemyPerDamageZone"]        = BT04_DebuffEnemyPerDamageZone;
        _skillHandlers["BuffAllAlliesIfExactlyTwo"]       = BT04_BuffAllAlliesIfExactlyTwo;

        // ── 배치 2 ──
        _entryHandlers["EntryDiscardRecoverFactionUnit"]  = BT04_EntryDiscardRecoverFactionUnit;
        _entryHandlers["EntryRecoverFactionByDamageZoneCost"] = BT04_EntryRecoverFactionByDamageZoneCost;
        _entryHandlers["EntryTrashCardToDamageZone"]      = BT04_EntryTrashCardToDamageZone;
        _entryHandlers["EntryDrawThenHandToDamageZone"]   = BT04_EntryDrawThenHandToDamageZone;
        _entryHandlers["EntryHandToDamageZoneOrTrashAlly"] = BT04_EntryHandToDamageZoneOrTrashAlly;
        _entryHandlers["EntryTrashEnemiesByDamageZoneCost"] = BT04_EntryTrashEnemiesByDamageZoneCost;
        _entryHandlers["EntryTrashAllyTrashEnemyByCost"]  = BT04_EntryTrashAllyTrashEnemyByCost;
        _entryHandlers["EntryTrashEncounterToDamageZone"] = BT04_EntryTrashEncounterToDamageZone;
        _entryHandlers["EntryTrashAllyDrawPower"]         = BT04_EntryTrashAllyDrawPower;
        _entryHandlers["EntryGrantEndTurnTrashHitBoost"]  = BT04_EntryGrantEndTurnTrashHitBoost;
        _skillHandlers["DamageZoneTrashIfThreshold"]      = BT04_DamageZoneTrashIfThreshold;
        _skillHandlers["TrashEnemyByDamageZoneFaction"]   = BT04_TrashEnemyByDamageZoneFaction;
        _skillHandlers["DamageIfFactionInTrash"]          = BT04_DamageIfFactionInTrash;
        _skillHandlers["DebuffEnemyByAllyPowerTrashAlly"] = BT04_DebuffEnemyByAllyPowerTrashAlly;
        _skillHandlers["MassReviveFactionIgnoreSize"]     = BT04_MassReviveFactionIgnoreSize;
        _skillHandlers["DamageZoneSwapDraw"]              = BT04_DamageZoneSwapDraw;
        _skillHandlers["DrawIfDamageZoneBonusCount"]      = BT04_DrawIfDamageZoneBonusCount;
        _exitHandlers["ExitEffectTrashDiscardDamage"]     = BT04_ExitEffectTrashDiscardDamage;
        _exitHandlers["ExitRecoverFactionFromTrash"]      = BT04_ExitRecoverFactionFromTrash;
        _exitHandlers["ExitBuffAllyUntilOpponentTurn"]    = BT04_ExitBuffAllyUntilOpponentTurn;
        _exitHandlers["ExitDebuffEnemyTurnConditional"]   = BT04_ExitDebuffEnemyTurnConditional;
        _exitHandlers["ExitMillDrawIfExitUnit"]           = BT04_ExitMillDrawIfExitUnit;
        _triggerHandlers["TriggerTrashSelfTrashEnemyByDamageZoneCost"] = BT04_TriggerTrashSelfTrashEnemyByDamageZoneCost;

        // ── 배치 3 ──
        _entryHandlers["EntryMillDamageZoneDamageOrPowerBoost"] = BT04_EntryMillDamageZoneDamageOrPowerBoost;
        _entryHandlers["EntryLockExitByTotalDamageZone"]   = BT04_EntryLockExitByTotalDamageZone;
        _entryHandlers["EntryConditionalAlliesBoostEncounterTrash"] = BT04_EntryConditionalAlliesBoostEncounterTrash;
        _entryHandlers["EntryDamageZoneSacrificePowerByCost"] = BT04_EntryDamageZoneSacrificePowerByCost;
        _attackerHandlers["AttackerDiscardToDamageZoneDamageOrPowerBoost"] = BT04_AttackerDiscardToDamageZoneDamageOrPowerBoost;
        _exitHandlers["ExitDiscardRecoverUnitByCostOrFaction"] = BT04_ExitDiscardRecoverUnitByCostOrFaction;
        _exitHandlers["ExitRevealSelectDamageZoneHandTrashRest"] = BT04_ExitRevealSelectDamageZoneHandTrashRest;
        _exitHandlers["ExitEffectTrashDiscardDelayedDeploy"] = BT04_ExitEffectTrashDiscardDelayedDeploy;
        _skillHandlers["BuffNextDeployPlunderPower"]       = BT04_BuffNextDeployPlunderPower;
        _skillHandlers["RecoverCardThenDiscard"]           = BT04_RecoverCardThenDiscard;

        // ── 배치 6 ──
        _entryHandlers["EntryGrantDamageZoneOnTrash"]      = BT04_EntryGrantDamageZoneOnTrash;
        _attackerHandlers["AttackerGainExitRecoverByAttackCount"] = BT04_AttackerGainExitGrant;
        _attackerHandlers["AttackerGainExitReviveByAttackCount"]  = BT04_AttackerGainExitGrant;
        _exitHandlers["ExitDiscardDeclareEffectLock"]      = BT04_ExitDiscardDeclareEffectLock;
        _exitHandlers["ExitMillDeckBottomReviveSelf"]      = BT04_ExitMillDeckBottomReviveSelf;
        _triggerHandlers["TriggerConditionalTrashDamageByDamageZone"] = BT04_TriggerConditionalTrashDamageByDamageZone;
    }

    // ── 엔트리 핸들러 ──

    // BT04-051: 패1 골라 대미지 존에 → 드로우1
    private void BT04_EntryHandToDamageZoneDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        if (owner.Hand.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "대미지 존에 놓을 패를 선택하세요", new List<CardData>(owner.Hand),
            picked =>
            {
                owner.RemoveFromHand(picked);
                PlaceToDamageZone(isPlayer, picked);
                EffectActions.Draw(isPlayer, 1);
            },
            aiPick: list => list.OrderBy(c => c.Cost).First());
    }

    // BT04-005: 덱 맨 위 1장을 대미지 존에 놓을 수 있다 → 드로우1 (유익하므로 자동 실행)
    private void BT04_EntryMillToDamageZoneDraw(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        if (owner.DrawPile.Count == 0) return;
        var top = owner.DrawPile[0];
        owner.RemoveFromDeckAt(0);
        PlaceToDamageZone(isPlayer, top);
        EffectActions.Draw(isPlayer, value > 0 ? value : 1);
    }

    // BT04-065: 필드의 자신 유닛 1장 트래시
    private void BT04_EntryTrashAlly(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            "트래시할 자신 유닛을 선택하세요",
            picked => EffectActions.TrashUnit(isPlayer, picked));
    }

    // BT04-058: 자신 유닛 1장 트래시 → 이 유닛 이번 턴 히트+N
    private void BT04_EntryTrashAllyHitBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            "트래시할 자신 유닛을 선택하세요",
            picked =>
            {
                EffectActions.TrashUnit(isPlayer, picked);
                AddTurnHitBoost(isPlayer, lane, value > 0 ? value : 1);
            });
    }

    // BT04-020: 《계승자》/《과거 혹은 미래》 다른 아군 1장마다 드로우N
    private void BT04_EntryDrawPerFactionAlly(bool isPlayer, int lane, CardData card, int value, string et)
    {
        int cnt = 0;
        for (int i = 0; i < 3; i++)
        {
            if (i == lane) continue;
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u?.Faction != null && (u.Faction.Contains("계승자") || u.Faction.Contains("과거 혹은 미래"))) cnt++;
        }
        if (cnt > 0) EffectActions.Draw(isPlayer, cnt * (value > 0 ? value : 1));
    }

    // BT04-056: 패1 트래시 → 조우 유닛이 N코스트 이하면 트래시
    private void BT04_EntryDiscardTrashLowCostEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.DiscardHand(isPlayer, 1, _ =>
        {
            var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (enc != null && enc.Cost <= value)
                EffectActions.TrashUnit(!isPlayer, lane);
        });
    }

    // ── 어태커 핸들러 ──

    // BT04-007: 어태커 — 대미지 존 1장마다 파워+N (이 공격 끝까지)
    private void BT04_AttackerPowerPerDamageZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        int boost = GetDamageZoneCount(isPlayer) * value;
        if (boost > 0) AddAttackBoost(isPlayer, lane, boost);
        Debug.Log($"[BT04 어태커] {card.CardName} → DZ {GetDamageZoneCount(isPlayer)}장 → 파워+{boost}");
    }

    // BT04-007: 어태커 — 대미지 존 M장 이상이면 히트+H ("AttackerHitBoostIfDamageZone:M:H")
    private void BT04_AttackerHitBoostIfDamageZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var p = et.Split(':');
        int thresh = value;
        int hitv = p.Length > 2 && int.TryParse(p[2], out int h) ? h : 1;
        if (GetDamageZoneCount(isPlayer) >= thresh)
            EffectActions.Buff(isPlayer, lane, 0, hitv, BoostUntil.EndOfAttack);
    }

    // BT04-024: 어태커 — 양측 DZ 합 N장 이상이면 상대 1대미지
    private void BT04_AttackerDamageIfTotalDamageZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (GetTotalDamageZoneCount(isPlayer) >= value)
            EffectActions.DamageOpponent(isPlayer, 1);
    }

    // BT04-015: 어태커 — 양측 DZ 합 N1 이상이면 드로우1, N2 이상이면 추가 드로우1 ("...:N1:N2")
    private void BT04_AttackerDrawIfTotalDamageZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var p = et.Split(':');
        int n1 = value;
        int n2 = p.Length > 2 && int.TryParse(p[2], out int x) ? x : int.MaxValue;
        int total = GetTotalDamageZoneCount(isPlayer);
        if (total >= n1) EffectActions.Draw(isPlayer, 1);
        if (total >= n2) EffectActions.Draw(isPlayer, 1);
    }

    // ── 엑시트 핸들러 ──

    // BT04-047: 이 유닛을 자신의 대미지 존에 놓는다 (트래시 대신)
    private void BT04_ExitSelfToDamageZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        owner.RemoveFromTrash(card); // 트래시로 갔던 자신을 회수
        PlaceToDamageZone(isPlayer, card);
    }

    // ── 스킬 핸들러 ──

    // BT04-077: 패1 골라 대미지 존에 → 드로우N
    private void BT04_HandToDamageZoneDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        if (owner.Hand.Count == 0) { EffectActions.Draw(isPlayer, value); return; }
        EffectActions.PickFromCards(isPlayer, "대미지 존에 놓을 패를 선택하세요", new List<CardData>(owner.Hand),
            picked =>
            {
                owner.RemoveFromHand(picked);
                PlaceToDamageZone(isPlayer, picked);
                EffectActions.Draw(isPlayer, value);
            },
            aiPick: list => list.OrderBy(c => c.Cost).First());
    }

    // BT04-034: 필드 아군 전체 이번 턴 파워+2000, 양측 DZ 합 N 이상이면 드로우1
    private void BT04_DrawIfTotalDamageZone(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        for (int i = 0; i < 3; i++)
            if (FieldManager.Instance.GetUnit(isPlayer, i) != null)
                AddTurnBoost(isPlayer, i, 2000);
        if (GetTotalDamageZoneCount(isPlayer) >= value)
            EffectActions.Draw(isPlayer, 1);
    }

    // BT04-032: 상대 유닛 1장 → 이번 턴 자신 DZ 1장마다 파워-N
    private void BT04_DebuffEnemyPerDamageZone(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        int debuff = GetDamageZoneCount(isPlayer) * value;
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
            $"파워-{debuff}를 줄 상대 유닛을 선택하세요",
            picked => AddTurnBoost(!isPlayer, picked, -debuff));
    }

    // BT04-036: 필드 자신 유닛이 정확히 2장이면 전체 이번 턴 파워+N
    private void BT04_BuffAllAlliesIfExactlyTwo(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        int allyCount = Enumerable.Range(0, 3).Count(i => FieldManager.Instance.GetUnit(isPlayer, i) != null);
        if (allyCount != 2) return;
        for (int i = 0; i < 3; i++)
            if (FieldManager.Instance.GetUnit(isPlayer, i) != null)
                AddTurnBoost(isPlayer, i, value);
    }

    // ═══════════════════ 배치 2 ═══════════════════

    // BT04-017: 패1 트래시 가능 → 트래시존에서 해당 소속 유닛 회수
    private void BT04_EntryDiscardRecoverFactionUnit(bool isPlayer, int lane, CardData card, int value, string et)
    {
        string faction = et.Split(':').ElementAtOrDefault(1) ?? "";
        EffectActions.DiscardHandOptional(isPlayer, 1, d =>
        {
            if (d < 1) return;
            EffectActions.RecoverFromTrash(isPlayer,
                c => c.Type == CardType.Unit && ((c.Faction != null && c.Faction.Contains(faction)) || c.Keywords.Contains(faction)),
                $"트래시에서 회수할 《{faction}》 유닛을 선택하세요");
        });
    }

    // BT04-012: 코스트 ≤ DZ수 이고 해당 소속을 가진 카드를 트래시존에서 패로
    private void BT04_EntryRecoverFactionByDamageZoneCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        string faction = et.Split(':').ElementAtOrDefault(1) ?? "";
        int cap = GetDamageZoneCount(isPlayer);
        EffectActions.RecoverFromTrash(isPlayer,
            c => c.Cost <= cap && ((c.Faction != null && c.Faction.Contains(faction)) || c.Keywords.Contains(faction)),
            $"트래시에서 회수할 《{faction}》 카드를 선택하세요 (코스트 {cap} 이하)");
    }

    // BT04-064: 트래시존 카드 1장 골라 대미지 존에 (선택)
    private void BT04_EntryTrashCardToDamageZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        if (owner.TrashPile.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "대미지 존에 놓을 트래시 카드를 선택하세요", new List<CardData>(owner.TrashPile),
            picked => { owner.RemoveFromTrash(picked); PlaceToDamageZone(isPlayer, picked); },
            aiPick: list => list.OrderByDescending(c => c.Cost).First(),
            onCancel: () => { });
    }

    // BT04-053: 드로우N 가능 → 패1 골라 대미지 존에
    private void BT04_EntryDrawThenHandToDamageZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.Draw(isPlayer, value);
        var owner = GetOwner(isPlayer);
        if (owner.Hand.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "대미지 존에 놓을 패를 선택하세요", new List<CardData>(owner.Hand),
            picked => { owner.RemoveFromHand(picked); PlaceToDamageZone(isPlayer, picked); },
            aiPick: list => list.OrderBy(c => c.Cost).First());
    }

    // BT04-068: 패1 DZ에 놓기(가능). 놓지 않았다면 아군 1장 트래시
    private void BT04_EntryHandToDamageZoneOrTrashAlly(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        void TrashAllyFallback()
        {
            EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
                "트래시할 자신 유닛을 선택하세요", picked => EffectActions.TrashUnit(isPlayer, picked));
        }
        if (owner.Hand.Count == 0) { TrashAllyFallback(); return; }
        EffectActions.PickFromCards(isPlayer, "대미지 존에 놓을 패를 선택하세요 (취소 시 아군 트래시)", new List<CardData>(owner.Hand),
            picked => { owner.RemoveFromHand(picked); PlaceToDamageZone(isPlayer, picked); },
            aiPick: list => list.OrderBy(c => c.Cost).First(),
            onCancel: TrashAllyFallback);
    }

    // BT04-073: 코스트 합이 DZ 소속 수 이하가 되도록 상대 유닛을 (원하는 만큼) 트래시 — 저코스트 우선 자동
    private void BT04_EntryTrashEnemiesByDamageZoneCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        string faction = et.Split(':').ElementAtOrDefault(1) ?? "";
        int budget = GetDamageZoneFactionCount(isPlayer, faction);
        var enemies = Enumerable.Range(0, 3)
            .Select(i => (i, u: FieldManager.Instance.GetUnit(!isPlayer, i)))
            .Where(t => t.u != null)
            .OrderBy(t => t.u.Cost).ToList();
        foreach (var (i, u) in enemies)
        {
            if (u.Cost > budget) continue;
            budget -= u.Cost;
            EffectActions.TrashUnit(!isPlayer, i);
        }
    }

    // BT04-074: 다른 아군 1장 트래시 가능 → 그 코스트 이하 상대 유닛 1장 트래시
    private void BT04_EntryTrashAllyTrashEnemyByCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => l != lane,
            "트래시할 다른 자신 유닛을 선택하세요 (취소 가능)",
            allyLane =>
            {
                int cap = FieldManager.Instance.GetUnit(isPlayer, allyLane)?.Cost ?? 0;
                EffectActions.TrashUnit(isPlayer, allyLane);
                EffectActions.SelectUnit(isPlayer, !isPlayer, (l2, u2) => u2.Cost <= cap,
                    $"트래시할 {cap}코스트 이하 상대 유닛을 선택하세요",
                    enemyLane => EffectActions.TrashUnit(!isPlayer, enemyLane));
            },
            onCancel: () => { });
    }

    // BT04-075: DZ 소속 카드가 N장 이상이면 조우 유닛을 상대의 대미지 존에 놓는다
    private void BT04_EntryTrashEncounterToDamageZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var p = et.Split(':');
        string faction = p.ElementAtOrDefault(1) ?? "";
        int thresh = p.Length > 2 && int.TryParse(p[2], out int t) ? t : 8;
        if (GetDamageZoneFactionCount(isPlayer, faction) < thresh) return;
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc == null) return;
        FieldManager.Instance.RemoveUnit(!isPlayer, lane);
        GetOwner(!isPlayer).RemoveFromTrash(enc);
        PlaceToDamageZone(!isPlayer, enc);
    }

    // BT04-070: 아군 1장 트래시. 다른 아군을 트래시했다면 드로우1 + 상대 턴까지 파워+N
    private void BT04_EntryTrashAllyDrawPower(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            "트래시할 자신 유닛을 선택하세요",
            picked =>
            {
                bool other = picked != lane;
                EffectActions.TrashUnit(isPlayer, picked);
                if (other)
                {
                    EffectActions.Draw(isPlayer, 1);
                    EffectActions.Buff(isPlayer, lane, value, 0, BoostUntil.EndOfOpponentTurn);
                }
            });
    }

    // BT04-049: 아군 1장에 「턴 끝 트래시」 부여 + 이번 턴 히트+N
    private void BT04_EntryGrantEndTurnTrashHitBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            "「턴 끝 트래시」+히트를 부여할 자신 유닛을 선택하세요",
            picked =>
            {
                _endTurnTrashGrants.Add(Key(isPlayer, picked));
                AddTurnHitBoost(isPlayer, picked, value > 0 ? value : 1);
            });
    }

    // BT04-080: DZ가 N장 이상이면 그중 1장 골라 트래시
    private void BT04_DamageZoneTrashIfThreshold(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        if (GetDamageZoneCount(isPlayer) < value || owner.DamageZone.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "트래시할 대미지 존 카드를 선택하세요", new List<CardData>(owner.DamageZone),
            picked =>
            {
                owner.RemoveFromDamageZone(picked);
                owner.AddToTrash(picked);
                DamageZoneView.NotifyDamageChanged();
                HUDView.NotifyStatusChanged();
            },
            aiPick: list => list.OrderBy(c => c.Cost).First());
    }

    // BT04-081: DZ 소속 카드 N장 이상이면 M코스트 이하 상대 유닛 1장 트래시 ("...:faction:N:M")
    private void BT04_TrashEnemyByDamageZoneFaction(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var p = et.Split(':');
        string faction = p.ElementAtOrDefault(1) ?? "";
        int thresh = p.Length > 2 && int.TryParse(p[2], out int t) ? t : 5;
        int costCap = p.Length > 3 && int.TryParse(p[3], out int m) ? m : 5;
        if (GetDamageZoneFactionCount(isPlayer, faction) < thresh) return;
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => u.Cost <= costCap,
            $"트래시할 {costCap}코스트 이하 상대 유닛을 선택하세요",
            enemyLane => EffectActions.TrashUnit(!isPlayer, enemyLane));
    }

    // BT04-079: 트래시존에 해당 소속 유닛이 N장 이상이면 상대 1대미지 ("...:faction:N")
    private void BT04_DamageIfFactionInTrash(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var p = et.Split(':');
        string faction = p.ElementAtOrDefault(1) ?? "";
        int thresh = p.Length > 2 && int.TryParse(p[2], out int t) ? t : 5;
        int cnt = GetOwner(isPlayer).TrashPile.Count(c => c.Type == CardType.Unit
            && ((c.Faction != null && c.Faction.Contains(faction)) || c.Keywords.Contains(faction)));
        if (cnt >= thresh) EffectActions.DamageOpponent(isPlayer, 1);
    }

    // BT04-035: 자신1+상대1 골라, 상대 파워를 자신 파워만큼 감소 → 고른 자신 유닛 트래시
    private void BT04_DebuffEnemyByAllyPowerTrashAlly(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            "기준이 될 자신 유닛을 선택하세요 (트래시됨)",
            allyLane =>
            {
                var ally = FieldManager.Instance.GetUnit(isPlayer, allyLane);
                if (ally == null) return;
                int pow = GetEffectivePower(isPlayer, allyLane, ally);
                EffectActions.SelectUnit(isPlayer, !isPlayer, (l2, u2) => true,
                    $"파워-{pow}를 줄 상대 유닛을 선택하세요",
                    enemyLane =>
                    {
                        AddTurnBoost(!isPlayer, enemyLane, -pow);
                        EffectActions.TrashUnit(isPlayer, allyLane);
                    });
            });
    }

    // BT04-082: 자신 유닛 존이 전부 비었으면 트래시존에서 해당 소속·카드명 다른 유닛 N장 부활(사이즈 무시, 턴 끝 트래시)
    private void BT04_MassReviveFactionIgnoreSize(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var p = et.Split(':');
        string faction = p.ElementAtOrDefault(1) ?? "";
        int n = p.Length > 2 && int.TryParse(p[2], out int x) ? x : 3;
        bool allEmpty = Enumerable.Range(0, 3).All(i => FieldManager.Instance.GetUnit(isPlayer, i) == null);
        if (!allEmpty) return;
        var owner = GetOwner(isPlayer);
        var candidates = owner.TrashPile
            .Where(c => c.Type == CardType.Unit && ((c.Faction != null && c.Faction.Contains(faction)) || c.Keywords.Contains(faction)))
            .GroupBy(c => c.CardName).Select(g => g.First())   // 카드명 다른 것
            .OrderByDescending(c => c.Cost).Take(n).ToList();
        int laneIdx = 0;
        foreach (var c in candidates)
        {
            while (laneIdx < 3 && FieldManager.Instance.GetUnit(isPlayer, laneIdx) != null) laneIdx++;
            if (laneIdx >= 3) break;
            owner.RemoveFromTrash(c);
            FieldManager.Instance.ForcePlace(isPlayer, laneIdx, c);
            OnUnitPlaced(isPlayer, laneIdx, c);
            _endTurnTrashGrants.Add(Key(isPlayer, laneIdx));
            laneIdx++;
        }
    }

    // BT04-076: DZ에서 최대 N장 트래시 → 같은 수만큼 트래시존에서 DZ로. DZ 소속≥M이면 드로우1 ("...:N:faction:M")
    private void BT04_DamageZoneSwapDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var p = et.Split(':');
        int maxN = value;
        string faction = p.ElementAtOrDefault(2) ?? "";
        int thresh = p.Length > 3 && int.TryParse(p[3], out int t) ? t : 5;
        var owner = GetOwner(isPlayer);
        int swapped = 0;
        // 간소화: DZ에서 저코스트부터 maxN장 트래시, 같은 수만큼 트래시존 고코스트부터 DZ로
        var dzTrash = owner.DamageZone.OrderBy(c => c.Cost).Take(maxN).ToList();
        foreach (var c in dzTrash) { owner.RemoveFromDamageZone(c); owner.AddToTrash(c); swapped++; }
        var back = owner.TrashPile.Where(c => !dzTrash.Contains(c)).OrderByDescending(c => c.Cost).Take(swapped).ToList();
        foreach (var c in back) { owner.RemoveFromTrash(c); owner.AddToDamageZone(c); }
        DamageZoneView.NotifyDamageChanged();
        HUDView.NotifyStatusChanged();
        if (GetDamageZoneFactionCount(isPlayer, faction) >= thresh) EffectActions.Draw(isPlayer, 1);
    }

    // BT04-031: DZ가 N 이상이면 드로우1 + 이번 턴 DZ 참조 수 +M ("...:N:M")
    private void BT04_DrawIfDamageZoneBonusCount(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var p = et.Split(':');
        int thresh = value;
        int bonus = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 0;
        if (GetDamageZoneCount(isPlayer) >= thresh) EffectActions.Draw(isPlayer, 1);
        _damageZoneTurnBonus[Idx(isPlayer)] += bonus;
    }

    // BT04-071: (효과 트래시였다면) 패1 트래시 가능 → 상대 1대미지  ※효과트래시 판정은 근사 생략
    private void BT04_ExitEffectTrashDiscardDamage(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.DiscardHandOptional(isPlayer, 1, d =>
        {
            if (d >= 1) EffectActions.DamageOpponent(isPlayer, value > 0 ? value : 1);
        });
    }

    // BT04-054: 트래시존에서 해당 소속 카드 1장 패로
    private void BT04_ExitRecoverFactionFromTrash(bool isPlayer, int lane, CardData card, int value, string et)
    {
        string faction = et.Split(':').ElementAtOrDefault(1) ?? "";
        EffectActions.RecoverFromTrash(isPlayer,
            c => (c.Faction != null && c.Faction.Contains(faction)) || c.Keywords.Contains(faction),
            $"트래시에서 회수할 《{faction}》 카드를 선택하세요");
    }

    // BT04-049: 아군 1장 상대 턴 끝까지 파워+N
    private void BT04_ExitBuffAllyUntilOpponentTurn(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            $"파워+{value}를 줄 자신 유닛을 선택하세요",
            picked => EffectActions.Buff(isPlayer, picked, value, 0, BoostUntil.EndOfOpponentTurn));
    }

    // BT04-046: 상대 1장 자신 턴 끝까지 파워-N, 자신의 턴이면 추가 파워-M ("...:N:M")
    private void BT04_ExitDebuffEnemyTurnConditional(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var p = et.Split(':');
        int baseDebuff = value;
        int extra = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 0;
        bool ownTurn = TurnManager.Instance != null && TurnManager.Instance.IsPlayerTurn == isPlayer;
        int total = baseDebuff + (ownTurn ? extra : 0);
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
            $"파워-{total}를 줄 상대 유닛을 선택하세요",
            picked => EffectActions.Buff(!isPlayer, picked, -total, 0, BoostUntil.EndOfTurn));
    }

    // BT04-044: 덱 맨 위 N장 트래시. 그중 엑시트 유닛이 1장 이상이면 드로우1
    private void BT04_ExitMillDrawIfExitUnit(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var milled = EffectActions.Mill(isPlayer, value);
        if (milled.Any(c => c.Type == CardType.Unit && c.Keywords.Contains("엑시트")))
            EffectActions.Draw(isPlayer, 1);
    }

    // BT04-073 트리거: 이 카드 트래시, 코스트 ≤ DZ 소속 수인 상대 유닛 1장 트래시
    private void BT04_TriggerTrashSelfTrashEnemyByDamageZoneCost(bool isPlayer, CardData card, int value, string et)
    {
        string faction = et.Split(':').ElementAtOrDefault(1) ?? "";
        EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
        int cap = GetDamageZoneFactionCount(isPlayer, faction);
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => u.Cost <= cap,
            $"트래시할 {cap}코스트 이하 상대 유닛을 선택하세요",
            enemyLane => EffectActions.TrashUnit(!isPlayer, enemyLane));
    }

    // ═══════════════════ 배치 3 ═══════════════════

    // BT04-008: 상대 DZ ≤ N이면 덱top1 DZ에 놓고 상대1대미지. 아니면 이번 턴 파워+M ("...:N:M")
    private void BT04_EntryMillDamageZoneDamageOrPowerBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var p = et.Split(':');
        int thresh = value;
        int boost = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 3000;
        if (GetOwner(!isPlayer).DamageZone.Count <= thresh)
        {
            var owner = GetOwner(isPlayer);
            if (owner.DrawPile.Count > 0)
            {
                var top = owner.DrawPile[0]; owner.RemoveFromDeckAt(0);
                PlaceToDamageZone(isPlayer, top);
            }
            EffectActions.DamageOpponent(isPlayer, 1);
        }
        else AddTurnBoost(isPlayer, lane, boost);
    }

    // BT04-028: 양측 DZ 합 N 이상이면 이번 턴 상대 엑시트 발동 잠금
    private void BT04_EntryLockExitByTotalDamageZone(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (GetTotalDamageZoneCount(isPlayer) >= value)
        {
            _exitDisabledThisTurn[Idx(!isPlayer)] = true;
            Debug.Log($"[BT04] {card.CardName} → 상대 엑시트 잠금 (이번 턴)");
        }
    }

    // BT04-029: 양측 DZ 합 N 이상이면 이번 턴 다른 아군 전체 파워+M. P 이상이면 추가로 조우 트래시 ("...:N:M:P")
    private void BT04_EntryConditionalAlliesBoostEncounterTrash(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var p = et.Split(':');
        int thresh = value;
        int boost = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 6000;
        int thresh2 = p.Length > 3 && int.TryParse(p[3], out int q) ? q : int.MaxValue;
        int total = GetTotalDamageZoneCount(isPlayer);
        if (total < thresh) return;
        for (int i = 0; i < 3; i++)
            if (i != lane && FieldManager.Instance.GetUnit(isPlayer, i) != null)
                AddTurnBoost(isPlayer, i, boost);
        if (total >= thresh2)
        {
            var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
            if (enc != null) EffectActions.TrashUnit(!isPlayer, lane);
        }
    }

    // BT04-072: DZ 소속 카드 N 이상이면 DZ에서 1장 트래시하고 이번 턴 그 카드 코스트만큼 파워+ (근사) ("...:faction:N")
    private void BT04_EntryDamageZoneSacrificePowerByCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var p = et.Split(':');
        string faction = p.ElementAtOrDefault(1) ?? "";
        int thresh = p.Length > 2 && int.TryParse(p[2], out int n) ? n : 5;
        var owner = GetOwner(isPlayer);
        if (GetDamageZoneFactionCount(isPlayer, faction) < thresh || owner.DamageZone.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "트래시할 대미지 존 카드를 선택하세요 (그 코스트만큼 파워+)", new List<CardData>(owner.DamageZone),
            picked =>
            {
                owner.RemoveFromDamageZone(picked); owner.AddToTrash(picked);
                DamageZoneView.NotifyDamageChanged(); HUDView.NotifyStatusChanged();
                AddTurnBoost(isPlayer, lane, picked.Cost * 1000);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // BT04-019 어태커: 상대 DZ ≤N이면 패1 DZ에 놓고 상대1대미지. ≥N+1이면 이 공격 파워+M ("...:N:M")
    private void BT04_AttackerDiscardToDamageZoneDamageOrPowerBoost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var p = et.Split(':');
        int thresh = value;
        int boost = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 4000;
        if (GetOwner(!isPlayer).DamageZone.Count <= thresh)
        {
            var owner = GetOwner(isPlayer);
            if (owner.Hand.Count > 0)
            {
                EffectActions.PickFromCards(isPlayer, "대미지 존에 놓을 패를 선택하세요", new List<CardData>(owner.Hand),
                    picked => { owner.RemoveFromHand(picked); PlaceToDamageZone(isPlayer, picked); EffectActions.DamageOpponent(isPlayer, 1); },
                    aiPick: list => list.OrderBy(c => c.Cost).First(), onCancel: () => { });
            }
        }
        else AddAttackBoost(isPlayer, lane, boost);
    }

    // BT04-062 엑시트: 패1 트래시 가능 → 트래시존 N코이하 유닛 회수 (소속 트래시 시 원하는 카드) ("...:N:faction")
    private void BT04_ExitDiscardRecoverUnitByCostOrFaction(bool isPlayer, int lane, CardData card, int value, string et)
    {
        int cap = value;
        EffectActions.DiscardHandOptional(isPlayer, 1, d =>
        {
            if (d < 1) return;
            EffectActions.RecoverFromTrash(isPlayer,
                c => c.Type == CardType.Unit && c.Cost <= cap,
                $"트래시에서 회수할 {cap}코스트 이하 유닛을 선택하세요");
        });
    }

    // BT04-066 엑시트: 덱top4 공개 → 1장 DZ, 1장 패, 나머지 트래시
    private void BT04_ExitRevealSelectDamageZoneHandTrashRest(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        int n = Mathf.Min(value, owner.DrawPile.Count);
        if (n == 0) return;
        var revealed = owner.PeekDeckTop(n);
        owner.RemoveDeckTop(n);
        EffectActions.PickFromCards(isPlayer, "대미지 존에 놓을 카드를 선택하세요", new List<CardData>(revealed),
            toDz =>
            {
                revealed.Remove(toDz); PlaceToDamageZone(isPlayer, toDz);
                if (revealed.Count == 0) return;
                EffectActions.PickFromCards(isPlayer, "패에 넣을 카드를 선택하세요", new List<CardData>(revealed),
                    toHand =>
                    {
                        revealed.Remove(toHand); owner.AddToHand(toHand);
                        foreach (var r in revealed) owner.AddToTrash(r);
                        HandView.Instance?.RefreshHand();
                    },
                    aiPick: list => list.OrderByDescending(c => c.Cost).First());
            },
            aiPick: list => list.OrderBy(c => c.Cost).First());
    }

    // BT04-045 엑시트: (효과 트래시였다면) 패1 트래시 가능 → 턴 끝에 빈 유닛 존에 이 카드 배치 (근사: 즉시 배치)
    private void BT04_ExitEffectTrashDiscardDelayedDeploy(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.DiscardHandOptional(isPlayer, 1, d =>
        {
            if (d < 1) return;
            var owner = GetOwner(isPlayer);
            for (int i = 0; i < 3; i++)
            {
                if (FieldManager.Instance.GetUnit(isPlayer, i) == null)
                {
                    owner.RemoveFromTrash(card);
                    FieldManager.Instance.ForcePlace(isPlayer, i, card);
                    OnUnitPlaced(isPlayer, i, card);
                    break;
                }
            }
        });
    }

    // BT04-033 스킬: 이 턴 다음에 배치하는 유닛 1장에 어태커 약탈[1] + 파워+N 부여
    private void BT04_BuffNextDeployPlunderPower(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        _nextDeployPower[Idx(isPlayer)]   = value;
        _nextDeployPlunder[Idx(isPlayer)] = 1;
    }

    // BT04-037 스킬: 성약 잠금 + 트래시존 N코이하 1장 패로 → 패1 트래시 (계승자 0코화는 근사 생략)
    private void BT04_RecoverCardThenDiscard(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        _covenantLocked[Idx(isPlayer)] = true;
        var owner = GetOwner(isPlayer);
        var cands = owner.TrashPile.Where(c => c.Cost <= value).ToList();
        if (cands.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, $"트래시에서 회수할 {value}코스트 이하 카드를 선택하세요", cands,
            picked =>
            {
                owner.RemoveFromTrash(picked); owner.AddToHand(picked);
                HandView.Instance?.RefreshHand();
                EffectActions.DiscardHand(isPlayer, 1);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // ═══════════════════ 배치 4: 액티브 (TriggerActiveEffect 훅) ═══════════════════

    private static readonly HashSet<string> _bt04ActiveTypes = new()
    {
        "ActiveThresholdDebuffEncounter", "ActiveThresholdMillDamageZoneDraw",
        "ActiveThresholdChoiceBuffOrSetEncounterPower", "ActiveThresholdChoiceDebuffOrPierceInfiltrate",
        "ActiveThresholdChoiceRecoverFactionOrDuelist", "ActiveThresholdChoiceDebuffOrExtraAttack",
        "ActiveThresholdChoiceTrashSkillOrPierceHit", "ActiveAttackExtraAttackIfTotalDamageZone",
        "ActiveMainAllyToDamageZonePowerHit", "ActiveMainDrawIfDamageZonePlacedThisTurn",
        "ActiveMainDrawIfAnyCardToDamageZoneThisTurn", "ActiveMainHandToDamageZoneRecoverUnit",
        "ActiveMainTrashEncounterByDamageZoneFaction", "ActiveMainBuffAllyIfAllyTrashedThisTurn",
    };

    // ═══════════════════ 배치 6 ═══════════════════

    // BT04-028: 양측 DZ 합 N 이상이면 상대 유닛 1장에 「트래시될 때 자신 DZ로」 부여
    private void BT04_EntryGrantDamageZoneOnTrash(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (GetTotalDamageZoneCount(isPlayer) < value) return;
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
            "「트래시 시 대미지 존으로」를 부여할 상대 유닛을 선택하세요",
            enemyLane => _grantDamageZoneOnTrash.Add(Key(!isPlayer, enemyLane)));
    }

    // BT04-052/067 어태커: 이 턴 끝까지 「엑시트: 호문클루스 공격 수 참조」 획득 (부여형 엑시트)
    private void BT04_AttackerGainExitGrant(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var (type, _) = Parse(et);
        _grantedExit[Key(isPlayer, lane)] = (type, 0);
        Debug.Log($"[BT04 어태커] {card.CardName} → 엑시트 부여 ({type})");
    }

    // BT04-070 엑시트: 패1 트래시 가능 → 효과 1개 선언(다음 턴 상대 잠금) — 근사: 선언 로그만
    private void BT04_ExitDiscardDeclareEffectLock(bool isPlayer, int lane, CardData card, int value, string et)
    {
        EffectActions.DiscardHandOptional(isPlayer, 1, d =>
        {
            if (d >= 1) Debug.Log($"[BT04] {card.CardName} → 효과 선언 (다음 턴 잠금은 근사 미구현)");
        });
    }

    // BT04-069 엑시트: 트래시에서 트리거 없고 카드명 다른 카드 N장 덱 맨 아래로 → 이 유닛 배치 (조건부 추가는 근사)
    private void BT04_ExitMillDeckBottomReviveSelf(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var picks = owner.TrashPile.Where(c => !c.IsTrigger && c != card)
            .GroupBy(c => c.CardName).Select(g => g.First())
            .OrderByDescending(c => c.Cost).Take(value).ToList();
        foreach (var c in picks) { owner.RemoveFromTrash(c); owner.AddToDeckBottom(c); }
        // 이 유닛을 빈 유닛 존에 배치
        for (int i = 0; i < 3; i++)
            if (FieldManager.Instance.GetUnit(isPlayer, i) == null)
            {
                owner.RemoveFromTrash(card);
                FieldManager.Instance.ForcePlace(isPlayer, i, card);
                OnUnitPlaced(isPlayer, i, card);
                break;
            }
    }

    // BT04-021/028 트리거: 자신 DZ≥N이면 이 카드 트래시. 상대 DZ≤M이면 상대 1대미지 ("...:N:M")
    private void BT04_TriggerConditionalTrashDamageByDamageZone(bool isPlayer, CardData card, int value, string et)
    {
        var p = et.Split(':');
        int selfThr = value;
        int oppMax = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 6;
        if (GetDamageZoneCount(isPlayer) >= selfThr)
            EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
        if (GetOwner(!isPlayer).DamageZone.Count <= oppMax)
            EffectActions.DamageOpponent(isPlayer, 1);
    }

    // ═══════════════════ 배치 5: 패시브 / 아이템 ═══════════════════

    // BT04 파워 패시브 (GetPassiveBonus에서 합산)
    private int GetBT04PassiveBonus(bool isPlayer, int lane, CardData card)
    {
        int bonus = 0;
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            var p = et.Split(':');
            switch (type)
            {
                case "PassivePowerIfAllyTrashedFromField": // 이 턴 필드 트래시 아군 1+이면 +N
                    if (_unitsTrashedThisTurn[Idx(isPlayer)] >= 1) bonus += value;
                    break;
                case "PassiveTurnPowerPerFactionDamageZone": // 자신 턴 동안 DZ 소속 1장마다 +M
                {
                    string faction = p.ElementAtOrDefault(1) ?? "";
                    int per = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 1000;
                    if (TurnManager.Instance != null && TurnManager.Instance.IsPlayerTurn == isPlayer)
                        bonus += GetDamageZoneFactionCount(isPlayer, faction) * per;
                    break;
                }
                case "PassivePowerPerFactionDamageZoneHit": // DZ 소속 1장마다 +M (히트는 별도)
                {
                    string faction = p.ElementAtOrDefault(1) ?? "";
                    int per = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 1000;
                    bonus += GetDamageZoneFactionCount(isPlayer, faction) * per;
                    break;
                }
            }
        }
        return bonus;
    }

    // BT04 히트 패시브 (GetEffectiveHit에서 합산)
    private int GetBT04HitBonus(bool isPlayer, int lane, CardData card)
    {
        int hit = 0;
        foreach (var et in card.EffectTypes)
        {
            var (type, _) = Parse(et);
            if (type != "PassivePowerPerFactionDamageZoneHit") continue;
            var p = et.Split(':');           // faction:per:thresh:hit
            string faction = p.ElementAtOrDefault(1) ?? "";
            int thresh = p.Length > 3 && int.TryParse(p[3], out int t) ? t : int.MaxValue;
            int hv     = p.Length > 4 && int.TryParse(p[4], out int h) ? h : 1;
            if (GetDamageZoneFactionCount(isPlayer, faction) >= thresh) hit += hv;
        }
        return hit;
    }

    // BT04 유닛 액티브 디스패처 — 처리했으면 true (TriggerActiveEffect에서 호출)
    private bool TryBT04Active(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var (type, _) = Parse(et);
        if (!_bt04ActiveTypes.Contains(type)) return false;
        int selfPower = GetEffectivePower(isPlayer, lane, card);
        var p = et.Split(':');

        switch (type)
        {
            case "ActiveThresholdDebuffEncounter": // ≥N이면 조우 -M
            {
                int thr = value, deb = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 3000;
                if (selfPower >= thr && FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
                    AddTurnBoost(!isPlayer, lane, -deb);
                break;
            }
            case "ActiveThresholdMillDamageZoneDraw": // ≥N이면 덱top1 DZ→드로우M
            {
                int thr = value, draw = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 2;
                if (selfPower >= thr)
                {
                    var owner = GetOwner(isPlayer);
                    if (owner.DrawPile.Count > 0) { var t = owner.DrawPile[0]; owner.RemoveFromDeckAt(0); PlaceToDamageZone(isPlayer, t); }
                    EffectActions.Draw(isPlayer, draw);
                }
                break;
            }
            case "ActiveThresholdChoiceBuffOrSetEncounterPower": // ≥8000: 조우 파워=1000 / ≥5000: 다른 아군 +3000
                if (selfPower >= 8000)
                {
                    var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
                    if (enc != null) AddTurnBoost(!isPlayer, lane, 1000 - GetEffectivePower(!isPlayer, lane, enc));
                }
                else if (selfPower >= 5000)
                    EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => l != lane, "파워+3000를 줄 다른 자신 유닛", pk => AddTurnBoost(isPlayer, pk, 3000));
                break;
            case "ActiveThresholdChoiceDebuffOrPierceInfiltrate": // ≥8000: 관통[1](+침투 근사) / ≥5000: 상대 -2000
                if (selfPower >= 8000)
                    _grantedPenetration[Key(isPlayer, lane)] = 1;
                else if (selfPower >= 5000)
                    EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true, "파워-2000를 줄 상대 유닛", pk => AddTurnBoost(!isPlayer, pk, -2000));
                break;
            case "ActiveThresholdChoiceRecoverFactionOrDuelist": // ≥8000: 듀얼리스트 / ≥5000: 3코이하 나탈론학원 회수
                if (selfPower >= 8000)
                    _grantedDuelistThisTurn.Add(Key(isPlayer, lane));
                else if (selfPower >= 5000)
                    EffectActions.RecoverFromTrash(isPlayer,
                        c => c.Type == CardType.Unit && c.Cost <= 3 && c.CardName != card.CardName
                             && ((c.Faction != null && c.Faction.Contains("나탈론 학원")) || c.Keywords.Contains("나탈론 학원")),
                        "트래시에서 회수할 3코스트 이하 《나탈론 학원》 유닛");
                break;
            case "ActiveThresholdChoiceDebuffOrExtraAttack": // ≥12000: 추가공격 / ≥9000: 상대 -7000
                if (selfPower >= 12000)
                    AddExtraAttack(isPlayer, lane);
                else if (selfPower >= 9000)
                    EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true, "파워-7000를 줄 상대 유닛", pk => AddTurnBoost(!isPlayer, pk, -7000));
                break;
            case "ActiveThresholdChoiceTrashSkillOrPierceHit": // ≥9000: 관통[1]+히트+1 / ≥6000: 스킬존 스킬1 트래시
                if (selfPower >= 9000)
                {
                    _grantedPenetration[Key(isPlayer, lane)] = 1;
                    AddTurnHitBoost(isPlayer, lane, 1);
                }
                else if (selfPower >= 6000)
                {
                    var sz = GetOwner(isPlayer).SkillZone;
                    if (sz.Count > 0)
                        EffectActions.PickFromCards(isPlayer, "트래시할 스킬존 스킬", new List<CardData>(sz),
                            pk => { GetOwner(isPlayer).RemoveFromSkillZone(pk); GetOwner(isPlayer).AddToTrash(pk); }, aiPick: l => l[0]);
                }
                break;
            case "ActiveAttackExtraAttackIfTotalDamageZone": // (어택) 양측 DZ≥N이면 패1 트래시 → 추가공격
                if (GetTotalDamageZoneCount(isPlayer) >= value)
                    EffectActions.DiscardHandOptional(isPlayer, 1, d => { if (d >= 1) AddExtraAttack(isPlayer, lane); });
                break;
            case "ActiveMainAllyToDamageZonePowerHit": // 다른 아군1 DZ에 → 이 유닛 파워+N,히트+M
            {
                int pw = value, ht = p.Length > 2 && int.TryParse(p[2], out int m) ? m : 1;
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => l != lane, "대미지 존에 놓을 다른 자신 유닛",
                    pk =>
                    {
                        var u = FieldManager.Instance.GetUnit(isPlayer, pk);
                        FieldManager.Instance.RemoveUnit(isPlayer, pk);
                        GetOwner(isPlayer).RemoveFromTrash(u);
                        PlaceToDamageZone(isPlayer, u);
                        EffectActions.Buff(isPlayer, lane, pw, ht, BoostUntil.EndOfTurn);
                    });
                break;
            }
            case "ActiveMainDrawIfDamageZonePlacedThisTurn":
            case "ActiveMainDrawIfAnyCardToDamageZoneThisTurn":
                if (_cardPlacedToDamageZoneThisTurn[Idx(isPlayer)]) EffectActions.Draw(isPlayer, 1);
                break;
            case "ActiveMainHandToDamageZoneRecoverUnit": // 패1 DZ → 트래시존 유닛1 패로
            {
                var owner = GetOwner(isPlayer);
                if (owner.Hand.Count == 0) break;
                EffectActions.PickFromCards(isPlayer, "대미지 존에 놓을 패", new List<CardData>(owner.Hand),
                    pk =>
                    {
                        owner.RemoveFromHand(pk); PlaceToDamageZone(isPlayer, pk);
                        EffectActions.RecoverFromTrash(isPlayer, c => c.Type == CardType.Unit, "트래시에서 회수할 유닛");
                    },
                    aiPick: l => l.OrderBy(c => c.Cost).First());
                break;
            }
            case "ActiveMainTrashEncounterByDamageZoneFaction": // 조우 코스트 < DZ소속수면 패1 트래시 → 조우 트래시
            {
                string faction = p.ElementAtOrDefault(1) ?? "";
                var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
                if (enc != null && enc.Cost < GetDamageZoneFactionCount(isPlayer, faction))
                    EffectActions.DiscardHandOptional(isPlayer, 1, d => { if (d >= 1) EffectActions.TrashUnit(!isPlayer, lane); });
                break;
            }
            case "ActiveMainBuffAllyIfAllyTrashedThisTurn": // 이 턴 필드 트래시 아군 1+이면 아군1 +N
                if (_unitsTrashedThisTurn[Idx(isPlayer)] >= 1)
                    EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, $"파워+{value}를 줄 자신 유닛", pk => AddTurnBoost(isPlayer, pk, value));
                break;
        }
        return true;
    }
}
