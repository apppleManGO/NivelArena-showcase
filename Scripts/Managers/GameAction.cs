// Assets/Scripts/Managers/GameAction.cs
// ─────────────────────────────────────────────────────────────────────────
// 플레이어/AI의 "행동"을 표현하는 데이터 (아직 실행이 아님).
//  · UI/AI는 이 데이터를 만들어 GameActionGateway.Submit()에 넘긴다.
//  · 1단계(관문): 로컬에서 즉시 실행.
//  · 3단계(서버 대전): 이 데이터를 네트워크로 전송 → 서버가 검증·실행 후 양쪽에 복제.
// ─────────────────────────────────────────────────────────────────────────
public enum GameActionType
{
    PlaceUnit,      // 유닛 배치
    Upgrade,        // 유닛 업그레이드 (더 높은 코스트로 교체)
    EquipItem,      // 아이템 장착
    DeclareAttack,  // 공격 선언 (방어 선택 팝업은 내부 흐름 — 2단계에서 async화)
    PlaySkill,      // 스킬 발동 (코스트 소비까지. 타겟 선택은 내부 흐름 — 2단계)
    LeaderActive,   // 리더 액티브 발동 (각성면, 턴당 1회)
}

public struct GameAction
{
    public GameActionType Type;
    public bool     IsPlayer;   // 행동 주체 (true=플레이어, false=AI/원격 상대)
    public int      Lane;
    public CardData Card;       // ※ 로컬 시범용 참조. 3단계(네트워크)에선 "인스턴스 ID"로 교체 필요
                                //    (덱에 같은 카드가 여러 장이라 cardId만으론 부족 → 장별 고유 번호)
}
