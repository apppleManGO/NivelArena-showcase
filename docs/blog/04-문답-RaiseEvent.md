# [문답] Photon의 `RaiseEvent` — 함수를 부르지 않고 소포를 보낸다

> Unity TCG(니벨아레나) 개발기 부록.
> [3편](03-상태복제-무엇을-보내지-않을것인가.md)에서 다룬 상태 복제가 **실제로 어떤 수단으로 전송되는지**에 대한 문답 정리입니다.
> 공부하면서 실제로 오갔던 질문 순서 그대로 실었습니다.

---

## Q1. 모든 행동을 서버에 보내서 검증받는 방식이잖아. RPC 통신? 그런 이름이 있었던 것 같은데

방향은 맞습니다. **RPC(Remote Procedure Call, 원격 프로시저 호출)** — "저쪽 컴퓨터의 함수를 여기서 호출한다"는 개념이고, 네트워크 게임에서 가장 널리 쓰이는 용어입니다.

다만 Photon(PUN)에는 원격 호출 방식이 **두 가지** 있고, 이 프로젝트는 그중 두 번째를 씁니다.

```csharp
// ① PunRPC — 저쪽의 "함수 이름"을 불러서 실행시킨다
photonView.RPC("PlaceUnit", RpcTarget.Others, lane, cardId);

// ② RaiseEvent — 번호 붙인 "소포"를 보낸다
PhotonNetwork.RaiseEvent(EV_STATE, payload, options, sendOptions);
```

실제로 세어보면 이 프로젝트는 **`PunRPC` 0회, `RaiseEvent` 30회**입니다. ②만 씁니다.

### 왜 ②인가

**첫째, `PhotonView`가 필요 없습니다.**
RPC는 오브젝트(총알, 캐릭터)마다 `PhotonView` 컴포넌트를 붙여 네트워크 ID를 부여하는 구조입니다.
그런데 이 게임에는 **동기화할 오브젝트가 없습니다.** 판 전체가 하나의 상태 덩어리라서, 오브젝트 단위 동기화가 아예 맞지 않습니다.

**둘째, "저쪽 함수를 실행시킨다"는 발상 자체를 피하고 싶었습니다.**
RPC를 쓰면 자연스럽게 `RPC("PlaceUnit", ...)` 같은 코드를 쓰게 되고, 그건 **클라이언트가 게임 로직을 실행하게 되는 길**입니다.
3편에서 본 "엔트리 효과가 양쪽에서 두 번 터지는" 문제로 직행합니다.

`RaiseEvent`는 데이터만 던지고, **받는 쪽이 그걸 어떻게 해석할지 스스로 정하게** 강제합니다.
이 프로젝트에서 그 해석 지점이 `ReplicateSetUnit()` 같은 **그리기 전용** 함수입니다.

| | `[PunRPC]` | `RaiseEvent` |
|---|---|---|
| 발상 | **"저쪽 함수를 호출"** | **"저쪽에 데이터 전달"** |
| 필요한 것 | `PhotonView` 컴포넌트 | 없음 |
| 메서드 지정 | 문자열 이름 | 직접 스위치 분기 |
| 어울리는 곳 | 오브젝트별 동기화(이동, 발사) | 게임 전체 상태·요청/응답 |

---

## Q2. 그럼 `RaiseEvent`는 정확히 뭘 하는 함수야?

한 줄로 말하면 이렇습니다.

> **"번호가 적힌 소포를 방 안의 누군가에게 보낸다."**

인자가 4개입니다. 클라가 행동을 보내는 실제 코드로 보겠습니다.

```csharp
PhotonNetwork.RaiseEvent(
    EV_ACTION,                                                        // ① 무슨 소포인가 (byte)
    data,                                                             // ② 내용물 (object)
    new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },  // ③ 누구에게
    SendOptions.SendReliable);                                        // ④ 어떻게 보낼까
```

### ① 이벤트 코드 — 소포에 붙이는 번호표

`byte`라서 **0~255 사이 숫자 하나**입니다. 이걸로 "무슨 우편인지"를 구분합니다.

```csharp
private const byte EV_ACTION = 1;
private const byte EV_STATE  = 4;
```

⚠️ **0~199만 쓸 수 있습니다.** 200 이상은 Photon이 내부적으로 씁니다(방 입장, 속성 동기화 등).

### ② 내용물 — 아무거나 넣을 수 있지만, 아무거나는 아니다

