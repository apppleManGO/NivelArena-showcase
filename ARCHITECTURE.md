# 니벨아레나 — 아키텍처 안내 (코드 학습용)

이 문서는 코드를 처음 읽는 사람이 **"무엇이 어디에 있고, 왜 그렇게 나눴는지"** 를 빠르게 파악하도록 정리한 지도입니다.
맨 아래 **[학습 추천 순서](#학습-추천-순서)** 부터 봐도 좋습니다.

---

## 1. 큰 그림 (게임이 시작되는 흐름)

```
[DeckSelect 씬] ── 덱/모드 선택 ──▶ [SampleScene 씬] (대전)
      │                                  │
      └── 덱빌더 버튼 ──▶ [DeckBuilder 씬]  └── GameManager.Start()
                                              → 덱 로드 + 검증
                                              → MulliganThenStart() (선후공·멀리건)
                                              → TurnManager.StartGame()
                                              → 턴 루프 시작
```

- **진입 씬**: `DeckSelect` (빌드 0번)
- **대전 씬**: `SampleScene` — 실제 게임
- **덱 편집**: `DeckBuilder`

턴 루프(핵심 게임 흐름)는 **`TurnManager`** 가 돌립니다: `레벨업 → 드로우 → 메인 → 어택 → 엔드`.

---

## 2. 시스템 지도 (매니저별 역할)

`Assets/Scripts/Managers/` 안에 있습니다. 각 매니저는 **하나의 책임**만 갖습니다.

| 매니저 | 역할 | 먼저 볼 함수 |
|---|---|---|
| **TurnManager** | 턴/페이즈 진행, 선후공, 턴 종료 | `StartGame`, `RunTurn`, `ChangePhase` |
| **GameManager** | 덱 로드·검증, 멀리건, 승패/결과 | `Start`, `MulliganThenStart`, `OnPlayerDefeated`, `BuildDeck` |
| **FieldManager** | 필드 상태(레인별 유닛), 배치/업그레이드/장착/제거 | `PlaceUnit`, `UpgradeUnit`, `EquipItem`, `GetUnit` (+ 이벤트 `OnUnitPlaced`) |
| **CombatManager** | 전투 해결(조우/다이렉트/방어/가디언/돌파), 대미지존 | `PlayerAttackLane`, `ForceAttackLane`, `ResolveEncounter`, `ProcessDirectAttack` |
| **PlayerController** | 한 플레이어의 상태(손패·덱·트래시·대미지존·스킬존·리더레벨) | `InitializeDeck`, `DrawCard`, `TakeDamage` |
| **EffectSystem** | **효과 엔진** (아래 3장에서 상세) | 파일이 여러 개로 쪼개져 있음 |
| **EffectActions** | 효과의 "원자 블록"(드로우/버프/카드선택 등) | `Draw`, `Buff`, `PickFromCards`, `RecoverFromTrash` |
| **GameActionGateway** | **모든 행동의 단일 입구**(배치/공격/스킬…) | `Submit`, `Execute` |
| **ChoiceBroker** | **모든 선택의 단일 지점**(카드선택/방어) | `PickCard`, `DecideDefense` |
| **NetHub / PhotonNetManager** | 네트워크 경계(오프라인/온라인 공통) | `NetHub.*`, `PhotonNetManager.OnEvent` |
| **CardLookup** | cardId → CardData 조회(네트워크 복원용) | `ById` |

---

## 3. 효과 시스템 — 이 프로젝트의 심장 ❤️

카드 효과가 **710종**인데도 관리 가능한 이유는 두 가지 설계 덕분입니다.

### (1) 텍스트 파싱 금지 → `effectTypes` 리스트 + 핸들러 레지스트리
카드는 효과를 **문자열 리스트**로 가집니다: `"AttackerPowerBoost:1000"`, `"Chain:C:3"` 처럼 `"타입:값"` 형식.
엔진은 이 타입을 **Dictionary로 디스패치**합니다 (거대한 switch가 아님):

```
card.EffectTypes 순회 → _entryHandlers[type] 호출
                        (Entry/Attacker/Exit/Trigger/Skill 별 레지스트리)
```

> **새 효과 추가법**: 해당 `EffectSystem.*Handlers.cs`에 핸들러 메서드 1개 작성 + `Init*Handlers()`에 1줄 등록. **엔진(OnUnitPlaced 등)은 안 건드림.**

### (2) partial class 분할 — `EffectSystem`이 여러 파일
`EffectSystem`은 한 클래스지만 파일이 쪼개져 있습니다 (`partial class`):

| 파일 | 담당 |
|---|---|
| `EffectSystem.core` | 공통/헬퍼 진입 |
| `EffectSystem.Entry` / `.EntryHandlers` | 배치 시 효과(엔트리) |
| `EffectSystem.Attacker` / `.AttackerHandlers` | 공격 시 효과(어태커·돌파) |
| `EffectSystem.Exit` | 트래시될 때 효과(엑시트) |
| `EffectSystem.Trigger` / `.TriggerHandlers` | 대미지존 트리거 |
| `EffectSystem.Skill` / `.SkillHandlers` | 스킬 카드 효과 |
| `EffectSystem.Active` | 유닛/리더 액티브 (+ 리더 액티브·패시브) |
| `EffectSystem.Passive` / `.Boosts` | 패시브 파워/히트, 버프 저장소 |
| `EffectSystem.Helpers` | 공용 선택/이동 헬퍼 |
| `EffectSystem.BT04/BT05/BT06/BT07/SB01/SB02` | **세트 격리** — 세트별 복잡 효과를 한 파일에 몰아 `Init*Handlers()`로 등록 |

**파워/히트 계산의 흐름** (전투 수치가 어떻게 나오나):
`CombatManager.GetEffectivePower` → `EffectSystem.GetEffectivePower` → `기본파워 + 부스트(턴/공격) + GetPassiveBonus(아군아우라·자기패시브) + 아이템 + 리더패시브` → `Mathf.Max(0, …)`.

---

## 4. 입력의 흐름 — 게이트웨이 / 브로커 / NetHub

"누가(플레이어/AI/원격) 무엇을(행동/선택) 하는가"를 **세 개의 단일 지점**으로 모았습니다. 덕분에 **오프라인·온라인이 같은 코드**로 돕니다.

```
UI/AI ──▶ GameActionGateway.Submit(GameAction)   ← 모든 "행동"(배치/공격/스킬…)의 입구
              │  권위(오프라인/호스트)면 → 검증 후 Execute
              │  온라인 클라면 → NetHub.SendAction (호스트가 대신 실행)
              ▼
          ChoiceBroker.PickCard / DecideDefense    ← 모든 "선택"(카드/방어)의 지점
              │  로컬 인간 → 팝업 / AI → 자동 / 온라인 원격 → NetHub 요청
              ▼
          NetHub → INetChannel                      ← 네트워크 경계
              ├─ LocalChannel   (오프라인: 내가 곧 권위)
              └─ PhotonNetManager (온라인: 호스트권위 + 상태복제 EV_*)
```

- **행동은 게이트웨이**, **선택은 브로커**, **네트워크는 NetHub** — 이 세 곳 안쪽에만 "오프라인/온라인 분기"가 있고, UI·AI·효과 코드는 그걸 몰라도 됩니다.
- 온라인은 **호스트 권위**: 호스트만 시뮬레이션하고, 상태 스냅샷(`EV_STATE`)을 클라에 복제. 상대 손패는 개수만 보냄(히든 정보).

---

## 5. 데이터 & 저장

`Assets/Scripts/Data/`

| 타입 | 역할 |
|---|---|
| `CardData` / `LeaderData` | 카드/리더 정의(ScriptableObject). 파워·히트·`EffectTypes` 등 |
| `DeckData` | 런타임 덱(리더 + 카드 리스트) |
| `CustomDeckSave` | 저장용 덱(JSON: deckName/leaderId/cardIds/savedAtUtc) |
| `DeckSaveLoad` | 덱 저장 **파사드** — 실제 저장은 `IDeckStore`에 위임 |
| `IDeckStore` / `LocalDeckStore` / `UgsDeckStore` | 저장 백엔드 경계 + 로컬/클라우드 구현 |
| `AuthManager` | 인증 **파사드** — 실제 인증은 `IAuthProvider`에 위임 |
| `IAuthProvider` / `LocalAuthProvider` / `UgsAuthProvider` | 인증 백엔드 경계 + 기기전용/UGS 구현 |
| `DeckValidator` | 덱 합법성 검증(40장/3장/트리거8/속성서약) — 치트 방지 |

> 커스텀 덱은 `persistentDataPath`(앱 바깥)에 저장 → 업데이트해도 유지.

---

## 6. 계정과 덱 저장 — 경계를 먼저 세우기

로그인과 클라우드 덱 동기화는 **4장의 NetHub와 똑같은 방식**으로 만들었습니다.
게임 코드는 파사드만 부르고, 실제 구현은 뒤에서 갈아끼웁니다.

```
게임 코드 (덱빌더 · DeckSelect · GameManager · DeckValidator …)
      │  DeckSaveLoad.Save / Load / GetSavedDeckNames / Delete
      ▼
  DeckSaveLoad  (파사드: public static IDeckStore Store)
      ├─ LocalDeckStore(accountId)   로그아웃/오프라인
      └─ UgsDeckStore(accountId)     로그인 — 로컬 캐시 + Cloud Save

  LoginView ──▶ AuthManager (파사드: public static IAuthProvider Provider)
                   ├─ LocalAuthProvider   이 기기 전용 프로필(백엔드 없이 흐름 테스트용)
                   └─ UgsAuthProvider     UGS 아이디/비밀번호 = 진짜 계정
```

**두 경계를 잇는 곳은 `AuthManager.ApplyDeckScope()` 한 곳뿐**입니다. 로그인 상태가 바뀌면 여기서
`DeckSaveLoad.Store`를 갈아끼웁니다 — 그래서 덱빌더도 DeckSelect도 "지금 로그인 상태인가"를 몰라도 됩니다.

> 이 구조 덕분에 **로컬 저장 → 계정별 저장 → 클라우드 동기화**로 두 번 바뀌는 동안
> `DeckSaveLoad` 호출부(12곳)는 한 줄도 바뀌지 않았습니다.

### 저장 위치

```
로그아웃   persistentDataPath/CustomDecks/*.json                  ← ★ 기존 경로 그대로
로그인     persistentDataPath/CustomDecks/u_{PlayerId}/*.json
삭제 표식  persistentDataPath/DeckSync/u_{PlayerId}.tombstones.json
클라우드   Cloud Save 키 "custom_decks" (덱 전체 + 삭제 표식을 JSON 하나로)
```

- ★ **로그아웃 경로는 절대 바꾸지 말 것.** 이미 배포된 빌드에서 만든 덱이 그대로 보여야 합니다.
- 로그인 직후 로그아웃 상태의 덱을 계정으로 **복사**할지 묻습니다(원본은 남음).
- 표식 파일을 덱 폴더 안에 두면 `LocalDeckStore`가 `*.json`을 전부 덱으로 읽어 **목록에 섞입니다** → 별도 폴더.

### 왜 이렇게 했나 (설계 결정)

| 결정 | 이유 |
|---|---|
| `IDeckStore`가 **동기(sync)** 시그니처 | async로 갔으면 덱빌더·DeckSelect·GameManager가 전부 async로 뒤집힌다. 읽기는 로컬 캐시라 즉시 답하고, 쓰기는 로컬 반영 후 전송이 뒤따른다 → **오프라인·서버장애에도 게임이 돈다** |
| Cloud Save **키 하나**에 덱 전체 | 덱 이름을 키로 쓰면 한글이 문자 제약에 걸리고, 키를 나누면 일부만 동기화돼 불일치가 생긴다. 덱 1개≈수백 B라 수십 개여도 수십 KB |
| 병합은 **시각 비교 + 합집합** | `savedAtUtc`가 큰 쪽이 승. 한쪽에만 있는 덱은 둘 다 보존 → **어느 쪽 덱도 조용히 사라지지 않는다** |
| 삭제는 **표식(tombstone)** | 그냥 지우면 그 덱을 아직 가진 다른 기기가 "저쪽에 없네" 하며 되살린다. 대신 "이 이름은 언제 삭제됐다"를 남기고 표식도 함께 동기화한다(분산 DB의 통상 방식). 90일 후 청소 |
| 로그인 UI를 **코드로 생성** | `VersionChecker`와 같은 방식. 씬 배치·프리팹 연결이 없어 **에디터 작업 0** |

### `LocalAuthProvider`는 보안이 아니다

계정 파일이 그 PC에만 있어 지우거나 바꿔치기하면 그만입니다. "한 PC를 여러 명이 쓸 때 덱 분리" 용도이자,
백엔드 없이 UI·흐름을 완성하기 위한 구현입니다. **진짜 계정은 `UgsAuthProvider`가 담당**합니다 —
비밀번호가 이 PC에 남지 않고(서버가 해시 보관, 로컬엔 세션 토큰만), 계정 판정이 서버에 있습니다.

### 알려진 한계

- 두 기기에서 같은 덱을 각각 고치면 **나중에 저장한 쪽이 이깁니다**(자동 병합 아님)
- 표식 보존 기간(90일)이 지난 뒤 오래 안 켠 기기가 접속하면 삭제한 덱이 되살아날 수 있습니다
- UGS는 한 서비스라도 무료 한도를 넘기면 **API 전체가 차단**됩니다

---

## 7. UI (`Assets/Scripts/UI/`)
- `LaneSlot` — 필드 한 칸(드롭·배치·공격·수치표시)
- `HandView` / `HUDView` / `PhaseBarView` — 손패·상태·페이즈바
- `DefensePopup` / `HandPickPopup` — 방어/카드선택 팝업 (브로커가 사용)
- `DeckBuilder/DeckBuilderManager` — 덱빌더
- `LobbyManager` — 온라인 로비(방코드·덱 전송)
- `ResultView` — 승패 화면
- `LoginView` / `VersionChecker` — 로그인 창·업데이트 알림. **둘 다 씬 배치 없이 코드로 UI 생성**

---

## 학습 추천 순서

작은 것 → 큰 것, 데이터 → 흐름 → 효과 순으로 읽으면 이해가 빠릅니다.

1. **데이터가 뭔지**: `CardData` · `LeaderData` · `DeckData` · `PlayerController` (한 장의 카드/한 명의 플레이어가 무엇인가)
2. **게임 흐름**: `TurnManager` (턴 루프) → `GameManager` (시작~승패)
3. **보드와 전투**: `FieldManager` (필드 상태) → `CombatManager` (공격 해결) — 이벤트 `OnUnitPlaced` 흐름 따라가기
4. **효과의 핵심**: `EffectSystem.core` → 핸들러 파일 하나(`EffectSystem.EntryHandlers`) → `EffectActions` (효과가 원자 블록으로 어떻게 조립되나). **여기가 제일 중요.**
5. **입력의 흐름**: `GameActionGateway` → `ChoiceBroker` (행동·선택이 어떻게 한 곳으로 모이나)
6. **온라인**: `NetHub`/`INetChannel` → `PhotonNetManager` (오프라인 코드가 어떻게 온라인이 되나)
7. **계정·저장**: `DeckSaveLoad`/`IDeckStore` → `AuthManager`/`IAuthProvider` → `UgsDeckStore`
   — 6번과 **똑같은 경계 패턴**이라, 6번을 읽었다면 금방 읽힙니다
8. **UI**: `LaneSlot` → `HandView` → `DeckBuilderManager`

> 팁: 공부하며 "이 함수 뭐지?" 싶은 게 나오면, 각 `EffectSystem.*Handlers.cs` / `BT0X.cs`의 **핸들러 주석에 카드번호·효과텍스트가 병기**돼 있어 대조하기 좋습니다. 전체 설계·진행 메모는 `CLAUDE.md` 참고.
