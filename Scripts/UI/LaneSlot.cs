// Assets/Script/UI/LaneSlot.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class LaneSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("설정")]
    public int  LaneIndex;
    public bool IsPlayerLane;

    [Header("UI")]
    public Image    SlotBackground;
    public Image    UnitCardImage;
    public TMP_Text PowerText;      // 카드 파워 표시 (선택)
    public TMP_Text   ItemText;       // 장착 아이템 이름 표시 (선택)
    public Transform  ItemContainer;  // 아이템 이미지를 동적으로 넣을 부모
    public GameObject ItemImagePrefab; // Image + Button 프리팹

    public Color NormalColor      = new Color(1f, 1f, 1f, 0.15f);
    public Color HighlightColor   = new Color(0.3f, 1f, 0.3f, 0.4f);
    public Color OccupiedColor    = new Color(1f, 1f, 1f, 0f);
    public Color AttackReadyColor = new Color(1f, 0.9f, 0f, 0.5f);
    public Color AttackedColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    public Color SkillTargetColor = new Color(0.3f, 0.8f, 1f, 0.6f); // 하늘색 — 스킬 타겟 가능

    [Header("수치 변동 표시")]
    public Color PowerUpColor   = new Color(0.4f, 1f, 0.4f);
    public Color PowerDownColor = new Color(1f, 0.35f, 0.35f);
    public float DeltaFontSize  = 28f;  // 라벨 글자 크기
    public float DeltaYOffset   = -6f;  // 카드(슬롯) 하단 경계로부터의 간격. 음수 = 아래쪽
    public float DeltaWidth     = 160f; // 라벨 가로 폭 (숫자가 잘리면 늘릴 것)

    private CardData _occupiedCard;
    private bool     _hasAttacked;
    private bool     _hasPlaced;   // 이 턴에 이미 유닛을 배치/업그레이드했으면 true

    private void Awake()
    {
        if (SlotBackground == null)
            SlotBackground = GetComponent<Image>();

        if (UnitCardImage == null)
        {
            var images = GetComponentsInChildren<Image>();
            if (images.Length > 1) UnitCardImage = images[1];
        }
    }

    private void Start()
    {
        FieldManager.Instance.OnUnitPlaced    += OnUnitPlaced;
        FieldManager.Instance.OnUnitRemoved   += OnUnitRemoved;
        CombatManager.Instance.OnAttackStateChanged += RefreshAttackState;
        TurnManager.Instance.OnPhaseChanged   += OnPhaseChanged;

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (FieldManager.Instance  != null) FieldManager.Instance.OnUnitPlaced    -= OnUnitPlaced;
        if (FieldManager.Instance  != null) FieldManager.Instance.OnUnitRemoved   -= OnUnitRemoved;
        if (CombatManager.Instance != null) CombatManager.Instance.OnAttackStateChanged -= RefreshAttackState;
        if (TurnManager.Instance   != null) TurnManager.Instance.OnPhaseChanged   -= OnPhaseChanged;
    }

    private void OnPhaseChanged(PhaseType phase)
    {
        _hasAttacked = false;
        _hasPlaced   = false;
        RefreshUI();
    }

    private void RefreshAttackState()
    {
        if (!IsPlayerLane) return;
        _hasAttacked = !CombatManager.Instance.CanAttack(LaneIndex);
        RefreshUI();
    }

    private void OnUnitPlaced(bool isPlayer, int lane, CardData card)
    {
        if (isPlayer == IsPlayerLane && lane == LaneIndex)
        {
            _occupiedCard = card;
            RefreshUI();
        }
    }

    private void OnUnitRemoved(bool isPlayer, int lane)
    {
        if (isPlayer == IsPlayerLane && lane == LaneIndex)
        {
            _occupiedCard = null;
            RefreshUI();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!IsPlayerLane) return;

        CardView cardView = eventData.pointerDrag?.GetComponent<CardView>();
        if (cardView == null) return;

        CardData card = cardView.Data;

        // 자신의 메인 페이즈에만 배치/장착 가능 (상대 턴엔 즉시 손으로 복귀)
        if (!TurnManager.Instance.IsPlayerTurn || TurnManager.Instance.CurrentPhase != PhaseType.Main)
        {
            Debug.Log("[배치] 자신의 메인 페이즈가 아니라 배치 불가 — 손으로 복귀");
            cardView.ReturnToHand();
            HandView.Instance?.RefreshHand(); // 손패 레이아웃 재배치(안 하면 카드가 빠진 채로 보임)
            return;
        }

        // 아이템 카드: 유닛이 있는 레인에 장착
        if (card.Type == CardType.Item)
        {
            if (_occupiedCard == null)
            {
                Debug.Log($"[아이템] 유닛 없는 레인에 장착 불가");
                cardView.ReturnToHand();
                return;
            }
            // 아이템 장착 — 관문 경유 (검증+장착+코스트차감은 관문이)
            bool equipped = GameActionGateway.Submit(new GameAction {
                Type = GameActionType.EquipItem, IsPlayer = true, Lane = LaneIndex, Card = card,
            });
            if (!equipped)
            {
                Debug.Log($"[아이템 거부] {card.CardName} (코스트/장착조건)");
                cardView.ReturnToHand();
                return;
            }
            Destroy(cardView.gameObject);
            HandView.Instance.RefreshHand();
            HUDView.NotifyStatusChanged();
            RefreshUI();
            return;
        }

        if (card.Type != CardType.Unit)
        {
            cardView.ReturnToHand();
            return;
        }

        PlayerController player = PlayerController.PlayerInstance;

        // 룰북 6.4.1.1.3: 이 턴에 이미 이 레인에 배치/업그레이드했으면 불가
        if (_hasPlaced)
        {
            Debug.Log($"[배치 불가] 레인{LaneIndex} 이번 턴 이미 배치함");
            cardView.ReturnToHand();
            return;
        }

        // 업그레이드 시도 (기존 유닛보다 높은 코스트) — 관문 경유
        if (FieldManager.Instance.CanUpgrade(true, LaneIndex, card))
        {
            bool upgraded = GameActionGateway.Submit(new GameAction {
                Type = GameActionType.Upgrade, IsPlayer = true, Lane = LaneIndex, Card = card,
            });
            if (!upgraded)
            {
                Debug.Log($"[업그레이드 거부] {card.CardName} (코스트 부족 등)");
                cardView.ReturnToHand();
                return;
            }
            Destroy(cardView.gameObject);
            HandView.Instance.RefreshHand();
            HUDView.NotifyStatusChanged();
            _hasPlaced    = true;
            _occupiedCard = card;
            RefreshUI();
            return;
        }

        // ── 1단계 관문 시범 ──
        // 검증 + 배치 로직은 관문(GameActionGateway)이 담당. UI는 "요청"만 하고 결과로 처리.
        bool placed = GameActionGateway.Submit(new GameAction {
            Type     = GameActionType.PlaceUnit,
            IsPlayer = true,
            Lane     = LaneIndex,
            Card     = card,
        });
        if (!placed)
        {
            Debug.Log($"[배치 거부] {card.CardName} → 레인{LaneIndex} (코스트/점유/차단)");
            cardView.ReturnToHand();
            return;
        }

        // 성공 시 UI 갱신 (UI는 호출부가 담당)
        Destroy(cardView.gameObject);
        HandView.Instance.RefreshHand();
        HUDView.NotifyStatusChanged();
        _hasPlaced    = true;
        _occupiedCard = card;
        RefreshUI();
    }

    // ── 카드 길게 누르기 → 확대 ─────────────────────────────────
    private Coroutine _holdCoroutine;
    private bool      _zoomTriggered;
    private const float HoldDuration = 1f;

    public void OnPointerDown(PointerEventData eventData)
    {
        CancelHold();
        if (_occupiedCard != null)
            _holdCoroutine = StartCoroutine(HoldTimer());
    }

    public void OnPointerUp(PointerEventData eventData) => CancelHold();

    private void CancelHold()
    {
        if (_holdCoroutine != null)
        {
            StopCoroutine(_holdCoroutine);
            _holdCoroutine = null;
        }
    }

    private IEnumerator HoldTimer()
    {
        yield return new WaitForSeconds(HoldDuration);
        _holdCoroutine = null;
        if (_occupiedCard != null)
        {
            _zoomTriggered = true;
            CardZoomView.Instance?.Show(_occupiedCard);
        }
    }

    // 레인 클릭 → 어택 페이즈: 공격 / 메인 페이즈: 스킬 타겟 선택 / 트리거 타겟 선택
    public void OnPointerClick(PointerEventData eventData)
    {
        // 길게 눌러서 확대했으면 클릭 동작(공격/타겟 선택)은 무시
        if (_zoomTriggered)
        {
            _zoomTriggered = false;
            return;
        }

        // 스킬/트리거 타겟 선택 중
        if (SkillZoneSlot.IsTargeting)
        {
            if (IsValidSkillTarget())
            {
                SkillZoneSlot.OnLaneClicked(LaneIndex);
                RefreshUI();
            }
            return;
        }

        // 메인 페이즈: 액티브 효과 발동
        if (IsPlayerLane
            && TurnManager.Instance.CurrentPhase == PhaseType.Main
            && TurnManager.Instance.IsPlayerTurn
            && _occupiedCard != null
            && EffectSystem.Instance.HasActiveEffectForPhase(true, LaneIndex, _occupiedCard, PhaseType.Main))
        {
            EffectSystem.Instance.TriggerActiveEffect(true, LaneIndex, _occupiedCard);
            return;
        }

        // 어택 페이즈: 액티브 효과 발동 (턴당 1회 — 미사용이면 첫 클릭은 액티브, 이후 클릭은 공격)
        if (IsPlayerLane
            && TurnManager.Instance.CurrentPhase == PhaseType.Attack
            && TurnManager.Instance.IsPlayerTurn
            && _occupiedCard != null
            && EffectSystem.Instance.CanUseUnitActive(true, LaneIndex, _occupiedCard, PhaseType.Attack))
        {
            EffectSystem.Instance.TriggerActiveEffect(true, LaneIndex, _occupiedCard);
            EffectSystem.Instance.MarkUnitActiveUsed(true, LaneIndex);
            return;
        }

        // 어택 페이즈 공격
        if (!IsPlayerLane) return;
        if (TurnManager.Instance.CurrentPhase != PhaseType.Attack) return;
        if (!TurnManager.Instance.IsPlayerTurn) return;
        if (_occupiedCard == null) return;

        // 공격 선언 — 관문 경유 (검증 후 전투 코루틴 시작)
        GameActionGateway.Submit(new GameAction {
            Type = GameActionType.DeclareAttack, IsPlayer = true, Lane = LaneIndex,
        });
    }

    // 스킬/트리거 타겟으로 유효한지 확인
    private bool IsValidSkillTarget()
    {
        // 트리거 발동자 기준: 발동자의 상대 레인이 EnemyLane, 발동자 레인이 AllyLane
        bool ownerIsPlayer = SkillZoneSlot.IsTriggerTargeting
            ? SkillZoneSlot.TriggerOwnerIsPlayer
            : true; // 스킬은 항상 플레이어 발동

        switch (SkillZoneSlot.CurrentMode)
        {
            case SkillZoneSlot.TargetMode.EnemyLane:
                // 발동자의 적 = !ownerIsPlayer 레인
                bool targetIsPlayerLane = !ownerIsPlayer;
                if (IsPlayerLane != targetIsPlayerLane) return false;
                var enemyUnit = FieldManager.Instance.GetUnit(!ownerIsPlayer, LaneIndex);
                if (enemyUnit == null) return false;
                if (SkillZoneSlot.PendingCostLimit >= 0 && enemyUnit.Cost > SkillZoneSlot.PendingCostLimit) return false;
                return true;
            case SkillZoneSlot.TargetMode.AllyLane:
                return IsPlayerLane && FieldManager.Instance.GetUnit(true, LaneIndex) != null;
            case SkillZoneSlot.TargetMode.AnyLane:
                return FieldManager.Instance.GetUnit(true, LaneIndex) != null
                    && FieldManager.Instance.GetUnit(false, LaneIndex) != null;
            default:
                return false;
        }
    }

    // 타겟 선택 UI 갱신 (SkillZoneSlot에서 호출)
    public void RefreshTargetingUI()
    {
        RefreshUI();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SlotBackground == null) return;

        bool isAttackPhase = TurnManager.Instance.CurrentPhase == PhaseType.Attack
                          && TurnManager.Instance.IsPlayerTurn;

        if (isAttackPhase && IsPlayerLane && CombatManager.Instance.CanAttack(LaneIndex))
            SlotBackground.color = new Color(1f, 1f, 0f, 0.6f); // 호버 시 노란색 강조
        else if (_occupiedCard == null && IsPlayerLane)
            SlotBackground.color = HighlightColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        CancelHold();
        RefreshUI();
    }

    private void RefreshItemDisplay()
    {
        if (ItemContainer == null || ItemImagePrefab == null) return;

        // 기존 아이템 이미지 제거
        foreach (Transform child in ItemContainer)
            Destroy(child.gameObject);

        if (_occupiedCard == null) return;

        var items = FieldManager.Instance?.GetEquippedItems(IsPlayerLane, LaneIndex);
        if (items == null || items.Count == 0) return;

        foreach (var item in items)
        {
            var go = Instantiate(ItemImagePrefab, ItemContainer);
            var imgTransform = go.transform.Find("ItemImage");
            if (imgTransform != null && item.Artwork != null)
            {
                var img = imgTransform.GetComponent<Image>();
                if (img != null) img.sprite = item.Artwork;
            }

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            var captured = item;
            btn.onClick.AddListener(() => CardZoomView.Instance?.Show(captured));
        }
    }

    public void SetUnit(CardData card)
    {
        _occupiedCard = card;
        RefreshUI();
    }

    public void ClearUnit()
    {
        _occupiedCard = null;
        RefreshUI();
    }

    // 카드 효과(버프/디버프 등)가 UI 갱신을 요청할 때 씬의 모든 레인을 다시 그림 (EffectActions.RefreshUI에서 호출)
    public static void RefreshAllLanes()
    {
        foreach (var slot in FindObjectsByType<LaneSlot>(FindObjectsSortMode.None))
            slot.RefreshUI();
    }

    private void RefreshUI()
    {
        if (SlotBackground == null) return;

        if (_occupiedCard != null)
        {
            if (_occupiedCard.Artwork != null)
                SlotBackground.sprite = _occupiedCard.Artwork;

            // 스킬 타겟 선택 중
            if (SkillZoneSlot.IsTargeting && IsValidSkillTarget())
            {
                SlotBackground.color = SkillTargetColor;
            }
            // 어택 페이즈 상태에 따라 색상 표시
            else if (IsPlayerLane
                  && TurnManager.Instance != null
                  && TurnManager.Instance.CurrentPhase == PhaseType.Attack
                  && TurnManager.Instance.IsPlayerTurn)
            {
                // 공격함 → 강조 없음(그냥 카드) / 아직 → 노란 글로우. 공격 여부를 한눈에 구분
                SlotBackground.color = _hasAttacked
                    ? (_occupiedCard.Artwork != null ? Color.white : OccupiedColor)
                    : AttackReadyColor;
            }
            else
            {
                SlotBackground.color = _occupiedCard.Artwork != null
                    ? Color.white
                    : OccupiedColor;
            }

            if (UnitCardImage != null)
            {
                UnitCardImage.gameObject.SetActive(true);
                if (_occupiedCard.Artwork != null)
                    UnitCardImage.sprite = _occupiedCard.Artwork;
            }

            // 파워 계산 — PowerText 슬롯이 비어 있어도(현재 씬 미할당) 아래 수치 변동 라벨에는 필요하므로 밖에서 계산
            int power = EffectSystem.Instance != null
                ? EffectSystem.Instance.GetEffectivePower(IsPlayerLane, LaneIndex, _occupiedCard)
                : _occupiedCard.AttackPower;

            if (PowerText != null)
            {
                PowerText.gameObject.SetActive(true);
                PowerText.text = power.ToString();
            }
            RefreshPowerDeltaLabel(power);

            // 아이템 텍스트 & 아이템 이미지 동적 생성
            var equippedItems = FieldManager.Instance?.GetEquippedItems(IsPlayerLane, LaneIndex);
            bool hasItems = equippedItems != null && equippedItems.Count > 0;

            if (ItemText != null)
            {
                if (hasItems)
                {
                    ItemText.gameObject.SetActive(true);
                    ItemText.text = string.Join("\n", equippedItems.ConvertAll(i => $"[{i.CardName}]"));
                }
                else
                {
                    ItemText.gameObject.SetActive(false);
                }
            }

            RefreshItemDisplay();
        }
        else
        {
            SlotBackground.sprite = null;
            SlotBackground.color  = NormalColor;
            if (UnitCardImage != null) UnitCardImage.gameObject.SetActive(false);
            if (PowerText    != null) PowerText.gameObject.SetActive(false);
            if (ItemText     != null) ItemText.gameObject.SetActive(false);
            HidePowerDeltaLabel();
            RefreshItemDisplay();
        }
    }

    private TextMeshProUGUI _powerDeltaLabel; // 카드 아래 상시 표시되는 증감 라벨 (최초 필요 시 1회 생성 후 재사용)

    // 기본 파워 대비 현재 증감분(+1000/-3000)을 카드 아래에 상시 표시. 증감이 없으면 숨김.
    // 버프/디버프가 걸려있는 동안 계속 떠 있고, 효과가 사라지면 자동으로 없어진다.
    private void RefreshPowerDeltaLabel(int effectivePower)
    {
        if (_occupiedCard == null) { HidePowerDeltaLabel(); return; }

        int delta = effectivePower - _occupiedCard.AttackPower;
        if (delta == 0) { HidePowerDeltaLabel(); return; }

        if (_powerDeltaLabel == null) _powerDeltaLabel = CreatePowerDeltaLabel();
        _powerDeltaLabel.gameObject.SetActive(true);
        _powerDeltaLabel.text  = delta > 0 ? $"+{delta}" : delta.ToString();
        _powerDeltaLabel.color = delta > 0 ? PowerUpColor : PowerDownColor;
    }

    private void HidePowerDeltaLabel()
    {
        if (_powerDeltaLabel != null) _powerDeltaLabel.gameObject.SetActive(false);
    }

    // 레인 슬롯 자신의 자식으로 하단 중앙에 매다는 라벨 생성.
    // 앵커=하단중앙 + 피벗=위쪽(0.5,1) → 라벨 윗변이 슬롯 하단 경계에 붙어 통째로 슬롯 바깥 아래에 위치.
    // 카드 그림은 슬롯 rect 안에 그려지므로 겹치지 않는다.
    private TextMeshProUGUI CreatePowerDeltaLabel()
    {
        var go = new GameObject("PowerDelta", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(DeltaWidth, DeltaFontSize * 1.3f);
        rt.anchoredPosition = new Vector2(0f, DeltaYOffset);

        var text = go.AddComponent<TextMeshProUGUI>();
        // PowerText가 씬에 할당돼 있으면 폰트를 맞추고, 없으면 TMP 기본 폰트 사용
        // (표시 내용이 숫자와 +/- 부호뿐이라 어떤 폰트든 문제없음)
        if (PowerText != null && PowerText.font != null) text.font = PowerText.font;
        text.fontSize      = DeltaFontSize;
        text.fontStyle     = FontStyles.Bold;
        text.alignment     = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.outlineWidth  = 0.2f;        // 배경과 겹쳐도 읽히도록 검은 외곽선
        text.outlineColor  = Color.black;

        go.transform.SetAsLastSibling(); // 다른 UI 요소 위에 그려지도록
        return text;
    }
}