```csharp
object[] data = {
    (byte)action.Type,                                  // enum → byte
    action.Lane,                                        // int
    action.Card != null ? action.Card.CardId : "",      // string
};
```

`object` 타입이라 뭐든 될 것 같지만, **Photon이 직렬화할 수 있는 타입만** 됩니다.

| 보낼 수 있음 | 보낼 수 없음 |
|---|---|
| `int`, `float`, `bool`, `string`, `byte` | `CardData` (ScriptableObject) |
| 위 타입의 배열 (`string[]`, `object[]`) | `GameObject`, `MonoBehaviour` |
| `Vector3`, `Quaternion` | 임의의 커스텀 클래스 |

**이것이 카드를 `CardId` 문자열로 주고받는 이유 중 하나입니다.** `CardData`는 애초에 전송이 불가능합니다.
보낼 수 있다 해도 카드 한 장에 필드가 수십 개라 무겁고요.

```
보낼 때:  card.CardId          → "BT07-045"      (문자열 9바이트)
받을 때:  CardLookup.ById(id)  → 원래 CardData   (자기 에셋에서 복원)
```

양쪽이 같은 카드 에셋을 빌드에 갖고 있으니 가능한 방식이고, 덤으로 **치트 방지**이기도 합니다.
클라가 "내 카드 파워는 99999"라고 우겨도, 호스트는 자기 에셋을 보고 계산하니까요.

### ③ 수신자 — 여기에 권한 구조가 그대로 드러난다

```csharp
ReceiverGroup.MasterClient  // 호스트에게만  (클라→호스트 요청)
ReceiverGroup.Others        // 나 빼고 전부  (호스트→클라 복제)
ReceiverGroup.All           // 나 포함 전부
```

이 프로젝트는 앞의 둘만 씁니다. **`All`은 쓰지 않습니다.**

호스트는 자기 스냅샷을 받을 이유가 없기 때문입니다 — 자기가 원본이니까요.
`All`로 보냈다면 호스트가 자기 상태를 자기 미러 코드로 덮어써서 판이 뒤집혔을 겁니다.

### ④ 전송 옵션 — 안전 배송이냐 빠른 배송이냐

```csharp
SendOptions.SendReliable     // 보장: 반드시 도착 + 보낸 순서대로
SendOptions.SendUnreliable   // 무보장: 유실·역전 가능, 대신 빠름
```

이 프로젝트는 **전부 `SendReliable`** 입니다.

> 스냅샷이 순서 없이 도착하면 **옛 상태가 최신 상태를 덮어씁니다.** 판이 과거로 되돌아갑니다.

`Unreliable`은 "매 프레임 위치를 보내는" 경우에 씁니다. 하나 유실돼도 다음 프레임이 곧 오니까요.
카드 게임의 "배치했음"은 유실되면 그것으로 끝입니다.

---

## Q3. 받는 쪽은 어떻게 처리해?

인터페이스 하나 붙이고, 스위치 하나로 분배합니다.

```csharp
public class PhotonNetManager : MonoBehaviourPunCallbacks, INetChannel, IOnEventCallback
//                                                                      ↑ 이걸 붙이면 우편함이 열린다
{
    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)                                    // 번호표를 보고
        {
            case EV_ACTION: OnActionReceived(photonEvent); break;    // 담당자에게 배달
            case EV_STATE:  OnStateReceived(photonEvent);  break;
            // ... 12종
        }
    }
}
```

`IOnEventCallback`을 구현하면 `OnEvent`가 자동 호출됩니다.
(등록 코드가 보이지 않는 이유는 `MonoBehaviourPunCallbacks`가 대신 해주기 때문입니다.)

내용물은 **보낸 순서대로 캐스팅**해서 꺼냅니다.

```csharp
var payload = (object[])e.CustomData;
var f       = (string[])payload[0];
bool turn   = (bool)payload[1];
var phase   = (PhaseType)(byte)payload[2];
```

---

## Q4. 이 방식의 약점은 없어?

있습니다. 위 코드에서 이미 보입니다.

```csharp
var items = (string[])payload[15];   // 15번 칸이 아이템 배열이라는 걸 어떻게 알까?
```

**아무도 알려주지 않습니다. 그냥 약속입니다.**

