# 오프라인 게임을 UI 수정 없이 온라인으로 확장하기 — 모든 행동을 관문 하나로 모으기

> Unity로 만든 TCG(니벨아레나)를 오프라인으로 완성한 뒤 온라인 대전을 붙이면서 얻은,
> 이 프로젝트에서 가장 값진 설계 이야기.

---

## 1. 상황 — 다 만들어놓고 온라인을 붙이려니

카드 배치, 업그레이드, 아이템 장착, 공격, 스킬, 리더 액티브.
게임의 "행동"은 6가지였고, 오프라인 대전(플레이어 vs AI)까지는 잘 굴러갔습니다.

그런데 온라인 대전을 붙이려고 보니, 행동 하나를 처리하는 상황이 **4가지**로 늘어나 있었습니다.

| 상황 | 해야 할 일 |
|---|---|
| 오프라인 | 내가 곧 심판 → 바로 실행 |
| 온라인 **호스트** | 내가 심판 → 실행 후 상대에게 결과 복제 |
| 온라인 **클라이언트** | 나는 심판 아님 → 호스트에게 "이거 해줘" 전송 |
| AI | 자동 판단 후 실행 |

아무 대비 없이 이 상황을 맞았다면, 코드는 이렇게 됐을 겁니다.

```csharp
// 😱 나쁜 예 — 배치 코드에 이 분기가 들어간다
if (online && isHost)       { 배치실행(); 상대에게복제(); }
else if (online && !isHost) { 호스트에게전송(); }
else                        { 배치실행(); }
```

문제는 이 덩어리가 **배치, 업그레이드, 장착, 공격, 스킬, 리더액티브마다 복붙**된다는 것입니다.
게다가 배치는 UI(드래그앤드롭)에서도 하고 AI도 하니까, 같은 분기가 두 벌씩 생깁니다.
네트워크 코드가 UI 전체에 번지고, 새 행동을 추가할 때마다 이 의식을 반복해야 합니다.

---

## 2. 아이디어 — 변하는 것을 한 곳에 가둔다

해법은 단순합니다. **"온라인이냐 아니냐"라는 변수를 아는 곳을 최소한으로 줄이는 것.**
그래서 통과 지점(choke point)을 세 개만 만들었습니다.

| 무엇이 지나가나 | 단일 창구 | 역할 |
|---|---|---|
| 행동 (배치/공격/스킬…) | `GameActionGateway` | 모든 행동의 유일한 입구 |
| 선택 (카드/방어) | `ChoiceBroker` | 모든 선택의 유일한 지점 |
| 네트워크 | `NetHub` | 오프라인/온라인의 경계 |

🏤 **우체국 창구** 비유가 잘 맞습니다.
편지를 창구 하나에 내면, 그게 동네 배달이든 해외 배송이든 처리 방식은 **우체국 안에서** 정해집니다.
편지 내는 사람(UI)은 그걸 알 필요가 없습니다.

이 글에서는 그중 첫 번째, `GameActionGateway`를 뜯어봅니다.

---

## 3. 행동을 "데이터"로 만든다

먼저 행동을 실행이 아니라 **요청서**로 바꿉니다. 아직 아무 일도 일어나지 않은 순수한 데이터입니다.

```csharp
public enum GameActionType
{
    PlaceUnit, Upgrade, EquipItem, DeclareAttack, PlaySkill, LeaderActive,
}

public struct GameAction
{
    public GameActionType Type;
    public bool     IsPlayer;   // 행동 주체 (true=플레이어, false=AI/원격 상대)
    public int      Lane;
    public CardData Card;
}
```

행동이 데이터가 되는 순간 **전송할 수 있게 됩니다.** 이게 온라인 확장의 씨앗이었습니다.
"실행"은 전송할 수 없지만 "요청서"는 네트워크로 보낼 수 있으니까요.

---

## 4. ★ 흔한 오해 — 관문은 허가증 발급소가 아니다

제가 처음에 헷갈렸던 지점이고, 아마 가장 중요한 부분입니다.

> ❌ "UI가 Gateway에 물어보고, **된다고 하면 UI가 배치한다**"
> ✅ "Gateway가 검증하고 **배치까지 끝낸 뒤**, 결과만 알려준다. UI는 **화면만** 갱신한다"

