// Assets/Scripts/UI/DeckBuilder/DeckBuilderManager.cs
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class DeckBuilderManager : MonoBehaviour
{
    // ── Inspector 연결 ──────────────────────────────────────────

    [Header("헤더")]
    public TMP_InputField DeckNameInput;
    public Button         SaveButton;
    public Button         BackButton;

    [Header("필터 패널")]
    public TMP_InputField SearchInput;
    public Button[]       AttributeFilterButtons; // 전체/화염/대지/파도/폭풍/번개 순
    public Button[]       TypeFilterButtons;      // 전체/유닛/스킬/아이템 순
    public Button[]       IpFilterButtons;        // 전체/브라운더스트2/니케/에픽세븐/이터널리턴/스텔라블레이드 순
    public Button         TriggerFilterButton;    // 눌러서 켜면 트리거 카드만 표시(토글)
    public Color          FilterActiveColor   = new(0.3f, 0.7f, 1f);
    public Color          FilterInactiveColor = new(0.25f, 0.25f, 0.25f);

    [Header("카드 그리드 (좌측)")]
    public Transform      CardGridParent;
    public GameObject     CardListItemPrefab;

    [Header("덱 패널 (중앙)")]
    public Image          LeaderImage;
    public TMP_Text       LeaderNameText;
    public Transform      DeckListParent;
    public GameObject     DeckListItemPrefab;
    public TMP_Text       DeckCountText;    // "XX / 40장"
    public TMP_Text       DeckTriggerText;  // (선택) "트리거 Y장" — 없으면 DeckCountText에 함께 표시
    public TMP_Text       ValidationText;   // 저장 전 경고 메시지

    [Header("카드 상세 패널 (좌측)")]
    public Image          DetailCardImage;
    public TMP_Text       DetailCardName;
    public TMP_Text       DetailCardCost;
    public TMP_Text       DetailCardPower;
    public TMP_Text       DetailCardHit;
    public TMP_Text       DetailCardType;
    public TMP_Text       DetailCardKeywords;
    public TMP_Text       DetailCardEffect;

    [Header("상세 패널 크기 조절 (인스펙터에서 수정 가능)")]
    [Tooltip("카드 이미지 높이(px). 너비는 카드 비율(0.714)로 자동. 값을 키우면 이미지가 커진다.")]
    public float DetailImageHeight  = 480f;
    [Tooltip("효과문 스크롤 박스의 기본 높이(px). 공간이 모자라면 이 아래 최소값까지 줄어든다.")]
    public float DetailEffectHeight = 300f;
    [Tooltip("좌측 세로 목록의 요소 간 간격(px).")]
    public float DetailSpacing      = 12f;
    [Tooltip("좌측 상세 패널의 고정 폭(px). 카드마다 패널 폭이 변하지 않게 고정.")]
    public float DetailPanelWidth   = 420f;

    // ── 내부 상태 ────────────────────────────────────────────────

    CardData[]   _allCards;
    LeaderData[] _allLeaders;

    // 현재 덱 구성: cardId → 매수
    readonly Dictionary<string, int>      _deckCounts = new();
    readonly Dictionary<string, CardData> _cardMap    = new();

    LeaderData _selectedLeader;

    // 필터 상태
    static readonly string[] AttributeNames = { "전체", "화염", "대지", "파도", "폭풍", "번개" };
    static readonly string[] TypeNames      = { "전체", "Unit", "Skill", "Item", "Leader" };
    static readonly string[] IpNames        = { "전체", "브라운더스트2", "니케", "에픽세븐", "이터널리턴", "스텔라블레이드" };
    int _attrFilter = 0;
    int _typeFilter = 0;
    int _ipFilter   = 0;
    bool _triggerOnly = false;

    // 생성된 아이템 캐시 (새로고침용)
    readonly List<CardListItem> _cardItems = new();
    readonly List<DeckListItem> _deckItems = new();

    // 편집 중인 덱 이름 (로드 시 설정)
    string _originalDeckName;

    // ── 초기화 ────────────────────────────────────────────────────

    void Start()
    {
        LockDetailLayout(); // 상세 패널이 카드마다 커졌다 작아졌다 하지 않도록 고정
        StartCoroutine(SizeDeckGridToWidth()); // 덱 목록 카드를 8열이 폭을 꽉 채우도록 크게

        // null 에셋 필터링 (마이그레이션 후 깨진 에셋 방어)
        _allCards   = Resources.LoadAll<CardData>("Cards")
                               .Where(c => c != null && !string.IsNullOrEmpty(c.CardId))
                               .ToArray();
        _allLeaders = Resources.LoadAll<LeaderData>("Leaders")
                               .Where(l => l != null && !string.IsNullOrEmpty(l.CardId))
                               .ToArray();

        // 리더도 우측 카드 그리드에서 드래그로 선택할 수 있도록, 표시 전용 임시 CardData(Type=Leader)를 생성해 병합
        // (LeaderData는 그대로 게임 로직의 원본 데이터로 유지 — _selectedLeader는 항상 LeaderData를 가리킴)
        var leaderShadows = _allLeaders.Select(l =>
        {
            var shadow = ScriptableObject.CreateInstance<CardData>();
            shadow.CardId    = l.CardId;
            shadow.CardName  = l.LeaderName;
            shadow.Type      = CardType.Leader;
            shadow.Attribute = l.Attribute;
            shadow.IpName    = l.IpName;
            shadow.Faction   = l.Faction;
            shadow.Artwork   = l.BaseArtwork;
            // 리더 효과 텍스트를 상세창에 보이도록 복사 (기본면/각성면)
            var texts = new List<string>();
            if (!string.IsNullOrEmpty(l.BaseEffectText))   texts.Add($"[기본] {l.BaseEffectText}");
            if (!string.IsNullOrEmpty(l.AwakenEffectText)) texts.Add($"[각성] {l.AwakenEffectText}");
            shadow.EffectTexts = texts;
            return shadow;
        }).ToArray();
        _allCards = _allCards.Concat(leaderShadows).ToArray();

        Debug.Log($"[DeckBuilder] 카드 {_allCards.Length - leaderShadows.Length}종, 리더 {_allLeaders.Length}종 로드됨");

        if (CardListItemPrefab == null)
        {
            Debug.LogError("[DeckBuilder] CardListItemPrefab이 연결되지 않았습니다.");
            return;
        }
        if (CardGridParent == null)
        {
            Debug.LogError("[DeckBuilder] CardGridParent가 연결되지 않았습니다.");
            return;
        }

        foreach (var c in _allCards)
            if (!_cardMap.ContainsKey(c.CardId))
                _cardMap[c.CardId] = c;

        SetupFilterButtons();
        if (SearchInput != null) SearchInput.onValueChanged.AddListener(_ => RefreshCardGrid());
        if (SaveButton != null) SaveButton.onClick.AddListener(OnSave);
        if (BackButton != null) BackButton.onClick.AddListener(OnBack);

        // 편집할 덱 이름이 GameSettings에 있으면 로드
        if (GameSettings.Instance != null && !string.IsNullOrEmpty(GameSettings.Instance.EditDeckName))
            LoadDeck(GameSettings.Instance.EditDeckName);

        RefreshCardGrid();
        RefreshDeckPanel();
    }

    // ── 필터 버튼 설정 ─────────────────────────────────────────

    void SetupFilterButtons()
    {
        for (int i = 0; i < AttributeFilterButtons.Length; i++)
        {
            int idx = i;
            AttributeFilterButtons[i].onClick.AddListener(() =>
            {
                _attrFilter = idx;
                UpdateFilterUI(AttributeFilterButtons, idx);
                RefreshCardGrid();
            });
        }
        UpdateFilterUI(AttributeFilterButtons, 0);

        for (int i = 0; i < TypeFilterButtons.Length; i++)
        {
            int idx = i;
            TypeFilterButtons[i].onClick.AddListener(() =>
            {
                _typeFilter = idx;
                UpdateFilterUI(TypeFilterButtons, idx);
                RefreshCardGrid();
            });
        }
        UpdateFilterUI(TypeFilterButtons, 0);

        for (int i = 0; i < IpFilterButtons.Length; i++)
        {
            int idx = i;
            IpFilterButtons[i].onClick.AddListener(() =>
            {
                _ipFilter = idx;
                UpdateFilterUI(IpFilterButtons, idx);
                RefreshCardGrid();
            });
        }
        UpdateFilterUI(IpFilterButtons, 0);

        // 트리거 전용 토글: 켜면 IsTrigger 카드만
        if (TriggerFilterButton != null)
        {
            TriggerFilterButton.onClick.AddListener(() =>
            {
                _triggerOnly = !_triggerOnly;
                UpdateTriggerFilterUI();
                RefreshCardGrid();
            });
            UpdateTriggerFilterUI();
        }
    }

    void UpdateTriggerFilterUI()
    {
        if (TriggerFilterButton == null) return;
        var img = TriggerFilterButton.GetComponent<Image>();
        if (img != null) img.color = _triggerOnly ? FilterActiveColor : FilterInactiveColor;
    }

    void UpdateFilterUI(Button[] buttons, int activeIdx)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            var img = buttons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == activeIdx) ? FilterActiveColor : FilterInactiveColor;
        }
    }

    // ── 카드 그리드 ──────────────────────────────────────────────

    void RefreshCardGrid()
    {
        string search = SearchInput.text.Trim();
        // 인덱스 방어: 씬의 필터 버튼 수가 Names 배열보다 많아도 크래시하지 않음
        string attrFilter = (_attrFilter > 0 && _attrFilter < AttributeNames.Length) ? AttributeNames[_attrFilter] : null;
        string typeFilter = (_typeFilter > 0 && _typeFilter < TypeNames.Length)      ? TypeNames[_typeFilter]      : null;
        string ipFilter   = (_ipFilter   > 0 && _ipFilter   < IpNames.Length)        ? IpNames[_ipFilter]          : null;
        // 타입 행에 추가한 "트리거" 버튼(TypeNames 범위 밖 인덱스)은 트리거 필터로 해석
        bool triggerFilter = _triggerOnly || _typeFilter >= TypeNames.Length;

        var filtered = _allCards.Where(c =>
        {
            if (attrFilter != null && c.Attribute != attrFilter) return false;
            if (typeFilter != null && c.Type.ToString() != typeFilter) return false;
            if (ipFilter != null && c.IpName != ipFilter) return false;
            if (triggerFilter && !c.IsTrigger) return false;
            if (!string.IsNullOrEmpty(search) &&
                !c.CardName.Contains(search) && !c.CardId.Contains(search)) return false;
            return true;
        }).OrderBy(c => c.Attribute).ThenBy(c => c.Cost).ThenBy(c => c.CardName).ToArray();

        // 기존 아이템 재사용 (생성/삭제 최소화)
        int existing = _cardItems.Count;
        for (int i = 0; i < filtered.Length; i++)
        {
            CardListItem item;
            if (i < existing)
            {
                item = _cardItems[i];
                item.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(CardListItemPrefab, CardGridParent);
                item = go.GetComponent<CardListItem>();
                _cardItems.Add(item);
            }
            item.Setup(filtered[i], this);
        }
        for (int i = filtered.Length; i < existing; i++)
            _cardItems[i].gameObject.SetActive(false);
    }

    // ── 덱 패널 ───────────────────────────────────────────────────

    void RefreshDeckPanel()
    {
        // 덱 구성 목록 (0장인 카드 제외, 속성→코스트→이름 순, 매수만큼 이미지 반복 표시)
        var inDeck = _deckCounts
            .Where(kv => kv.Value > 0 && _cardMap.ContainsKey(kv.Key))
            .OrderBy(kv => _cardMap[kv.Key].Attribute)
            .ThenBy(kv => _cardMap[kv.Key].Cost)
            .ThenBy(kv => _cardMap[kv.Key].CardName)
            .SelectMany(kv => Enumerable.Repeat(_cardMap[kv.Key], kv.Value))
            .ToArray();

        int existing = _deckItems.Count;
        for (int i = 0; i < inDeck.Length; i++)
        {
            DeckListItem item;
            if (i < existing)
            {
                item = _deckItems[i];
                item.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(DeckListItemPrefab, DeckListParent);
                item = go.GetComponent<DeckListItem>();
                _deckItems.Add(item);
            }
            item.Setup(inDeck[i], this);
        }
        for (int i = inDeck.Length; i < existing; i++)
            _deckItems[i].gameObject.SetActive(false);

        // 그리드가 아이템을 셀 크기로 즉시 배치하도록 강제 리빌드 (안 하면 프리팹 기본 100x40으로 남아 이미지가 작게 보임)
        if (DeckListParent is RectTransform dlrt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(dlrt);

        int total = _deckCounts.Values.Sum();
        // 트리거 카드 매수 합계
        int triggerTotal = _deckCounts
            .Where(kv => kv.Value > 0 && _cardMap.ContainsKey(kv.Key) && _cardMap[kv.Key].IsTrigger)
            .Sum(kv => kv.Value);

        if (DeckTriggerText != null)
        {
            DeckCountText.text    = $"{total} / 40장";
            DeckTriggerText.text  = $"트리거 {triggerTotal}장";
        }
        else
        {
            // 별도 텍스트 미할당 시 한 줄에 같이 표시
            DeckCountText.text = $"{total} / 40장   |   트리거 {triggerTotal}장";
        }

        // 리더 표시
        if (_selectedLeader != null)
        {
            LeaderImage.sprite  = _selectedLeader.BaseArtwork;
            LeaderImage.enabled = _selectedLeader.BaseArtwork != null;
            LeaderNameText.text = _selectedLeader.LeaderName;
        }
        else
        {
            LeaderImage.enabled = false;
            LeaderNameText.text = "리더 미선택";
        }
    }

    // ── 카드 추가/제거 ────────────────────────────────────────────

    public void AddCard(CardData card)
    {
        // 리더는 40장 덱에 포함되지 않고 1장만 선택되는 별도 슬롯 — 드래그하면 바로 리더로 지정
        if (card.Type == CardType.Leader)
        {
            var leader = _allLeaders.FirstOrDefault(l => l.CardId == card.CardId);
            if (leader == null) return;
            _selectedLeader = leader;
            OnDeckChanged();
            return;
        }

        int cur = CountInDeck(card.CardId);
        int max = DeckSaveLoad.MaxCopies(card.Rarity);
        if (cur >= max) return;

        int total = _deckCounts.Values.Sum();
        if (total >= 40) return;

        // 룰북 5.1.2.3: 트리거가 표시된 카드는 덱 전체에서 최대 8장까지
        if (card.IsTrigger && CountTriggerCards() >= DeckSaveLoad.MaxTriggerCards) return;

        _deckCounts[card.CardId] = cur + 1;
        OnDeckChanged();
    }

    int CountTriggerCards()
    {
        int total = 0;
        foreach (var kv in _deckCounts)
            if (kv.Value > 0 && _cardMap.TryGetValue(kv.Key, out var c) && c.IsTrigger)
                total += kv.Value;
        return total;
    }

    public void RemoveCard(string cardId)
    {
        if (!_deckCounts.ContainsKey(cardId) || _deckCounts[cardId] <= 0) return;
        _deckCounts[cardId]--;
        if (_deckCounts[cardId] == 0) _deckCounts.Remove(cardId);
        OnDeckChanged();
    }

    public int CountInDeck(string cardId)
    {
        // 리더는 _deckCounts에 들어가지 않으므로 현재 선택된 리더인지로 판단 (0 또는 1)
        if (_selectedLeader != null && _selectedLeader.CardId == cardId) return 1;
        return _deckCounts.TryGetValue(cardId, out int v) ? v : 0;
    }

    void OnDeckChanged()
    {
        // 그리드 카운트 뱃지 갱신
        foreach (var item in _cardItems)
            if (item.gameObject.activeSelf) item.RefreshCount();
        RefreshDeckPanel();
    }

    // ── 저장 ──────────────────────────────────────────────────────

    void OnSave()
    {
        string msg = Validate();
        if (!string.IsNullOrEmpty(msg))
        {
            if (ValidationText != null) ValidationText.text = msg;
            return;
        }
        if (ValidationText != null) ValidationText.text = "";

        string name = DeckNameInput.text.Trim();
        if (string.IsNullOrEmpty(name)) name = "새 덱";

        var save = new CustomDeckSave
        {
            deckName = name,
            leaderId = _selectedLeader.CardId,
            cardIds  = new List<string>()
        };
        foreach (var kv in _deckCounts)
            for (int i = 0; i < kv.Value; i++)
                save.cardIds.Add(kv.Key);

        // 이름이 바뀌었으면 기존 파일 삭제
        if (!string.IsNullOrEmpty(_originalDeckName) && _originalDeckName != name)
            DeckSaveLoad.Delete(_originalDeckName);

        DeckSaveLoad.Save(save);
        _originalDeckName = name;

        if (ValidationText != null) ValidationText.text = "저장 완료!";
        Debug.Log($"[DeckBuilder] '{name}' 저장 완료 ({save.cardIds.Count}장)");
    }

    string Validate()
    {
        if (_selectedLeader == null) return "리더를 선택해주세요.";
        int total = _deckCounts.Values.Sum();
        if (total != 40) return $"덱은 정확히 40장이어야 합니다. (현재 {total}장)";
        int triggerTotal = CountTriggerCards();
        if (triggerTotal > DeckSaveLoad.MaxTriggerCards)
            return $"트리거 카드는 최대 {DeckSaveLoad.MaxTriggerCards}장까지 넣을 수 있습니다. (현재 {triggerTotal}장)";
        return "";
    }

    // ── 로드 ──────────────────────────────────────────────────────

    void LoadDeck(string deckName)
    {
        var save = DeckSaveLoad.Load(deckName);
        if (save == null) return;

        _originalDeckName    = save.deckName;
        DeckNameInput.text   = save.deckName;
        _deckCounts.Clear();

        foreach (var id in save.cardIds)
        {
            _deckCounts.TryGetValue(id, out int cur);
            _deckCounts[id] = cur + 1;
        }

        _selectedLeader = _allLeaders.FirstOrDefault(l => l.CardId == save.leaderId);

        if (GameSettings.Instance != null)
            GameSettings.Instance.EditDeckName = "";
    }

    // ── 카드 상세 표시 ───────────────────────────────────────────

    // 상세 패널이 카드마다(텍스트량/이미지) 커졌다 작아졌다 하지 않도록 런타임에 고정한다.
    //  좌측 패널은 VerticalLayoutGroup(ChildControlHeight)라 자식 높이가 "내용 기준"으로 계산됨
    //  → 각 자식에 고정 높이(LayoutElement)를 박아 내용과 무관하게 크기를 고정한다.
    // 덱 목록(DeckList) 카드가 8열이 폭을 꽉 채우도록 GridLayoutGroup 셀 크기를 폭에 맞춰 계산.
    //  레이아웃이 잡힌 뒤(한 프레임 대기) 실제 폭을 읽어야 정확 → 코루틴.
    IEnumerator SizeDeckGridToWidth()
    {
        yield return null; // 캔버스 레이아웃 계산 대기
        var rt = DeckListParent as RectTransform;
        var grid = DeckListParent != null ? DeckListParent.GetComponent<GridLayoutGroup>() : null;
        if (rt == null || grid == null) yield break;

        const int cols = 8;
        const float cardRatio = 84f / 60f; // 카드 세로/가로 비율(기존 셀 60x84)

        // 폭이 아직 0이면 부모(뷰포트)에서 가져오기
        float w = rt.rect.width;
        if (w < 10f && rt.parent is RectTransform prt) w = prt.rect.width;
        if (w < 10f) yield break;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;
        float pad = grid.padding.left + grid.padding.right;
        float sp  = grid.spacing.x * (cols - 1);
        float cellW = Mathf.Floor((w - pad - sp) / cols);
        if (cellW < 10f) yield break;
        grid.cellSize = new Vector2(cellW, cellW * cardRatio);

        // 스크롤 콘텐츠는 상단 고정이어야 함. full-stretch(0,0)-(1,1)+ContentSizeFitter면
        //  콘텐츠가 뷰포트보다 높아질 때 pivot(0.5) 중앙정렬로 윗줄이 잘려 안 보임 → 상단 앵커/피벗으로.
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        var csf = DeckListParent.GetComponent<ContentSizeFitter>();
        if (csf != null)
        {
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    void LockDetailLayout()
    {
        // 세로 그룹: 간격이 과해(100) 배치가 불안정 → 적당히 줄이고 높이 자동제어 유지
        if (DetailCardImage != null)
        {
            var panel = DetailCardImage.transform.parent;
            var vlg = panel != null ? panel.GetComponent<VerticalLayoutGroup>() : null;
            if (vlg != null)
            {
                vlg.spacing = DetailSpacing;
                vlg.childControlHeight   = true;
                vlg.childForceExpandHeight = false;
            }
            // 좌측 패널 폭 고정: 안 하면 카드 이미지 스프라이트 원본 너비가 패널 폭을 좌우해
            //  리더(길티/수아 등)마다 좌측이 커졌다 작아졌다 함 → LayoutElement로 폭 못박음.
            if (panel != null)
            {
                var ple = panel.GetComponent<LayoutElement>();
                if (ple == null) ple = panel.gameObject.AddComponent<LayoutElement>();
                ple.preferredWidth = DetailPanelWidth;
                ple.minWidth       = DetailPanelWidth;
                ple.flexibleWidth  = 0;
            }
        }

        // 카드 이미지: 크게 고정. AspectRatioFitter가 FitInParent(3)라 높이를 무시하고 작아졌음
        //  → HeightControlsWidth로 바꿔 "레이아웃이 정한 높이 → 비율대로 너비"가 되게 하고 높이를 크게.
        if (DetailCardImage != null)
        {
            DetailCardImage.preserveAspect = true;
            var arf = DetailCardImage.GetComponent<AspectRatioFitter>();
            if (arf != null) arf.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
            SetHeight(DetailCardImage, DetailImageHeight, DetailImageHeight, flexible: 0); // 인스펙터 값. 너비는 비율로 자동
        }

        // 이름: 크고 굵게 가운데
        FixText(DetailCardName, 40);
        if (DetailCardName != null) { DetailCardName.fontStyle = FontStyles.Bold; DetailCardName.alignment = TextAlignmentOptions.Center; }

        // 타입: 작고 은은하게 / 키워드: 강조색
        FixText(DetailCardType, 24);
        if (DetailCardType != null) { DetailCardType.alignment = TextAlignmentOptions.Center; DetailCardType.color = new Color(0.62f, 0.68f, 0.80f); }
        FixText(DetailCardKeywords, 44);
        if (DetailCardKeywords != null) { DetailCardKeywords.alignment = TextAlignmentOptions.Center; DetailCardKeywords.color = new Color(0.98f, 0.85f, 0.45f); }

        // 스탯 줄(InfoRow): 값 있는 것만 가운데로 모이게(빈 스탯=0폭으로 사라짐) + 라벨/색은 ShowDetail에서
        if (DetailCardCost != null && DetailCardCost.transform.parent != null)
        {
            var hlg = DetailCardCost.transform.parent.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.childForceExpandWidth = false; // 3등분 강제 해제 → 내용만큼만
                hlg.childControlWidth     = true;
                hlg.childAlignment        = TextAnchor.MiddleCenter;
                hlg.spacing               = 20f;
            }
            SetHeight(DetailCardCost.transform.parent, 34, 34, 0); // 스탯 줄 높이
        }
        StyleStat(DetailCardCost);
        StyleStat(DetailCardPower);
        StyleStat(DetailCardHit);

        // 효과문 스크롤: 씬에서 ScrollRect의 content==viewport로 잘못 세팅돼 클리핑/스크롤이 깨짐.
        //  → 런타임에 재구성: 뷰포트(마스크 창)=고정, content=텍스트가 내용만큼 세로로 늘어나며 스크롤.
        if (DetailCardEffect != null)
        {
            var sr = DetailCardEffect.GetComponentInParent<ScrollRect>();
            if (sr != null)
            {
                SetHeight(sr, DetailEffectHeight, 180, flexible: 1); // 바깥 박스 고정 높이

                // 뷰포트 = 텍스트의 직접 부모(마스크 보유). 창처럼 크기 고정(레이아웃/피터 끔) + 꽉 채움.
                var viewport = DetailCardEffect.transform.parent as RectTransform;
                if (viewport != null)
                {
                    var vcsf = viewport.GetComponent<ContentSizeFitter>();
                    if (vcsf != null) vcsf.enabled = false;
                    var vvlg = viewport.GetComponent<VerticalLayoutGroup>();
                    if (vvlg != null) vvlg.enabled = false;
                    viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
                    viewport.offsetMin = Vector2.zero; viewport.offsetMax = Vector2.zero;
                    sr.viewport = viewport;
                }

                // content = 텍스트. 상단 고정 + 폭 꽉 + 내용만큼 세로로 늘어남(ContentSizeFitter) → 뷰포트 안 스크롤.
                var textRT = DetailCardEffect.rectTransform;
                textRT.anchorMin = new Vector2(0f, 1f);
                textRT.anchorMax = new Vector2(1f, 1f);
                textRT.pivot     = new Vector2(0.5f, 1f);
                textRT.offsetMin = new Vector2(0f, textRT.offsetMin.y);
                textRT.offsetMax = new Vector2(0f, textRT.offsetMax.y);

                var le = DetailCardEffect.GetComponent<LayoutElement>();
                if (le != null) le.enabled = false; // 고정높이 LayoutElement면 ContentSizeFitter와 충돌
                DetailCardEffect.enableAutoSizing = false;
                DetailCardEffect.overflowMode = TextOverflowModes.Overflow; // 넘치면 세로로 늘어남

                var tcsf = DetailCardEffect.GetComponent<ContentSizeFitter>();
                if (tcsf == null) tcsf = DetailCardEffect.gameObject.AddComponent<ContentSizeFitter>();
                tcsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                tcsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

                sr.content    = textRT;
                sr.horizontal = false;
                sr.vertical   = true;
            }
        }
    }

    // GameObject에 LayoutElement로 높이 지정(없으면 추가). min=pref면 안 줄어드는 고정 높이.
    static void SetHeight(Component c, float preferred, float min, float flexible)
    {
        var le = c.GetComponent<LayoutElement>();
        if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = preferred;
        le.minHeight       = min;
        le.flexibleHeight  = flexible;
    }

    // 텍스트: 고정 높이 + 폰트 자동축소(내용 길이와 무관하게 박스 크기 유지).
    static void FixText(TMP_Text t, float h)
    {
        if (t == null) return;
        SetHeight(t, h, h, 0);
        AutoSize(t);
    }

    // 스탯 텍스트: 확실히 보이도록 고정 폰트/가운데 정렬(내용 폭에 맞춰 배치되도록 자동축소 끔).
    static void StyleStat(TMP_Text t)
    {
        if (t == null) return;
        t.enableAutoSizing = false;
        t.fontSize     = 26;
        t.alignment    = TextAlignmentOptions.Center;
        t.color        = Color.white;
        t.richText     = true;
        t.overflowMode = TextOverflowModes.Overflow;
    }

    static void AutoSize(TMP_Text t)
    {
        if (t == null) return;
        var fitter = t.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false; // 내용에 맞춰 박스가 늘어나는 것 방지
        t.enableAutoSizing = true;
        t.fontSizeMin = 14;
        t.fontSizeMax = t.fontSize > 0 ? t.fontSize : 32;
        t.overflowMode = TextOverflowModes.Truncate;
    }

    public void ShowDetail(CardData card)
    {
        if (card == null) return;

        if (DetailCardImage != null)
        {
            DetailCardImage.preserveAspect = true;
            DetailCardImage.sprite  = card.Artwork;
            DetailCardImage.enabled = card.Artwork != null;
        }
        if (DetailCardName     != null) DetailCardName.text     = card.CardName;
        // 스탯: 라벨은 색, 값은 흰색. 값 없으면(0) 빈칸 → InfoRow에서 자리 안 먹고 사라짐
        if (DetailCardCost     != null) DetailCardCost.text     = card.Cost > 0        ? $"<color=#8FC9FF>코스트</color> {card.Cost}"        : "";
        if (DetailCardPower    != null) DetailCardPower.text    = card.AttackPower > 0 ? $"<color=#FFA87A>파워</color> {card.AttackPower}" : "";
        if (DetailCardHit      != null) DetailCardHit.text      = card.Hit > 0         ? $"<color=#93E093>히트</color> {card.Hit}"          : "";
        if (DetailCardType     != null) DetailCardType.text     = card.Type.ToString();
        if (DetailCardKeywords != null) DetailCardKeywords.text = card.Keywords != null ? string.Join("  |  ", card.Keywords) : "";
        if (DetailCardEffect   != null)
        {
            string body = card.EffectTexts != null ? string.Join("\n", card.EffectTexts) : "";
            // 트리거 효과 텍스트도 함께 표시 (별도 필드라 그동안 안 보였음)
            if (card.IsTrigger && !string.IsNullOrEmpty(card.TriggerEffect))
            {
                if (!string.IsNullOrEmpty(body)) body += "\n\n";
                body += $"<color=#FFD24A>[트리거]</color> {card.TriggerEffect}";
            }
            DetailCardEffect.richText = true;
            DetailCardEffect.text = body;
        }
    }

    // ── 뒤로가기 ─────────────────────────────────────────────────

    void OnBack() => SceneManager.LoadScene("DeckSelect");
}
