// Assets/Script/Data/LeaderData.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewLeader", menuName = "NivelArena/LeaderData")]
public class LeaderData : ScriptableObject
{
    [Header("기본 정보")]
    public string CardId;
    public string LeaderName;
    public string IpName;
    public string Faction;
    public string Attribute;
    public Sprite BaseArtwork;
    public Sprite AwakenArtwork;

    [Header("서약 / 각성")]
    public string VowCondition;
    public int    AwakenLevel;
    public string AwakenCondition;

    [Header("효과 텍스트")]
    public string BaseEffectText;
    public string AwakenEffectText;
    public string BaseEffectType;
    public string AwakenEffectType;  // 각성면 전용 효과 (ST05~ 리더는 효과가 각성면에만 있음)

    [Header("런타임 효과 (나중에 구현)")]
    [SerializeReference]
    public List<IEffect> BaseEffects   = new();
    [SerializeReference]
    public List<IEffect> AwakenEffects = new();
}