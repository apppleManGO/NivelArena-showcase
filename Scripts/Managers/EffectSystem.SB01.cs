// Assets/Scripts/Managers/EffectSystem.SB01.cs
// ─────────────────────────────────────────────────────────────────────────
// SB01(니케 콜라보) effectType 구현 — 격리 파일.
//  리더 없음(leader: null), 25종 전부 신규 타입(기존 재사용: AttackerDuelist/LevelLinkHitBoost/
//  GuardianWall/DefenderPowerBoost/ArmedPowerBoost — 이미 다른 곳에서 직접 읽는 키워드형이라 핸들러 불필요).
//  테마: 어태커(필드 어태커 수 조건) + 디펜더(방어 확정 시 트리거/오라) + 암드(장착 아이템 수 조건).
//  핸들러는 기존 레지스트리에 InitSB01Handlers()로 등록 (Awake 호출).
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class EffectSystem : MonoBehaviour
{
    // ── 상태 (전부 "이 턴이 끝날 때까지") ──
    private bool[] _sb01AmplifyActiveDamage = new bool[2];              // SB01-013: 액티브 대미지 2배
    private bool[] _sb01DrawPunish = new bool[2];                       // SB01-004: 효과 드로우 시 1대미지 (대상 인덱스)
    private Dictionary<string, (bool caster, CardData skill)> _sb01DamageZoneOnTrashGrant = new(); // SB01-005, Key(대상=상대,lane)
    private HashSet<string> _sb01PainEaterLink = new();                 // SB01-014, Key(isPlayer,lane)

    // 레인 귀속 상태 정리/이동 훅 (ForEachLaneState에서 호출 — Boosts.cs 주석 참고)
    // ※ _sb01DamageZoneOnTrashGrant는 엑시트 타이밍(SB01_OnUnitTrashedHook)에 읽히므로
    //   ForEachExitGrant 쪽에 등록돼 있음
    private void ForEachLaneStateSB01(ILaneStateOp op)
    {
        op.Apply(_sb01PainEaterLink);
    }

    private void InitSB01Handlers()
    {
        // 엔트리
        _entryHandlers["EntryTrashSkillZoneDebuffPerCost"] = SB01_EntryTrashSkillZoneDebuffPerCost;
        _entryHandlers["EntryAmplifyDamageSwapDeploy"] = SB01_EntryAmplifyDamageSwapDeploy;
        _entryHandlers["EntryDiscardBounceEncounter"] = SB01_EntryDiscardBounceEncounter;
        _entryHandlers["EntryEquipItemsFromTrashHand"] = SB01_EntryEquipItemsFromTrashHand;
        _entryHandlers["EntryDiscardDrawBonusItems"] = SB01_EntryDiscardDrawBonusItems;

        // 엑시트
        _exitHandlers["ExitRevealDeployZeroCost"] = SB01_ExitRevealDeployZeroCost;
        _exitHandlers["ExitDrawPerEffectTrashed"] = SB01_ExitDrawPerEffectTrashed;

        // 어태커
        _attackerHandlers["AttackerPenetrationIfAttackers"] = SB01_AttackerPenetrationIfAttackers;
        _attackerHandlers["AttackerPowerPerDamageZoneDischargeDamage"] = SB01_AttackerPowerPerDamageZoneDischargeDamage;

        // 스킬
        _skillHandlers["DrawPerAttackerPunishDraw"] = SB01_DrawPerAttackerPunishDraw;
        _skillHandlers["DrawGrantDamageZoneOnTrash"] = SB01_DrawGrantDamageZoneOnTrash;
        _skillHandlers["DeployFromTrashLinkSkill"] = SB01_DeployFromTrashLinkSkill;
        _skillHandlers["TrashAllyReviveSameName"] = SB01_TrashAllyReviveSameName;
        _skillHandlers["DiscardItemsDraw"] = SB01_DiscardItemsDraw;
    }

    // ═══════════════════ 액티브 (TriggerActiveEffect에서 호출) ═══════════════════

    private static readonly HashSet<string> _sb01ActiveTypes = new()
    {
        "ActiveDiscardBuffAttackersPerCost", "ActiveAttackSizeGrantBreakthrough",
        "ActiveSelfTrashDamage", "ArmedActiveRecoverByItemCount",
    };

    private bool TrySB01Active(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var (type, _) = Parse(et);
        if (!_sb01ActiveTypes.Contains(type)) return false;

        switch (type)
        {
            // SB01-002: 패1 트래시 → 코스트≤트래시코스트 & 어태커 키워드 가진 아군 전체 이번턴 파워+(1000×트래시코스트), 히트+1
            case "ActiveDiscardBuffAttackersPerCost":
            {
                var owner = GetOwner(isPlayer);
                if (owner.Hand.Count == 0) break;
                EffectActions.PickFromCards(isPlayer, "트래시할 패를 선택하세요", new List<CardData>(owner.Hand),
                    picked =>
                    {
                        owner.RemoveFromHand(picked);
                        owner.AddToTrash(picked);
                        HandView.Instance?.RefreshHand();
                        int boost = picked.Cost * value;
                        for (int i = 0; i < 3; i++)
                        {
                            var u = FieldManager.Instance.GetUnit(isPlayer, i);
                            if (u != null && u.Cost <= picked.Cost && u.Keywords != null && u.Keywords.Contains("어태커"))
                                EffectActions.Buff(isPlayer, i, boost, 1, BoostUntil.EndOfTurn);
                        }
                    },
                    aiPick: list => list.OrderBy(c => c.Cost).First());
                break;
            }

            // SB01-006: 액티브:어택 — 사이즈가 필드 코스트합보다 N 이상 높으면 패1 트래시 → 3코 이하 아군 1장에 이번턴 어태커 돌파 부여
            case "ActiveAttackSizeGrantBreakthrough":
            {
                int diff = GetOwner(isPlayer).Size - FieldManager.Instance.GetFieldCostSum(isPlayer);
                if (diff < value) break;
                EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
                {
                    if (discarded < 1) return;
                    EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => u.Cost <= value,
                        "돌파를 부여할 3코스트 이하 아군을 선택하세요",
                        picked => _breakthroughGrants.Add(Key(isPlayer, picked)));
                });
                break;
            }

            // SB01-012: 액티브:메인 — 이 턴 아군 트래시 1장 이상이면 이 유닛 트래시 → 상대에게 1대미지(증폭 가능)
            case "ActiveSelfTrashDamage":
                if (_unitsTrashedThisTurn[Idx(isPlayer)] >= 1)
                {
                    EffectActions.TrashUnit(isPlayer, lane);
                    SB01_ActiveDamage(isPlayer, value);
                }
                break;

            // SB01-024: 암드 액티브:메인 — 장착 중이면 트래시에서 코스트≤장착수인 카드 1장 패로
            case "ArmedActiveRecoverByItemCount":
            {
                int itemCount = FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count;
                if (itemCount <= 0) break;
                var owner = GetOwner(isPlayer);
                var cands = owner.TrashPile.Where(c => c.Cost <= itemCount).ToList();
                if (cands.Count == 0) break;
                EffectActions.PickFromCards(isPlayer, $"{itemCount}코스트 이하 카드를 패로 가져오세요", cands,
                    picked =>
                    {
                        owner.RemoveFromTrash(picked);
                        owner.AddToHand(picked);
                        HandView.Instance?.RefreshHand();
                    },
                    aiPick: list => list.OrderByDescending(c => c.Cost).First());
                break;
            }
        }
        return true;
    }

    // SB01-013 EntryAmplifyDamageSwapDeploy로 증폭된 상태면 액티브 대미지를 2배로 적용 (이 세트 내부 범위 근사)
    private void SB01_ActiveDamage(bool isPlayer, int amount)
        => EffectActions.DamageOpponent(isPlayer, _sb01AmplifyActiveDamage[Idx(isPlayer)] ? amount * 2 : amount);

    // ═══════════════════ 엔트리 ═══════════════════

    // SB01-001: 스킬존 스킬 1장 골라 트래시 가능 → 상대 유닛 1장 골라 이번턴 트래시코스트×N만큼 파워 감소
    private void SB01_EntryTrashSkillZoneDebuffPerCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        if (owner.SkillZone.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "트래시할 스킬존 카드를 선택하세요 (선택 안 해도 됨)", new List<CardData>(owner.SkillZone),
            pickedSkill =>
            {
                owner.RemoveFromSkillZone(pickedSkill);
                owner.AddToTrash(pickedSkill);
                EffectActions.RefreshUI();
                EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
                    "파워를 감소시킬 상대 유닛을 선택하세요",
                    picked => AddTurnBoost(!isPlayer, picked, -pickedSkill.Cost * value));
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // SB01-013: 이번턴 자신의 액티브 대미지 2배. 이 유닛을 트래시 가능 → 패에서 N코스트 유닛 1장 골라 같은 존에 배치
    private void SB01_EntryAmplifyDamageSwapDeploy(bool isPlayer, int lane, CardData card, int value, string et)
    {
        _sb01AmplifyActiveDamage[Idx(isPlayer)] = true;
        var owner = GetOwner(isPlayer);
        var cands = owner.Hand.Where(c => c.Type == CardType.Unit && c.Cost == value).ToList();
        if (cands.Count == 0) return; // 배치할 카드가 없으면 트래시하지 않음(근사)
        FieldManager.Instance.RemoveUnit(isPlayer, lane); // 자체 엑시트 미발동(교체 배치 목적)
        owner.AddToTrash(card);
        EffectActions.PickFromCards(isPlayer, $"{value}코스트 유닛을 골라 배치하세요", cands,
            picked =>
            {
                owner.RemoveFromHand(picked);
                FieldManager.Instance.ForcePlace(isPlayer, lane, picked);
                OnUnitPlaced(isPlayer, lane, picked);
                HandView.Instance?.RefreshHand();
            },
            aiPick: list => list[0]);
    }

    // SB01-018: 조우 유닛이 N코스트 이상이면 패1 트래시 가능 → 조우 유닛+장착 아이템 모두 패로 반환
    private void SB01_EntryDiscardBounceEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc == null || enc.Cost < value) return;
        EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
        {
            if (discarded >= 1) EffectActions.BounceUnit(!isPlayer, lane);
        });
    }

    // SB01-021: 필드 자신 유닛 수만큼, 트래시+패의 아이템을 골라 필드 자신 유닛에 1장씩 장착
    private void SB01_EntryEquipItemsFromTrashHand(bool isPlayer, int lane, CardData card, int value, string et)
    {
        int fieldUnitCount = 0;
        for (int i = 0; i < 3; i++) if (FieldManager.Instance.GetUnit(isPlayer, i) != null) fieldUnitCount++;
        SB01_EquipItemsStep(isPlayer, fieldUnitCount);
    }

    private void SB01_EquipItemsStep(bool isPlayer, int remaining)
    {
        if (remaining <= 0) return;
        var owner = GetOwner(isPlayer);
        var pool = owner.TrashPile.Where(c => c.Type == CardType.Item)
            .Concat(owner.Hand.Where(c => c.Type == CardType.Item)).ToList();
        if (pool.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, $"장착할 아이템을 선택하세요 (선택 안 해도 됨, {remaining}회 남음)", pool,
            item =>
            {
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "장착할 유닛을 선택하세요",
                    targetLane =>
                    {
                        if (!FieldManager.Instance.EquipItem(isPlayer, targetLane, item)) return;
                        if (owner.Hand.Contains(item)) owner.RemoveFromHand(item); else owner.RemoveFromTrash(item);
                        HandView.Instance?.RefreshHand();
                        SB01_EquipItemsStep(isPlayer, remaining - 1);
                    });
            },
            aiPick: list => list[0]);
    }

    // SB01-023: 패 2장까지 트래시 → 그 수만큼 드로우, 아이템 2장 이상 트래시했다면 추가 드로우1
    private void SB01_EntryDiscardDrawBonusItems(bool isPlayer, int lane, CardData card, int value, string et)
        => SB01_DiscardUpToStep(isPlayer, value, 0, 0);

    private void SB01_DiscardUpToStep(bool isPlayer, int remaining, int discardedCount, int itemCount)
    {
        var owner = GetOwner(isPlayer);
        if (remaining <= 0 || owner.Hand.Count == 0)
        {
            if (discardedCount > 0) EffectActions.Draw(isPlayer, discardedCount);
            if (itemCount >= 2) EffectActions.Draw(isPlayer, 1);
            return;
        }
        EffectActions.PickFromCards(isPlayer, $"트래시할 패를 선택하세요 (선택 안 해도 됨, {remaining}장 남음)", new List<CardData>(owner.Hand),
            picked =>
            {
                owner.RemoveFromHand(picked);
                owner.AddToTrash(picked);
                HandView.Instance?.RefreshHand();
                SB01_DiscardUpToStep(isPlayer, remaining - 1, discardedCount + 1, itemCount + (picked.Type == CardType.Item ? 1 : 0));
            },
            onCancel: () =>
            {
                if (discardedCount > 0) EffectActions.Draw(isPlayer, discardedCount);
                if (itemCount >= 2) EffectActions.Draw(isPlayer, 1);
            },
            aiPick: list => list.OrderBy(c => c.Cost).First());
    }

    // ═══════════════════ 엑시트 ═══════════════════

    // SB01-007: 덱 위 N장 공개 가능. 패1 트래시하면 그중 유닛 1장 골라 빈 존에 배치(0코스트화). 나머지 트래시.
    private void SB01_ExitRevealDeployZeroCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        int take = Mathf.Min(value, owner.DrawPile.Count);
        if (take == 0) return;
        var revealed = owner.PeekDeckTop(take);
        owner.RemoveDeckTop(take);
        var unitReveal = revealed.Where(c => c.Type == CardType.Unit).ToList();
        bool hasEmpty = Enumerable.Range(0, 3).Any(i => FieldManager.Instance.GetUnit(isPlayer, i) == null);

        if (unitReveal.Count == 0 || !hasEmpty)
        {
            owner.AddToTrash(revealed);
            EffectActions.RefreshUI();
            return;
        }

        EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
        {
            if (discarded >= 1)
            {
                EffectActions.PickFromCards(isPlayer, "배치할 유닛을 선택하세요", unitReveal,
                    picked =>
                    {
                        revealed.Remove(picked);
                        int emptyLane = -1;
                        for (int i = 0; i < 3; i++) if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { emptyLane = i; break; }
                        if (emptyLane >= 0)
                        {
                            FieldManager.Instance.ForcePlace(isPlayer, emptyLane, picked);
                            _zeroCostGrants.Add(Key(isPlayer, emptyLane));
                            OnUnitPlaced(isPlayer, emptyLane, picked);
                        }
                        owner.AddToTrash(revealed);
                        EffectActions.RefreshUI();
                    },
                    aiPick: list => list.OrderByDescending(c => c.Cost).First());
            }
            else
            {
                owner.AddToTrash(revealed);
                EffectActions.RefreshUI();
            }
        });
    }

    // SB01-011: 자신 턴이면 이 턴 효과로 트래시된 자신 유닛 수만큼 드로우. 3장 이상 드로우했다면 상대에게 1대미지.
    private void SB01_ExitDrawPerEffectTrashed(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (TurnManager.Instance == null || TurnManager.Instance.IsPlayerTurn != isPlayer) return;
        int count = _effectTrashedThisTurn[Idx(isPlayer)]; // 근사: 전투 포함 전체 카운트(기존 컨벤션)
        if (count <= 0) return;
        EffectActions.Draw(isPlayer, count);
        if (count >= 3) EffectActions.DamageOpponent(isPlayer, 1);
    }

    // ═══════════════════ 어태커 ═══════════════════

    // SB01-002: 필드에 어태커 가진 자신 유닛이 threshold 이상이면 이 공격 끝까지 관통[amount] 획득
    // 형식: "AttackerPenetrationIfAttackers:threshold:amount"
    private void SB01_AttackerPenetrationIfAttackers(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var parts = et.Split(':');
        int threshold = int.Parse(parts[1]);
        int amount = parts.Length > 2 ? int.Parse(parts[2]) : 1;
        int atkCount = 0;
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.Keywords != null && u.Keywords.Contains("어태커")) atkCount++;
        }
        if (atkCount >= threshold) _grantedPenetration[Key(isPlayer, lane)] = amount;
    }

    // SB01-003: 이 공격 끝까지 자신 대미지 존 카드 1장마다 파워+N (전투 승리 시 후속 효과는 OnAttackWon에서 처리)
    private void SB01_AttackerPowerPerDamageZoneDischargeDamage(bool isPlayer, int lane, CardData card, int value, string et)
        => AddAttackBoost(isPlayer, lane, GetOwner(isPlayer).DamageZone.Count * value);

    // SB01-003 후속: 전투로 상대 유닛 트래시 시 그 유닛의 히트 이하로 패를 골라 트래시 가능 → 1장마다 상대에게 1대미지
    private void SB01_DischargeDamageStep(bool isPlayer, int remaining, int discardedCount)
    {
        var owner = GetOwner(isPlayer);
        if (remaining <= 0 || owner.Hand.Count == 0)
        {
            if (discardedCount > 0) EffectActions.DamageOpponent(isPlayer, discardedCount);
            return;
        }
        EffectActions.PickFromCards(isPlayer, $"트래시할 패를 선택하세요 (선택 안 해도 됨, {remaining}장 남음)", new List<CardData>(owner.Hand),
            picked =>
            {
                owner.RemoveFromHand(picked);
                owner.AddToTrash(picked);
                HandView.Instance?.RefreshHand();
                SB01_DischargeDamageStep(isPlayer, remaining - 1, discardedCount + 1);
            },
            onCancel: () => { if (discardedCount > 0) EffectActions.DamageOpponent(isPlayer, discardedCount); },
            aiPick: list => list.OrderBy(c => c.Cost).First());
    }

    // ═══════════════════ 스킬 ═══════════════════

    // SB01-004: 필드 어태커 가진 자신 유닛 수만큼 드로우. 상대 턴 끝까지 "상대가 효과로 드로우하면 상대에게 1대미지" 부여.
    private void SB01_DrawPerAttackerPunishDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        int atkCount = 0;
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.Keywords != null && u.Keywords.Contains("어태커")) atkCount++;
        }
        if (atkCount > 0) EffectActions.Draw(isPlayer, atkCount);
        _sb01DrawPunish[Idx(!isPlayer)] = true;
    }

    // SB01-005: 드로우1. 상대 유닛 1장 골라 이번턴 "트래시되면 시전자의 트래시존의 이 카드를 시전자의 대미지존으로" 부여.
    // 필드 어태커 가진 자신 유닛 2장 이상이면 이 스킬을 트래시(항상 유리하므로 자동 실행 — 근사).
    private void SB01_DrawGrantDamageZoneOnTrash(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        EffectActions.Draw(isPlayer, 1);
        EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
            "효과를 부여할 상대 유닛을 선택하세요 (선택 안 해도 됨)",
            picked => _sb01DamageZoneOnTrashGrant[Key(!isPlayer, picked)] = (isPlayer, skill));

        int atkCount = 0;
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.Keywords != null && u.Keywords.Contains("어태커")) atkCount++;
        }
        if (atkCount >= 2)
        {
            var owner = GetOwner(isPlayer);
            if (owner.RemoveFromSkillZone(skill))
            {
                owner.AddToTrash(skill);
                EffectActions.RefreshUI();
            }
        }
    }

    // SB01-014: 트래시에서 2코 이하 유닛 1장 골라 빈 존에 사이즈 무시 배치. 그 유닛은 이번턴
    // "트래시되면 스킬존에서 〈페인 이터〉를 골라 트래시" 획득.
    private void SB01_DeployFromTrashLinkSkill(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var cands = owner.TrashPile.Where(c => c.Type == CardType.Unit && c.Cost <= value).ToList();
        if (cands.Count == 0) return;
        bool hasEmpty = Enumerable.Range(0, 3).Any(i => FieldManager.Instance.GetUnit(isPlayer, i) == null);
        if (!hasEmpty) return;
        EffectActions.PickFromCards(isPlayer, "빈 유닛 존에 배치할 유닛을 선택하세요", cands,
            picked =>
            {
                int emptyLane = -1;
                for (int i = 0; i < 3; i++) if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { emptyLane = i; break; }
                if (emptyLane < 0) return;
                owner.RemoveFromTrash(picked);
                FieldManager.Instance.ForcePlace(isPlayer, emptyLane, picked);
                OnUnitPlaced(isPlayer, emptyLane, picked);
                _sb01PainEaterLink.Add(Key(isPlayer, emptyLane));
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // SB01-015: 필드 5코 이하 자신 유닛 1장 골라 트래시 → 원래 코스트+카드명이 같은 유닛을 트래시에서 골라 그 존에 배치
    private void SB01_TrashAllyReviveSameName(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => u.Cost <= value, "트래시할 아군 유닛을 선택하세요",
            allyLane =>
            {
                var trashedUnit = FieldManager.Instance.GetUnit(isPlayer, allyLane);
                if (trashedUnit == null) return;
                string name = trashedUnit.CardName;
                int cost = trashedUnit.Cost;
                EffectActions.TrashUnit(isPlayer, allyLane);
                var owner = GetOwner(isPlayer);
                var revive = owner.TrashPile.FirstOrDefault(c => c.Type == CardType.Unit && c.CardName == name && c.Cost == cost);
                if (revive == null) return;
                owner.RemoveFromTrash(revive);
                FieldManager.Instance.ForcePlace(isPlayer, allyLane, revive);
                OnUnitPlaced(isPlayer, allyLane, revive);
            });

    // SB01-025: 패의 아이템 카드를 1장 이상 골라 트래시 → 그 수만큼 드로우
    private void SB01_DiscardItemsDraw(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => SB01_DiscardItemsStep(isPlayer, 0);

    private void SB01_DiscardItemsStep(bool isPlayer, int discardedCount)
    {
        var owner = GetOwner(isPlayer);
        var itemsInHand = owner.Hand.Where(c => c.Type == CardType.Item).ToList();
        if (itemsInHand.Count == 0)
        {
            if (discardedCount > 0) EffectActions.Draw(isPlayer, discardedCount);
            return;
        }
        EffectActions.PickFromCards(isPlayer, "트래시할 아이템을 선택하세요 (선택 안 해도 됨)", itemsInHand,
            picked =>
            {
                owner.RemoveFromHand(picked);
                owner.AddToTrash(picked);
                HandView.Instance?.RefreshHand();
                SB01_DiscardItemsStep(isPlayer, discardedCount + 1);
            },
            onCancel: () => { if (discardedCount > 0) EffectActions.Draw(isPlayer, discardedCount); },
            aiPick: list => list[0]);
    }

    // ═══════════════════ 패시브 / 방어 / 트래시 훅 ═══════════════════

    // GetPassiveBonus에서 호출 — SB01 전용 파워 보너스
    public int GetSB01PassiveBonus(bool isPlayer, int lane, CardData card)
    {
        int bonus = 0;

        // SB01-016: 디펜더 가진 자신 유닛 1장마다(그랜터 존재 시) 필드의 모든 자신 유닛 파워+N
        int defCount = 0;
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.Keywords != null && u.Keywords.Contains("디펜더")) defCount++;
        }
        for (int i = 0; i < 3; i++)
        {
            var granter = FieldManager.Instance.GetUnit(isPlayer, i);
            if (granter == null) continue;
            foreach (var et in granter.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "PassivePowerPerDefenderAlly") bonus += defCount * value;
            }
        }

        // SB01-008: 전선구축 — 자신의 모든 유닛 존에 유닛이 있다면(자기 자신에게) 파워+N
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (type == "FrontlinePowerGrantExitRedeploy"
                && Enumerable.Range(0, 3).All(i => FieldManager.Instance.GetUnit(isPlayer, i) != null))
                bonus += value;
        }

        return bonus;
    }

    // OnDefendDeclared에서 호출 — SB01 디펜더 트리거 전부 처리
    public void TrySB01DefendDeclared(bool isPlayer, int lane, CardData defender)
    {
        foreach (var et in defender.EffectTypes)
        {
            var (type, value) = Parse(et);
            switch (type)
            {
                // SB01-016: 필드 상대 유닛 1장 골라 이번턴 공격 불가 (선택)
                case "DefenderDisableEnemyAttack":
                    EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
                        "공격 불가로 만들 상대 유닛을 선택하세요 (선택 안 해도 됨)",
                        picked => DisableAttack(!isPlayer, picked, BoostUntil.EndOfTurn));
                    break;

                // SB01-017: 패1 트래시 가능 → 이 전투에서 공격한 유닛은 다음 상대 턴 끝까지 공격 불가
                case "DefenderDiscardDisableAttacker":
                    EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
                    {
                        if (discarded < 1) return;
                        if (FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
                            DisableAttack(!isPlayer, lane, BoostUntil.EndOfOpponentTurn);
                    });
                    break;
            }
        }

        // SB01-010: 부여된 "상대가 히트 낮은 유닛으로 방어 시 그 차이만큼 패 강제 트래시" 판정
        var attackerUnit = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (attackerUnit != null && attackerUnit.EffectTypes.Contains("AttackerDefendCostByHitDiff"))
        {
            int diff = GetEffectiveHit(!isPlayer, lane, attackerUnit) - GetEffectiveHit(isPlayer, lane, defender);
            for (int i = 0; i < diff; i++) ApplyForceDiscard(isPlayer);
        }

        // SB01-019: 그랜터가 있으면 디펜더 가진 자신 유닛(자신 포함)이 방어 확정 시 상대에게 1대미지 (+그랜터 파워 조건부 드로우1)
        if (defender.Keywords != null && defender.Keywords.Contains("디펜더"))
        {
            for (int gi = 0; gi < 3; gi++)
            {
                var granter = FieldManager.Instance.GetUnit(isPlayer, gi);
                if (granter == null) continue;
                var grantEt = granter.EffectTypes.FirstOrDefault(e => e.StartsWith("PassiveGrantDefendersDamageDraw"));
                if (grantEt == null) continue;
                EffectActions.DamageOpponent(isPlayer, 1);
                if (GetEffectivePower(isPlayer, gi, granter) >= Parse(grantEt).value)
                    EffectActions.Draw(isPlayer, 1);
            }
        }
    }

    // SB01-020: 이 유닛(그랜터) 파워가 N 이상이면 효과로 트래시될 때 패1 트래시로 생존 (근사: 상대 효과 한정 아닌 전체 효과-트래시 대상)
    // EffectActions.TrashUnit에서 트래시 직전에 호출
    public bool TryEffectTrashSurvive(bool isPlayer, int lane, CardData card)
    {
        var grantEt = card.EffectTypes.FirstOrDefault(e => e.StartsWith("PassiveGrantDefendersBerserkProtect"));
        if (grantEt == null || GetEffectivePower(isPlayer, lane, card) < Parse(grantEt).value) return false;

        var owner = GetOwner(isPlayer);
        if (owner.Hand.Count == 0) return false;

        if (!isPlayer)
        {
            var discard = owner.Hand[0];
            owner.RemoveFromHandAt(0);
            owner.AddToTrash(discard);
            HandView.Instance?.RefreshHand();
            Debug.Log($"[SB01 AI 생존] {discard.CardName} 트래시 → {card.CardName} 생존");
            return true;
        }

        bool survived = false;
        EffectActions.PickFromCards(isPlayer, $"{card.CardName} 생존을 위해 트래시할 패를 선택하세요 (취소 시 유닛 트래시)", new List<CardData>(owner.Hand),
            picked =>
            {
                owner.RemoveFromHand(picked);
                owner.AddToTrash(picked);
                HandView.Instance?.RefreshHand();
                survived = true;
            },
            onCancel: () => survived = false);
        return survived;
    }

    // OnUnitTrashed에서 호출 — SB01 트래시 반응 전부 처리 (그 유닛 자체의 EffectTypes와 무관한 부여/오라형)
    public void SB01_OnUnitTrashedHook(bool isPlayer, int lane, CardData card)
    {
        // SB01-005: 부여된 「트래시되면 시전자의 트래시존의 스킬 카드를 시전자의 대미지존으로」
        {
            string key = Key(isPlayer, lane);
            if (_sb01DamageZoneOnTrashGrant.TryGetValue(key, out var grant))
            {
                _sb01DamageZoneOnTrashGrant.Remove(key);
                var casterOwner = GetOwner(grant.caster);
                if (casterOwner.RemoveFromTrash(grant.skill))
                {
                    PlaceToDamageZone(grant.caster, grant.skill);
                    Debug.Log($"[SB01] {card.CardName} 트래시 → {grant.skill.CardName} 대미지존행");
                }
            }
        }

        // SB01-014: 부여된 「트래시되면 스킬존에서 〈페인 이터〉를 골라 트래시」
        {
            string key = Key(isPlayer, lane);
            if (_sb01PainEaterLink.Contains(key))
            {
                _sb01PainEaterLink.Remove(key);
                var owner = GetOwner(isPlayer);
                var painEater = owner.SkillZone.FirstOrDefault(c => c.CardName == "페인 이터");
                if (painEater != null)
                {
                    owner.RemoveFromSkillZone(painEater);
                    owner.AddToTrash(painEater);
                }
            }
        }

        // SB01-008: 전선구축 그랜터가 있으면, 3코 이하 자신 유닛이 트래시될 때 패1 트래시 가능 → 그 존에 재배치 (근사: 트래시 원인 무관)
        if (card.Type == CardType.Unit && card.Cost <= 3 && lane >= 0)
        {
            bool hasGranter = Enumerable.Range(0, 3).Any(i =>
                FieldManager.Instance.GetUnit(isPlayer, i)?.EffectTypes.Contains("FrontlinePowerGrantExitRedeploy") == true);
            if (hasGranter)
                EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
                {
                    if (discarded < 1) return;
                    if (FieldManager.Instance.GetUnit(isPlayer, lane) != null) return; // 이미 다른 유닛이 채움
                    var owner = GetOwner(isPlayer);
                    if (!owner.RemoveFromTrash(card)) return;
                    FieldManager.Instance.ForcePlace(isPlayer, lane, card);
                    OnUnitPlaced(isPlayer, lane, card);
                });
        }

        // SB01-022: 그랜터가 있으면, 트래시된 유닛과 같은 레인의 상대(트래시된 유닛 기준 상대)측 장착 유닛이 있으면 상대는 패1장 강제 트래시
        if (lane >= 0)
        {
            bool watcherIsPlayer = !isPlayer;
            bool hasGranter = Enumerable.Range(0, 3).Any(i =>
                FieldManager.Instance.GetUnit(watcherIsPlayer, i)?.EffectTypes.Contains("PassiveGrantAllArmedForceDiscard") == true);
            bool watcherArmedInLane = FieldManager.Instance.GetUnit(watcherIsPlayer, lane) != null
                && FieldManager.Instance.GetEquippedItems(watcherIsPlayer, lane).Count > 0;
            if (hasGranter && watcherArmedInLane)
                ApplyForceDiscard(isPlayer);
        }
    }
}
