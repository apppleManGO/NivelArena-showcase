// Assets/Scripts/Data/CardData.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "NivelArena/CardData")]
public class CardData : ScriptableObject
{
    [Header("기본 정보")]
    public string CardId;
    public string CardName;
    public CardType Type;
    public string Attribute;
    public string Rarity;
    public Sprite Artwork;

    [Header("스탯 (유닛)")]
    public int Cost;
    public int AttackPower;
    public int Hit;

    [Header("소속")]
    public string Faction;
    public string IpName;

    [Header("효과 텍스트")]
    public List<string> Keywords    = new();
    public List<string> EffectTexts = new();
    public string TriggerEffect;
    public bool   IsTrigger;

    [Header("효과 타입 (코드용)")]
    // 형식: "EffectType" 또는 "EffectType:값" (예: "AttackerPowerBoost:1000")
    public List<string> EffectTypes        = new(); // 일반 효과
    public List<string> TriggerEffectTypes = new(); // 트리거 효과

    [Header("아이템 전용")]
    public string EquipCondition;
    public int    MinCostToEquip; // 0 = 제한 없음

    [Header("런타임 효과 (나중에 구현)")]
    [SerializeReference]
    public List<IEffect> Effects = new();
}