관문을 도입한다는 건 호출부 밑에 `Submit()` 한 줄을 **덧붙이는** 게 아니라,
**실행 로직 자체를 관문 안으로 이사시키는 것**입니다.

관문 안쪽(`GameActionGateway.ExecutePlaceUnit`):

```csharp
private static bool ExecutePlaceUnit(GameAction a)
{
    // ── 검증 ──
    if (!IsTurnPhase(a.IsPlayer, PhaseType.Main))              return false; // 상대 턴 배치 차단
    if (!FieldManager.Instance.CanPlayCard(a.IsPlayer, card, pc.Size)) return false; // 코스트
    if (!FieldManager.Instance.CanPlace(a.IsPlayer, a.Lane))    return false; // 레인 점유
    if (EffectSystem.Instance.IsLaneDeployBlocked(...))         return false; // 효과에 의한 배치 금지

    // ── 실행 (실무는 기존 매니저들이) ──
    FieldManager.Instance.PlaceUnit(a.IsPlayer, a.Lane, card);
    pc.Hand.Remove(card);
    EffectSystem.Instance.OnUnitPlaced(a.IsPlayer, a.Lane, card); // 엔트리 효과 발동
    return true;
}
```

바깥쪽(UI — `LaneSlot.OnDrop`)에 남은 코드:

```csharp
bool placed = GameActionGateway.Submit(new GameAction {
    Type = GameActionType.PlaceUnit, IsPlayer = true, Lane = LaneIndex, Card = card,
});
if (!placed)
{
    cardView.ReturnToHand();   // 거부됐으니 카드를 손으로 되돌린다
    return;
}

// 성공 — 여기서부터는 전부 "그림" 작업이다
Destroy(cardView.gameObject);      // 손에 있던 카드 그림 제거
HandView.Instance.RefreshHand();   // 손패 재정렬
HUDView.NotifyStatusChanged();     // 코스트 숫자 갱신
RefreshUI();                       // 레인에 카드 그리기
```

`Destroy`, `Refresh`, 다시 그리기. **게임 로직은 한 줄도 없습니다.**
`bool placed`에 답이 돌아온 시점에는 유닛이 **이미 필드에 서 있습니다.**

🏦 은행 창구와 같습니다. 출금 신청서를 내면 직원이 잔액을 확인하고 **실제로 계좌에서 돈을 빼서** 현금을 줍니다.
나는 그걸 지갑에 넣을 뿐입니다. 내가 계좌를 직접 건드리지도 않고, 창구가 "출금해도 됩니다~"라고 허락만 하고 마는 것도 아닙니다.

### 덤: `false` = "아무 일도 일어나지 않았다"

검증에 걸리면 상태를 건드리기 **전에** `return false` 합니다.
그래서 실패는 항상 원자적입니다 — "반쯤 배치된" 상태가 존재할 수 없습니다.
덕분에 UI는 안심하고 카드를 손으로 되돌릴 수 있습니다.

---

## 5. 그래서 호출부는 딱 11곳

이 구조에서 게임 상태를 바꿀 수 있는 입구는 전부 여기뿐입니다.

```
LaneSlot.cs         4곳  (장착 / 업그레이드 / 배치 / 공격선언)
SkillZoneSlot.cs    1곳  (스킬)
HUDView.cs          1곳  (리더 액티브)
AIController.cs     4곳  (배치 / 업그레이드 / 아이템 / 공격)
PhotonNetManager.cs 1곳  (네트워크 수신 — 6장 참고)
```

**입구는 11개, 문은 1개.**

여기서 조용히 중요한 사실 하나. **AI도 사람과 똑같은 관문을 지납니다.**

```csharp
// AIController — IsPlayer만 false일 뿐, 사람과 완전히 같은 코드
if (GameActionGateway.Submit(new GameAction {
        Type = GameActionType.PlaceUnit, IsPlayer = false, Lane = lane, Card = card }))
{ ... }
```

덕분에 "AI는 코스트 검증을 안 해서 치트가 된다" 같은 부류의 버그가 구조적으로 사라집니다.
검증 코드가 한 벌뿐이니 둘이 어긋날 수가 없습니다.

---

## 6. 온라인 붙이기 — 실제로 고친 건 이 8줄

여기가 하이라이트입니다. 온라인 대전을 붙이면서 **행동 관련해서 수정한 코드는 `Submit()` 안쪽이 전부**입니다.

