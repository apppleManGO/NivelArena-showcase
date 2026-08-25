// Assets/Scripts/AI/AIController.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AIController : MonoBehaviour
{
    public static AIController Instance { get; private set; }

    [Header("AI 설정")]
    public float ActionDelay = 0.8f; // 카드 낼 때 딜레이 (자연스러운 연출)

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── 메인 페이즈: 카드 배치 ──────────────────────────────────

    public IEnumerator PlayTurn()
    {
        yield return new WaitForSeconds(ActionDelay);

        PlayerController ai = PlayerController.AIInstance;

        // 빈 레인 확인
        List<int> emptyLanes = GetEmptyLanes(false);
        if (emptyLanes.Count == 0)
        {
            Debug.Log("[AI] 빈 레인 없음 — 패스");
            yield break;
        }

        // 룰북 6.4.1.1.3: 레인별 이번 턴 배치 여부 추적
        var placedThisTurn = new HashSet<int>();

        // 룰북 6.4.1.1.2: 코스트 조건 만족하는 유닛, 파워 높은 순
        List<CardData> playable = ai.Hand
            .Where(c => c.Type == CardType.Unit
                     && (FieldManager.Instance.CanPlayCard(false, c, ai.Size)
                         || ExistsUpgradeLane(false, c, ai.Size)))
            .OrderByDescending(c => c.AttackPower)
            .ToList();

        foreach (CardData card in playable)
        {
            // 빈 레인 우선 배치 (BT06-061: 고비용 유닛 배치가 차단된 레인은 제외)
            var deployableLanes = emptyLanes
                .Where(l => !EffectSystem.Instance.IsLaneDeployBlocked(false, l, card))
                .ToList();
            if (deployableLanes.Count > 0 && FieldManager.Instance.CanPlayCard(false, card, ai.Size))
            {
                int lane = deployableLanes[Random.Range(0, deployableLanes.Count)];
                // ── 1단계 관문 시범 ── 플레이어와 동일한 관문 사용 (배치+Hand.Remove+OnUnitPlaced 중복 제거)
                if (GameActionGateway.Submit(new GameAction {
                        Type = GameActionType.PlaceUnit, IsPlayer = false, Lane = lane, Card = card }))
                {
                    emptyLanes.Remove(lane);
                    placedThisTurn.Add(lane);
                    AIHandView.NotifyHandChanged();
                    ToastView.Show($"AI: {card.CardName} → 레인{lane + 1} 배치");
                    Debug.Log($"[AI] {card.CardName}(코스트:{card.Cost}) → 레인{lane}");
                    yield return new WaitForSeconds(ActionDelay);
                }
                continue;
            }

            // 업그레이드 가능한 레인 탐색 (이번 턴 미배치 레인만)
            int upgradeLane = FindUpgradeLane(false, card, ai.Size, placedThisTurn);
            if (upgradeLane >= 0)
            {
                if (GameActionGateway.Submit(new GameAction {
                        Type = GameActionType.Upgrade, IsPlayer = false, Lane = upgradeLane, Card = card }))
                {
                    placedThisTurn.Add(upgradeLane);
                    AIHandView.NotifyHandChanged();
                    ToastView.Show($"AI: {card.CardName} → 레인{upgradeLane + 1} 업그레이드");
                    Debug.Log($"[AI] 업그레이드 {card.CardName}(코스트:{card.Cost}) → 레인{upgradeLane}");
                    yield return new WaitForSeconds(ActionDelay);
                }
            }
        }

        // 아이템 카드 장착 (유닛 배치 후)
        yield return StartCoroutine(EquipItemsFromHand());

        // 스킬 카드도 사용 (코스트 + 필드 합 ≤ 사이즈)
        List<CardData> skills = ai.Hand
            .Where(c => c.Type == CardType.Skill
                     && FieldManager.Instance.CanPlayCard(false, c, ai.Size)
                     && EffectSystem.Instance.CanCastSkill(false, c))
            .ToList();

        foreach (CardData skill in skills)
        {
            if (!FieldManager.Instance.CanPlayCard(false, skill, ai.Size)) continue;
            if (!EffectSystem.Instance.CanCastSkill(false, skill)) continue; // 성약 발동 금지 재확인 (루프 중 잠길 수 있음)

            int targetLane = DecideSkillTarget(skill);

            ai.PlaySkill(skill);
            EffectSystem.Instance.ExecuteSkillEffect(false, skill, targetLane);
            AISkillZoneView.Instance?.ShowSkillCard(skill);
            ToastView.Show($"AI: 스킬 {skill.CardName} 사용");
            Debug.Log($"[AI] 스킬 {skill.CardName}(코스트:{skill.Cost}) 사용 → 레인{targetLane}");
            yield return new WaitForSeconds(ActionDelay * 0.5f);
        }

        // 필드 유닛의 액티브 효과 발동 (아니스, 브리드 등 — 메인 페이즈 전용)
        for (int lane = 0; lane < 3; lane++)
        {
            CardData unit = FieldManager.Instance.GetUnit(false, lane);
            if (unit == null) continue;
            if (!EffectSystem.Instance.HasActiveEffectForPhase(false, lane, unit, PhaseType.Main)) continue;
            // 패 소모형 액티브(아니스/브리드)는 각 구현부에서 자체적으로 빈 패를 확인함 —
            // 독사의 손길처럼 패가 필요 없는 아이템 액티브도 있어 여기서 일괄 차단하지 않음

            // 아니스(ActiveReturnCardDebuff): 조우 상대가 있을 때만 유효
            bool hasEncounterTarget = FieldManager.Instance.GetUnit(true, lane) != null;
            bool worthUsing = ShouldUseActiveEffect(unit, lane, hasEncounterTarget);
            if (!worthUsing) continue;

            EffectSystem.Instance.TriggerActiveEffect(false, lane, unit);
            ToastView.Show($"AI: {unit.CardName} 액티브 효과 발동");
            Debug.Log($"[AI] 액티브 효과 발동: {unit.CardName} 레인{lane}");
            yield return new WaitForSeconds(ActionDelay * 0.5f);
        }

        // 리더 액티브 (각성면 — 발동 가능하면 사용) — 관문 경유
        if (EffectSystem.Instance.CanUseLeaderActive(false))
        {
            GameActionGateway.Submit(new GameAction {
                Type = GameActionType.LeaderActive, IsPlayer = false,
            });
            ToastView.Show("AI: 리더 액티브 발동");
            Debug.Log("[AI] 리더 액티브 발동");
            yield return new WaitForSeconds(ActionDelay * 0.5f);
        }
    }

    // 액티브 효과 발동 가치 판단
    private bool ShouldUseActiveEffect(CardData unit, int lane, bool hasEncounterTarget)
    {
        foreach (var et in unit.EffectTypes)
        {
            string type = et.Contains(':') ? et.Substring(0, et.IndexOf(':')) : et;
            switch (type)
            {
                case "ActiveReturnCardDebuff":
                    // 조우 상대가 있을 때만 의미 있음
                    return hasEncounterTarget;
                case "ActiveTrashBuffBase":
                    // 아군 필드에 베이스 유닛이 있을 때만 의미 있음
                    for (int i = 0; i < 3; i++)
                    {
                        var u = FieldManager.Instance.GetUnit(false, i);
                        if (u != null && u.Faction != null && u.Faction.StartsWith("베이스"))
                            return true;
                    }
                    return false;
            }
        }

        // 장착 아이템 액티브 (예: ST11 독사의 손길 — 패 소모 없이 항상 사용 가치 있음)
        foreach (var item in FieldManager.Instance.GetEquippedItems(false, lane))
            if (item.EffectTypes.Any(e => e.StartsWith("ItemActiveDrawBoth")))
                return true;

        return false;
    }

    // ── 어택 페이즈: 공격 ────────────────────────────────────────

    public IEnumerator AttackTurn()
    {
        yield return new WaitForSeconds(ActionDelay);

        // 어택 페이즈 전용 유닛 액티브 효과 발동 (릴리벳/후미르 등)
        for (int lane = 0; lane < 3; lane++)
        {
            CardData unit = FieldManager.Instance.GetUnit(false, lane);
            if (unit == null) continue;
            if (!EffectSystem.Instance.CanUseUnitActive(false, lane, unit, PhaseType.Attack)) continue;
            if (!ShouldUseAttackActiveEffect(unit)) continue;

            EffectSystem.Instance.TriggerActiveEffect(false, lane, unit);
            EffectSystem.Instance.MarkUnitActiveUsed(false, lane);
            ToastView.Show($"AI: {unit.CardName} 액티브 효과 발동");
            Debug.Log($"[AI] 어택 페이즈 액티브 효과 발동: {unit.CardName} 레인{lane}");
            yield return new WaitForSeconds(ActionDelay * 0.5f);
        }

        // 실제 전투는 TurnManager → CombatManager.ProcessAttackPhase(false) 에서 처리
        Debug.Log("[AI] 어택 페이즈");
    }

    // 어택 페이즈 유닛 액티브 발동 가치 판단 (자기 트래시가 뒤따르므로 보수적으로 판단)
    private bool ShouldUseAttackActiveEffect(CardData unit)
    {
        foreach (var et in unit.EffectTypes)
        {
            string type = et.Contains(':') ? et.Substring(0, et.IndexOf(':')) : et;
            switch (type)
            {
                // 릴리벳: 아군 1장 트래시(대가 없음) — 다른 아군에게 부여된 엑시트나 정적 엑시트가 있을 때만 가치 있음
                case "ActiveAttackTrashAlly":
                    for (int i = 0; i < 3; i++)
                    {
                        var u = FieldManager.Instance.GetUnit(false, i);
                        if (u != null && u.Keywords.Contains("엑시트"))
                            return true;
                    }
                    return false;

                // 후미르: 호문클루스+엑시트 아군 트래시 → 히트+1
                // ※ AI 한계: 이 판단은 실제 공격(ProcessAIAttackPhase) 이전에 실행되므로, "먼저 공격해 엑시트를 부여받은 뒤
                //   트래시" 콤보는 AI가 활용하지 못함(정적 엑시트만 인식). 플레이어는 클릭 순서를 자유롭게 정할 수 있어 가능.
                case "ActiveAttackTrashExitAllyHitBoost":
                    for (int i = 0; i < 3; i++)
                    {
                        var u = FieldManager.Instance.GetUnit(false, i);
                        if (u != null && u.Faction != null && u.Faction.Contains("호문클루스") && u.Keywords.Contains("엑시트"))
                            return true;
                    }
                    return false;
            }
        }
        return false;
    }

    // ── AI 스킬 타겟 자동 선택 ───────────────────────────────────

    private int DecideSkillTarget(CardData skill)
    {
        foreach (var et in skill.EffectTypes)
        {
            int idx = et.IndexOf(':');
            string type = idx < 0 ? et : et.Substring(0, idx);

            switch (type)
            {
                // 상대(플레이어) 유닛 중 파워 가장 높은 레인 선택
                case "DebuffEnemyUnit":
                    return GetStrongestEnemyLane(true);

                // 아군(AI) 유닛 중 파워 가장 높은 레인 선택
                case "BuffAllyUnit":
                case "TrashAllySelfDraw":
                    return GetStrongestAllyLane(false);

                // 다 덤벼!: 아군이 지고있는 레인 (조우 상대보다 약한 레인)
                case "TrashBothUnits":
                    return GetLosingLane();

                // 엑셀러레이션: 양쪽 유닛 있는 레인 중 플레이어가 약한 레인
                case "TrashWeakestInLane":
                    return GetBothOccupiedLane();
            }
        }
        return -1; // 타겟 없음
    }

    // 필드 위 유닛의 "실제 전투 수치" — 버프/아이템/패시브/리더 패시브가 전부 반영된다.
    // 인쇄값(card.AttackPower)으로 비교하면 실제 전투 결과와 다른 판단을 하게 된다.
    private int Power(bool isPlayer, int lane, CardData unit)
        => EffectSystem.Instance.GetEffectivePower(isPlayer, lane, unit);

    // 플레이어(isEnemy=true) 또는 AI(isEnemy=false) 중 파워 강한 레인
    private int GetStrongestEnemyLane(bool isEnemy)
    {
        int best = -1, bestPow = -1;
        for (int i = 0; i < 3; i++)
        {
            var unit = FieldManager.Instance.GetUnit(isEnemy, i);
            if (unit == null) continue;
            int pow = Power(isEnemy, i, unit);
            if (pow > bestPow) { bestPow = pow; best = i; }
        }
        return best;
    }

    private int GetStrongestAllyLane(bool isPlayer)
    {
        int best = -1, bestPow = -1;
        for (int i = 0; i < 3; i++)
        {
            var unit = FieldManager.Instance.GetUnit(isPlayer, i);
            if (unit == null) continue;
            int pow = Power(isPlayer, i, unit);
            if (pow > bestPow) { bestPow = pow; best = i; }
        }
        return best;
    }

    // AI가 지고 있는 레인 (AI 유닛 파워 < 플레이어 유닛 파워)
    private int GetLosingLane()
    {
        for (int i = 0; i < 3; i++)
        {
            var aiUnit = FieldManager.Instance.GetUnit(false, i);
            var plUnit = FieldManager.Instance.GetUnit(true,  i);
            if (aiUnit != null && plUnit != null &&
                Power(false, i, aiUnit) < Power(true, i, plUnit))
                return i;
        }
        return GetStrongestAllyLane(false); // 없으면 강한 레인
    }

    // 양쪽 유닛 있는 레인 중 플레이어가 약한 레인
    private int GetBothOccupiedLane()
    {
        int best = -1, bestDiff = int.MinValue;
        for (int i = 0; i < 3; i++)
        {
            var aiUnit = FieldManager.Instance.GetUnit(false, i);
            var plUnit = FieldManager.Instance.GetUnit(true,  i);
            if (aiUnit != null && plUnit != null)
            {
                int diff = Power(false, i, aiUnit) - Power(true, i, plUnit);
                if (diff > bestDiff) { bestDiff = diff; best = i; }
            }
        }
        return best;
    }

    // ── 아이템 장착 ───────────────────────────────────────────────

    private IEnumerator EquipItemsFromHand()
    {
        PlayerController ai = PlayerController.AIInstance;

        List<CardData> items = ai.Hand
            .Where(c => c.Type == CardType.Item
                     && FieldManager.Instance.CanPlayCard(false, c, ai.Size))
            .OrderByDescending(c => GetItemTotalBoost(c))
            .ToList();

        foreach (CardData item in items)
        {
            if (!FieldManager.Instance.CanPlayCard(false, item, ai.Size)) continue;

            int targetLane = DecideItemTargetLane(item);
            if (targetLane < 0) continue;

            if (!GameActionGateway.Submit(new GameAction {
                    Type = GameActionType.EquipItem, IsPlayer = false, Lane = targetLane, Card = item })) continue;

            AIHandView.NotifyHandChanged();
            HUDView.NotifyStatusChanged();
            ToastView.Show($"AI: {item.CardName} → 레인{targetLane + 1} 장착");
            Debug.Log($"[AI] 아이템 {item.CardName} → 레인{targetLane} 장착");
            yield return new WaitForSeconds(ActionDelay * 0.5f);
        }
    }

    // 아이템을 장착할 레인 선택: 지고 있는 레인 → 다이렉트 레인 → 가장 강한 아군 레인
    private int DecideItemTargetLane(CardData item)
    {
        // 1순위: 조우 중이고 AI가 지고 있는 레인 (아이템으로 역전 가능성)
        for (int i = 0; i < 3; i++)
        {
            var aiUnit = FieldManager.Instance.GetUnit(false, i);
            var plUnit = FieldManager.Instance.GetUnit(true,  i);
            if (aiUnit == null || plUnit == null) continue;
            if (item.MinCostToEquip > 0 && aiUnit.Cost < item.MinCostToEquip) continue;
            int aiPow = EffectSystem.Instance.GetEffectivePower(false, i, aiUnit);
            int plPow = EffectSystem.Instance.GetEffectivePower(true,  i, plUnit);
            if (aiPow < plPow) return i;
        }

        // 2순위: 다이렉트 어택 가능한 레인 (아이템 어태커 효과 활용)
        for (int i = 0; i < 3; i++)
        {
            var aiUnit = FieldManager.Instance.GetUnit(false, i);
            if (aiUnit == null) continue;
            if (FieldManager.Instance.GetUnit(true, i) != null) continue;
            if (item.MinCostToEquip > 0 && aiUnit.Cost < item.MinCostToEquip) continue;
            return i;
        }

        // 3순위: 가장 강한 아군 유닛 레인
        int best = -1, bestPow = -1;
        for (int i = 0; i < 3; i++)
        {
            var aiUnit = FieldManager.Instance.GetUnit(false, i);
            if (aiUnit == null) continue;
            if (item.MinCostToEquip > 0 && aiUnit.Cost < item.MinCostToEquip) continue;
            int pow = EffectSystem.Instance.GetEffectivePower(false, i, aiUnit);
            if (pow > bestPow) { bestPow = pow; best = i; }
        }
        return best;
    }

    // 아이템의 총 파워/히트 보너스 합산 (정렬 기준)
    private int GetItemTotalBoost(CardData item)
    {
        int total = 0;
        foreach (var et in item.EffectTypes)
        {
            if (!et.Contains(':')) continue;
            string type = et.Substring(0, et.IndexOf(':'));
            if (int.TryParse(et.Substring(et.IndexOf(':') + 1), out int v))
            {
                if (type is "ItemPowerBoost" or "AttackerPowerBoost" or "ItemHitBoost")
                    total += v;
            }
        }
        return total;
    }

    // 업그레이드 가능한 레인이 있는지 확인
    private bool ExistsUpgradeLane(bool isPlayer, CardData card, int size)
        => FindUpgradeLane(isPlayer, card, size, null) >= 0;

    // 업그레이드 가능한 레인 중 파워가 가장 낮은 레인 반환 (교체 효율 최대화)
    private int FindUpgradeLane(bool isPlayer, CardData card, int size, HashSet<int> excludeLanes)
    {
        int best = -1, bestPow = int.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            if (excludeLanes != null && excludeLanes.Contains(i)) continue;
            if (!FieldManager.Instance.CanUpgrade(isPlayer, i, card)) continue;
            if (!FieldManager.Instance.CanPlayCardAsUpgrade(isPlayer, i, card, size)) continue;
            var existing = FieldManager.Instance.GetUnit(isPlayer, i);
            int pow = existing?.AttackPower ?? 0;
            if (pow < bestPow) { bestPow = pow; best = i; }
        }
        return best;
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    private List<int> GetEmptyLanes(bool isPlayer)
    {
        var empty = new List<int>();
        for (int i = 0; i < 3; i++)
            if (FieldManager.Instance.CanPlace(isPlayer, i))
                empty.Add(i);
        return empty;
    }
}