- 보내는 쪽이 15번에 아이템을 넣고, 받는 쪽이 15번에서 꺼내기로 **암묵적으로** 합의한 것
- 중간에 칸을 하나 끼워 넣으면 **그 뒤가 전부 밀려서** 엉뚱한 데이터를 엉뚱한 타입으로 캐스팅 → 런타임 크래시
- **컴파일러는 아무것도 잡아주지 못합니다**

그래서 실제로 방어 코드가 들어가 있습니다. 방어 요청 페이로드에 나중에 파워 수치를 추가했을 때 이렇게 처리했습니다.

```
수신측은 data.Length 체크로 구버전 페이로드 폴백.
수치 인자는 전부 기본값 -1(= 인쇄 파워로 폴백)이라 누락돼도 안전.
```

**배열 길이를 세서 "이 소포는 구버전이구나"를 판단**합니다. 원시적이지만 실전에서 필요한 대비입니다.

> 제대로 하려면 페이로드를 배열이 아니라 **이름 있는 구조**(`Dictionary<string, object>`나 커스텀 직렬화 클래스)로 만들어야 합니다. 대신 데이터가 커지고 느려집니다.
> **인덱스 배열 = 가볍지만 깨지기 쉬움 / 이름 있는 구조 = 안전하지만 무거움** 의 트레이드오프입니다.

---

## Q5. 그럼 전체 흐름을 다시 정리하면?

```
① 클라 LaneSlot 드롭
     → GameActionGateway.Submit()
     → NetHub.SendAction()
     → PhotonNetManager.SendAction()      [클라]   RaiseEvent(EV_ACTION → MasterClient)

② 호스트 PhotonNetManager.OnEvent()
     → OnActionReceived()                  [호스트]  받은 cardId로 GameAction 재구성
     → GameActionGateway.Submit()           ← 같은 관문으로 재진입!
     → 검증 + 실행

③ 호스트 GameActionGateway
     → NetHub.BroadcastState()
     → PhotonNetManager.BroadcastState()   [호스트]  RaiseEvent(EV_STATE → Others)

④ 클라 PhotonNetManager.OnEvent()
     → OnStateReceived()                   [클라]   미러 변환 후
     → FieldManager.ReplicateSetUnit()               값 대입 + 화면 갱신
```

**②에서 관문으로 다시 들어가는 것이 핵심입니다.**
호스트 입장에서는 "내 UI가 낸 요청"이든 "네트워크로 온 요청"이든 같은 문으로 들어오므로,
네트워크 전용 실행 경로가 아예 존재하지 않습니다.

### 표현 두 개는 조심해서 쓸 것

처음에 이렇게 이해했다가 고친 부분입니다.

| 흔한 오해 | 실제 |
|---|---|
| 호스트가 클라에게 **"복제 명령"** 을 내린다 | 명령이 아니라 **데이터**다. "이렇게 해라"가 아니라 "지금 이렇게 생겼다" |
| 클라가 받아서 **실행**한다 | **반영**(그리기)한다. 클라에는 게임 로직을 실행할 통로가 없다 |

---

## Q6. 이 통신 코드는 전부 한 파일에 있는 거야?

네. 그리고 그건 **의도된 것**입니다.

```
Photon API를 쓰는 파일 = 딱 2개
├─ PhotonNetManager.cs   587줄 · Photon 참조 49회 · RaiseEvent 30회   ← 통신 전부
└─ LobbyManager.cs        UI 쪽 (방 만들기 / 입장 버튼)
```

`NetHub.cs`, `INetChannel.cs`, `LocalChannel.cs`에도 "Photon"이란 단어가 나오지만 **전부 주석**입니다.

```csharp
// INetChannel.cs
// 네트워크 추상화 — 게임 코드가 Photon에 직접 의존하지 않게 하는 경계.
//  · 게임 코드(관문/중개소)는 이 인터페이스만 안다. Photon SDK 이름은 여기 안 나옴.
```

`GameActionGateway`, `CombatManager`, `LaneSlot` — 어느 파일을 열어도 `PhotonNetwork`라는 단어가 나오지 않습니다.

> 그래서 Photon을 다른 SDK(Mirror, Fusion, 직접 만든 서버)로 갈아끼운다면 **이 파일 하나만 새로 쓰면 됩니다.**
> 게임 코드는 한 줄도 바뀌지 않습니다. 1편의 `INetChannel` 경계가 실제로 값을 하는 지점입니다.

### 파일 안의 구조

587줄이 이렇게 구획돼 있습니다.