```csharp
public static bool Submit(GameAction action)
{
    // 권위(오프라인 or 온라인 호스트)면 직접 검증·실행
    if (NetHub.IsAuthority)
    {
        bool ok = Execute(action);
        if (ok && NetHub.IsOnline)
        {
            Narrate(action);           // 상대에게 "상대: OO 배치" 토스트
            NetHub.BroadcastState();   // 실행 결과를 상태 스냅샷으로 복제
        }
        return ok;
    }

    // 온라인 클라면 호스트에게 요청서를 전송
    NetHub.SendAction(action);
    return true; // 전송 "접수" (실제 반영은 호스트의 복제로 도착)
}
```

`LaneSlot`, `SkillZoneSlot`, `HUDView`, `AIController` — **호출부는 한 글자도 바뀌지 않았습니다.**
배치도, 공격도, 스킬도, 리더 액티브도 전부 이 문을 지나니까 한 번에 온라인을 지원하게 됩니다.

### 클라의 `true`는 "성공"이 아니라 "접수됨"

미묘하지만 재미있는 지점입니다.
내가 클라이언트면 `Submit`은 **아무것도 실행하지 않고** 전송만 한 뒤 `true`를 돌려줍니다.
UI는 그것도 모르고 평소처럼 카드 그림을 지우고, 진짜 결과는 잠시 후 **호스트가 보낸 상태 스냅샷**이 도착해 화면을 덮어씁니다.

즉 UI는 자기가 오프라인인지 호스트인지 클라인지 **끝까지 모릅니다.** 알 필요가 없습니다.

### 그리고 네트워크 수신도 같은 문으로 들어온다

호스트가 클라의 행동을 수신했을 때 하는 일이 이겁니다.

```csharp
// PhotonNetManager — 받은 요청서를 그대로 같은 문에 집어넣는다
GameActionGateway.Submit(new GameAction {
    Type = type, IsPlayer = false, Lane = lane, Card = card });
```

호스트 입장에선 "내 UI가 낸 요청"이든 "네트워크로 온 요청"이든 **구분 없이 똑같이 검증·실행**됩니다.
네트워크 수신 전용 실행 경로를 따로 만들 필요가 없다는 뜻이고,
"클라가 조작된 행동을 보내면?" 하는 걱정도 관문의 검증이 그대로 막아줍니다. (호스트 권위)

---

## 7. NetHub — 한 줄 교체로 오프라인↔온라인 (전략 패턴)

`Submit` 안에서 판단 근거로 쓰인 `NetHub.IsAuthority`는 어디서 오는 걸까요?

```csharp
public static class NetHub
{
    public static INetChannel Channel = new LocalChannel();   // 기본: 오프라인

    public static bool IsOnline    => Channel.IsOnline;       // 채널에 물어본다
    public static bool IsAuthority => Channel.IsAuthority;
    public static void SendAction(GameAction a) => Channel.SendAction(a);
}
```

`INetChannel` 구현은 두 개뿐입니다.

```csharp
public class LocalChannel : INetChannel {          // 오프라인
    public bool IsOnline    => false;
    public bool IsAuthority => true;               // 오프라인은 내가 유일한 심판
}

public class PhotonNetManager : ..., INetChannel { // 온라인
    public bool IsOnline    => PhotonNetwork.InRoom;
    public bool IsAuthority => PhotonNetwork.IsMasterClient;  // 호스트만 심판
}
```

그래서 모드 전환이 **대입문 한 줄**입니다.

```csharp
NetHub.Channel = photonNetManager;    // 온라인 진입
NetHub.Channel = new LocalChannel();  // 오프라인 복귀
```

전형적인 **전략 패턴(Strategy)** — 같은 인터페이스를 다르게 구현해두고 런타임에 갈아끼우는 것.
재밌는 건 이 프로젝트에 같은 패턴이 하나 더 있다는 점입니다.
덱 저장도 `IDeckStore`(로컬 JSON ↔ 클라우드)로 경계를 잘라뒀고, `DeckSaveLoad.Store` 교체 한 줄로 백엔드가 바뀝니다.
**"변하는 축을 인터페이스로 뽑고, 나머지 코드는 그걸 모르게 한다"** 는 같은 사고의 반복입니다.

