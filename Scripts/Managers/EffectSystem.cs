// Assets/Scripts/Managers/EffectSystem.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class EffectSystem : MonoBehaviour
{
    public static EffectSystem Instance { get; private set; }

    // 임시 파워 변동 (공격 종료 시 제거)
    private Dictionary<string, int> _attackBoosts = new();
    // 이번 턴 종료 시 제거
    private Dictionary<string, int> _turnBoosts    = new();
    private Dictionary<string, int> _turnHitBoosts = new();
    // 상대 턴 종료 시 제거 (BuffGuardianAlly 등 "상대의 턴이 끝날 때까지" 효과)
    private Dictionary<string, int> _opponentTurnBoosts    = new();
    private Dictionary<string, int> _opponentTurnHitBoosts = new();
    // 이번 턴 동안 돌파 부여 (BuffGuardianAllyBreakthrough)
    private HashSet<string> _breakthroughGrants = new();

    // ── 턴 단위 카운터/부여 상태 (OnTurnEnd에서 초기화) ──
    private int[] _attackCountThisTurn   = new int[2];  // 이 턴 자신 유닛이 공격한 횟수 (체인 조건)
    private int[] _unitsTrashedThisTurn  = new int[2];  // 이 턴 필드에서 트래시된 자신 유닛 수
    private bool[] _effectDamageDraw     = new bool[2]; // 몽환 나비: 이번 턴 효과 대미지마다 드로우1
    private Dictionary<string, int> _attackerGrants = new(); // 부여된 「어태커 파워+N」 (이번 턴)

    // 공격/방어 제한 및 추가 공격 (턴 단위)
    private HashSet<string> _attackDisabledTurn    = new(); // 이번 턴 공격 불가
    private HashSet<string> _attackDisabledOppTurn = new(); // 상대 턴 끝까지 공격 불가
    private HashSet<string> _defendDisabledTurn    = new(); // 이번 턴 방어 불가
    private Dictionary<string, int> _extraAttacks  = new(); // 어택 페이즈 추가 공격 횟수

    // ── 성약 (ST06 스킬 체인 공통 꼬리) ──
    private bool[] _covenantLocked = new bool[2]; // 이번 턴 《성약》 스킬 발동 금지
    private HashSet<string> _zeroCostGrants = new(); // 부여된 0코스트 (Key(isPlayer,lane)) — 이번 턴

    // ── ST07: 어태커 → 엑시트 부여 (호문클루스 공격 횟수 스케일링) ──
    private int[] _homunculusAttackCountThisTurn = new int[2]; // 이 턴 《호문클루스》 아군이 공격한 횟수
    private Dictionary<string, (string type, int value)> _grantedExit = new(); // Key(isPlayer,lane) → 부여된 엑시트

    // ── 유닛 액티브: 페이즈별 1회 사용 제한 (어택 페이즈 액티브용 — 메인 페이즈는 기존처럼 무제한) ──
    private HashSet<string> _unitActiveUsedThisTurn = new(); // Key(isPlayer,lane)

    // ── ST11: 부여형 방어/돌파 ("상대 턴이 끝날 때까지") ──
    private Dictionary<string, int> _grantedDefenderBoost = new();          // Key(isPlayer,lane) → 방어 시 파워+N
    private Dictionary<string, int> _conditionalBreakthroughGrants = new(); // Key(isPlayer,lane) → 돌파 코스트 상한

    // ── ST09: 자신/상대 턴마다 1회 제한 패시브 ──
    private HashSet<string> _deckBottomDamageUsedThisTurn = new();  // Key(isPlayer,lane)의 패시브 유닛 — ST09 아비게일
    private HashSet<string> _effectTrashDamageUsedThisTurn = new(); // Key(isPlayer,lane)의 패시브 유닛 — ST09 엠마

    // ── 이스케이프 (메인 페이즈 시작 훅) ──
    // 요밀로: 상대가 파워 N 이하 유닛 배치 시 드로우+대미지 (감시 활성 플레이어별 임계값, -1=비활성)
    private int[] _escapeDeployPunishThreshold = new int[2] { -1, -1 };
    // ST09 아비게일 각성 액티브로 부여된 「이스케이프: 덱 밑+상대 N대미지」 — Key(isPlayer,lane) → 대미지량
    private Dictionary<string, int> _grantedEscapeDamage = new();

    // ── ST10: 피의 기사로 부여된 「어태커: 조우 유닛 트래시」 (이번 턴, Key(isPlayer,lane)) ──
    private HashSet<string> _grantedAttackerTrashEncounter = new();

    // BT02 글레링 아이즈로 부여된 「어태커 약탈[N]」 — Key(isPlayer,lane) → N
    private Dictionary<string, int> _grantedAttackerPlunder = new();

    // ── BT03 신규 상태 ──
    // 이 턴 패 트래시 발생 여부 — [isPlayer idx]
    private bool[] _handTrashedThisTurn = { false, false };
    // 엔트리트래시 후 엑시트:패로귀환 부여 — Key(isPlayer,lane)
    private HashSet<string> _grantedExitReturn = new();
    // 엔트리로 부여된 DefenderFinisher — Key(isPlayer,lane)
    private HashSet<string> _grantedDefenderFinisher = new();
    // 배치 불가 코스트 하한 (이번 턴) — [0]=플레이어측 제한, [1]=AI측 제한
    private int[] _highCostDeployLocked = { 0, 0 }; // 0 = 제한 없음
    // GrantPassiveDrawOnEquip 부여 — Key(isPlayer,lane)
    private HashSet<string> _grantedPassiveDrawOnEquip = new();
    // DrawPerEntryAllyLockOpponentEntry: 상대 엔트리 잠금 — [isPlayer idx]
    private bool[] _entryLocked = { false, false };
    // PassiveOpponentNonTriggerDrawPunish: 상대가 비트리거 드로우 시 패널티 부여 — Key(isPlayer,lane)→minCost
    // PassiveDrawOnEnemyAttack: 상대 공격 시 드로우 부여 — Key(isPlayer,lane)
    // 두 패시브는 OnAttackDeclared / OnTriggerEffect 훅으로 처리

    // ── BT02 신규 상태 ──
    private int[] _effectTrashedThisTurn = new int[2]; // 이 턴 효과로 트래시된 자신 유닛 수 (길로틴, 그레이브용)
    private HashSet<string> _discardDrawUsedThisTurn = new(); // PassiveDiscardDrawOnce 이미 발동한 유닛 키
    // ArmedOnTrashSacrificeItemSurvive: 트래시되려는 유닛의 "아이템 희생 생존" 예약
    // ItemExpiresEndOfOpponentTurn: 상대 턴 끝에 트래시될 아이템
    private HashSet<string> _itemExpiresEndOfOpponentTurn = new(); // Key(isPlayer,lane) — 그 레인의 장착 아이템 전부가 대상
    // PassiveGrantBerserkerToHighCostEnemies: 적용 중인 "상대 N코 이상 광전사 부여" Key(isPlayer,lane)→N
    private Dictionary<string, int> _grantedBerserkerToEnemyMinCost = new();

    // ── ST06: 리나크로 부여된 듀얼리스트 (이번 턴), 기원의 라스로 부여된 어태커 관통[N] (이번 턴) ──
    private HashSet<string> _grantedDuelistThisTurn = new();      // Key(isPlayer,lane)
    private Dictionary<string, int> _grantedPenetration = new();  // Key(isPlayer,lane) → 관통 대미지

    private int Idx(bool isPlayer) => isPlayer ? 0 : 1;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        InitEntryHandlers();    // 엔트리 효과 핸들러 레지스트리 초기화 (Dictionary dispatch)
        InitAttackerHandlers(); // 어태커 효과 핸들러 레지스트리 초기화
        InitExitHandlers();     // 엑시트 효과 핸들러 레지스트리 초기화
        InitTriggerHandlers();  // 트리거 효과 핸들러 레지스트리 초기화
        InitSkillHandlers();    // 스킬 효과 핸들러 레지스트리 초기화
        InitBT04Handlers();     // BT04(대미지존 테마) 핸들러 등록 — 위 레지스트리에 추가
        InitBT05Handlers();     // BT05(믹스/이스케이프 테마) 핸들러 등록
        InitSB02Handlers();     // SB02(콜라보, 마지막 세트) 핸들러 등록
        InitBT06Handlers();     // BT06(체인/스킬존 버프 액티브/광전사/디펜더 테마) 핸들러 등록
        InitBT07Handlers();     // BT07(이동/포지션/이브 진화 라인 테마) 핸들러 등록
        InitSB01Handlers();     // SB01(니케 콜라보 — 어태커/디펜더/암드 시너지) 핸들러 등록
    }

    // ── 외부 호출 진입점 ────────────────────────────────────────

}
