// Assets/Scripts/Data/IEffect.cs
using UnityEngine;

public interface IEffect
{
    string Description { get; }          // 카드에 표시될 효과 설명
    void Execute(GameContext context);    // 효과 실행
}

// 효과 실행 시 필요한 게임 상태 정보를 담는 컨테이너
// (지금은 비워두고 나중에 채워요)
public class GameContext
{
    public PlayerState ActivePlayer;
    public PlayerState OpponentPlayer;
    public CardData SourceCard;
}

public class PlayerState
{
    public int LeaderLevel;
    public int DamageCount;
    public int Size => LeaderLevel + DamageCount;  // 사이즈 = 레벨 + 대미지
}