역할을 정확히 말하면 이렇습니다.

- **NetHub** = "지금 온라인인가? 내가 권위인가?"를 **알려주고**, 전송을 **담당**하는 경계
- **Gateway** = 그 답을 보고 **실행할지 전송할지 분기**

NetHub이 모든 걸 결정하는 게 아니라, 판단 재료를 제공하고 판단은 관문이 합니다.

---

## 8. 선택은 왜 따로 있나 — ChoiceBroker

행동은 `bool` 하나로 끝나지만, **"카드를 고르세요", "방어할까요?"** 는 그렇지 않습니다.
사람이면 팝업을 띄우고 기다려야 하고, AI면 즉시 자동 판단, 원격 상대면 네트워크 왕복이 필요합니다.

```csharp
// ChoiceBroker — 선택의 단일 지점
if (IsLocalHuman)          팝업을띄운다(onPicked);
else if (NetHub.IsOnline)  NetHub.RequestCard(...);   // 상대에게 물어본다
else                       AI자동선택();
```

동기(`bool` 반환)와 비동기(콜백)라는 성질이 다르기 때문에 관문과 브로커를 나눴습니다.
그리고 `PickFromCards`를 쓰는 **수십 개의 카드 효과가 자동으로 온라인을 지원**하게 됐습니다 — 효과 코드는 "누가 고르는지" 모른 채로요.

정리하면 역할 분담은 이렇습니다.

| 성격 | 담당 | 형태 |
|---|---|---|
| 배치/공격/스킬발동 — 즉시 판정 | `GameActionGateway` | 동기, `bool` 반환 |
| 카드선택/방어 — 물어봐야 함 | `ChoiceBroker` | 비동기, 콜백 |
| 온라인/오프라인 판별·전송 | `NetHub` | 상태 제공 + 경계 |

---

## 9. 솔직한 한계 — 이건 컴파일러가 아니라 사람이 지키는 규칙이다

미화하지 않고 적어두고 싶은 부분입니다.

**C#은 "모든 행동은 관문을 지나라"를 강제해주지 않습니다.**
마음만 먹으면 UI에서 `FieldManager.PlaceUnit()`을 직접 부를 수 있습니다.
관문은 컴파일러가 지키는 제약이 아니라 **개발자가 지키는 규율(convention)** 이고,
한 곳이라도 우회하면 **그 행동만 온라인에서 조용히 깨집니다.**

실제로 겪었습니다. 클라이언트의 공격이 우회 경로(`ForceAttackLane` — 원래는 특정 카드의 보너스 공격용이라 "턴당 1회" 제한을 걸지 않는 함수)를 타는 바람에 공격 기록이 남지 않았고, **클라만 무한 공격이 가능**했습니다.
고친 방법도 결국 "관문으로 되돌리기"였습니다 — 검증과 기록을 `Submit` 시점에 하도록 옮기니 해결됐습니다.

그리고 아직 문 밖에 남은 것도 있습니다.

```csharp
// ExecutePlaySkill — 검증 + 코스트 소비(패→스킬존)까지만!
pc.Hand.Remove(card);
pc.SkillZone.Add(card);
return true;
```

스킬의 **효과 해결과 타겟 선택은 아직 호출부에 남아 있습니다.**
팝업이 뜨고 콜백이 이어지는 비동기 흐름이라 `bool` 반환 하나로 끝나지 않기 때문입니다.
설계는 완성형으로 태어나지 않고, 이렇게 경계가 조금씩 정리되어 갑니다.

---

## 10. 정리

- **모든 행동은 `GameActionGateway` 한 문으로, 모든 선택은 `ChoiceBroker` 한 지점으로, 네트워크는 `NetHub` 한 경계로.**
- 관문은 허가증 발급소가 아니라 **실행자**다. UI에 남는 건 화면 갱신뿐이고, 그래서 UI는 자기가 온라인인지도 모른다.
- 그 결과 **오프라인 게임을 UI·AI·카드 효과 코드 수정 없이 온라인으로 확장**할 수 있었다. 실제로 바꾼 건 `Submit()` 안쪽 여덟 줄이었다.

설계의 맛은 결국 이 한 문장이라고 생각합니다.

> **변하는 것(네트워크 유무)을 한 곳에 가두고, 나머지는 그것을 모르게 한다.**
