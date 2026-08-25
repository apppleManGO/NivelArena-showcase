// Assets/Scripts/Managers/PlayerController.cs
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController PlayerInstance { get; private set; }
    public static PlayerController AIInstance    { get; private set; }

    [Header("설정")]
    public bool IsHuman = true;
    public DeckData DeckAsset;

    // 게임 상태
    public LeaderData Leader { get; private set; }
    public int LeaderLevel   { get; private set; } = 1;
    public bool IsAwakened   { get; private set; } = false;

    // ── 존 (실체는 private, 외부에는 읽기 전용으로만 노출) ──────────────
    // 예전에는 public List라 어디서든 내용을 바꿀 수 있었고, 바꾼 쪽이 UI 갱신을
    // 직접 호출해야 했다(RefreshHand/NotifyStatusChanged를 빠뜨리면 화면이 안 바뀜).
    // 이제 변경은 아래 메서드들만 통하고, 그 안에서 갱신 예약이 자동으로 일어난다.
    // ★ 새 존을 추가하면 Mark*Changed 호출을 잊지 말 것.
    private readonly List<CardData> _damageZone = new();
    private readonly List<CardData> _drawPile   = new();
    private readonly List<CardData> _hand       = new();
    private readonly List<CardData> _trashPile  = new();
    private readonly List<CardData> _skillZone  = new();

    // 룰북 1.2.2.1: 대미지 존에 카드가 10장 이상이면 패배
    // 대미지 존 = 실제 카드 리스트 (덱 위 카드가 이동해옴)
    public IReadOnlyList<CardData> DamageZone => _damageZone;
    public int DamageCount                    => _damageZone.Count;

    // 룰북 4.1: 사이즈 = 리더 레벨 + 대미지 존 카드 수 (+ BT07 SizeBoostTemp/PassiveMoveSizeBoost 임시 보너스)
    public int Size => LeaderLevel + DamageCount + (EffectSystem.Instance != null ? EffectSystem.Instance.GetTempSizeBoost(IsHuman) : 0);

    // ★ 컨벤션: DrawPile[0] = 덱 맨 위 (드로우·대미지 모두 index 0에서 가져감)
    public IReadOnlyList<CardData> DrawPile   => _drawPile;
    public IReadOnlyList<CardData> Hand       => _hand;
    public IReadOnlyList<CardData> TrashPile  => _trashPile;
    public IReadOnlyList<CardData> SkillZone  => _skillZone;

    // 손패가 바뀔 때마다 발행 (UI 갱신은 아래 LateUpdate가 자동 처리 — 별도 구독 불필요)
    public event System.Action OnHandChanged;

    // ── 갱신 예약 ────────────────────────────────────────────────
    // 존이 바뀌면 플래그만 세우고, 실제 UI 갱신은 프레임당 1회로 몰아서 처리한다.
    // (HUDView.NotifyStatusChanged / DamageZoneView.NotifyDamageChanged는 내부가
    //  FindObjectsByType이라 매 변경마다 부르면 씬 탐색이 그만큼 반복된다.
    //  효과 하나가 카드를 수십 장 옮겨도 갱신은 한 번이면 충분.)
    private bool _handDirty, _damageDirty, _statusDirty;

    private void MarkHandChanged()
    {
        _handDirty = true; _statusDirty = true;
        OnHandChanged?.Invoke();
    }
    private void MarkDamageZoneChanged() { _damageDirty = true; _statusDirty = true; }
    private void MarkZoneChanged()       { _statusDirty = true; }

    private void LateUpdate()
    {
        if (_handDirty)
        {
            _handDirty = false;
            if (IsHuman) HandView.Instance?.RefreshHand();
            else         AIHandView.NotifyHandChanged();
        }
        if (_damageDirty) { _damageDirty = false; DamageZoneView.NotifyDamageChanged(); }
        if (_statusDirty) { _statusDirty = false; HUDView.NotifyStatusChanged(); }
    }

    // 예약된 갱신을 지금 즉시 반영 (같은 프레임 안에서 화면이 꼭 최신이어야 할 때)
    public void FlushUI() => LateUpdate();

    // ── 손패 ─────────────────────────────────────────────────────
    public void AddToHand(CardData card)
    {
        if (card == null) return;
        _hand.Add(card); MarkHandChanged();
    }
    public void AddToHand(IEnumerable<CardData> cards)
    {
        if (cards == null) return;
        _hand.AddRange(cards); MarkHandChanged();
    }
    public bool RemoveFromHand(CardData card)
    {
        bool ok = _hand.Remove(card);
        if (ok) MarkHandChanged();
        return ok;
    }
    public void RemoveFromHandAt(int index)
    {
        if (index < 0 || index >= _hand.Count) return;
        _hand.RemoveAt(index); MarkHandChanged();
    }
    public void ClearHand() { _hand.Clear(); MarkHandChanged(); }

    // ── 트래시 ───────────────────────────────────────────────────
    public void AddToTrash(CardData card)
    {
        if (card == null) return;
        _trashPile.Add(card); MarkZoneChanged();
    }
    public void AddToTrash(IEnumerable<CardData> cards)
    {
        if (cards == null) return;
        // 인자가 이 플레이어의 다른 존일 수 있어(예: 손패 전체 트래시) 먼저 복사 —
        // 그대로 넘기면 열거 중 원본이 바뀔 수 있다
        _trashPile.AddRange(new List<CardData>(cards)); MarkZoneChanged();
    }
    public bool RemoveFromTrash(CardData card)
    {
        bool ok = _trashPile.Remove(card);
        if (ok) MarkZoneChanged();
        return ok;
    }
    public int RemoveFromTrashAll(System.Predicate<CardData> match)
    {
        int n = _trashPile.RemoveAll(match);
        if (n > 0) MarkZoneChanged();
        return n;
    }
    public void ClearTrash() { _trashPile.Clear(); MarkZoneChanged(); }

    // ── 덱 (index 0 = 맨 위) ──────────────────────────────────────
    public void AddToDeckTop(CardData card)
    {
        if (card == null) return;
        _drawPile.Insert(0, card); MarkZoneChanged();
    }
    public void AddToDeckBottom(CardData card)
    {
        if (card == null) return;
        _drawPile.Add(card); MarkZoneChanged();
    }
    public void AddToDeckBottom(IEnumerable<CardData> cards)
    {
        if (cards == null) return;
        _drawPile.AddRange(new List<CardData>(cards)); MarkZoneChanged();
    }
    // 덱의 임의 위치에 끼워넣기 (셔플-인 계열 효과)
    public void InsertIntoDeck(int index, CardData card)
    {
        if (card == null) return;
        _drawPile.Insert(Mathf.Clamp(index, 0, _drawPile.Count), card); MarkZoneChanged();
    }
    public bool RemoveFromDeck(CardData card)
    {
        bool ok = _drawPile.Remove(card);
        if (ok) MarkZoneChanged();
        return ok;
    }
    public void RemoveFromDeckAt(int index)
    {
        if (index < 0 || index >= _drawPile.Count) return;
        _drawPile.RemoveAt(index); MarkZoneChanged();
    }
    public void ClearDeck() { _drawPile.Clear(); MarkZoneChanged(); }
    public void SetDeck(IEnumerable<CardData> cards)
    {
        _drawPile.Clear();
        if (cards != null) _drawPile.AddRange(cards);
        MarkZoneChanged();
    }
    // 덱 맨 위 n장 훔쳐보기 (제거하지 않음) — 예전 DrawPile.GetRange(0, n)
    public List<CardData> PeekDeckTop(int n)
        => _drawPile.GetRange(0, Mathf.Clamp(n, 0, _drawPile.Count));

    // 덱 맨 위 n장 제거 (공개 후 처리한 카드를 덱에서 걷어낼 때) — 예전 DrawPile.RemoveRange(0, n)
    public void RemoveDeckTop(int n)
    {
        n = Mathf.Clamp(n, 0, _drawPile.Count);
        if (n == 0) return;
        _drawPile.RemoveRange(0, n); MarkZoneChanged();
    }

    // 덱 셔플 (Fisher-Yates) — 예전엔 호출부마다 루프를 직접 복사해 쓰고 있었음
    public void ShuffleDeck()
    {
        for (int i = _drawPile.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
        }
        MarkZoneChanged();
    }

    // ── 대미지 존 ────────────────────────────────────────────────
    public void AddToDamageZone(CardData card)
    {
        if (card == null) return;
        _damageZone.Add(card); MarkDamageZoneChanged();
    }
    public bool RemoveFromDamageZone(CardData card)
    {
        bool ok = _damageZone.Remove(card);
        if (ok) MarkDamageZoneChanged();
        return ok;
    }
    public void ClearDamageZone() { _damageZone.Clear(); MarkDamageZoneChanged(); }

    // ── 스킬 존 ──────────────────────────────────────────────────
    public void AddToSkillZone(CardData card)
    {
        if (card == null) return;
        _skillZone.Add(card); MarkZoneChanged();
    }
    public bool RemoveFromSkillZone(CardData card)
    {
        bool ok = _skillZone.Remove(card);
        if (ok) MarkZoneChanged();
        return ok;
    }
    public void ClearSkillZone() { _skillZone.Clear(); MarkZoneChanged(); }

    private void Awake()
    {
        if (IsHuman) PlayerInstance = this;
        else         AIInstance     = this;
    }

    // 3c 상태 복제 전용: 존/레벨/각성을 호스트 값으로 세팅 (효과·시뮬 없이).
    //  · DamageZone·TrashPile은 앞면 공개라 내용(카드)까지, DrawPile은 순서 숨김이라 개수만(플레이스홀더).
    public void ReplicateZones(int leaderLevel, bool awakened,
        List<CardData> damageZone, int drawCount, List<CardData> trashPile, CardData placeholder)
    {
        LeaderLevel = leaderLevel;
        IsAwakened  = awakened;
        _damageZone.Clear(); if (damageZone != null) _damageZone.AddRange(damageZone);
        _drawPile.Clear();   for (int i = 0; i < drawCount; i++) _drawPile.Add(placeholder);
        _trashPile.Clear();  if (trashPile  != null) _trashPile.AddRange(trashPile);
        MarkDamageZoneChanged(); MarkZoneChanged();
    }

    public void InitializeDeck()
    {
        Leader    = DeckAsset.Leader;
        SetDeck(DeckAsset.GetShuffledDeck());
        ClearHand();
        ClearTrash();
        ClearDamageZone();
        ClearSkillZone();
        LeaderLevel = 1;
        IsAwakened  = false;

        // 룰북 5.3: 초기 손패 5장
        DrawCard(5);
    }

    public void DrawCard(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // 룰북 1.2.2.4: 드로우해야 하는데 덱이 비어있으면 패배
            if (_drawPile.Count == 0)
            {
                MarkHandChanged(); // 여기까지 뽑은 만큼은 화면에 반영하고 끝낸다
                GameManager.Instance.OnPlayerDefeated(this);
                return;
            }

            var card = _drawPile[0];
            _drawPile.RemoveAt(0);
            _hand.Add(card);
        }

        MarkHandChanged(); // 덱→손패 이동 = 손패·덱 수 둘 다 갱신 대상
    }

    public void LevelUpLeader()
    {
        // 룰북: 리더 레벨은 제한 없이 올라감 (각성 조건 체크 포함)
        LeaderLevel++;
        Debug.Log($"[레벨업] {name} 리더 레벨 {LeaderLevel}");

        // 각성 조건 체크
        if (!IsAwakened && Leader != null && Leader.AwakenLevel > 0 && LeaderLevel >= Leader.AwakenLevel)
        {
            IsAwakened = true;
            Debug.Log($"[각성] {Leader.LeaderName} 각성! (Lv {LeaderLevel})");
        }

        HUDView.NotifyStatusChanged();
    }

    // 룰북 4.5.4: 대미지 처리
    // - 1장씩 처리
    // - 트리거 발동 시 나머지 대미지는 0
    public void TakeDamage(int amount = 1)
    {
        int remaining = amount;

        while (remaining > 0)
        {
            // 룰북 4.5.4.1: 대미지에서 1 차감
            remaining--;

            // 룰북 9.2.1.3: 대미지 처리 시점에 덱이 비어있으면 패배
            if (DrawPile.Count == 0)
            {
                GameManager.Instance.OnPlayerDefeated(this);
                return;
            }

            // 룰북 4.5.4.2: 덱 맨 위 카드를 대미지 존에 앞면 공개
            CardData topCard = _drawPile[0];
            _drawPile.RemoveAt(0);
            _damageZone.Add(topCard);
            MarkDamageZoneChanged();

            Debug.Log($"[대미지] {name} ← {topCard.CardName} ({DamageCount}장)");

            // 룰북 4.5.4.3: 트리거 효과 발동
            if (topCard.IsTrigger)
            {
                //ui로 트리거 보여주기 (온라인: 데미지는 호스트에서만 해결되므로 클라도 보도록 릴레이)
                Debug.Log($"[트리거 발동] {topCard.CardName}: {topCard.TriggerEffect}");
                CardZoomView.Instance?.ShowTimed(topCard.Artwork, 3f);
                if (NetHub.IsOnline && NetHub.IsAuthority)
                    NetHub.RelayTrigger(topCard.CardId);

                if (EffectSystem.Instance != null)
                    EffectSystem.Instance.OnTriggerEffect(IsHuman, topCard);

                // 트리거 카드는 보통 "이 카드를 트래시한다" — 패로 들어가는 경우(TriggerReturnToHand)만
                // 예외이며, 그 경우는 이미 대미지 존에서 빠져나가 있음. 나머지는 트래시로 이동.
                if (_damageZone.Contains(topCard))
                {
                    _damageZone.Remove(topCard);
                    _trashPile.Add(topCard);
                    MarkDamageZoneChanged();
                }

                // 룰북 4.5.4.3.1: 트리거 발동 후 남은 대미지를 0으로
                remaining = 0;
            }

            // 룰북 9.2.1.1: 대미지 존 10장 이상이면 패배
            if (_damageZone.Count >= 10)
            {
                GameManager.Instance.OnPlayerDefeated(this);
                return;
            }

            // 룰북 4.5.4.5: 대미지가 남아있으면 계속 반복 (while문이 처리)
        }
    }

    private int _handLimitDiscardCount; // ST09 키아라(PassiveEndTurnDiscardDamage) 집계용

    public void OnEndPhase()
    {
        // 룰북 6.6.1.3: 스킬 존 카드를 트래시로 이동
        AddToTrash(_skillZone);
        ClearSkillZone();

        // 룰북 6.6.1.4: 패 제한 7장 초과 시 트래시 (리더 패시브 PassiveHandLimitBoost로 증가 가능 — SB02-025)
        int HandLimit = 7 + (EffectSystem.Instance != null ? EffectSystem.Instance.GetHandLimitBonus(this == PlayerInstance) : 0);
        _handLimitDiscardCount = 0;
        if (IsHuman && Hand.Count > HandLimit)
        {
            DiscardToHandLimit(HandLimit);
            return; // 팝업 콜백에서 나머지 처리
        }

        DiscardAIToHandLimit(HandLimit);
    }

    // 스킬 카드를 스킬 존에 배치
    public bool PlaySkill(CardData card)
    {
        if (card.Type != CardType.Skill) return false;
        RemoveFromHand(card);
        AddToSkillZone(card);
        Debug.Log($"[스킬] {name} → {card.CardName} 스킬 존 배치");
        return true;
    }

    // 플레이어: HandPickPopup으로 1장씩 선택해서 트래시
    private void DiscardToHandLimit(int limit)
    {
        if (Hand.Count <= limit)
        {
            FinalizeEndPhase();
            return;
        }

        int overCount = Hand.Count - limit;
        HandPickPopup.Instance?.Show(
            $"트래시할 카드를 선택하세요 (남은 {overCount}장)",
            new System.Collections.Generic.List<CardData>(_hand),
            card =>
            {
                RemoveFromHand(card);
                AddToTrash(card);
                _handLimitDiscardCount++;
                Debug.Log($"[패 제한] {name} → {card.CardName} 트래시 (플레이어 선택)");
                DiscardToHandLimit(limit); // 아직 초과면 반복
            }
        );
    }

    // AI: 랜덤 트래시
    private void DiscardAIToHandLimit(int limit)
    {
        while (Hand.Count > limit)
        {
            int idx = Random.Range(0, _hand.Count);
            var discard = _hand[idx];
            RemoveFromHandAt(idx);
            AddToTrash(discard);
            _handLimitDiscardCount++;
            Debug.Log($"[패 제한] {name} → {discard.CardName} 트래시");
        }
        FinalizeEndPhase();
    }

    private void FinalizeEndPhase()
    {
        // ST09 해변가 키아라: 자신 턴 끝 패 제한 트래시가 있었다면 그 수만큼 상대에게 대미지
        if (_handLimitDiscardCount > 0 && EffectSystem.Instance != null)
        {
            bool isPlayer = (this == PlayerInstance);
            if (FieldHasPassive(isPlayer, "PassiveEndTurnDiscardDamage"))
                EffectSystem.Instance.NotifyEndTurnDiscardDamage(isPlayer, _handLimitDiscardCount);
        }
        HandView.Instance?.RefreshHand();
        HUDView.NotifyStatusChanged();
    }

    private bool FieldHasPassive(bool isPlayer, string effectType)
    {
        for (int i = 0; i < 3; i++)
        {
            var u = FieldManager.Instance.GetUnit(isPlayer, i);
            if (u != null && u.EffectTypes.Contains(effectType)) return true;
        }
        return false;
    }

    // 트래시 존에서 덱으로 재구성 (룰북: 트래시를 섞어서 새 덱)
    private void ReshuffleTrash()
    {
        if (_trashPile.Count == 0) return;
        AddToDeckBottom(_trashPile); // 내부에서 복사본을 만들므로 아래 ClearTrash와 순서 무관
        ClearTrash();
        ShuffleDeck();
        Debug.Log($"[리셔플] {name} 트래시 → 덱 ({_drawPile.Count}장)");
    }
}