// Assets/Scripts/Managers/EffectSystem.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public partial class EffectSystem : MonoBehaviour
{
    public void OnTurnEnd(bool isPlayerTurn)
    {
        // ST07 어비스 드레이크 가죽갑옷: 자신의 턴이 끝날 때 장착 유닛 트래시 → 드로우N
        // (턴 단위 상태를 지우기 전에 처리 — 트래시되는 유닛에 부여된 엑시트 등이 정상 발동하도록)
        ProcessItemEndTurnTrashUnit(isPlayerTurn);

        _turnBoosts.Clear();
        _turnHitBoosts.Clear();
        _breakthroughGrants.Clear();
        _attackerGrants.Clear();
        _attackCountThisTurn[0]  = _attackCountThisTurn[1]  = 0;
        _unitsTrashedThisTurn[0] = _unitsTrashedThisTurn[1] = 0;
        _effectDamageDraw[0]     = _effectDamageDraw[1]     = false;
        _leaderActiveUsed[0]     = _leaderActiveUsed[1]     = false;
        _attackDisabledTurn.Clear();
        _defendDisabledTurn.Clear();
        _extraAttacks.Clear();
        _covenantLocked[0] = _covenantLocked[1] = false;
        _selfLockedSkillsThisTurn.Clear();
        _zeroCostGrants.Clear();
        _homunculusAttackCountThisTurn[0] = _homunculusAttackCountThisTurn[1] = 0;
        _grantedExit.Clear();
        _unitActiveUsedThisTurn.Clear();
        _deckBottomDamageUsedThisTurn.Clear();
        _effectTrashDamageUsedThisTurn.Clear();
        _grantedAttackerTrashEncounter.Clear();
        _grantedDuelistThisTurn.Clear();
        _grantedPenetration.Clear();
        _grantedAttackerPlunder.Clear();
        _effectTrashedThisTurn[0] = _effectTrashedThisTurn[1] = 0;
        _discardDrawUsedThisTurn.Clear();
        _grantedExitReturn.Clear();
        _grantedDefenderFinisher.Clear();
        _highCostDeployLocked[0] = _highCostDeployLocked[1] = 0;
        _grantedPassiveDrawOnEquip.Clear();
        _entryLocked[0] = _entryLocked[1] = false;
        _handTrashedThisTurn[0] = _handTrashedThisTurn[1] = false;
        ProcessEndTurnTrashGrants(isPlayerTurn);                                     // BT04 「턴 끝 트래시」
        _damageZoneTurnBonus[0] = _damageZoneTurnBonus[1] = 0;                       // BT04
        _cardPlacedToDamageZoneThisTurn[0] = _cardPlacedToDamageZoneThisTurn[1] = false; // BT04
        _exitDisabledThisTurn[0] = _exitDisabledThisTurn[1] = false;                 // BT04
        _grantDamageZoneOnTrash.Clear();                                             // BT04
        ProcessBT05EndOpponentTurn(isPlayerTurn);                                    // BT05 아이템 상대턴종료 패시브
        ProcessSB02TurnEnd(isPlayerTurn);                                            // SB02 아이템 턴종료
        _cardsToDeckBottomThisTurn[0] = _cardsToDeckBottomThisTurn[1] = 0;           // SB02 덱-밑 카운터
        _bt06ZeroCostSkillZoneCards.Clear();                                         // BT06 스킬존 임시 0코스트화
        _bt06ChainInfiltrateThisAttack.Clear();                                      // BT06 체인 침투(공격 단위, 안전망)
        _bt06DrawOnOppDrawUsed.Clear();                                              // BT06 상대 드로우 감지 패시브 1회 제한
        _bt07MovedThisTurn.Clear();                                                  // BT07 이동 추적(이 턴)
        _bt07MoveCountThisTurn.Clear();
        _bt07MoveReactionUsed.Clear();
        _bt07TempSizeBoost[0] = _bt07TempSizeBoost[1] = 0;
        _bt07EveDeployBlocked[0] = _bt07EveDeployBlocked[1] = false;
        _bt07EveDeployedThisTurn[0] = _bt07EveDeployedThisTurn[1] = false;
        _bt07EveAttackerDebuffPerItem.Clear();
        _bt07EveAttackerTrashByItems.Clear();
        _sb01AmplifyActiveDamage[0] = _sb01AmplifyActiveDamage[1] = false;   // SB01-013
        _sb01DamageZoneOnTrashGrant.Clear();                                 // SB01-005
        _sb01PainEaterLink.Clear();                                          // SB01-014

        // 상대 턴 종료 시 "상대의 턴이 끝날 때까지" 효과 제거
        if (!isPlayerTurn)
        {
            _opponentTurnBoosts.Clear();
            _opponentTurnHitBoosts.Clear();
            _attackDisabledOppTurn.Clear();
            _grantedDefenderBoost.Clear();
            _conditionalBreakthroughGrants.Clear();
            // BT02 매터 타입 아이템: 상대 턴 끝에 트래시
            ProcessItemExpiresEndOfOpponentTurn();
            _itemExpiresEndOfOpponentTurn.Clear();
            // BT02 헬름: 부여된 "광전사" 해제 (상대 턴 끝까지 효과)
            _grantedBerserkerToEnemyMinCost.Clear();
            // BT06: 광전사 오라/전체부여, 히트=1 오라, 고비용 배치 차단, 어태커 효과 봉쇄, 공격 시 상대 드로우 부여 해제
            _bt06BerserkAura.Clear();
            _bt06AllEnemiesBerserker[0] = _bt06AllEnemiesBerserker[1] = false;
            _bt06HitSetOneAura.Clear();
            _bt06DeployBlockHighCost.Clear();
            _bt06AttackerEffectsDisabled[0] = _bt06AttackerEffectsDisabled[1] = false;
            _bt06AttackerDrawPenalty.Clear();
            _bt07DeployBlockLowCost.Clear(); // BT07-015 저비용 배치 차단
            _sb01DrawPunish[0] = _sb01DrawPunish[1] = false; // SB01-004
        }
        // 요밀로 감시: 방금 끝난 턴이 감시 대상(R)의 상대 턴이었다면 해제
        if (_escapeDeployPunishThreshold[0] >= 0 && !isPlayerTurn) _escapeDeployPunishThreshold[0] = -1; // R=플레이어, 상대(AI)턴 종료
        if (_escapeDeployPunishThreshold[1] >= 0 &&  isPlayerTurn) _escapeDeployPunishThreshold[1] = -1; // R=AI, 상대(플레이어)턴 종료
        Debug.Log("[EffectSystem] 턴 종료 — 임시 효과 제거");
        LaneSlot.RefreshAllLanes(); // 만료된 버프/디버프의 카드 아래 증감 라벨 즉시 제거
    }

    // ── 액티브 효과 발동 ──────────────────────────────────────────

    // 메인 페이즈에 필드 유닛의 액티브 효과를 발동한다.
    // isPlayer: 발동자, lane: 해당 레인
    public void TriggerActiveEffect(bool isPlayer, int lane, CardData card)
    {
        foreach (var et in card.EffectTypes)
        {
            var (type, value) = Parse(et);
            if (TryBT04Active(isPlayer, lane, card, value, et)) continue; // BT04 액티브
            if (TryBT05Active(isPlayer, lane, card, value, et)) continue; // BT05 액티브
            if (TryBT06Active(isPlayer, lane, card, value, et)) continue; // BT06 액티브
            if (TryBT07Active(isPlayer, lane, card, value, et)) continue; // BT07 액티브
            if (TrySB01Active(isPlayer, lane, card, value, et)) continue; // SB01 액티브
            if (TrySB02Active(isPlayer, lane, card, value, et)) continue; // SB02 액티브
            switch (type)
            {
                // 아니스: 패 1장을 덱에 넣고 섞음 → 조우 유닛 파워-3000 (이 턴)
                case "ActiveReturnCardDebuff":
                    if (!isPlayer) { ApplyActiveReturnCardDebuff(isPlayer, lane, value); break; }
                    HandPickPopup.Instance.Show(
                        $"[{card.CardName}] 덱에 넣을 패를 선택하세요",
                        new List<CardData>(GetOwner(isPlayer).Hand),
                        picked =>
                        {
                            var owner = GetOwner(isPlayer);
                            owner.RemoveFromHand(picked);
                            owner.InsertIntoDeck(Random.Range(0, owner.DrawPile.Count + 1), picked);
                            HandView.Instance?.RefreshHand();
                            HUDView.NotifyStatusChanged();
                            AddTurnBoost(!isPlayer, lane, -value);
                            Debug.Log($"[액티브] {card.CardName} → {picked.CardName} 덱에 삽입, 조우 파워-{value}");
                        });
                    break;

                // 레어 메탈 글러브: 드로우N
                case "ActiveDraw":
                    GetOwner(isPlayer).DrawCard(value);
                    HandView.Instance?.RefreshHand();
                    HUDView.NotifyStatusChanged();
                    Debug.Log($"[액티브] {card.CardName} → 드로우{value}");
                    break;

                // ST11 미카엘라: 버프:1 액티브 — 스킬존≥1이면 히트 N 이하 상대 1장 상대 턴 끝까지 공격 불가
                case "BuffActiveSkillZoneDisableAttack":
                    if (GetOwner(isPlayer).SkillZone.Count >= 1)
                        EffectActions.SelectUnit(isPlayer, !isPlayer,
                            (l, u) => GetEffectiveHit(!isPlayer, l, u) <= value,
                            "공격 불가로 만들 상대 유닛을 선택하세요",
                            picked => DisableAttack(!isPlayer, picked, BoostUntil.EndOfOpponentTurn));
                    break;

                // ST11 이클립스: 버프:1 액티브 — 스킬존≥1이면 아군 전체 상대 턴 끝까지 파워+N
                case "BuffActiveSkillZoneAllBoost":
                    if (GetOwner(isPlayer).SkillZone.Count >= 1)
                        for (int i = 0; i < 3; i++)
                            if (FieldManager.Instance.GetUnit(isPlayer, i) != null)
                                EffectActions.Buff(isPlayer, i, value, 0, BoostUntil.EndOfOpponentTurn);
                    break;

                // ST10 루비아: 버프:1 액티브 — 스킬존≥1이면 「어태커 파워+N」 획득
                // (원문 "상대 턴 끝까지" — 부여 저장소가 턴 단위라 이번 턴으로 근사)
                case "BuffActiveSkillZoneAttacker":
                    if (GetOwner(isPlayer).SkillZone.Count >= 1)
                        GrantAttackerBoost(isPlayer, lane, value);
                    break;

                // ST06 빛의 루엘: 액티브:메인 — 패 스킬 최대 2장 트래시, 이 턴이 끝날 때까지 트래시 코스트합 1마다 조우 유닛 파워-N
                case "ActiveDiscardSkillsDebuffEncounter":
                    DiscardSkillsForDebuffStep(isPlayer, lane, card, value, 2, 0);
                    break;

                // 브리드: 패 1장을 트래시 → 베이스 유닛 전체 히트+1 (이 턴)
                case "ActiveTrashBuffBase":
                    if (!isPlayer) { ApplyActiveTrashBuffBase(isPlayer); break; }
                    HandPickPopup.Instance.Show(
                        $"[{card.CardName}] 트래시할 패를 선택하세요",
                        new List<CardData>(GetOwner(isPlayer).Hand),
                        picked =>
                        {
                            var owner = GetOwner(isPlayer);
                            owner.RemoveFromHand(picked);
                            owner.AddToTrash(picked);
                            HandView.Instance?.RefreshHand();
                            HUDView.NotifyStatusChanged();
                            ApplyActiveTrashBuffBase(isPlayer);
                            Debug.Log($"[액티브] {card.CardName} → {picked.CardName} 트래시, 베이스 히트+1");
                        });
                    break;

                // ST07 릴리벳: 액티브:어택 — 필드에 있는 자신 유닛을 1장 골라 트래시한다
                case "ActiveAttackTrashAlly":
                    EffectActions.SelectUnit(isPlayer, isPlayer,
                        (l, u) => true,
                        "트래시할 아군 유닛을 선택하세요",
                        picked => EffectActions.TrashUnit(isPlayer, picked));
                    break;

                // ST07 후미르: 액티브:어택 — 《호문클루스》+엑시트(정적/부여 모두 인정) 아군 1장 트래시 → 히트+N
                case "ActiveAttackTrashExitAllyHitBoost":
                    EffectActions.SelectUnit(isPlayer, isPlayer,
                        (l, u) => u.Faction != null && u.Faction.Contains("호문클루스")
                                  && (u.Keywords.Contains("엑시트") || HasGrantedExit(isPlayer, l)),
                        "트래시할 《호문클루스》+엑시트 유닛을 선택하세요",
                        picked =>
                        {
                            EffectActions.TrashUnit(isPlayer, picked);
                            EffectActions.Buff(isPlayer, lane, 0, value, BoostUntil.EndOfTurn);
                        });
                    break;

                // ST07 벨리안: 액티브:메인 — (이번 턴 아군 트래시됨) 패1 트래시 가능 → 트래시의 엑시트 N코 이하 유닛을 빈 존에 배치
                case "ActiveReviveExitUnit":
                    if (_unitsTrashedThisTurn[Idx(isPlayer)] < 1) break;
                    EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
                    {
                        if (discarded == 0) return;
                        var owner = GetOwner(isPlayer);
                        var candidates = owner.TrashPile
                            .Where(c => c.Type == CardType.Unit && c.Cost <= value && c.Keywords.Contains("엑시트"))
                            .ToList();
                        bool hasEmptyLane = Enumerable.Range(0, 3).Any(i => FieldManager.Instance.GetUnit(isPlayer, i) == null);
                        if (candidates.Count == 0 || !hasEmptyLane) return;

                        EffectActions.PickFromCards(isPlayer, "필드에 배치할 유닛을 선택하세요", candidates,
                            picked =>
                            {
                                owner.RemoveFromTrash(picked);
                                int emptyLane = -1;
                                for (int i = 0; i < 3; i++)
                                    if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { emptyLane = i; break; }
                                if (emptyLane >= 0)
                                {
                                    FieldManager.Instance.PlaceUnit(isPlayer, emptyLane, picked);
                                    OnUnitPlaced(isPlayer, emptyLane, picked);
                                    Debug.Log($"[액티브] {card.CardName} → {picked.CardName} 필드 배치");
                                }
                                EffectActions.RefreshUI();
                            },
                            aiPick: list => list.OrderByDescending(c => c.Cost).First());
                    });
                    break;

                // ST11 명월 달비: 버프:1 액티브 — 스킬존≥1이면 아군 전체에 상대 턴 끝까지 「디펜더 파워+N」 부여
                case "BuffActiveSkillZoneDefenderAll":
                    if (GetOwner(isPlayer).SkillZone.Count >= 1)
                        for (int i = 0; i < 3; i++)
                            if (FieldManager.Instance.GetUnit(isPlayer, i) != null)
                                GrantDefenderBoost(isPlayer, i, value);
                    break;

                // ST11 사도 모르페아: 버프:1 액티브 — 스킬존≥1이면 자신에게 상대 턴 끝까지 돌파[N코 이하] 획득
                case "BuffActiveSkillZoneBreakthrough":
                    if (GetOwner(isPlayer).SkillZone.Count >= 1)
                        GrantConditionalBreakthrough(isPlayer, lane, value);
                    break;

                // ST08 불꽃놀이 아야: 액티브:메인 — 패에서 리더레벨 이하 코스트 유닛 1장 사이즈 무시 배치, 이번 턴 공격 불가
                case "ActiveDeployFromHandByLevel":
                {
                    var owner = GetOwner(isPlayer);
                    var candidates = owner.Hand.Where(c => c.Type == CardType.Unit && c.Cost <= owner.LeaderLevel).ToList();
                    bool hasEmptyLane = Enumerable.Range(0, 3).Any(i => FieldManager.Instance.GetUnit(isPlayer, i) == null);
                    if (candidates.Count == 0 || !hasEmptyLane) break;

                    EffectActions.PickFromCards(isPlayer, "사이즈 무시로 배치할 유닛을 선택하세요 (리더 레벨 이하)", candidates,
                        picked =>
                        {
                            owner.RemoveFromHand(picked);
                            int emptyLane = -1;
                            for (int i = 0; i < 3; i++)
                                if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { emptyLane = i; break; }
                            if (emptyLane >= 0)
                            {
                                FieldManager.Instance.PlaceUnit(isPlayer, emptyLane, picked);
                                OnUnitPlaced(isPlayer, emptyLane, picked);
                                DisableAttack(isPlayer, emptyLane, BoostUntil.EndOfTurn);
                            }
                            EffectActions.RefreshUI();
                        },
                        aiPick: list => list.OrderByDescending(c => c.Cost).First());
                    break;
                }

                // ST08 타락의 유열 샬럿: 액티브:어택 — 파워가 N 이상이면 이번 어택 페이즈 중 추가 공격 1회
                case "ActiveAttackExtraAttackByPower":
                    if (GetEffectivePower(isPlayer, lane, card) >= value)
                        AddExtraAttack(isPlayer, lane);
                    break;

                // BT01 유닛 액티브: 메인 페이즈 상대 유닛 1장 파워-N
                case "ActiveMainDebuffEnemy":
                    EffectActions.SelectUnit(isPlayer, !isPlayer,
                        (l, u) => true,
                        $"파워-{value}를 줄 상대 유닛을 선택하세요",
                        picked => AddTurnBoost(!isPlayer, picked, -value));
                    break;

                // BT01 유닛 액티브: 메인 페이즈 패 1장 트래시 → 아군 1장 파워+N
                case "ActiveMainDiscardBuffAlly":
                    EffectActions.DiscardHandOptional(isPlayer, 1, discarded =>
                    {
                        if (discarded < 1) return;
                        EffectActions.SelectUnit(isPlayer, isPlayer,
                            (l, u) => true,
                            $"파워+{value}를 줄 아군 유닛을 선택하세요",
                            picked => EffectActions.Buff(isPlayer, picked, value, 0, BoostUntil.EndOfTurn));
                    });
                    break;

                // BT01 유닛 액티브: 메인 페이즈 아군 1장 트래시 → 다른 아군 1장 파워+N
                case "ActiveMainTrashAllyBuffOther":
                {
                    EffectActions.SelectUnit(isPlayer, isPlayer,
                        (l, u) => true,
                        "트래시할 아군 유닛을 선택하세요",
                        allyLane =>
                        {
                            EffectActions.TrashUnit(isPlayer, allyLane);
                            EffectActions.SelectUnit(isPlayer, isPlayer,
                                (l, u) => true,
                                $"파워+{value}를 줄 다른 아군 유닛을 선택하세요",
                                picked => EffectActions.Buff(isPlayer, picked, value, 0, BoostUntil.EndOfTurn));
                        });
                    break;
                }

                // BT02 길로틴: 액티브:메인 — 이 턴 효과로 트래시된 수만큼 상대 N대미지
                case "ActiveMainEffectTrashDamage":
                {
                    var parts2 = et.Split(':');
                    int perDmg = value;
                    int tCount = _effectTrashedThisTurn[Idx(isPlayer)];
                    if (tCount <= 0) break;
                    GetOwner(!isPlayer).TakeDamage(tCount * perDmg);
                    Debug.Log($"[액티브] {card.CardName} → 효과 트래시 {tCount}장 × {perDmg}대미지");
                    break;
                }

                // BT03 액티브:메인 — 패 유닛 1장 트래시 → 상대 조우 파워에 맞추거나 그보다 높으면 그 유닛 트래시, 아니면 N대미지
                case "ActiveMainDiscardUnitForceMatchOrDamage":
                {
                    var owner3 = GetOwner(isPlayer);
                    var unitCands = owner3.Hand.Where(c => c.Type == CardType.Unit).ToList();
                    if (unitCands.Count == 0) break;
                    EffectActions.PickFromCards(isPlayer, "트래시할 패 유닛을 선택하세요", unitCands,
                        picked =>
                        {
                            owner3.RemoveFromHand(picked);
                            owner3.AddToTrash(picked);
                            HandView.Instance?.RefreshHand();
                            var encounter = FieldManager.Instance.GetUnit(!isPlayer, lane);
                            if (encounter != null)
                            {
                                if (picked.AttackPower >= GetEffectivePower(!isPlayer, lane, encounter))
                                    EffectActions.TrashUnit(!isPlayer, lane);
                                else
                                    EffectActions.DamageOpponent(isPlayer, value);
                            }
                            Debug.Log($"[액티브] {card.CardName} → {picked.CardName} 트래시 후 판정");
                        },
                        aiPick: list => list.OrderByDescending(c => c.AttackPower).First());
                    break;
                }

                // BT03 액티브:메인 — 자신 파워가 조우 파워보다 높으면 관통[N] 획득
                case "ActiveMainPowerAdvantageGrantPenetration":
                {
                    var encounter2 = FieldManager.Instance.GetUnit(!isPlayer, lane);
                    if (encounter2 != null
                        && GetEffectivePower(isPlayer, lane, card) > GetEffectivePower(!isPlayer, lane, encounter2))
                    {
                        _grantedPenetration[Key(isPlayer, lane)] = value;
                        Debug.Log($"[액티브] {card.CardName} → 관통[{value}] 획득");
                    }
                    break;
                }

                // BT03 액티브:메인 — 상대 패가 N장 이하면 아군 파워+M
                case "ActiveMainPowerBoostIfOpponentLowHand":
                {
                    var parts3 = et.Split(':');
                    int handThresh = parts3.Length >= 2 ? int.Parse(parts3[1]) : 0;
                    int pBoost = parts3.Length >= 3 ? int.Parse(parts3[2]) : 0;
                    if (GetOwner(!isPlayer).Hand.Count <= handThresh)
                        AddTurnBoost(isPlayer, lane, pBoost);
                    Debug.Log($"[액티브] {card.CardName} → 상대패{GetOwner(!isPlayer).Hand.Count}장 ≤ {handThresh}: 파워+{pBoost}");
                    break;
                }

                // BT03 액티브:메인 — 트래시에서 이 유닛에 장착된 아이템을 전부 패로 (0코스트화)
                case "ActiveMainRecoverEquipItemsZeroCost":
                {
                    var owner4 = GetOwner(isPlayer);
                    var trashItems = owner4.TrashPile.Where(c => c.Type == CardType.Item).ToList();
                    if (trashItems.Count == 0) break;
                    owner4.AddToHand(trashItems);
                    owner4.RemoveFromTrashAll(c => trashItems.Contains(c));
                    HandView.Instance?.RefreshHand();
                    Debug.Log($"[액티브] {card.CardName} → 트래시 아이템 {trashItems.Count}장 패로");
                    break;
                }

                // BT03 액티브:메인 — 스킬존 스킬 1장 트래시 → 아군에 관통[N] 부여
                case "ActiveMainSkillZoneTrashGrantPenetration":
                {
                    var owner5 = GetOwner(isPlayer);
                    if (owner5.SkillZone.Count == 0) break;
                    EffectActions.PickFromCards(isPlayer, "트래시할 스킬존 스킬을 선택하세요", new List<CardData>(owner5.SkillZone),
                        pickedSkill =>
                        {
                            owner5.RemoveFromSkillZone(pickedSkill);
                            owner5.AddToTrash(pickedSkill);
                            EffectActions.SelectUnit(isPlayer, isPlayer,
                                (l, u) => true,
                                "관통[N]을 부여할 아군을 선택하세요",
                                allyLane2 =>
                                {
                                    _grantedPenetration[Key(isPlayer, allyLane2)] = value;
                                    Debug.Log($"[액티브] {card.CardName} → 레인{allyLane2} 관통[{value}] 부여");
                                });
                        },
                        aiPick: list => list[0]);
                    break;
                }

                // BT03 액티브:메인 — 스킬존 스킬 1장 트래시 → 트래시에서 그 코스트 이하 아이템/유닛 회수
                case "ActiveMainSkillZoneTrashRecoverLowerCost":
                {
                    var owner6 = GetOwner(isPlayer);
                    if (owner6.SkillZone.Count == 0) break;
                    EffectActions.PickFromCards(isPlayer, "트래시할 스킬존 스킬을 선택하세요", new List<CardData>(owner6.SkillZone),
                        pickedSkill2 =>
                        {
                            owner6.RemoveFromSkillZone(pickedSkill2);
                            owner6.AddToTrash(pickedSkill2);
                            int costCap = pickedSkill2.Cost;
                            var recCands = owner6.TrashPile
                                .Where(c => c.Cost <= costCap && (c.Type == CardType.Unit || c.Type == CardType.Item))
                                .ToList();
                            if (recCands.Count == 0) return;
                            EffectActions.PickFromCards(isPlayer, $"트래시에서 {costCap}코 이하 카드를 회수하세요", recCands,
                                recPicked =>
                                {
                                    owner6.RemoveFromTrash(recPicked);
                                    owner6.AddToHand(recPicked);
                                    HandView.Instance?.RefreshHand();
                                    Debug.Log($"[액티브] {card.CardName} → {recPicked.CardName} 회수");
                                },
                                aiPick: list2 => list2.OrderByDescending(c => c.Cost).First());
                        },
                        aiPick: list => list.OrderByDescending(c => c.Cost).First());
                    break;
                }

                // BT03 액티브:메인 — 암드(아이템 장착) 상태: 덱 위 N장 트래시, 그중 아이템 1장마다 히트+1
                case "ArmedActiveMillHitBoostPerItem":
                {
                    if (FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count == 0) break;
                    var owner7 = GetOwner(isPlayer);
                    int take2 = Mathf.Min(value, owner7.DrawPile.Count);
                    int itemCount = 0;
                    for (int mi = 0; mi < take2; mi++)
                    {
                        var c = owner7.DrawPile[0];
                        owner7.RemoveFromDeckAt(0);
                        owner7.AddToTrash(c);
                        if (c.Type == CardType.Item) itemCount++;
                    }
                    if (itemCount > 0) AddTurnHitBoost(isPlayer, lane, itemCount);
                    Debug.Log($"[액티브] {card.CardName} → 밀링 {take2}장, 아이템 {itemCount}장 → 히트+{itemCount}");
                    break;
                }
            }
        }

        // 장착 아이템의 액티브 효과도 함께 처리 (예: ST11 독사의 손길)
        foreach (var item in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
        {
            foreach (var et in item.EffectTypes)
            {
                var (type, value) = Parse(et);
                switch (type)
                {
                    // BT07-085: 패 아이템 1장 트래시 → 인접 레인(상대 유닛 있는) 강제 공격
                    case "ItemActiveAttackAdjacentLane":
                        BT07_ItemActiveAttackAdjacentLane(isPlayer, lane);
                        break;

                    // BT07-087: 트래시 논트리거 카드 N장 덱 밑 → 드로우M
                    case "ItemActiveAttackReturnCardsDraw":
                    {
                        var pp7 = et.Split(':');
                        int retN = pp7.Length > 1 ? int.Parse(pp7[1]) : 0;
                        int drawM = pp7.Length > 2 ? int.Parse(pp7[2]) : 0;
                        BT07_ItemActiveReturnCardsDraw(isPlayer, retN, drawM);
                        break;
                    }

                    // ST11 독사의 손길: 자신 드로우N → 상대도 드로우N
                    case "ItemActiveDrawBoth":
                        EffectActions.Draw(isPlayer, value);
                        EffectActions.Draw(!isPlayer, value);
                        Debug.Log($"[아이템 액티브] {item.CardName} → 자신 드로우{value}, 상대도 드로우{value}");
                        break;

                    // BT04-083 ItemActiveAttackHitBoostByFieldTrash:_:_:3:2 — 이 턴 필드 트래시 아군 1+ 히트+1, 3+ 히트+2
                    case "ItemActiveAttackHitBoostByFieldTrash":
                    {
                        var pp = et.Split(':');
                        int t2 = pp.Length > 3 && int.TryParse(pp[3], out int a) ? a : 3;
                        int trashed = _unitsTrashedThisTurn[Idx(isPlayer)];
                        if (trashed >= t2) AddTurnHitBoost(isPlayer, lane, 2);
                        else if (trashed >= 1) AddTurnHitBoost(isPlayer, lane, 1);
                        break;
                    }

                    // BT05-082 ItemActiveDrawDiscard:N — 드로우N → 패1 트래시
                    case "ItemActiveDrawDiscard":
                        EffectActions.Draw(isPlayer, value);
                        EffectActions.DiscardHand(isPlayer, 1);
                        break;

                    // BT05-080 ItemActiveAttackMoveItem — 아군 장착 아이템 1장을 다른 아군에 이동
                    case "ItemActiveAttackMoveItem":
                        BT05_ItemActiveMoveItem(isPlayer, item);
                        break;

                    // SB02-024 ItemActiveMillReturnCards:N — 덱top N 트래시 → 트래시 트리거없는 카드 N장 덱 맨 아래로
                    case "ItemActiveMillReturnCards":
                        EffectActions.Mill(isPlayer, value);
                        SB02_ReturnTrashToBottom(isPlayer, value);
                        break;

                    // SB02-040 ItemActiveSwapWithDamageZone — 대미지 존 아이템 1장 이 유닛에 사이즈 무시 장착
                    case "ItemActiveSwapWithDamageZone":
                    {
                        var dzItems = GetOwner(isPlayer).DamageZone.Where(c => c.Type == CardType.Item).ToList();
                        if (dzItems.Count == 0) break;
                        EffectActions.PickFromCards(isPlayer, "장착할 대미지 존 아이템을 선택하세요", dzItems,
                            pk => { GetOwner(isPlayer).RemoveFromDamageZone(pk); FieldManager.Instance.EquipItem(isPlayer, lane, pk);
                                    DamageZoneView.NotifyDamageChanged(); HUDView.NotifyStatusChanged(); },
                            aiPick: l => l.OrderByDescending(c => c.Cost).First());
                        break;
                    }

                    // SB02-032 ItemActiveAttackZeroCost — 이 유닛 이번 턴 0코스트 (근사: 로그만)
                    case "ItemActiveAttackZeroCost":
                        Debug.Log($"[SB02] {item.CardName} → 장착 유닛 이번 턴 0코스트 (사이즈 계산 근사 미반영)");
                        break;

                    // BT03 아이템 액티브: 다른 엑시트 아군의 엑시트 효과 복사 발동 (ItemActiveCopyExitEffectFromAlly)
                    case "ItemActiveCopyExitEffectFromAlly":
                    {
                        var exitCands = new List<(int, CardData)>();
                        for (int ii = 0; ii < 3; ii++)
                        {
                            var u = FieldManager.Instance.GetUnit(isPlayer, ii);
                            if (u != null && u != card && u.Keywords.Contains("엑시트"))
                                exitCands.Add((ii, u));
                        }
                        if (exitCands.Count == 0) break;
                        EffectActions.PickFromCards(isPlayer, "엑시트 효과를 복사할 아군을 선택하세요",
                            exitCands.Select(t => t.Item2).ToList(),
                            pickedUnit =>
                            {
                                int pLane = exitCands.First(t => t.Item2 == pickedUnit).Item1;
                                // 해당 유닛의 엑시트 효과를 트래시 없이 발동 (근사: OnUnitTrashed 직접 호출 미지원 — 효과 only)
                                // 해당 유닛의 엑시트 효과를 OnUnitTrashed를 통해 발동 (트래시 없이 효과만)
                            // ※ 유닛을 실제로 트래시하지 않으므로 엑시트 효과만 수동 발동
                            foreach (var exitEt in pickedUnit.EffectTypes)
                            {
                                var (eType, eVal) = Parse(exitEt);
                                if (!eType.StartsWith("Exit")) continue;
                                switch (eType)
                                {
                                    case "ExitLevelUp": EffectActions.LevelUp(isPlayer); break;
                                    case "ExitDraw": EffectActions.Draw(isPlayer, eVal > 0 ? eVal : 1); break;
                                    case "ExitForceDiscard": ApplyForceDiscard(!isPlayer); break;
                                    default: Debug.Log($"[아이템 액티브 엑시트 복사] {eType} 미지원 — 스킵"); break;
                                }
                            }
                                Debug.Log($"[아이템 액티브] {item.CardName} → {pickedUnit.CardName} 엑시트 효과 복사");
                            },
                            aiPick: list => list.OrderByDescending(c => c.Cost).First());
                        break;
                    }

                    // BT03 아이템 액티브: 패1 트래시 → 상대 유닛 파워를 고정값 N으로 설정 (이번 턴)
                    case "ItemActiveDiscardSetEnemyPower":
                    {
                        EffectActions.DiscardHandOptional(isPlayer, 1, discardedI =>
                        {
                            if (discardedI < 1) return;
                            EffectActions.SelectUnit(isPlayer, !isPlayer,
                                (l, u) => true,
                                $"파워를 {value}으로 고정할 상대 유닛을 선택하세요",
                                enemyLane =>
                                {
                                    var enemy = FieldManager.Instance.GetUnit(!isPlayer, enemyLane);
                                    if (enemy != null)
                                    {
                                        // 현재 파워를 value로 만드는 디버프 (현재파워 - value)
                                        int curPow = GetEffectivePower(!isPlayer, enemyLane, enemy);
                                        AddTurnBoost(!isPlayer, enemyLane, value - curPow);
                                        Debug.Log($"[아이템 액티브] {item.CardName} → {enemy.CardName} 파워={value}로 고정");
                                    }
                                });
                        });
                        break;
                    }

                    // BT03 아이템 액티브: 덱 위 N장 트래시 → 그중 아이템 있으면 파워+M
                    case "ItemActiveMillPowerBoostIfItem":
                    {
                        var parts4 = et.Split(':');
                        int millN = parts4.Length >= 2 ? int.Parse(parts4[1]) : 0;
                        int boostM = parts4.Length >= 3 ? int.Parse(parts4[2]) : 0;
                        var ownerI = GetOwner(isPlayer);
                        int takeI = Mathf.Min(millN, ownerI.DrawPile.Count);
                        bool foundItem = false;
                        for (int mi2 = 0; mi2 < takeI; mi2++)
                        {
                            var c = ownerI.DrawPile[0];
                            ownerI.RemoveFromDeckAt(0);
                            ownerI.AddToTrash(c);
                            if (c.Type == CardType.Item) foundItem = true;
                        }
                        if (foundItem) AddTurnBoost(isPlayer, lane, boostM);
                        Debug.Log($"[아이템 액티브] {item.CardName} → 밀링{takeI}장, 아이템 발견:{foundItem} → 파워+{(foundItem ? boostM : 0)}");
                        break;
                    }
                }
            }
        }
    }

    // AI용 즉시 발동 (패 자동 선택)
    private void ApplyActiveReturnCardDebuff(bool isPlayer, int lane, int debuffValue)
    {
        var owner = GetOwner(isPlayer);
        if (owner.Hand.Count == 0) return;
        var picked = owner.Hand[0];
        owner.RemoveFromHand(picked);
        owner.InsertIntoDeck(Random.Range(0, owner.DrawPile.Count + 1), picked);
        AddTurnBoost(!isPlayer, lane, -debuffValue);
        Debug.Log($"[액티브-AI] 아니스 → {picked.CardName} 덱에 삽입, 조우 파워-{debuffValue}");
    }

    private void ApplyActiveTrashBuffBase(bool isPlayer)
    {
        for (int i = 0; i < 3; i++)
        {
            var unit = FieldManager.Instance.GetUnit(isPlayer, i);
            if (unit != null && unit.Faction != null && unit.Faction.StartsWith("베이스"))
            {
                string key = Key(isPlayer, i);
                _turnHitBoosts[key] = _turnHitBoosts.GetValueOrDefault(key) + 1;
                Debug.Log($"[액티브] 베이스 유닛 {unit.CardName} 히트+1");
            }
        }
    }

    public int GetEffectiveHit(bool isPlayer, int lane, CardData card)
    {
        int hit = card.Hit
            + _turnHitBoosts.GetValueOrDefault(Key(isPlayer, lane))
            + _opponentTurnHitBoosts.GetValueOrDefault(Key(isPlayer, lane));
        foreach (var item in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
            foreach (var et in item.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "ItemHitBoost") hit += value;
            }
        // ST07 비르기타: 이 턴 트래시된 자신 유닛 1장 이상이면 히트+1
        if (card.EffectTypes.Any(e => e.StartsWith("PassiveAllyTrashedBoost"))
            && _unitsTrashedThisTurn[Idx(isPlayer)] >= 1)
            hit += 1;
        // BT01 전선(같은 레인에 상대 유닛 존재) 시 히트+N / BT02 추가 히트 패시브
        foreach (var et in card.EffectTypes)
        {
            var (type2, val2) = Parse(et);
            if (type2 == "PassiveFrontlineHitBoost" && FieldManager.Instance.GetUnit(!isPlayer, lane) != null)
                hit += val2;
            // BT01 레벨링크 히트+N: 리더 레벨 조건 충족 시
            if (type2 == "LevelLinkHitBoost")
            {
                var parts = et.Split(':');
                if (parts.Length >= 3 && int.TryParse(parts[1], out int lvReq) && int.TryParse(parts[2], out int hitVal))
                    if (GetOwner(isPlayer).LeaderLevel >= lvReq) hit += hitVal;
            }
            // BT03-020 트리나: 레벨링크N + 전선구축(3칸 참) → 히트+K (파워는 GetPassiveBonus)
            if (type2 == "LevelLinkFrontlinePowerHitBoost")
            {
                var parts = et.Split(':');
                if (parts.Length >= 4 && int.TryParse(parts[1], out int lv) && int.TryParse(parts[3], out int ht)
                    && GetOwner(isPlayer).LeaderLevel >= lv && AllLanesFilled(isPlayer)) hit += ht;
            }
            // SB01-021 일레그: 아이템 장착 자신 유닛 3장↑ → 히트+1
            if (type2 == "ArmedPowerHitIfThreeEquipped" && CountUnitsWithItems(isPlayer) >= 3) hit += 1;
            // BT02 루피: 베이스 아군 1장마다 히트+N
            if (type2 == "PassiveHitPerBaseAlly")
            {
                int baseCnt = 0;
                for (int i = 0; i < 3; i++)
                {
                    var u = FieldManager.Instance.GetUnit(isPlayer, i);
                    if (u?.Faction != null && u.Faction.StartsWith("베이스")) baseCnt++;
                }
                hit += baseCnt * val2;
            }
            // BT02 일레그: 아이템 장착 아군 3장 이상이면 히트+N
            if (type2 == "PassiveHitIfThreeEquipped")
            {
                int eqCnt = 0;
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) != null
                        && FieldManager.Instance.GetEquippedItems(isPlayer, i).Count > 0)
                        eqCnt++;
                if (eqCnt >= 3) hit += val2;
            }
            // BT02 암드 히트+N
            if (type2 == "ArmedHitBoost" && FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count > 0)
                hit += val2;
        }
        hit += GetBT04HitBonus(isPlayer, lane, card); // BT04 대미지존 소속 조건 히트
        hit += GetBT05HitBonus(isPlayer, lane, card); // BT05 레벨링크 히트
        hit += GetBT07HitBonus(isPlayer, lane, card); // BT07 포지션:사이드 이브 아이템 조건 히트
        hit += GetSB02HitBonus(isPlayer, lane, card); // SB02 패≥8/믹스 이스케이프 오라 히트
        // BT06-072: 이 레인에 오라가 걸려있으면 히트를 가산이 아니라 1로 고정 (덧셈 계산 이후 마지막에 적용)
        if (_bt06HitSetOneAura.Contains(Key(!isPlayer, lane))) hit = 1;
        return Mathf.Max(0, hit);
    }

    // 해당 카드가 액티브 효과를 가지고 있으면 true
    public bool HasActiveEffect(CardData card)
        => card.EffectTypes.Any(e => e.StartsWith("Active"));

    // ── 파워 계산 ────────────────────────────────────────────────

    // 이 유닛의 "실제 전투 수치". 전투·UI·AI 판단이 전부 이 하나를 써야 한다 —
    // 계산식이 두 벌이 되면 화면에 보이는 값과 실제 결과가 어긋난다.
    // ※ 방어 시에만 붙는 「디펜더 파워+N」은 상황 한정이라 GetDefenderPowerBonus로 분리(여기 포함 X).
    public int GetEffectivePower(bool isPlayer, int lane, CardData card)
    {
        int power = card.AttackPower;
        power += GetAttackBoost(isPlayer, lane);
        power += GetTurnBoost(isPlayer, lane);
        power += _opponentTurnBoosts.GetValueOrDefault(Key(isPlayer, lane));
        power += GetPassiveBonus(isPlayer, lane, card);
        power += GetItemPowerBonus(isPlayer, lane);
        power += GetLeaderPassiveBonus(isPlayer, card);
        return Mathf.Max(0, power);
    }

    // 방어 시 추가 파워 (DefenderPowerBoost, ItemDefenderPowerBoost)
    public int GetDefenderPowerBonus(bool isPlayer, int lane)
    {
        int bonus = 0;
        var unit = FieldManager.Instance.GetUnit(isPlayer, lane);
        if (unit != null)
        {
            foreach (var et in unit.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "DefenderPowerBoost") bonus += value;
            }
        }
        foreach (var item in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
            foreach (var et in item.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "ItemDefenderPowerBoost") bonus += value;
            }
        // ST11 명월 달비: 버프:1 액티브로 부여된 「디펜더 파워+N」 (상대 턴 끝까지)
        bonus += _grantedDefenderBoost.GetValueOrDefault(Key(isPlayer, lane));
        return bonus;
    }

    // ST11 명월 달비: 아군 1장에 「디펜더 파워+N」 부여 (상대 턴 끝까지)
    public void GrantDefenderBoost(bool isPlayer, int lane, int amount)
    {
        string key = Key(isPlayer, lane);
        _grantedDefenderBoost[key] = _grantedDefenderBoost.GetValueOrDefault(key) + amount;
    }

    // ST11 사도 모르페아: 코스트 상한이 있는 조건부 돌파 부여 (상대 턴 끝까지)
    public void GrantConditionalBreakthrough(bool isPlayer, int lane, int costLimit)
        => _conditionalBreakthroughGrants[Key(isPlayer, lane)] = costLimit;

    // 부여된 돌파 여부 확인 (BuffGuardianAllyBreakthrough / BuffActiveSkillZoneBreakthrough)
    // defender를 넘기면 코스트 상한부 조건부 돌파(모르페아)도 함께 확인
    public bool HasGrantedBreakthrough(bool isPlayer, int lane, CardData defender = null)
    {
        if (_breakthroughGrants.Contains(Key(isPlayer, lane))) return true;
        if (defender != null && _conditionalBreakthroughGrants.TryGetValue(Key(isPlayer, lane), out int costLimit))
            return defender.Cost <= costLimit;
        return false;
    }

    // ST06 리나크가 부여한 「어태커 듀얼리스트」 (이번 턴)
    public bool HasGrantedDuelist(bool isPlayer, int lane) => _grantedDuelistThisTurn.Contains(Key(isPlayer, lane));

    // BT03 PassiveItemEquipRestriction: isPlayer 측이 아이템을 장착할 때, 상대에 이 패시브가 있으면 false
    public bool IsItemEquipRestricted(bool isPlayer)
    {
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(!isPlayer, i);
            if (u?.EffectTypes != null && u.EffectTypes.Contains("PassiveItemEquipRestriction"))
                return true;
        }
        return false;
    }

    // BT03 EntryDiscardLockHighCostDeploy: 이번 턴 N코스트 이상 배치 불가
    public bool CanDeployCard(bool isPlayer, CardData card)
    {
        int lockCost = _highCostDeployLocked[Idx(isPlayer)];
        if (lockCost > 0 && card.Cost >= lockCost)
        {
            Debug.Log($"[배치 불가] {card.CardName}({card.Cost}코) — 이번 턴 {lockCost}코스트 이상 배치 불가");
            return false;
        }

        // BT07-069/070: 엔트리로 이번 턴 〈이브〉 배치 금지
        if (IsEveCard(card) && _bt07EveDeployBlocked[Idx(isPlayer)])
        {
            Debug.Log($"[배치 불가] {card.CardName} — 이번 턴 〈이브〉 배치 금지");
            return false;
        }
        // BT07-045 이브 리더 패시브: 필드에 이미 〈이브〉가 있으면 추가로 〈이브〉 배치 불가 (업그레이드는 이 체크 대상 아님)
        if (IsEveCard(card))
        {
            var owner = GetOwner(isPlayer);
            if (owner.Leader != null)
            {
                string let = owner.IsAwakened && !string.IsNullOrEmpty(owner.Leader.AwakenEffectType)
                    ? owner.Leader.AwakenEffectType : owner.Leader.BaseEffectType;
                if (!string.IsNullOrEmpty(let) && let.StartsWith("PassiveEveDeployRestriction"))
                {
                    bool hasEveOnField = false;
                    for (int i = 0; i < 3; i++)
                    {
                        var u = FieldManager.Instance.GetUnit(isPlayer, i);
                        if (u != null && IsEveCard(u)) { hasEveOnField = true; break; }
                    }
                    if (hasEveOnField)
                    {
                        Debug.Log($"[배치 불가] {card.CardName} — 필드에 이미 〈이브〉가 있어 배치 불가");
                        return false;
                    }
                }
            }
        }
        return true;
    }

    // BT02 헬름 파시브: 상대 N코스트 이상 유닛은 광전사로 취급 (내가 필드에 이 유닛이 있을 때)
    public bool HasGrantedBerserkerToUnit(bool attackerIsPlayer, int attackerLane, CardData attacker)
    {
        // 공격자의 상대편(방어자 쪽) 필드를 순회해 PassiveGrantBerserkerToHighCostEnemies 확인
        bool defIsPlayer = !attackerIsPlayer;
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(defIsPlayer, i);
            if (u?.EffectTypes == null) continue;
            foreach (var et in u.EffectTypes)
            {
                var (type, val) = Parse(et);
                if (type == "PassiveGrantBerserkerToHighCostEnemies" && attacker.Cost >= val)
                    return true;
            }
        }

        // SB01-020: 디펜더 가진 자신 유닛은 모두 "조우 유닛은 광전사" 획득 (그랜터가 필드에 있는 동안, 그랜터도 포함)
        bool hasSb01Granter = false;
        for (int gi = 0; gi < 3; gi++)
        {
            var gu = FieldManager.Instance.GetUnit(defIsPlayer, gi);
            if (gu != null && gu.EffectTypes.Contains("PassiveGrantDefendersBerserkProtect")) { hasSb01Granter = true; break; }
        }
        if (hasSb01Granter)
        {
            var defenderAtLane = FieldManager.Instance.GetUnit(defIsPlayer, attackerLane);
            if (defenderAtLane != null && defenderAtLane.Keywords != null && defenderAtLane.Keywords.Contains("디펜더"))
                return true;
        }
        return false;
    }

    private int GetItemPowerBonus(bool isPlayer, int lane)
    {
        int bonus = 0;
        foreach (var item in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
            foreach (var et in item.EffectTypes)
            {
                var (type, value) = Parse(et);
                if (type == "ItemPowerBoost") bonus += value;
                // BT04 ItemOwnTurnPowerBoost / BT05 ItemPassiveTurnPowerBoost: 자신의 턴 동안 파워+N
                else if ((type == "ItemOwnTurnPowerBoost" || type == "ItemPassiveTurnPowerBoost")
                         && TurnManager.Instance != null && TurnManager.Instance.IsPlayerTurn == isPlayer)
                    bonus += value;
                // BT05 ItemPassivePowerPerEquipped: 장착 아이템 1장마다 파워+N
                else if (type == "ItemPassivePowerPerEquipped")
                    bonus += value * FieldManager.Instance.GetEquippedItems(isPlayer, lane).Count;
                // BT05 ItemMixPassivePowerBoost: 믹스면 파워+N
                else if (type == "ItemMixPassivePowerBoost")
                {
                    var u = FieldManager.Instance.GetUnit(isPlayer, lane);
                    if (u != null && HasMixCondition(isPlayer, u.Attribute)) bonus += value;
                }
            }
        foreach (var item in FieldManager.Instance.GetEquippedItems(isPlayer, lane))
            bonus += GetBT07ItemPowerBonus(isPlayer, lane, item); // BT07 포지션:센터/아이템 수 참조 아이템 파워
        return bonus;
    }

    // ※ GetEffectivePower 안에서 호출된다 — 여기서 GetEffectivePower를 (간접으로도) 부르면 무한 재귀.
    //   EvaluateLeaderPassive는 턴/키워드/필드 점유만 보므로 안전.
    public int GetLeaderPassiveBonus(bool isPlayer, CardData card)
    {
        var owner = GetOwner(isPlayer);
        if (owner == null || card == null) return 0; // 씬 초기화 중 등 소유자 미설정 상황 방어
        var leader = owner.Leader;
        if (leader == null) return 0;

        int bonus = EvaluateLeaderPassive(leader.BaseEffectType, isPlayer, card);

        // 각성 시 각성면 패시브 추가 적용 (ST05 프리바티 등)
        if (GetOwner(isPlayer).IsAwakened)
            bonus += EvaluateLeaderPassive(leader.AwakenEffectType, isPlayer, card);

        // fallback: 텍스트 기반 (BaseEffectType 미설정 레거시)
        if (bonus == 0 && string.IsNullOrEmpty(leader.BaseEffectType))
        {
            if (leader.BaseEffectText.Contains("모든 자신 유닛의 파워+1000"))
                return TurnManager.Instance.IsPlayerTurn == isPlayer ? 1000 : 0;
            if (leader.BaseEffectText.Contains("엑시트를 가진") && card.Keywords.Contains("엑시트"))
                return 1000;
        }

        return bonus;
    }

    private int EvaluateLeaderPassive(string effectType, bool isPlayer, CardData card)
    {
        if (string.IsNullOrEmpty(effectType)) return 0;
        var (type, value) = Parse(effectType);

        switch (type)
        {
            case "PassiveAllUnitsBoost":
                // 라피: 자신의 턴 동안 전체+N
                return TurnManager.Instance.IsPlayerTurn == isPlayer ? value : 0;

            case "PassiveOpponentTurnAllUnitsBoost":
                // 도로시: 상대의 턴 동안 전체+N
                return TurnManager.Instance.IsPlayerTurn != isPlayer ? value : 0;

            case "PassiveExitUnitsBoost":
                // 모더니아: 엑시트 유닛+N
                return card.Keywords.Contains("엑시트") ? value : 0;

            case "PassiveArmedAlliesBoost":
                // ST05 프리바티(각성면): 암드 가진 유닛+N
                return card.Keywords.Contains("암드") ? value : 0;

            case "PassiveFrontlineAllUnitsBoost":
                // BT03-018 나가: 자신의 3개 유닛 존이 모두 차 있으면 자신 모든 유닛+N
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) == null) return 0;
                return value;

            case "PassiveSizeBoost":
                // 사이즈 보너스는 전투와 무관
                return 0;
        }
        return 0;
    }

    // ── 리더 액티브 (각성면 "액티브: 메인" — 턴당 1회) ──────────────

    private bool[] _leaderActiveUsed = new bool[2];

    // 구현된 리더 액티브 타입 목록
    private static readonly HashSet<string> ImplementedLeaderActives = new()
    {
        "ActiveBuffZeroCostAlly",       // ST06 리나크
        "ActiveDiscardFactionHitBoost", // ST07 페네
        "ActiveRecoverSkillBySkillZone",// ST11 모르페아
        "ActiveDiscardRevealDeploy",    // ST08 수아
        "ActiveAttackDiscardExtraAttack",// ST10 빌헬미나 (어택 페이즈)
        "ActiveGrantEscapeDamage",      // ST09 아비게일
        "ActiveMainHandFactionToDamageZoneRecoverByCost", // BT04-041 셰나
        "ActiveAttackCopyAllyActiveAttack", // BT06-001 와일드독 루벤시아
        "ActiveMainDiscardRecoverNonTriggerSkillByCost", // BT05-001 훈련소장 카티야
        "ActiveBuffNextDeployUnit",       // BT04-002 조장 아룬카
        "ActiveMainTrashAllyBuffAlly",    // BT04-042 용의 반려 셰나
        "ActiveMillReturnCard",           // SB02-017 노블 에이스 바니 헤이즈
        "ActiveDiscardRecoverCreditUnit", // SB02-001 악역 영애 아드리아나
        "ActiveAttackHitBoostLowCost",    // SB02-009 전학생 샬럿 (어택)
        "ActiveSwapDamageZoneCard",       // SB02-033 장난꾸러기 곰돌이 메이드
        "ActiveMainSkillZoneDebuffEnemy",          // BT03-001 라피 : 레드 후드
        "ActiveMainConditionalForceDiscard",       // BT03-035 리틀 머메이드
        "ActiveMainSkillZoneTrashActivateEntryEffect", // BT03-052 마스트
        "ActiveMainMillDrawIfItem",                // BT03-069 레비아탄
        "ActiveMainMillActivateSkill",             // BT06-043 이클립스
        "ActiveMainDualChoiceGrantExitReturnOrTrashAlly", // BT05-032 니키
        "ActiveMainDiscardRecoverItemEquipSizeIgnore",    // BT05-063 아야
    };

    // "ActiveAttack~" 타입은 어택 페이즈, 나머지는 메인 페이즈에 발동
    private static PhaseType RequiredPhaseFor(string type)
        => type.StartsWith("ActiveAttack") ? PhaseType.Attack : PhaseType.Main;

    private string GetLeaderActiveType(bool isPlayer)
    {
        var owner = GetOwner(isPlayer);
        if (owner.Leader == null) return null;
        string et = owner.IsAwakened && !string.IsNullOrEmpty(owner.Leader.AwakenEffectType)
            ? owner.Leader.AwakenEffectType
            : owner.Leader.BaseEffectType;
        if (string.IsNullOrEmpty(et) || !et.StartsWith("Active")) return null;
        return et;
    }

    // SB02-025: 리더 패시브 "PassiveHandLimitBoost:N" → 패 제한 보너스. 각성면 패시브면 각성 시에만.
    public int GetHandLimitBonus(bool isPlayer)
    {
        var owner = GetOwner(isPlayer);
        if (owner == null || owner.Leader == null) return 0;
        int bonus = 0;
        void Scan(string et, bool active)
        {
            if (!active || string.IsNullOrEmpty(et)) return;
            var (t, v) = Parse(et);
            if (t == "PassiveHandLimitBoost") bonus += v;
        }
        Scan(owner.Leader.BaseEffectType, true);              // 기본면 패시브는 항상
        Scan(owner.Leader.AwakenEffectType, owner.IsAwakened); // 각성면 패시브는 각성 시에만
        return bonus;
    }

    // 리더 액티브를 지금 못 쓰는 이유 (null = 쓸 수 있음).
    //  · 화면 안내용. 클릭했는데 아무 일도 안 일어나면 플레이어는 "기능이 고장났다"고 받아들인다.
    //  · 세부 발동 조건은 액티브마다 달라 "조건 미충족"으로 묶는다(아래 switch를 복제하지 않기 위해).
    //  · 액티브가 없거나 미구현인 리더면 null — 안내할 것 자체가 없다(그냥 카드 확대만 되면 됨).
    public string LeaderActiveBlockReason(bool isPlayer)
    {
        string et = GetLeaderActiveType(isPlayer);
        if (et == null) return null;
        var (type, _) = Parse(et);
        if (!ImplementedLeaderActives.Contains(type)) return null;

        if (TurnManager.Instance.IsPlayerTurn != isPlayer) return "자신의 턴에만 발동할 수 있습니다.";
        if (_leaderActiveUsed[Idx(isPlayer)])              return "리더 액티브는 턴당 1회만 발동할 수 있습니다.";

        var need = RequiredPhaseFor(type);
        if (TurnManager.Instance.CurrentPhase != need)
            return need == PhaseType.Attack ? "어택 페이즈에만 발동할 수 있습니다."
                                            : "메인 페이즈에만 발동할 수 있습니다.";

        return CanUseLeaderActive(isPlayer) ? null : "발동 조건을 충족하지 않았습니다.";
    }

    // 지금 리더 액티브를 발동할 수 있는가 (자신 메인 페이즈, 턴당 1회, 발동 조건 충족)
    public bool CanUseLeaderActive(bool isPlayer)
    {
        if (_leaderActiveUsed[Idx(isPlayer)]) return false;
        if (TurnManager.Instance.IsPlayerTurn != isPlayer) return false;

        string et = GetLeaderActiveType(isPlayer);
        if (et == null) return false;
        var (type, value) = Parse(et);
        if (!ImplementedLeaderActives.Contains(type)) return false;
        if (TurnManager.Instance.CurrentPhase != RequiredPhaseFor(type)) return false;

        var owner = GetOwner(isPlayer);
        switch (type)
        {
            case "ActiveBuffZeroCostAlly": // 0코스트 아군이 있어야 함 (성약으로 부여된 것 포함)
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) is { } zu
                        && (zu.Cost == 0 || _zeroCostGrants.Contains(Key(isPlayer, i)))) return true;
                return false;
            case "ActiveDiscardFactionHitBoost":
            case "ActiveDiscardRevealDeploy":
                return owner.Hand.Count > 0;
            // ★ "패를 1장 트래시한다. 그러면 트래시에서 ~ 고른다" 순서라, 버리는 카드 자신도
            //   회수 대상이 된다(실행부가 MoveCard 후에 RecoverFromTrash를 부른다).
            //   트래시만 보면 게임 초반처럼 트래시가 빈 상황에서 조건을 영영 못 채워
            //   "리더를 눌러도 발동이 안 된다"가 된다 — 실제로 보고된 증상.
            case "ActiveMainDiscardRecoverNonTriggerSkillByCost": // BT05-001 카티야
            {
                bool Q(CardData c) => c.Type == CardType.Skill && !c.IsTrigger && c.Cost <= value;
                return owner.Hand.Count > 0 && (owner.TrashPile.Any(Q) || owner.Hand.Any(Q));
            }
            case "ActiveBuffNextDeployUnit":   // 다음 배치 유닛 버프 예약 — 언제나 가능
                return true;
            case "ActiveMainTrashAllyBuffAlly": // 트래시할 자신 유닛이 있어야
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) != null) return true;
                return false;
            case "ActiveMillReturnCard":       // 덱에 밀 카드가 있어야
                return owner.DrawPile.Count > 0;
            // SB02 아드리아나 — 카티야와 같은 구조(버린 카드도 회수 대상. 이름 제약 없음).
            //  히트 상한은 "버린 장수"라, 최대치(패 1~3장)로도 못 넘기는 유닛뿐이면 켜봐야 소용없다.
            //  ※ "패가 2장 이하라면"은 몇 장 버릴지에 따라 달라져 여기서 판정하지 않는다.
            //    카드 문구대로 트래시는 확정되고 회수만 불발되며, 그때 토스트로 이유를 알린다.
            case "ActiveDiscardRecoverCreditUnit":
            {
                int maxDiscard = Mathf.Min(3, owner.Hand.Count);
                bool Q(CardData c) => c.Type == CardType.Unit && !c.IsTrigger
                                      && c.Keywords.Contains("크레딧") && c.Hit <= maxDiscard;
                return owner.Hand.Count > 0 && (owner.TrashPile.Any(Q) || owner.Hand.Any(Q));
            }
            case "ActiveAttackHitBoostLowCost": // 비트리거 + N코 이하 자신 유닛 존재
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) is { } u && !u.IsTrigger && u.Cost <= value) return true;
                return false;
            case "ActiveSwapDamageZoneCard": // 아이템 장착 자신 유닛 有 + 패에 비트리거 카드 有
            {
                bool hasEquipped = false;
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) != null
                        && FieldManager.Instance.GetEquippedItems(isPlayer, i).Count > 0) { hasEquipped = true; break; }
                return hasEquipped && owner.Hand.Any(c => !c.IsTrigger);
            }
            case "ActiveMainSkillZoneDebuffEnemy": // 스킬존 스킬 ≥1 + 상대 유닛 존재
                if (owner.SkillZone.Count(c => c.Type == CardType.Skill) < 1) return false;
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(!isPlayer, i) != null) return true;
                return false;
            case "ActiveMainConditionalForceDiscard": // 상대 패 ≥ N + 자신 패 ≥1
                return GetOwner(!isPlayer).Hand.Count >= value && owner.Hand.Count > 0;
            case "ActiveMainSkillZoneTrashActivateEntryEffect": // 스킬존에 N코 스킬 + 자신 유닛 존재
                if (!owner.SkillZone.Any(c => c.Type == CardType.Skill && c.Cost == value)) return false;
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) != null) return true;
                return false;
            case "ActiveMainMillDrawIfItem":   // 덱에 밀 카드
            case "ActiveMainMillActivateSkill":
                return owner.DrawPile.Count > 0;
            case "ActiveMainDualChoiceGrantExitReturnOrTrashAlly": // 자신 유닛 존재
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) != null) return true;
                return false;
            // BT05-063 아야 — 위 둘과 달리 "카드명이 다른"이라 버린 카드는 회수 대상이 될 수 없다.
            //  → 트래시에 아이템이 미리 있어야 한다(손패에 있는 건 도움이 안 됨).
            //  ★ 그리고 "필드에 있는 자신 유닛에 장착"이라 장착 대상이 없으면
            //    패만 버리고 아무것도 못 얻는다 → 아군 유닛 존재도 조건에 포함.
            case "ActiveMainDiscardRecoverItemEquipSizeIgnore":
            {
                bool hasAlly = false;
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) != null) { hasAlly = true; break; }
                return owner.Hand.Count > 0 && hasAlly
                    && owner.TrashPile.Any(c => c.Type == CardType.Item);
            }
            case "ActiveRecoverSkillBySkillZone":
                return owner.SkillZone.Count >= 1
                    && owner.TrashPile.Any(c => c.Type == CardType.Skill && c.Cost < owner.SkillZone.Count);
            case "ActiveAttackDiscardExtraAttack": // 패 필요 + N코 이하 아군 존재
                if (owner.Hand.Count == 0) return false;
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) is { } u && u.Cost <= value) return true;
                return false;
            case "ActiveGrantEscapeDamage": // 부여할 아군 유닛이 있어야 함
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) != null) return true;
                return false;
            case "ActiveAttackCopyAllyActiveAttack": // 액티브:어택 가진 다른 아군 유닛이 있어야 함
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) is { } au && HasActiveEffectForPhase(au, PhaseType.Attack))
                        return true;
                return false;
        }
        return true;
    }

    // 리더 액티브 발동. 성공 시 true (턴당 1회 소모)
    public bool TriggerLeaderActive(bool isPlayer)
    {
        if (!CanUseLeaderActive(isPlayer)) return false;

        string et = GetLeaderActiveType(isPlayer);
        var (type, value) = Parse(et);
        var owner = GetOwner(isPlayer);
        _leaderActiveUsed[Idx(isPlayer)] = true;
        // 플레이어가 선택 팝업에서 취소하면 턴당 1회 사용권을 돌려준다
        System.Action refund = () =>
        {
            _leaderActiveUsed[Idx(isPlayer)] = false;
            Debug.Log("[리더 액티브] 취소 — 사용권 반환");
        };
        Debug.Log($"[리더 액티브] {owner.Leader.LeaderName} → {type}");

        switch (type)
        {
            // BT04-041 셰나: 패에서 《용의 계곡》/《혹한의 날들》 카드 1장을 대미지 존에 → 트래시존에서 그 코스트 이하 카드 1장 패로
            case "ActiveMainHandFactionToDamageZoneRecoverByCost":
            {
                var parts = et.Split(':');
                string fa = parts.ElementAtOrDefault(1) ?? "";
                string fb = parts.ElementAtOrDefault(2) ?? "";
                bool HasF(CardData c) =>
                    (c.Faction != null && (c.Faction.Contains(fa) || c.Faction.Contains(fb)))
                    || c.Keywords.Contains(fa) || c.Keywords.Contains(fb);
                var cands = owner.Hand.Where(HasF).ToList();
                if (cands.Count == 0) { refund(); break; }
                EffectActions.PickFromCards(isPlayer, "대미지 존에 놓을 카드를 선택하세요", cands,
                    picked =>
                    {
                        owner.RemoveFromHand(picked);
                        PlaceToDamageZone(isPlayer, picked);
                        EffectActions.RecoverFromTrash(isPlayer, c => c.Cost <= picked.Cost,
                            $"트래시에서 회수할 {picked.Cost}코스트 이하 카드를 선택하세요");
                    },
                    aiPick: l => l.OrderByDescending(c => c.Cost).First(),
                    onCancel: refund);
                break;
            }

            // BT05-001 훈련소장 카티야: 패 1장 트래시 → 트래시에서 트리거 없는 N코 이하 스킬 1장 패로
            case "ActiveMainDiscardRecoverNonTriggerSkillByCost":
                EffectActions.PickFromCards(isPlayer, "트래시할 패를 선택하세요",
                    new List<CardData>(owner.Hand),
                    picked =>
                    {
                        EffectActions.MoveCard(isPlayer, picked, CardZone.Hand, CardZone.Trash);
                        EffectActions.RecoverFromTrash(isPlayer,
                            c => c.Type == CardType.Skill && !c.IsTrigger && c.Cost <= value,
                            $"트래시에서 회수할 트리거 없는 {value}코스트 이하 스킬을 선택하세요");
                    },
                    onCancel: refund);
                break;

            // BT04-002 조장 아룬카: 이 턴 다음에 배치하는 유닛 1장 파워+N (턴 끝까지)
            case "ActiveBuffNextDeployUnit":
                _nextDeployPower[Idx(isPlayer)] += value;
                Debug.Log($"[리더 액티브] 다음 배치 유닛 파워+{value} 예약");
                break;

            // BT04-042 용의 반려 셰나: 자신 유닛1 트래시 → 자신 유닛1 파워+N(턴 끝까지)
            case "ActiveMainTrashAllyBuffAlly":
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
                    "트래시할 자신 유닛을 선택하세요",
                    pk1 =>
                    {
                        EffectActions.TrashUnit(isPlayer, pk1);
                        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
                            $"파워+{value}를 줄 자신 유닛을 선택하세요",
                            pk2 => EffectActions.Buff(isPlayer, pk2, value, 0, BoostUntil.EndOfTurn));
                    },
                    onCancel: refund);
                break;

            // SB02-017 노블 에이스 바니 헤이즈: 덱 맨 위 N장 트래시 → 트래시에서 트리거 없는 카드1 덱 맨 아래로
            case "ActiveMillReturnCard":
            {
                int m = Mathf.Min(value, owner.DrawPile.Count);
                for (int i = 0; i < m; i++)
                {
                    var top = owner.DrawPile[0];
                    owner.RemoveFromDeckAt(0);
                    owner.AddToTrash(top);
                }
                Debug.Log($"[리더 액티브] 덱 {m}장 밀");
                var cands = owner.TrashPile.Where(c => !c.IsTrigger).ToList();
                if (cands.Count == 0) break;
                EffectActions.PickFromCards(isPlayer, "덱 맨 아래에 놓을 트래시 카드를 선택하세요", cands,
                    picked =>
                    {
                        owner.RemoveFromTrash(picked);
                        owner.AddToDeckBottom(picked); // 덱 맨 아래
                    });
                break;
            }

            // SB02-001 악역 영애 아드리아나: 패 1~3장 트래시 → 트래시에서 크레딧 유닛1 회수 (근사)
            // SB02 아드리아나: 패 1~3장 트래시.
            //   "자신의 패가 2장 이하라면" + "히트가 이 효과로 트래시한 카드의 수 이하"인
            //   크레딧 비트리거 유닛 1장을 트래시에서 패로.
            // ※ 두 조건 모두 예전엔 빠져 있어 아무 크레딧 유닛이나 무제한으로 회수됐다.
            case "ActiveDiscardRecoverCreditUnit":
                EffectActions.DiscardHandOptional(isPlayer, Mathf.Min(3, owner.Hand.Count), d =>
                {
                    if (d < 1) { refund(); return; }

                    // ★ 버린 "뒤"의 패 수로 판정한다 — 트래시는 이미 확정됐으므로 되돌리지 않는다
                    //   (카드 문구가 "트래시한다. ~라면 ~할 수 있다" 순서라 회수만 불발된다).
                    if (owner.Hand.Count > 2)
                    {
                        Debug.Log($"[리더 액티브] 패가 {owner.Hand.Count}장(2장 초과) — 회수 조건 미충족");
                        ToastView.Show("패가 2장 이하가 아니라 회수하지 못했습니다.");
                        return;
                    }

                    // ★ 히트 상한 = 이번에 버린 장수
                    EffectActions.RecoverFromTrash(isPlayer,
                        c => c.Type == CardType.Unit && !c.IsTrigger
                             && c.Keywords.Contains("크레딧") && c.Hit <= d,
                        $"트래시에서 회수할 크레딧 유닛을 선택하세요 (히트 {d} 이하)");
                });
                break;

            // SB02-009 전학생 샬럿(어택): 비트리거 N코 이하 자신 유닛1 히트+1(상대턴끝). N코 이하 자신 유닛 3장↑이면 추가 히트+1
            case "ActiveAttackHitBoostLowCost":
            {
                int cheap = 0;
                for (int i = 0; i < 3; i++)
                    if (FieldManager.Instance.GetUnit(isPlayer, i) is { } cu && cu.Cost <= value) cheap++;
                int hitAmt = 1 + (cheap >= 3 ? 1 : 0);
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => !u.IsTrigger && u.Cost <= value,
                    $"히트+{hitAmt}를 줄 자신 유닛을 선택하세요",
                    pk => EffectActions.Buff(isPlayer, pk, 0, hitAmt, BoostUntil.EndOfOpponentTurn),
                    onCancel: refund);
                break;
            }

            // SB02-033 곰돌이 메이드: (아이템 장착 아군 有) 패 비트리거1 → 대미지존 → 대미지존에서 카드명 다른 카드1 패로
            case "ActiveSwapDamageZoneCard":
            {
                var handCands = owner.Hand.Where(c => !c.IsTrigger).ToList();
                if (handCands.Count == 0) { refund(); break; }
                EffectActions.PickFromCards(isPlayer, "대미지 존에 놓을 패를 선택하세요", handCands,
                    placed =>
                    {
                        owner.RemoveFromHand(placed);
                        PlaceToDamageZone(isPlayer, placed);
                        var dzCands = owner.DamageZone.Where(c => c.CardName != placed.CardName).ToList();
                        if (dzCands.Count == 0) return;
                        EffectActions.PickFromCards(isPlayer, "패에 넣을 대미지 존 카드를 선택하세요", dzCands,
                            got => { owner.RemoveFromDamageZone(got); owner.AddToHand(got); });
                    },
                    onCancel: refund);
                break;
            }

            // BT03-001 라피:레드후드: (스킬존 스킬≥1) 상대 유닛1 파워-N(턴끝까지)
            case "ActiveMainSkillZoneDebuffEnemy":
                EffectActions.SelectUnit(isPlayer, !isPlayer, (l, u) => true,
                    $"파워-{value}를 줄 상대 유닛을 선택하세요",
                    pk => EffectActions.Buff(!isPlayer, pk, -value, 0, BoostUntil.EndOfTurn),
                    onCancel: refund);
                break;

            // BT03-035 리틀머메이드: (상대 패≥N) 자신 패1 트래시 → 상대가 패1 골라 트래시
            case "ActiveMainConditionalForceDiscard":
                EffectActions.PickFromCards(isPlayer, "트래시할 자신의 패를 선택하세요",
                    new List<CardData>(owner.Hand),
                    mine =>
                    {
                        EffectActions.MoveCard(isPlayer, mine, CardZone.Hand, CardZone.Trash);
                        var opp = GetOwner(!isPlayer);
                        if (opp.Hand.Count == 0) return;
                        EffectActions.PickFromCards(!isPlayer, "트래시할 패를 선택하세요",
                            new List<CardData>(opp.Hand),
                            theirs => EffectActions.MoveCard(!isPlayer, theirs, CardZone.Hand, CardZone.Trash));
                    },
                    onCancel: refund);
                break;

            // BT03-052 마스트: 스킬존 N코 스킬1 트래시 → 자신 유닛1의 엔트리 재발동(근사: 엔트리 전체 재발동)
            case "ActiveMainSkillZoneTrashActivateEntryEffect":
            {
                var skills = owner.SkillZone.Where(c => c.Type == CardType.Skill && c.Cost == value).ToList();
                if (skills.Count == 0) { refund(); break; }
                EffectActions.PickFromCards(isPlayer, $"트래시할 {value}코스트 스킬을 선택하세요", skills,
                    sk =>
                    {
                        owner.RemoveFromSkillZone(sk); owner.AddToTrash(sk);
                        EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
                            "엔트리 효과를 재발동할 자신 유닛을 선택하세요",
                            pk => { var u = FieldManager.Instance.GetUnit(isPlayer, pk); if (u != null) OnUnitPlaced(isPlayer, pk, u); });
                    },
                    onCancel: refund);
                break;
            }

            // BT03-069 레비아탄: 덱 맨 위 N장 트래시 → 트래시된 아이템 ≥1이면 드로우1
            case "ActiveMainMillDrawIfItem":
            {
                int m = Mathf.Min(value, owner.DrawPile.Count);
                bool milledItem = false;
                for (int i = 0; i < m; i++)
                {
                    var t = owner.DrawPile[0]; owner.RemoveFromDeckAt(0); owner.AddToTrash(t);
                    if (t.Type == CardType.Item) milledItem = true;
                }
                Debug.Log($"[리더 액티브] 덱 {m}장 밀 (아이템={milledItem})");
                if (milledItem) EffectActions.Draw(isPlayer, 1);
                break;
            }

            // BT06-043 이클립스: 덱 맨 위 1장 트래시 → 그게 스킬이면 효과 발동(근사: 자동 발동)
            case "ActiveMainMillActivateSkill":
            {
                if (owner.DrawPile.Count == 0) break;
                var top = owner.DrawPile[0]; owner.RemoveFromDeckAt(0); owner.AddToTrash(top);
                Debug.Log($"[리더 액티브] 밀: {top.CardName}");
                if (top.Type == CardType.Skill) ExecuteSkillEffect(isPlayer, top, -1);
                break;
            }

            // BT05-032 니키: 아군1에 「엑시트 귀환」 부여 (근사)
            case "ActiveMainDualChoiceGrantExitReturnOrTrashAlly":
                EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true,
                    "「엑시트 귀환」을 부여할 자신 유닛을 선택하세요",
                    pk => _grantedExitReturn.Add(Key(isPlayer, pk)),
                    onCancel: refund);
                break;

            // BT05-063 아야: 패1 트래시 → 트래시 아이템1 아군에 사이즈 무시 장착
            // BT05-063 아야: 패1 트래시 → 트래시에서 "그 카드와 카드명이 다른" 아이템1을
            //                필드의 자신 유닛에 사이즈 무시 장착
            // ※ DiscardHandOptional은 개수만 돌려줘서 무엇을 버렸는지 알 수 없다.
            //   카드명 비교가 필요하므로 카티야와 같은 방식(PickFromCards + MoveCard)으로 버린다.
            case "ActiveMainDiscardRecoverItemEquipSizeIgnore":
                EffectActions.PickFromCards(isPlayer, "트래시할 패를 선택하세요",
                    new List<CardData>(owner.Hand),
                    picked =>
                    {
                        EffectActions.MoveCard(isPlayer, picked, CardZone.Hand, CardZone.Trash);

                        // ★ "이 효과로 트래시한 카드와 카드명이 다른" — 방금 버린 것과 같은 이름은 제외.
                        //   빠뜨리면 아이템을 버렸다가 그대로 되장착하는 공짜 우회가 된다.
                        var items = owner.TrashPile
                            .Where(c => c.Type == CardType.Item && c.CardName != picked.CardName)
                            .ToList();
                        if (items.Count == 0) return;

                        EffectActions.PickFromCards(isPlayer, "장착할 아이템을 선택하세요", items,
                            it => EffectActions.SelectUnit(isPlayer, isPlayer, (l, u) => true, "장착 대상 아군을 선택하세요",
                                al => { owner.RemoveFromTrash(it); FieldManager.Instance.EquipItem(isPlayer, al, it); }),
                            aiPick: l => l.OrderByDescending(c => c.Cost).First());
                    },
                    onCancel: refund);
                break;

            // ST06 리나크: 0코스트 아군 1장 이번 턴 파워+N, 히트+1
            case "ActiveBuffZeroCostAlly":
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => u.Cost == 0 || _zeroCostGrants.Contains(Key(isPlayer, l)), // 성약으로 부여된 0코스트 포함
                    $"파워+{value}, 히트+1을 줄 0코스트 유닛을 선택하세요",
                    picked => EffectActions.Buff(isPlayer, picked, value, 1, BoostUntil.EndOfTurn),
                    onCancel: refund);
                break;

            // ST07 페네: 패 1장 트래시 → 그 코스트 이하 《호문클루스》 아군 전체 이번 턴 히트+N
            case "ActiveDiscardFactionHitBoost":
                EffectActions.PickFromCards(isPlayer, "트래시할 패를 선택하세요",
                    new List<CardData>(owner.Hand),
                    picked =>
                    {
                        EffectActions.MoveCard(isPlayer, picked, CardZone.Hand, CardZone.Trash);
                        for (int i = 0; i < 3; i++)
                        {
                            var u = FieldManager.Instance.GetUnit(isPlayer, i);
                            if (u != null && u.Cost <= picked.Cost
                                && u.Faction != null && u.Faction.Contains("호문클루스"))
                                EffectActions.Buff(isPlayer, i, 0, value, BoostUntil.EndOfTurn);
                        }
                    },
                    onCancel: refund);
                break;

            // ST11 모르페아: 스킬존 수 미만 코스트 스킬을 트래시에서 회수
            case "ActiveRecoverSkillBySkillZone":
                EffectActions.RecoverFromTrash(isPlayer,
                    c => c.Type == CardType.Skill && c.Cost < owner.SkillZone.Count,
                    "트래시에서 회수할 스킬을 선택하세요",
                    onCancel: refund);
                break;

            // ST10 빌헬미나(어택 페이즈): 패 1장 트래시 → N코 이하 아군 1장 추가 공격 1회
            case "ActiveAttackDiscardExtraAttack":
                EffectActions.PickFromCards(isPlayer, "트래시할 패를 선택하세요",
                    new List<CardData>(owner.Hand),
                    picked =>
                    {
                        EffectActions.MoveCard(isPlayer, picked, CardZone.Hand, CardZone.Trash);
                        EffectActions.SelectUnit(isPlayer, isPlayer,
                            (l, u) => u.Cost <= value,
                            "추가 공격을 부여할 유닛을 선택하세요",
                            lane => AddExtraAttack(isPlayer, lane));
                    },
                    onCancel: refund);
                break;

            // ST08 수아: 패 1장 트래시 → 덱 위 N장 공개, 유닛이면 빈 존에 사이즈 무시 배치, 나머지 트래시
            case "ActiveDiscardRevealDeploy":
                EffectActions.PickFromCards(isPlayer, "트래시할 패를 선택하세요",
                    new List<CardData>(owner.Hand),
                    picked =>
                    {
                        EffectActions.MoveCard(isPlayer, picked, CardZone.Hand, CardZone.Trash);
                        // 덱 위 N장 공개 — 유닛은 빈 존에 배치, 나머지는 트래시
                        int take = Mathf.Min(Mathf.Max(value, 1), owner.DrawPile.Count);
                        for (int r = 0; r < take; r++)
                        {
                            var top = owner.DrawPile[0];
                            owner.RemoveFromDeckAt(0);
                            int emptyLane = -1;
                            for (int i = 0; i < 3; i++)
                                if (FieldManager.Instance.GetUnit(isPlayer, i) == null) { emptyLane = i; break; }
                            if (top.Type == CardType.Unit && emptyLane >= 0)
                            {
                                FieldManager.Instance.PlaceUnit(isPlayer, emptyLane, top);
                                OnUnitPlaced(isPlayer, emptyLane, top);
                                Debug.Log($"[리더 액티브] {top.CardName} 사이즈 무시 배치 (레인{emptyLane})");
                            }
                            else
                            {
                                owner.AddToTrash(top);
                                Debug.Log($"[리더 액티브] {top.CardName} 트래시 (배치 불가)");
                            }
                        }
                        EffectActions.RefreshUI();
                    },
                    onCancel: refund);
                break;

            // ST09 아비게일: 아군 1장에 다음 자신 턴까지 「이스케이프: 덱 밑+상대 N대미지」 부여
            case "ActiveGrantEscapeDamage":
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => true,
                    "「이스케이프: 덱 밑+상대에게 대미지」를 부여할 유닛을 선택하세요",
                    picked => _grantedEscapeDamage[Key(isPlayer, picked)] = value,
                    onCancel: refund);
                break;

            // BT06-001 와일드독 루벤시아: 액티브:어택 가진 다른 아군 1장 골라, 그 유닛의 액티브:어택 효과를 재발동
            case "ActiveAttackCopyAllyActiveAttack":
                EffectActions.SelectUnit(isPlayer, isPlayer,
                    (l, u) => HasActiveEffectForPhase(u, PhaseType.Attack),
                    "액티브:어택 효과를 복사 발동할 유닛을 선택하세요",
                    picked =>
                    {
                        var target = FieldManager.Instance.GetUnit(isPlayer, picked);
                        if (target != null) TriggerActiveEffect(isPlayer, picked, target);
                    },
                    onCancel: refund);
                break;
        }
        return true;
    }

    // ── 서브 효과 구현 ───────────────────────────────────────────

}
