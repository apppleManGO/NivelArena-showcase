// Assets/Scripts/Enums/CardType.cs
public enum CardType
{
    Unit,    // 유닛 카드
    Skill,   // 스킬 카드
    Item,    // 아이템 카드
    Leader   // 리더 카드
}

// 카드가 놓이는 영역 (EffectActions.MoveCard 등에서 사용)
public enum CardZone
{
    Hand,       // 패
    DeckTop,    // 덱 맨 위
    DeckBottom, // 덱 맨 아래
    Trash,      // 트래시 존
    Damage      // 대미지 존
}

public enum PhaseType
{
    LevelUp,   // 레벨업 페이즈
    Draw,      // 드로우 페이즈
    Main,      // 메인 페이즈
    Attack,    // 어택 페이즈
    End        // 엔드 페이즈
}