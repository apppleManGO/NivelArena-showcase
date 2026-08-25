// Assets/Scripts/Managers/EffectSystem.BT07.cs
// ─────────────────────────────────────────────────────────────────────────
// BT07 effectType 구현 — 격리 파일.
//  테마: **이동**(룰북 4.9 최초 사용) + **포지션(센터/사이드)** + **이브 진화 라인**(니벨 룰/스위치).
//  세 시스템 모두 이 세트에서 처음 등장 — 신규 인프라(FieldManager.MoveUnit/ForceUpgradeUnit,
//  이동 추적, 포지션 판정, 이브 이름 취급/스위치 디스패치) 전부 이 파일에 격리.
//  핸들러는 기존 레지스트리에 InitBT07Handlers()로 등록 (Awake 호출).
// ─────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class EffectSystem : MonoBehaviour
{
    // ── 이동 상태 ──
    private HashSet<CardData> _bt07MovedThisTurn = new();          // 이 턴 이동한 카드(레퍼런스)
    private Dictionary<CardData, int> _bt07MoveCountThisTurn = new(); // 이 턴 이동 횟수
    private HashSet<CardData> _bt07MoveReactionUsed = new();       // "자신/상대 턴마다 1번" 게이트 (턴 인스턴스당)
    private int[] _bt07TempSizeBoost = new int[2];                 // 이동/스킬로 부여된 임시 사이즈, 이번 턴
    private Dictionary<string, int> _bt07DeployBlockLowCost = new(); // Key(차단당하는쪽,lane)→코스트 상한(이하 차단), 상대 턴 끝까지

    // ── 이브 상태 ──
    private bool[] _bt07EveDeployBlocked = new bool[2];            // 이번 턴 〈이브〉 배치 불가
    private bool[] _bt07EveDeployedThisTurn = new bool[2];         // 이번 턴 〈이브〉를 배치했는가

    private void InitBT07Handlers()
    {
        // 엔트리
        _entryHandlers["EntryMove"] = BT07_EntryMove;
        _entryHandlers["EntryCenterLevelUp"] = BT07_EntryCenterLevelUp;
        _entryHandlers["EntryRevealTrashSelect"] = BT07_EntryRevealTrashSelect;
        _entryHandlers["EntryDrawIfEncounterCost"] = BT07_EntryDrawIfEncounterCost;
        _entryHandlers["EntryMoveToEveBoost"] = BT07_EntryMoveToEveBoost;
        _entryHandlers["EntryRecoverEveIfEncounter"] = BT07_EntryRecoverEveIfEncounter;
        _entryHandlers["EntrySideMillRecoverEve"] = BT07_EntrySideMillRecoverEve;
        _entryHandlers["EntrySideDrawDiscard"] = BT07_EntrySideDrawDiscard;
        _entryHandlers["EntrySideMillDrawIfItem"] = BT07_EntrySideMillDrawIfItem;
        _entryHandlers["EntrySidePowerCopyEve"] = BT07_EntrySidePowerCopyEve;
        _entryHandlers["EntrySideGrantAllPenetration"] = BT07_EntrySideGrantAllPenetration;
        _entryHandlers["EntryBlockEveDeploy"] = BT07_EntryBlockEveDeploy;
        _entryHandlers["EntryEveUpgradeExtraAttack"] = BT07_EntryEveUpgradeExtraAttack;

        // 엑시트
        _exitHandlers["ExitEffectTrashedDeploy"] = BT07_ExitEffectTrashedDeploy;
        _exitHandlers["ExitRevealSearchUnit"] = BT07_ExitRevealSearchUnit;
        _exitHandlers["ExitMoveAllyHitBoost"] = BT07_ExitMoveAllyHitBoost;
        _exitHandlers["ExitGrantPenetration"] = BT07_ExitGrantPenetration;

        // 어태커
        _attackerHandlers["AttackerTrashEncounterIfMoved"] = BT07_AttackerTrashEncounterIfMoved;
        _attackerHandlers["AttackerTrashEncounterByMoveCount"] = BT07_AttackerTrashEncounterByMoveCount;
        _attackerHandlers["PositionCenterAttackerPenetration"] = BT07_PositionCenterAttackerPenetration;
        _attackerHandlers["PositionSideAttackerLevelUp"] = BT07_PositionSideAttackerLevelUp;
        _attackerHandlers["PositionCenterAttackerDiscardTrashEncounter"] = BT07_PositionCenterAttackerDiscardTrashEncounter;
        _attackerHandlers["PositionSideAttackerPenetrationPerEveItem"] = BT07_PositionSideAttackerPenetrationPerEveItem;
        _attackerHandlers["LevelLinkAttackerPenetration"] = BT07_LevelLinkAttackerPenetration;

        // 스킬
        _skillHandlers["MoveAlly"] = BT07_MoveAlly;
        _skillHandlers["MoveAllyLevelUp"] = BT07_MoveAllyLevelUp;
        _skillHandlers["MoveAllyPowerHitBoost"] = BT07_MoveAllyPowerHitBoost;
        _skillHandlers["DrawPerMovedUnit"] = BT07_DrawPerMovedUnit;
        _skillHandlers["TrashWeakestInMovedLane"] = BT07_TrashWeakestInMovedLane;
        _skillHandlers["SizeBoostTemp"] = BT07_SizeBoostTemp;
        _skillHandlers["TrashEnemyByPowerReturnAlly"] = BT07_TrashEnemyByPowerReturnAlly;
        _skillHandlers["RecoverEveFromTrash"] = BT07_RecoverEveFromTrash;
        _skillHandlers["DiscardRecoverEves"] = BT07_DiscardRecoverEves;
        _skillHandlers["DiscardGrantEveAttackerTrashByItems"] = BT07_DiscardGrantEveAttackerTrashByItems;
        _skillHandlers["DiscardEveDebuffEncounterByPower"] = BT07_DiscardEveDebuffEncounterByPower;
        _skillHandlers["MillPerEveItemTypesHitBoost"] = BT07_MillPerEveItemTypesHitBoost;
        _skillHandlers["UpgradeEveFromHandByLevel"] = BT07_UpgradeEveFromHandByLevel;

        // 트리거
        _triggerHandlers["TriggerTrashSelfBuffMove"] = BT07_TriggerTrashSelfBuffMove;
        _triggerHandlers["TriggerDeployOrUpgradeEve"] = BT07_TriggerDeployOrUpgradeEve;
    }

    // ═══════════════════ 이동 (룰북 4.9) ═══════════════════

    public bool HasMoved(CardData card) => card != null && _bt07MovedThisTurn.Contains(card);
    public int GetMoveCount(CardData card) => card != null ? _bt07MoveCountThisTurn.GetValueOrDefault(card) : 0;
    public int GetTempSizeBoost(bool isPlayer) => _bt07TempSizeBoost[Idx(isPlayer)];
    public void AddTempSizeBoost(bool isPlayer, int amount) => _bt07TempSizeBoost[Idx(isPlayer)] += amount;

    // FieldManager.MoveUnit에서 이동이 실제로 일어난 직후 호출 (스왑된 상대편 유닛도 각각 호출됨)
    public void NotifyUnitMoved(bool isPlayer, int fromLane, int toLane, CardData unit)
    {
        _bt07MovedThisTurn.Add(unit);
        _bt07MoveCountThisTurn[unit] = _bt07MoveCountThisTurn.GetValueOrDefault(unit) + 1;

        // 리더 패시브: PassiveMoveBoost:N (BT07-001 레이븐 — 자신 유닛이 이동하면 그 유닛 이번턴 파워+N)
        var owner = GetOwner(isPlayer);
        if (owner.Leader != null)
        {
            string let = owner.IsAwakened && !string.IsNullOrEmpty(owner.Leader.AwakenEffectType)
                ? owner.Leader.AwakenEffectType : owner.Leader.BaseEffectType;
            if (!string.IsNullOrEmpty(let))
            {
                var (ltype, lvalue) = Parse(let);
                if (ltype == "PassiveMoveBoost")
                    EffectActions.Buff(isPlayer, toLane, lvalue, 0, BoostUntil.EndOfTurn);
            }
        }

        foreach (var et in unit.EffectTypes)
        {
            var (type, value) = Parse(et);
            string reactKey = null;
            switch (type)
            {
                // BT07-005: 이동할 때마다(제한 없음) 이번턴 사이즈+N
                case "PassiveMoveSizeBoost":
                    AddTempSizeBoost(isPlayer, value);
                    break;

                // BT07-018/027: 자신의 턴 동안 이동하면 상대턴 끝까지 파워+N (제한 없음, 자신 턴 한정)
                case "PassiveMoveBoostUntilOpponentTurn":
                    if (TurnManager.Instance != null && TurnManager.Instance.IsPlayerTurn == isPlayer)
                        EffectActions.Buff(isPlayer, toLane, value, 0, BoostUntil.EndOfOpponentTurn);
                    break;

                // BT07-006: 자신의 턴마다 1번씩, 이동하면 이번턴 히트+1
                case "PassiveMoveHitBoost":
                    reactKey = $"hitb_{unit.GetInstanceID()}";
                    if (TurnManager.Instance != null && TurnManager.Instance.IsPlayerTurn == isPlayer
                        && !_bt07MoveReactionUsed.Contains(unit))
                    {
                        _bt07MoveReactionUsed.Add(unit);
                        EffectActions.Buff(isPlayer, toLane, 0, value, BoostUntil.EndOfTurn);
                    }
                    break;

                // BT07-007: 자신/상대 턴마다 1번씩, 이동하면 리더 레벨+1
                case "PassiveMoveLevelUp":
                    if (!_bt07MoveReactionUsed.Contains(unit))
                    {
                        _bt07MoveReactionUsed.Add(unit);
                        owner.LevelUpLeader();
                        HUDView.NotifyStatusChanged();
                    }
                    break;

                // BT07-016: 자신/상대 턴마다 1번씩, 이동하면 이번턴 파워+N, 히트+1
                case "PassiveMovePowerHitBoost":
                    if (!_bt07MoveReactionUsed.Contains(unit))
                    {
                        _bt07MoveReactionUsed.Add(unit);
                        EffectActions.Buff(isPlayer, toLane, value, 1, BoostUntil.EndOfTurn);
                    }
                    break;

                // BT07-023: 자신/상대 턴마다 1번씩, 이동할 때마다 패≤5면 덱 위 N장 공개해 1장 패로
                case "PassiveMoveRevealSearch":
                    if (!_bt07MoveReactionUsed.Contains(unit) && owner.Hand.Count <= 5)
                    {
                        _bt07MoveReactionUsed.Add(unit);
                        EffectActions.RevealPickToHand(isPlayer, value, _ => true, "패에 넣을 카드를 선택하세요 (선택 안 해도 됨)");
                    }
                    break;

                // BT07-015: 자신/상대 턴마다 1번씩, 이동하면 상대턴 끝까지 이 레인에 상대의 N코 이하 유닛 배치 불가
                case "PassiveMoveBlockLowCostDeploy":
                    if (!_bt07MoveReactionUsed.Contains(unit))
                    {
                        _bt07MoveReactionUsed.Add(unit);
                        _bt07DeployBlockLowCost[Key(!isPlayer, toLane)] = value;
                    }
                    break;
            }
        }
    }

    // 인접한 자신 레인으로 이동 프롬프트 (플레이어=레인 클릭 선택, AI=빈 레인 우선 휴리스틱). 취소/후보없음 시 아무 일도 없음.
    private void BT07_PromptMove(bool isPlayer, int fromLane, System.Action<int> onMoved = null)
    {
        var candidates = new List<int>();
        if (fromLane - 1 >= 0) candidates.Add(fromLane - 1);
        if (fromLane + 1 <= 2) candidates.Add(fromLane + 1);
        if (candidates.Count == 0) return;

        EffectActions.SelectLane(isPlayer, l => candidates.Contains(l), "이동할 자신의 유닛 존을 선택하세요",
            toLane =>
            {
                if (FieldManager.Instance.MoveUnit(isPlayer, fromLane, toLane))
                    onMoved?.Invoke(toLane);
            },
            mode: SkillZoneSlot.TargetMode.AllyLane,
            aiPick: list =>
            {
                var empty = list.Where(l => FieldManager.Instance.GetUnit(isPlayer, l) == null).ToList();
                return empty.Count > 0 ? empty[0] : list[0];
            });
    }

    // BT07-024: 어택 페이즈가 시작할 때(자신/상대 turn 무관) PassiveAttackPhaseMove 가진 모든 유닛에게 이동 기회
    public void OnAttackPhaseStart(bool isPlayerTurn)
    {
        for (int side = 0; side < 2; side++)
        {
            bool sideIsPlayer = side == 0;
            for (int i = 0; i < 3; i++)
            {
                var u = FieldManager.Instance.GetUnit(sideIsPlayer, i);
                if (u == null) continue;
                if (u.EffectTypes.Contains("PassiveAttackPhaseMove"))
                    BT07_PromptMove(sideIsPlayer, i);
                // SB02-028 MixPassiveAttackPhaseDraw: 자신 어택 페이즈 시작 + 믹스면 드로우1
                if (sideIsPlayer == isPlayerTurn && u.EffectTypes.Contains("MixPassiveAttackPhaseDraw")
                    && HasMixCondition(sideIsPlayer, u.Attribute))
                    EffectActions.Draw(sideIsPlayer, 1);
            }
        }
    }

    // ═══════════════════ 포지션 (센터/사이드) ═══════════════════

    public bool IsCenterLane(int lane) => lane == 1;

    // ═══════════════════ 이브 (진화 라인) ═══════════════════

    // 이 카드가 "이브"로 취급되는가 (본체 이름이 이브이거나, 니벨 룰로 이브 취급을 얻은 카드)
    public bool IsEveCard(CardData c) => c != null && (c.CardName == "이브" || c.EffectTypes.Contains("NivelRuleEve"));

    // BT07-045 이브 리더 [각성] 전용 예외:
    //   "자신은 필드에 있는 〈이브〉를 코스트가 그 유닛의 코스트 이하인 〈이브〉로 업그레이드할 수 있다"
    // 룰북 3.5.5.1의 기본 업그레이드는 "더 높은 코스트"만 허용하므로, 각성면일 때만 '이하'를 추가 허용한다.
    // ※ 기본면(PassiveEveDeployRestriction)에는 이 문장이 없어 각성 전에는 불가 — AwakenEffectType을 정확히 확인.
    public bool CanEveEqualOrLowerUpgrade(bool isPlayer, CardData existing, CardData newCard)
    {
        if (!IsEveCard(existing) || !IsEveCard(newCard)) return false;
        if (newCard.Cost > existing.Cost) return false; // '초과'는 기본 규칙 소관

        var owner = GetOwner(isPlayer);
        if (owner?.Leader == null || !owner.IsAwakened) return false;
        return !string.IsNullOrEmpty(owner.Leader.AwakenEffectType)
            && owner.Leader.AwakenEffectType.StartsWith("PassiveEveDeployRestrictionUpgrade");
    }

    // FieldManager.ForceUpgradeUnit/UpgradeUnit에서 업그레이드 성사 직후 호출
    public void OnUnitUpgraded(bool isPlayer, int lane, CardData oldCard, CardData newCard)
    {
        if (!IsEveCard(oldCard)) return; // 스위치는 "〈이브〉에서 업그레이드됐다면"만 발동
        foreach (var et in newCard.EffectTypes)
        {
            var (type, value) = Parse(et);
            switch (type)
            {
                // 대부분의 이브 유닛 공통: 트래시에서 아이템 2장까지 회수
                case "SwitchEveRecoverItems":
                    BT07_SwitchRecoverItemsStep(isPlayer, value);
                    break;
                case "SwitchEveDeploySideUnit":
                    BT07_SwitchEveDeploySideUnit(isPlayer);
                    break;
                case "SwitchEveBreakthrough":
                    _breakthroughGrants.Add(Key(isPlayer, lane));
                    break;
                case "SwitchEveDiscardTrashEncounter":
                    BT07_SwitchEveDiscardTrashEncounter(isPlayer, lane);
                    break;
                case "SwitchEveDrawByLevel":
                    EffectActions.Draw(isPlayer, 1);
                    if (owner_LeaderLevel(isPlayer) >= value) EffectActions.Draw(isPlayer, 1);
                    break;
                case "SwitchEvePowerHitBoost":
                    EffectActions.Buff(isPlayer, lane, value, 1, BoostUntil.EndOfTurn);
                    break;
                case "SwitchEveSearchItem":
                    EffectActions.SearchDeck(isPlayer, c => c.Type == CardType.Item && c.Cost <= value,
                        $"덱에서 {value}코스트 이하 아이템을 선택하세요 (선택 안 해도 됨)");
                    break;
                case "SwitchEveGainSideAura":
                    _bt07EveSideAura[Key(isPlayer, lane)] = value;
                    break;
                case "SwitchEveDamageByLevel":
                {
                    int dmg = 1 + (owner_LeaderLevel(isPlayer) >= value ? 1 : 0);
                    EffectActions.DamageOpponent(isPlayer, dmg);
                    break;
                }
                case "SwitchEveGainAttackerDebuffPerItem":
                    _bt07EveAttackerDebuffPerItem[Key(isPlayer, lane)] = value;
                    break;
                case "SwitchEveTrashEnemyRecover":
                    EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => u.Cost <= value,
                        $"트래시할 {value}코스트 이하 상대 유닛을 선택하세요 (취소 가능)",
                        picked =>
                        {
                            var trashedUnit = FieldManager.Instance.GetUnit(!isPlayer, picked);
                            int cap = trashedUnit != null ? trashedUnit.Cost : 0;
                            EffectActions.TrashUnit(!isPlayer, picked);
                            EffectActions.RecoverFromTrash(isPlayer, c => c.Cost < cap,
                                $"트래시에서 회수할 {cap}코스트 미만 카드를 선택하세요");
                        });
                    break;
                case "SwitchEveRecoverSideUnits":
                    BT07_RecoverSideUnitsByCostSumStep(isPlayer, value, 2, new List<CardData>());
                    break;
            }
        }
    }

    private int owner_LeaderLevel(bool isPlayer) => GetOwner(isPlayer).LeaderLevel;

    private void BT07_SwitchRecoverItemsStep(bool isPlayer, int remaining)
    {
        if (remaining <= 0) return;
        var owner = GetOwner(isPlayer);
        var items = owner.TrashPile.Where(c => c.Type == CardType.Item).ToList();
        if (items.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, $"트래시에서 회수할 아이템을 선택하세요 (선택 안 해도 됨, {remaining}장 남음)", items,
            picked =>
            {
                EffectActions.MoveCard(isPlayer, picked, CardZone.Trash, CardZone.Hand);
                BT07_SwitchRecoverItemsStep(isPlayer, remaining - 1);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // BT07-056: 트래시에서 리더레벨 이하 코스트 + 포지션:사이드 가진 유닛 1장 골라 빈 존에 사이즈 무시 배치
    private void BT07_SwitchEveDeploySideUnit(bool isPlayer)
    {
        var owner = GetOwner(isPlayer);
        int levelCap = owner.LeaderLevel;
        var candidates = owner.TrashPile.Where(c => c.Type == CardType.Unit && c.Cost <= levelCap
            && c.Keywords != null && c.Keywords.Contains("포지션")).ToList();
        if (candidates.Count == 0) return;
        bool hasEmpty = Enumerable.Range(0, 3).Any(i => FieldManager.Instance.GetUnit(isPlayer, i) == null);
        if (!hasEmpty) return;
        EffectActions.PickFromCards(isPlayer, "빈 유닛 존에 배치할 유닛을 선택하세요 (선택 안 해도 됨)", candidates,
            picked =>
            {
                int emptyLane = -1;
                for (int i = 0; i < 3; i++) if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { emptyLane = i; break; }
                if (emptyLane < 0) return;
                owner.RemoveFromTrash(picked);
                FieldManager.Instance.ForcePlace(isPlayer, emptyLane, picked);
                OnUnitPlaced(isPlayer, emptyLane, picked);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // BT07-064: 조우 유닛 히트만큼 패에서 〈이브〉를 골라 트래시할 수 있다 → 조우 트래시
    private void BT07_SwitchEveDiscardTrashEncounter(bool isPlayer, int lane)
    {
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc == null) return;
        int need = GetEffectiveHit(!isPlayer, lane, enc);
        var owner = GetOwner(isPlayer);
        var eveInHand = owner.Hand.Where(IsEveCard).ToList();
        if (need <= 0 || eveInHand.Count < need) return;
        BT07_DiscardEveStep(isPlayer, need, () => EffectActions.TrashUnit(!isPlayer, lane));
    }

    private void BT07_DiscardEveStep(bool isPlayer, int remaining, System.Action onDone)
    {
        if (remaining <= 0) { onDone(); return; }
        var owner = GetOwner(isPlayer);
        var eveInHand = owner.Hand.Where(IsEveCard).ToList();
        if (eveInHand.Count == 0) return; // 근사: 부족하면 중단(효과 미발동)
        EffectActions.PickFromCards(isPlayer, $"트래시할 〈이브〉를 선택하세요 ({remaining}장 남음)", eveInHand,
            picked =>
            {
                EffectActions.MoveCard(isPlayer, picked, CardZone.Hand, CardZone.Trash);
                BT07_DiscardEveStep(isPlayer, remaining - 1, onDone);
            });
    }

    // BT07-062: 코스트 합이 리더레벨 이하가 되도록 트래시에서 포지션:사이드 유닛 최대 remaining장 회수
    private void BT07_RecoverSideUnitsByCostSumStep(bool isPlayer, int dummy, int remaining, List<CardData> picked)
    {
        if (remaining <= 0) return;
        var owner = GetOwner(isPlayer);
        int used = picked.Sum(c => c.Cost);
        int budget = owner.LeaderLevel - used;
        var candidates = owner.TrashPile.Where(c => c.Type == CardType.Unit && c.Cost <= budget
            && c.Keywords != null && c.Keywords.Contains("포지션") && !picked.Contains(c)).ToList();
        if (candidates.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, $"트래시에서 회수할 카드를 선택하세요 (코스트 합 ≤ 리더레벨, {remaining}장 남음)", candidates,
            card =>
            {
                EffectActions.MoveCard(isPlayer, card, CardZone.Trash, CardZone.Hand);
                picked.Add(card);
                BT07_RecoverSideUnitsByCostSumStep(isPlayer, dummy, remaining - 1, picked);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // 스위치 부여 오라 상태 (전투/공격 계산에서 참조)
    private Dictionary<string, int> _bt07EveSideAura = new();            // Key(isPlayer,lane) → 사이드 유닛 1장당 다른 아군 파워+N
    private Dictionary<string, int> _bt07EveAttackerDebuffPerItem = new(); // Key(isPlayer,lane) → 이 유닛 어태커: 상대1장 파워-N×(1+장착아이템수), 이번턴

    // 레인 귀속 상태 정리/이동 훅 (ForEachLaneState에서 호출 — Boosts.cs 주석 참고)
    // ※ _bt07DeployBlockLowCost는 "그 존에 배치 불가"라 자리 귀속 = 제외
    // ※ 이동 추적 3종(_bt07MovedThisTurn/_bt07MoveCountThisTurn/_bt07MoveReactionUsed)은
    //   카드 인스턴스 키라 이동해도 그대로 따라감 = 손댈 필요 없음
    private void ForEachLaneStateBT07(ILaneStateOp op)
    {
        op.Apply(_bt07EveSideAura);
        op.Apply(_bt07EveAttackerDebuffPerItem);
        op.Apply(_bt07EveAttackerTrashByItems);
    }

    // ═══════════════════ 패시브 / 히트 / 아이템 파워 ═══════════════════

    public int GetBT07PassiveBonus(bool isPlayer, int lane, CardData card)
    {
        int bonus = 0;
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            switch (type)
            {
                // BT07-028/032: 리더 레벨 1마다 파워+N
                case "PassivePowerPerLevel":
                    bonus += GetOwner(isPlayer).LeaderLevel * value;
                    break;

                // BT07-060/066: 포지션:사이드 — 사이드에 있으면 필드의 〈이브〉 장착 아이템 1장마다 파워+N
                case "PassiveSidePowerPerEveItem":
                    if (IsSideLane(lane)) bonus += value * BT07_CountEveEquippedItems(isPlayer);
                    break;

                // BT07-033: 포지션:센터 — 센터에 있으면 파워+N
                case "PositionCenterPowerBoost":
                    if (IsCenterLane(lane)) bonus += value;
                    break;
            }
        }

        // BT07-063 스위치: 필드의 포지션:사이드 아군 1장마다 다른 모든 아군 파워+N
        foreach (var kv in _bt07EveSideAura)
        {
            if (!kv.Key.StartsWith(isPlayer ? "p_" : "a_")) continue;
            int sideCount = 0;
            for (int i = 0; i < 3; i++)
            {
                var u = FieldManager.Instance.GetUnit(isPlayer, i);
                if (u != null && u != card && IsSideLane(i) && u.Keywords != null && u.Keywords.Contains("포지션"))
                    sideCount++;
            }
            bonus += kv.Value * sideCount;
        }
        return bonus;
    }

    public int GetBT07HitBonus(bool isPlayer, int lane, CardData card)
    {
        int hit = 0;
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            // BT07-060/066: 포지션:사이드 — 사이드에 있고 필드의 〈이브〉 장착 아이템 3장 이상이면 히트+1
            if (type == "PassiveSidePowerPerEveItem" && IsSideLane(lane) && BT07_CountEveEquippedItems(isPlayer) >= 3)
                hit += 1;
        }
        return hit;
    }

    public int GetBT07ItemPowerBonus(bool isPlayer, int lane, CardData item)
    {
        int bonus = 0;
        foreach (var et in item.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (type == "ItemPositionCenterPowerBoost" && IsCenterLane(lane)) bonus += value;
            else if (type == "ItemPowerPerItem") bonus += value * FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count;
        }
        return bonus;
    }

    private int BT07_CountEveEquippedItems(bool isPlayer)
    {
        int total = 0;
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && IsEveCard(u)) total += FieldManager.Instance.GetEquippedItems(isPlayer, i).Count;
        }
        return total;
    }

    // ═══════════════════ 액티브 (TriggerActiveEffect에서 호출) ═══════════════════

    private static readonly HashSet<string> _bt07ActiveTypes = new()
    {
        "ActiveCenterPenetrationMoveBoost", "ActiveMoveTrashAlly", "ActiveMoveTrashAllyDraw",
        "ActiveMove", "ActiveAttackMove", "ActiveMoveTrashAllyTiered",
        "ActiveSideEveDrawDiscard", "ActiveSideReturnCardsUpgradeEve",
        "ActiveEveRevealTakeItems", "ActiveCenterSelfUpgradeEve",
    };

    private bool TryBT07Active(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var (type, _) = Parse(et);
        if (!_bt07ActiveTypes.Contains(type)) return false;

        switch (type)
        {
            // BT07-020: 포지션:센터 — 센터면 관통[1] 획득, 이 턴 이동했다면 추가로 파워+N
            case "ActiveCenterPenetrationMoveBoost":
                if (!IsCenterLane(lane)) break;
                _grantedPenetration[Key(isPlayer, lane)] = 1;
                if (HasMoved(card)) EffectActions.Buff(isPlayer, lane, value, 0, BoostUntil.EndOfTurn);
                break;

            // BT07-021: 인접 레인 아군(자신 제외) 1장 트래시 → 그 존으로 이동
            case "ActiveMoveTrashAlly":
                BT07_ActiveMoveTrashAlly(isPlayer, lane, null);
                break;

            // BT07-025: 위와 동일 + 드로우N
            case "ActiveMoveTrashAllyDraw":
                BT07_ActiveMoveTrashAlly(isPlayer, lane, () => EffectActions.Draw(isPlayer, value));
                break;

            // BT07-022/031/035: 인접 자신 유닛 존으로 이동 (메인/어택 공통)
            case "ActiveMove":
            case "ActiveAttackMove":
                BT07_PromptMove(isPlayer, lane);
                break;

            // BT07-034: 인접 레인 아군 1장 트래시 후 이동, 트래시한 유닛 히트 티어별 추가 효과
            case "ActiveMoveTrashAllyTiered":
                BT07_ActiveMoveTrashAllyTiered(isPlayer, lane);
                break;

            // BT07-048: 포지션:사이드 — 사이드+이번턴〈이브〉배치했다면 드로우N → 패N장 트래시
            case "ActiveSideEveDrawDiscard":
                if (IsSideLane(lane) && _bt07EveDeployedThisTurn[Idx(isPlayer)])
                {
                    EffectActions.Draw(isPlayer, value);
                    EffectActions.DiscardHand(isPlayer, value);
                }
                break;

            // BT07-054: 포지션:사이드 — 트래시 논트리거 카드 N장 덱 밑으로 → 트래시의〈이브〉로 필드〈이브〉업그레이드(사이즈/코스트 무시)
            case "ActiveSideReturnCardsUpgradeEve":
                if (IsSideLane(lane)) BT07_ActiveSideReturnCardsUpgradeEve(isPlayer, value);
                break;

            // BT07-058: 필드에〈이브〉가 있으면 덱 위 N장 공개, 아이템은 전부 패로, 나머지 트래시
            case "ActiveEveRevealTakeItems":
            {
                bool hasEve = Enumerable.Range(0, 3).Any(i => IsEveCard(FieldManager.Instance.GetUnit(isPlayer, i)));
                if (!hasEve) break;
                var owner = GetOwner(isPlayer);
                int take = Mathf.Min(value, owner.DrawPile.Count);
                for (int i = 0; i < take; i++)
                {
                    var c = owner.DrawPile[0];
                    owner.RemoveFromDeckAt(0);
                    if (c.Type == CardType.Item) owner.AddToHand(c);
                    else owner.AddToTrash(c);
                }
                HandView.Instance?.RefreshHand();
                EffectActions.RefreshUI();
                break;
            }

            // BT07-067: 포지션:센터 — 센터면 패의 N코 이하〈이브〉로 이 유닛을 업그레이드
            case "ActiveCenterSelfUpgradeEve":
                if (IsCenterLane(lane)) BT07_ActiveCenterSelfUpgradeEve(isPlayer, lane, value);
                break;
        }
        return true;
    }

    // 인접 레인 아군(자신 제외) 1장 골라 트래시 → 그 존으로 이동. 성공 시 onDone 호출
    private void BT07_ActiveMoveTrashAlly(bool isPlayer, int lane, System.Action onDone)
    {
        var candidates = new List<int>();
        if (lane - 1 >= 0 && FieldManager.Instance.GetUnit(isPlayer, lane - 1) != null) candidates.Add(lane - 1);
        if (lane + 1 <= 2 && FieldManager.Instance.GetUnit(isPlayer, lane + 1) != null) candidates.Add(lane + 1);
        if (candidates.Count == 0) return;
        EffectActions.SelectLane(isPlayer, l => candidates.Contains(l), "트래시하고 이동할 인접 아군 유닛 존을 선택하세요",
            targetLane =>
            {
                EffectActions.TrashUnit(isPlayer, targetLane);
                if (FieldManager.Instance.MoveUnit(isPlayer, lane, targetLane))
                    onDone?.Invoke();
            },
            mode: SkillZoneSlot.TargetMode.AllyLane);
    }

    // BT07-034: 인접 레인 아군 1장 트래시 후 이동, 트래시한 유닛의 히트에 따라 효과를 모두 적용
    private void BT07_ActiveMoveTrashAllyTiered(bool isPlayer, int lane)
    {
        var candidates = new List<int>();
        if (lane - 1 >= 0 && FieldManager.Instance.GetUnit(isPlayer, lane - 1) != null) candidates.Add(lane - 1);
        if (lane + 1 <= 2 && FieldManager.Instance.GetUnit(isPlayer, lane + 1) != null) candidates.Add(lane + 1);
        if (candidates.Count == 0) return;
        EffectActions.SelectLane(isPlayer, l => candidates.Contains(l), "트래시하고 이동할 인접 아군 유닛 존을 선택하세요",
            targetLane =>
            {
                var trashedUnit = FieldManager.Instance.GetUnit(isPlayer, targetLane);
                if (trashedUnit == null) return;
                int hit = GetEffectiveHit(isPlayer, targetLane, trashedUnit);
                int trashedCost = trashedUnit.Cost;
                EffectActions.TrashUnit(isPlayer, targetLane);
                if (!FieldManager.Instance.MoveUnit(isPlayer, lane, targetLane)) return;

                if (hit >= 1)
                {
                    EffectActions.Draw(isPlayer, 1);
                    AddTempSizeBoost(isPlayer, 3);
                }
                if (hit >= 2)
                {
                    var owner = GetOwner(isPlayer);
                    var recCands = owner.TrashPile.Where(c => c.Type == CardType.Unit
                        && c.Cost < trashedCost && c.CardName != "레이븐-적우").ToList();
                    bool hasEmpty = Enumerable.Range(0, 3).Any(i => FieldManager.Instance.GetUnit(isPlayer, i) == null);
                    if (recCands.Count > 0 && hasEmpty)
                        EffectActions.PickFromCards(isPlayer, "빈 유닛 존에 배치할 유닛을 선택하세요 (선택 안 해도 됨)", recCands,
                            picked =>
                            {
                                int emptyLane = -1;
                                for (int i = 0; i < 3; i++) if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { emptyLane = i; break; }
                                if (emptyLane < 0) return;
                                owner.RemoveFromTrash(picked);
                                FieldManager.Instance.ForcePlace(isPlayer, emptyLane, picked);
                                OnUnitPlaced(isPlayer, emptyLane, picked);
                            },
                            aiPick: list => list.OrderByDescending(c => c.Cost).First());
                }
                if (hit >= 3)
                    EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
                        "트래시할 상대 유닛을 선택하세요",
                        picked => EffectActions.TrashUnit(!isPlayer, picked));
            },
            mode: SkillZoneSlot.TargetMode.AllyLane);
    }

    // 트래시 존의 논트리거 카드 remaining장을 덱 맨 아래로 이동 (순서 선택 UI 없음 — 선택 순서 그대로 근사)
    // 공용 헬퍼(PickCardsToDeckBottom)에 위임 — 동작은 동일하고, 덱 맨 아래 카운터(SB02 참조)까지 일관되게 갱신된다
    private void BT07_ReturnCardsToDeckBottomStep(bool isPlayer, List<CardData> pool, int remaining, System.Action onDone)
        => PickCardsToDeckBottom(isPlayer, pool, CardZone.Trash, remaining, _ => onDone());

    // BT07-054: 트래시 논트리거 카드 N장 덱 밑 → 트래시의〈이브〉로 필드〈이브〉업그레이드
    private void BT07_ActiveSideReturnCardsUpgradeEve(bool isPlayer, int count)
    {
        var owner = GetOwner(isPlayer);
        var cands = owner.TrashPile.Where(c => !c.IsTrigger).ToList();
        if (cands.Count == 0) return;
        BT07_ReturnCardsToDeckBottomStep(isPlayer, cands, Mathf.Min(count, cands.Count), () =>
        {
            var eveCands = owner.TrashPile.Where(IsEveCard).ToList();
            if (eveCands.Count == 0) return;
            EffectActions.PickFromCards(isPlayer, "트래시에서 업그레이드에 사용할〈이브〉를 선택하세요 (선택 안 해도 됨)", eveCands,
                newCard =>
                {
                    EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => IsEveCard(u), "업그레이드할 필드의〈이브〉를 선택하세요",
                        eveLane =>
                        {
                            owner.RemoveFromTrash(newCard);
                            FieldManager.Instance.ForceUpgradeUnit(isPlayer, eveLane, newCard);
                        });
                },
                aiPick: list => list.OrderByDescending(c => c.Cost).First());
        });
    }

    // BT07-067: 패의 maxCost 이하〈이브〉1장으로 이 유닛(lane)을 업그레이드
    private void BT07_ActiveCenterSelfUpgradeEve(bool isPlayer, int lane, int maxCost)
    {
        var owner = GetOwner(isPlayer);
        var cands = owner.Hand.Where(c => IsEveCard(c) && c.Cost <= maxCost).ToList();
        if (cands.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, $"업그레이드에 사용할 {maxCost}코스트 이하〈이브〉를 선택하세요", cands,
            newCard =>
            {
                owner.RemoveFromHand(newCard);
                HandView.Instance?.RefreshHand();
                FieldManager.Instance.ForceUpgradeUnit(isPlayer, lane, newCard);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // BT07-085: 아이템 액티브:어택 — 패 아이템 1장 트래시 → 인접 레인(상대 유닛 있는) 강제 공격
    public void BT07_ItemActiveAttackAdjacentLane(bool isPlayer, int lane)
    {
        var owner = GetOwner(isPlayer);
        var itemsInHand = owner.Hand.Where(c => c.Type == CardType.Item).ToList();
        if (itemsInHand.Count == 0) return;
        var candidates = new List<int>();
        if (lane - 1 >= 0 && FieldManager.Instance.GetUnit(!isPlayer, lane - 1) != null) candidates.Add(lane - 1);
        if (lane + 1 <= 2 && FieldManager.Instance.GetUnit(!isPlayer, lane + 1) != null) candidates.Add(lane + 1);
        if (candidates.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "트래시할 아이템을 선택하세요", itemsInHand,
            discardedItem =>
            {
                owner.RemoveFromHand(discardedItem);
                owner.AddToTrash(discardedItem);
                HandView.Instance?.RefreshHand();
                EffectActions.SelectLane(isPlayer, l => candidates.Contains(l), "공격할 인접 레인을 선택하세요",
                    targetLane => CombatManager.Instance.ForceAttackAdjacentLane(isPlayer, lane, targetLane),
                    mode: SkillZoneSlot.TargetMode.EnemyLane);
            },
            aiPick: list => list[0]);
    }

    // BT07-087: 아이템 액티브:어택 — 트래시 논트리거 카드 retCount장 덱 밑 → 드로우 drawCount
    public void BT07_ItemActiveReturnCardsDraw(bool isPlayer, int retCount, int drawCount)
    {
        var owner = GetOwner(isPlayer);
        var cands = owner.TrashPile.Where(c => !c.IsTrigger).ToList();
        int actualCount = Mathf.Min(retCount, cands.Count);
        if (actualCount <= 0) { EffectActions.Draw(isPlayer, drawCount); return; }
        BT07_ReturnCardsToDeckBottomStep(isPlayer, cands, actualCount, () => EffectActions.Draw(isPlayer, drawCount));
    }

    // ═══════════════════ 엔트리 ═══════════════════

    private void BT07_EntryMove(bool isPlayer, int lane, CardData card, int value, string et)
        => BT07_PromptMove(isPlayer, lane);

    private void BT07_EntryCenterLevelUp(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (IsCenterLane(lane)) { GetOwner(isPlayer).LevelUpLeader(); HUDView.NotifyStatusChanged(); }
    }

    // BT07-052: 덱 위 3장 공개, 그중 최대 3장 골라 트래시, 나머지는 덱에 넣고 섞음
    private void BT07_EntryRevealTrashSelect(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        int take = Mathf.Min(value, owner.DrawPile.Count);
        if (take == 0) return;
        var revealed = owner.PeekDeckTop(take);
        owner.RemoveDeckTop(take);
        BT07_RevealTrashSelectStep(isPlayer, revealed, value);
    }

    private void BT07_RevealTrashSelectStep(bool isPlayer, List<CardData> revealed, int remaining)
    {
        var owner = GetOwner(isPlayer);
        if (remaining <= 0 || revealed.Count == 0)
        {
            owner.AddToDeckBottom(revealed);
            EffectActions.ShuffleDeck(isPlayer);
            EffectActions.RefreshUI();
            return;
        }
        EffectActions.PickFromCards(isPlayer, $"트래시할 카드를 선택하세요 (선택 안 해도 됨, {remaining}장 남음)", new List<CardData>(revealed),
            picked =>
            {
                revealed.Remove(picked);
                owner.AddToTrash(picked);
                BT07_RevealTrashSelectStep(isPlayer, revealed, remaining - 1);
            },
            onCancel: () =>
            {
                owner.AddToDeckBottom(revealed);
                EffectActions.ShuffleDeck(isPlayer);
                EffectActions.RefreshUI();
            });
    }

    // BT07-050: 조우 유닛이 N코스트 이상이면 드로우1
    private void BT07_EntryDrawIfEncounterCost(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc != null && enc.Cost >= value) EffectActions.Draw(isPlayer, 1);
    }

    // BT07-057: 필드의 〈이브〉 1장 골라 그 존으로 이동(스왑) → 이번턴 이동한 모든 유닛 파워+N
    private void BT07_EntryMoveToEveBoost(bool isPlayer, int lane, CardData card, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => l != lane && IsEveCard(u),
            "이동할 〈이브〉를 선택하세요 (선택 안 해도 됨)",
            eveLane =>
            {
                var swapped = FieldManager.Instance.GetUnit(isPlayer, eveLane);
                if (FieldManager.Instance.MoveUnit(isPlayer, lane, eveLane))
                {
                    EffectActions.Buff(isPlayer, eveLane, value, 0, BoostUntil.EndOfTurn); // 이동해온 caster
                    if (swapped != null) EffectActions.Buff(isPlayer, lane, value, 0, BoostUntil.EndOfTurn); // 스왑되어 이동한 이브
                }
            });

    // BT07-059: 조우 유닛이 N코스트 이상이면 트래시에서 M코스트 이상 〈이브〉 1장 패로
    // 형식: "EntryRecoverEveIfEncounter:N:M"
    private void BT07_EntryRecoverEveIfEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var parts = et.Split(':');
        int encCostReq = int.Parse(parts[1]);
        int eveMinCost = int.Parse(parts[2]);
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc == null || enc.Cost < encCostReq) return;
        EffectActions.RecoverFromTrash(isPlayer, c => IsEveCard(c) && c.Cost >= eveMinCost,
            $"트래시에서 회수할 {eveMinCost}코스트 이상 〈이브〉를 선택하세요");
    }

    // BT07-046: 포지션:사이드 — 사이드에 있으면 덱 위 N장 트래시, 트래시에서 〈이브〉 1장 패로
    private void BT07_EntrySideMillRecoverEve(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!IsSideLane(lane)) return;
        EffectActions.Mill(isPlayer, value);
        EffectActions.RecoverFromTrash(isPlayer, IsEveCard, "트래시에서 회수할 〈이브〉를 선택하세요 (선택 안 해도 됨)");
    }

    // BT07-049: 포지션:사이드 — 사이드에 있으면 드로우N, 패N장 트래시
    private void BT07_EntrySideDrawDiscard(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!IsSideLane(lane)) return;
        EffectActions.Draw(isPlayer, value);
        EffectActions.DiscardHand(isPlayer, value);
    }

    // BT07-051: 포지션:사이드 — 사이드에 있으면 덱 위 N장 트래시, 아이템 1장 이상 트래시됐으면 드로우1
    private void BT07_EntrySideMillDrawIfItem(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!IsSideLane(lane)) return;
        var milled = EffectActions.Mill(isPlayer, value);
        if (milled.Any(c => c.Type == CardType.Item)) EffectActions.Draw(isPlayer, 1);
    }

    // BT07-053: 포지션:사이드 — 사이드에 있으면 필드의 〈이브〉 1장 골라 그 파워만큼 이번턴 파워 증가
    private void BT07_EntrySidePowerCopyEve(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!IsSideLane(lane)) return;
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => IsEveCard(u),
            "파워를 복사할 〈이브〉를 선택하세요",
            eveLane =>
            {
                var eve = FieldManager.Instance.GetUnit(isPlayer, eveLane);
                if (eve != null) EffectActions.Buff(isPlayer, lane, GetEffectivePower(isPlayer, eveLane, eve), 0, BoostUntil.EndOfTurn);
            });
    }

    // BT07-066: 포지션:사이드 — 사이드에 있고 리더레벨 이하 코스트 〈이브〉가 필드에 있으면 아군 전체 이번턴 관통[1]
    private void BT07_EntrySideGrantAllPenetration(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!IsSideLane(lane)) return;
        int levelCap = GetOwner(isPlayer).LeaderLevel;
        bool hasEve = Enumerable.Range(0, 3).Any(i =>
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            return u != null && IsEveCard(u) && u.Cost <= levelCap;
        });
        if (!hasEve) return;
        for (int i = 0; i < 3; i++)
            if (FieldManager.Instance.GetUnit(isPlayer, i) != null)
                _grantedPenetration[Key(isPlayer, i)] = value;
    }

    // BT07-069/070: 자신은 이번턴 〈이브〉를 배치할 수 없다
    private void BT07_EntryBlockEveDeploy(bool isPlayer, int lane, CardData card, int value, string et)
        => _bt07EveDeployBlocked[Idx(isPlayer)] = true;

    // BT07-061: 조우 4코 이상이면 패1 트래시 가능 → 이 유닛 코스트≤리더레벨이면 이번턴 어택페이즈 추가공격 1회 획득 (근사: 미래 업그레이드 대신 이 유닛에 즉시 적용)
    private void BT07_EntryEveUpgradeExtraAttack(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc == null || enc.Cost < 4) return;
        EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
        {
            if (discarded < 1) return;
            if (card.Cost <= GetOwner(isPlayer).LeaderLevel) AddExtraAttack(isPlayer, lane);
        });
    }

    public bool IsSideLane(int lane) => !IsCenterLane(lane);

    // ═══════════════════ 엑시트 ═══════════════════

    // BT07-009/014/019: 자기 유닛이 트래시됐다면(근사: 조건 생략) 패에서 N코 이하 유닛 1장 빈 존에 사이즈무시 배치 가능
    private void BT07_ExitEffectTrashedDeploy(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        bool hasEmpty = Enumerable.Range(0, 3).Any(i => FieldManager.Instance.GetUnit(isPlayer, i) == null);
        var candidates = owner.Hand.Where(c => c.Type == CardType.Unit && c.Cost <= value).ToList();
        if (!hasEmpty || candidates.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "빈 유닛 존에 배치할 유닛을 선택하세요 (선택 안 해도 됨)", candidates,
            picked =>
            {
                int emptyLane = -1;
                for (int i = 0; i < 3; i++) if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { emptyLane = i; break; }
                if (emptyLane < 0) return;
                owner.RemoveFromHand(picked);
                FieldManager.Instance.ForcePlace(isPlayer, emptyLane, picked);
                OnUnitPlaced(isPlayer, emptyLane, picked);
                HandView.Instance?.RefreshHand();
            });
    }

    private void BT07_ExitRevealSearchUnit(bool isPlayer, int lane, CardData card, int value, string et)
        => EffectActions.RevealPickToHand(isPlayer, value, c => c.Type == CardType.Unit, "패에 넣을 유닛을 선택하세요");

    // BT07-004: 아군 1장 골라 인접 이동 가능, 그러면 이번턴 히트+N
    private void BT07_ExitMoveAllyHitBoost(bool isPlayer, int lane, CardData card, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            "이동시킬 유닛을 선택하세요 (선택 안 해도 됨)",
            picked => BT07_PromptMove(isPlayer, picked, toLane => EffectActions.Buff(isPlayer, toLane, 0, value, BoostUntil.EndOfTurn)));

    private void BT07_ExitGrantPenetration(bool isPlayer, int lane, CardData card, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
            $"관통[{value}]을 부여할 유닛을 선택하세요",
            picked => _grantedPenetration[Key(isPlayer, picked)] = value);

    // ═══════════════════ 어태커 ═══════════════════

    // BT07-021: 이 유닛이 이번턴 이동했고 파워≥조우파워면 조우 트래시 가능 (근사: "이 카드 효과로 이동"→"이동했다면")
    private void BT07_AttackerTrashEncounterIfMoved(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!HasMoved(card)) return;
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc == null) return;
        if (GetEffectivePower(isPlayer, lane, card) < GetEffectivePower(!isPlayer, lane, enc)) return;
        EffectActions.PickFromCards(isPlayer, "조우 유닛을 트래시하시겠습니까?", new List<CardData> { enc },
            _ => EffectActions.TrashUnit(!isPlayer, lane));
    }

    // BT07-022: 이번턴 이동 횟수≥조우 코스트면 조우 트래시
    private void BT07_AttackerTrashEncounterByMoveCount(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc != null && GetMoveCount(card) >= enc.Cost)
            EffectActions.TrashUnit(!isPlayer, lane);
    }

    // BT07-030: 포지션:센터 — 센터에 있고 파워가 조우보다 N 이상 높으면 관통[1] 획득(이 턴 끝까지)
    private void BT07_PositionCenterAttackerPenetration(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!IsCenterLane(lane)) return;
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc == null) return;
        if (GetEffectivePower(isPlayer, lane, card) - GetEffectivePower(!isPlayer, lane, enc) >= value)
            _grantedPenetration[Key(isPlayer, lane)] = 1;
    }

    // BT07-030: 포지션:사이드 — 사이드에 있고 파워가 조우보다 N 이상 높으면 리더 레벨+1
    private void BT07_PositionSideAttackerLevelUp(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!IsSideLane(lane)) return;
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc == null) return;
        if (GetEffectivePower(isPlayer, lane, card) - GetEffectivePower(!isPlayer, lane, enc) >= value)
        {
            GetOwner(isPlayer).LevelUpLeader();
            HUDView.NotifyStatusChanged();
        }
    }

    // BT07-035: 포지션:센터 — 센터에 있으면 패1 트래시 가능 → 이 유닛 파워 이하인 조우 트래시
    private void BT07_PositionCenterAttackerDiscardTrashEncounter(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!IsCenterLane(lane)) return;
        var enc = FieldManager.Instance.GetUnit(!isPlayer, lane);
        if (enc == null || GetEffectivePower(!isPlayer, lane, enc) > GetEffectivePower(isPlayer, lane, card)) return;
        EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
        {
            if (discarded >= 1) EffectActions.TrashUnit(!isPlayer, lane);
        });
    }

    // BT07-071: 포지션:사이드 — 사이드에 있으면 관통[필드의 〈이브〉 장착 아이템 수]
    private void BT07_PositionSideAttackerPenetrationPerEveItem(bool isPlayer, int lane, CardData card, int value, string et)
    {
        if (!IsSideLane(lane)) return;
        int x = BT07_CountEveEquippedItems(isPlayer);
        if (x > 0) _grantedPenetration[Key(isPlayer, lane)] = x;
    }

    // BT07-028: 레벨링크:N 어태커 — 리더 레벨이 N 이상이면 관통[1] 획득
    // 형식: "LevelLinkAttackerPenetration:N:M" (M=관통량)
    private void BT07_LevelLinkAttackerPenetration(bool isPlayer, int lane, CardData card, int value, string et)
    {
        var parts = et.Split(':');
        int levelReq = int.Parse(parts[1]);
        int amount = parts.Length > 2 ? int.Parse(parts[2]) : 1;
        if (GetOwner(isPlayer).LeaderLevel >= levelReq)
            _grantedPenetration[Key(isPlayer, lane)] = amount;
    }

    // ═══════════════════ 스킬 ═══════════════════

    private void BT07_MoveAlly(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "이동시킬 유닛을 선택하세요",
            picked => BT07_PromptMove(isPlayer, picked));

    private void BT07_MoveAllyLevelUp(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "이동시킬 유닛을 선택하세요 (선택 안 해도 됨)",
            picked =>
            {
                BT07_PromptMove(isPlayer, picked);
                GetOwner(isPlayer).LevelUpLeader();
                HUDView.NotifyStatusChanged();
            });

    private void BT07_MoveAllyPowerHitBoost(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "이동시킬 유닛을 선택하세요 (선택 안 해도 됨)",
            picked =>
            {
                EffectActions.Buff(isPlayer, picked, value, 1, BoostUntil.EndOfTurn);
                BT07_PromptMove(isPlayer, picked);
            });

    private void BT07_DrawPerMovedUnit(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        int cnt = 0;
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && HasMoved(u)) cnt++;
        }
        EffectActions.Draw(isPlayer, cnt);
    }

    // BT07-042: 이번턴 이동했고 조우 유닛을 가진 자신 유닛이 있는 레인 하나 골라, 그 레인 최저 파워 유닛 트래시(동점=전부)
    private void BT07_TrashWeakestInMovedLane(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var eligible = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && HasMoved(u) && FieldManager.Instance.GetUnit(!isPlayer, i) != null)
                eligible.Add(i);
        }
        if (eligible.Count == 0) return;
        EffectActions.SelectLane(isPlayer, l => eligible.Contains(l), "대상 레인을 선택하세요",
            lane =>
            {
                var p = FieldManager.Instance.GetUnit(true, lane);
                var a = FieldManager.Instance.GetUnit(false, lane);
                if (p == null || a == null) return;
                int pPow = GetEffectivePower(true, lane, p);
                int aPow = GetEffectivePower(false, lane, a);
                if (pPow <= aPow) EffectActions.TrashUnit(true, lane);
                if (aPow <= pPow) EffectActions.TrashUnit(false, lane);
            },
            mode: SkillZoneSlot.TargetMode.AllyLane);
    }

    private void BT07_SizeBoostTemp(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        _selfLockedSkillsThisTurn.Add(SelfLockKey(isPlayer, skill.CardId));
        AddTempSizeBoost(isPlayer, value);
    }

    // BT07-044: 아군 1장 고름 → 그보다 파워 낮은 상대 1장 트래시 → 고른 아군 덱 맨 아래로
    private void BT07_TrashEnemyByPowerReturnAlly(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "기준이 될 유닛을 선택하세요",
            allyLane =>
            {
                int allyPower = GetEffectivePower(isPlayer, allyLane, FieldManager.Instance.GetUnit(isPlayer, allyLane));
                EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => GetEffectivePower(!isPlayer, l, u) < allyPower,
                    "트래시할 상대 유닛을 선택하세요",
                    enemyLane =>
                    {
                        EffectActions.TrashUnit(!isPlayer, enemyLane);
                        var ally = FieldManager.Instance.GetUnit(isPlayer, allyLane);
                        if (ally != null)
                        {
                            FieldManager.Instance.RemoveUnit(isPlayer, allyLane); // 엑시트 미발동 (덱으로 이동, 트래시 아님)
                            GetOwner(isPlayer).AddToDeckBottom(ally); // 덱 맨 아래
                            EffectActions.RefreshUI();
                        }
                    });
            });

    private void BT07_RecoverEveFromTrash(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.RecoverFromTrash(isPlayer, IsEveCard, "트래시에서 회수할 〈이브〉를 선택하세요");

    // BT07-076: 패를 원하는 수만큼 트래시 → 그 수만큼 트래시에서 〈이브〉 회수
    private void BT07_DiscardRecoverEves(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        EffectActions.DiscardHandOptional(isPlayer, owner.Hand.Count, discarded =>
        {
            if (discarded > 0) BT07_SwitchRecoverEvesStep(isPlayer, discarded);
        });
    }

    private void BT07_SwitchRecoverEvesStep(bool isPlayer, int remaining)
    {
        if (remaining <= 0) return;
        var owner = GetOwner(isPlayer);
        var candidates = owner.TrashPile.Where(IsEveCard).ToList();
        if (candidates.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, $"트래시에서 회수할 〈이브〉를 선택하세요 ({remaining}장 남음)", candidates,
            picked =>
            {
                EffectActions.MoveCard(isPlayer, picked, CardZone.Trash, CardZone.Hand);
                BT07_SwitchRecoverEvesStep(isPlayer, remaining - 1);
            },
            aiPick: list => list.OrderByDescending(c => c.Cost).First());
    }

    // BT07-077: 패1 트래시 → 필드 〈이브〉 1장 골라 이번턴 "어태커: 코스트≤장착아이템수인 상대 1장 트래시" + 히트+1
    private void BT07_DiscardGrantEveAttackerTrashByItems(bool isPlayer, CardData skill, int targetLane, int value, string et)
        => EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
        {
            if (discarded < 1) return;
            EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => IsEveCard(u), "효과를 부여할 〈이브〉를 선택하세요",
                picked =>
                {
                    _bt07EveAttackerDebuffPerItem.Remove(Key(isPlayer, picked)); // 다른 스위치 오라와 충돌 방지
                    _bt07EveAttackerTrashByItems.Add(Key(isPlayer, picked));
                    EffectActions.Buff(isPlayer, picked, 0, 1, BoostUntil.EndOfTurn);
                });
        });

    private HashSet<string> _bt07EveAttackerTrashByItems = new(); // Key(isPlayer,lane) — 이번턴, 어태커: 코스트≤장착아이템수 상대 트래시

    // BT07-078: 패의 〈이브〉 1장 트래시 → 필드 〈이브〉 1장 골라 그 조우 유닛 파워를 트래시한 카드 파워만큼 이번턴 감소
    private void BT07_DiscardEveDebuffEncounterByPower(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        var eveInHand = owner.Hand.Where(IsEveCard).ToList();
        if (eveInHand.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "트래시할 〈이브〉를 선택하세요", eveInHand,
            discarded =>
            {
                owner.RemoveFromHand(discarded);
                owner.AddToTrash(discarded);
                HandView.Instance?.RefreshHand();
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => IsEveCard(u), "대상 〈이브〉를 선택하세요",
                    eveLane =>
                    {
                        var enc = FieldManager.Instance.GetUnit(!isPlayer, eveLane);
                        if (enc != null) AddTurnBoost(!isPlayer, eveLane, -discarded.AttackPower);
                    });
            });
    }

    // BT07-079: 필드 〈이브〉 1장 골라, 장착 아이템 종류 수만큼 덱 위 트래시 → 그 종류 수만큼 이번턴 히트+
    private void BT07_MillPerEveItemTypesHitBoost(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        _selfLockedSkillsThisTurn.Add(SelfLockKey(isPlayer, skill.CardId));
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => IsEveCard(u), "대상 〈이브〉를 선택하세요",
            eveLane =>
            {
                int types = FieldManager.Instance.GetEquippedItems(isPlayer, eveLane)
                    .Select(it => it.CardName).Distinct().Count();
                if (types <= 0) return;
                EffectActions.Mill(isPlayer, types);
                EffectActions.Buff(isPlayer, eveLane, 0, types, BoostUntil.EndOfTurn);
            });
    }

    // BT07-074: 패에서 리더레벨 이하 코스트 〈이브〉 1장 골라 필드 〈이브〉를 사이즈 무시하고 그 카드로 업그레이드
    private void BT07_UpgradeEveFromHandByLevel(bool isPlayer, CardData skill, int targetLane, int value, string et)
    {
        _selfLockedSkillsThisTurn.Add(SelfLockKey(isPlayer, skill.CardId));
        var owner = GetOwner(isPlayer);
        int levelCap = owner.LeaderLevel;
        var handCandidates = owner.Hand.Where(c => IsEveCard(c) && c.Cost <= levelCap).ToList();
        if (handCandidates.Count == 0) return;
        EffectActions.PickFromCards(isPlayer, "업그레이드에 사용할 〈이브〉를 선택하세요", handCandidates,
            newCard =>
            {
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => IsEveCard(u), "업그레이드할 필드의 〈이브〉를 선택하세요",
                    lane =>
                    {
                        owner.RemoveFromHand(newCard);
                        HandView.Instance?.RefreshHand();
                        FieldManager.Instance.ForceUpgradeUnit(isPlayer, lane, newCard);
                    });
            });
    }

    // ═══════════════════ 트리거 ═══════════════════

    // BT07-021/031: 트래시, 아군 1장 이번턴 파워+N, 그 유닛 인접 이동 가능
    private void BT07_TriggerTrashSelfBuffMove(bool isPlayer, CardData card, int value, string et)
    {
        EffectActions.MoveCard(isPlayer, card, CardZone.Damage, CardZone.Trash);
        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "파워를 부여할 유닛을 선택하세요 (선택 안 해도 됨)",
            picked =>
            {
                EffectActions.Buff(isPlayer, picked, value, 0, BoostUntil.EndOfTurn);
                BT07_PromptMove(isPlayer, picked);
            });
    }

    // BT07-050/056/059 등: 빈 존에 사이즈 무시 배치하거나, 필드의 〈이브〉를 사이즈/코스트 무시 업그레이드. 안 했으면 트래시.
    private void BT07_TriggerDeployOrUpgradeEve(bool isPlayer, CardData card, int value, string et)
    {
        var owner = GetOwner(isPlayer);
        bool hasEmpty = Enumerable.Range(0, 3).Any(i => FieldManager.Instance.GetUnit(isPlayer, i) == null);
        bool hasEveOnField = Enumerable.Range(0, 3).Any(i =>
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            return u != null && IsEveCard(u);
        });

        if (!hasEmpty && !hasEveOnField)
        {
            owner.AddToTrash(card);
            EffectActions.RefreshUI();
            return;
        }

        var options = new List<CardData>();
        var deployMarker = ScriptableObject.CreateInstance<CardData>();
        deployMarker.CardName = $"{card.CardName} (배치)";
        var upgradeMarker = ScriptableObject.CreateInstance<CardData>();
        upgradeMarker.CardName = $"{card.CardName} (업그레이드)";
        if (hasEmpty) options.Add(deployMarker);
        if (hasEveOnField) options.Add(upgradeMarker);

        EffectActions.PickFromCards(isPlayer, "배치할지 업그레이드할지 선택하세요", options,
            choice =>
            {
                if (choice == deployMarker)
                {
                    int emptyLane = -1;
                    for (int i = 0; i < 3; i++) if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { emptyLane = i; break; }
                    FieldManager.Instance.ForcePlace(isPlayer, emptyLane, card);
                    OnUnitPlaced(isPlayer, emptyLane, card);
                }
                else
                {
                    EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => IsEveCard(u), "업그레이드할 〈이브〉를 선택하세요",
                        lane => FieldManager.Instance.ForceUpgradeUnit(isPlayer, lane, card));
                }
            },
            aiPick: list => list[0], // AI는 항상 첫 옵션(배치 우선)
            onCancel: () => { owner.AddToTrash(card); EffectActions.RefreshUI(); });
    }
}