```
53   ── 접속/방 입장 ─────────────    Photon 서버 연결, 방 만들기/입장
95   ── 진단 콜백 ───────────────    연결 실패 원인 로그
154  ── INetChannel 구현 ─────────    IsOnline / IsAuthority  ← 게임 코드가 보는 창구
166  ── 행동 릴레이 ──────────────    EV_ACTION
204  ── 상태 복제 ───────────────    EV_STATE
322  ── 페이즈 종료 릴레이 ─────────    EV_ENDPHASE
346  ── OnEvent (우편함) ─────────    받은 소포를 번호별로 분배
365  ── 커멘터리/트리거 릴레이 ──────    EV_TOAST / EV_TRIGGER
401  ── 승패 결과 동기화 ───────────    EV_GAMEOVER
423  ── 원격 멀리건 ──────────────    EV_MULL_REQ / RESP
468  ── 원격 카드 선택 ────────────    EV_CARD_REQ / RESP
535  ── 행동 수신 / 방어 요청·응답 ──    EV_DEFENSE_REQ / RESP
```

**이벤트 12종이 각각 자기 구획을 갖고 있고, 구획마다 모양이 똑같습니다.**

```csharp
// [호스트] 커멘터리 토스트를 클라에 전송.
public void RelayToast(string message) { PhotonNetwork.RaiseEvent(EV_TOAST, ...); }

// [클라] 커멘터리 토스트 수신 → 그대로 표시
private void OnToastReceived(EventData e) { ToastView.Show((string)e.CustomData); }
```

보내는 함수 1개 + 받는 함수 1개가 항상 짝입니다.
주석의 `[호스트]` / `[클라]` 표시가 중요한데, **같은 파일이 두 컴퓨터에서 각각 다른 부분만 실행되기 때문**입니다.
이 표시가 없으면 "이 코드는 누가 실행하는 건가"가 금방 헷갈립니다.

요청/응답이 필요한 것들은 **쌍**으로 존재합니다.

```
EV_DEFENSE_REQ (호스트→클라 "방어할래?")  ↔  EV_DEFENSE_RESP (클라→호스트 "응/아니")
EV_CARD_REQ    ↔  EV_CARD_RESP
EV_MULL_REQ    ↔  EV_MULL_RESP
```

2편의 `WaitUntil` 패턴이 이 왕복 뒤에 숨어 있습니다.
호스트는 `_pendingDefense` 콜백을 들고 기다리다가, `RESP`가 도착하면 그 콜백을 호출해 멈춰 있던 전투 코루틴을 재개시킵니다.

### 587줄이 한 파일인 건 괜찮은가

솔직히 **슬슬 쪼갤 만한 크기**입니다. 지금은 구획 주석으로 버티고 있고, 이벤트가 더 늘면 `partial`로 나누는 것이 자연스러운 다음 수순입니다.

다만 지금 쪼개지 않은 것이 틀린 판단은 아닙니다.
587줄은 한 사람이 전체를 머릿속에 담을 수 있는 크기이고, "통신 코드는 여기 다 있다"는 단순함의 이점이 아직 더 큽니다.

> 파일을 쪼개는 기준은 줄 수가 아니라 **"한 번에 다 읽고 이해할 수 있는가"** 입니다.

---

## 정리

- `RaiseEvent(코드, 데이터, 수신자, 옵션)` — **번호표 붙인 데이터 소포**를 방 안에 보내는 것. 함수 호출이 아니다.
- `PunRPC` 대신 이걸 고른 건 **"저쪽 함수를 실행시킨다"는 발상을 애초에 차단**하기 위해서다.
- **수신자 지정에 권한 구조가 그대로 드러난다** — 행동은 `MasterClient`에게, 상태는 `Others`에게. `All`은 쓰지 않는다.
- **`SendReliable` 필수** — 스냅샷이 역전되면 판이 과거로 돌아간다.
- 보낼 수 있는 건 기본 타입뿐이라, **카드는 `CardId` 문자열로 보내고 받는 쪽이 에셋에서 복원**한다.
- 약점은 **인덱스 기반 페이로드** — 순서가 곧 약속이고 컴파일러가 지켜주지 않는다. 길이 체크로 버전 호환을 방어했다.
- 통신 코드는 **`PhotonNetManager.cs` 한 파일**에 모여 있다. SDK를 갈아끼워도 게임 코드는 바뀌지 않는다.
