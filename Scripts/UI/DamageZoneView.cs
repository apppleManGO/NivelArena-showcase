// Assets/Scripts/UI/DamageZoneView.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DamageZoneView : MonoBehaviour
{
    [Header("설정")]
    public bool IsPlayer = true;

    [Header("UI")]
    public Transform CardContainer;  // Horizontal Layout Group
    public GameObject CardSlotPrefab; // Image + 테두리 Image 자식을 가진 프리팹

    [Header("색상")]
    public Color TriggerBorderColor = new Color(1f, 0.85f, 0f, 1f); // 금색
    public Color NormalBorderColor  = new Color(0f, 0f, 0f, 0f);    // 투명

    private readonly List<GameObject> _slots = new();

    private void Start()
    {
        // 대미지 변화 감지: TurnManager 페이즈 변경마다 갱신
        TurnManager.Instance.OnPhaseChanged += _ => Refresh();
        Refresh();
    }

    public void Refresh()
    {
        PlayerController pc = IsPlayer
            ? PlayerController.PlayerInstance
            : PlayerController.AIInstance;

        if (pc == null) return;

        // 슬롯 수 맞추기
        while (_slots.Count < pc.DamageZone.Count)
        {
            var slot = Instantiate(CardSlotPrefab, CardContainer);
            _slots.Add(slot);
        }

        while (_slots.Count > pc.DamageZone.Count)
        {
            Destroy(_slots[_slots.Count - 1]);
            _slots.RemoveAt(_slots.Count - 1);
        }

        // 각 슬롯 업데이트
        for (int i = 0; i < pc.DamageZone.Count; i++)
        {
            CardData card = pc.DamageZone[i];
            GameObject slot = _slots[i];

            // 카드 이미지
            Image cardImage = slot.GetComponent<Image>();
            if (cardImage != null && card.Artwork != null)
                cardImage.sprite = card.Artwork;

            // 트리거 테두리 (자식 첫 번째 Image)
            Image border = slot.transform.childCount > 0
                ? slot.transform.GetChild(0).GetComponent<Image>()
                : null;

            if (border != null)
                border.color = card.IsTrigger ? TriggerBorderColor : NormalBorderColor;

            // 길게 누르면 카드 확대 (없으면 추가)
            var zoom = slot.GetComponent<CardLongPressZoom>();
            if (zoom == null) zoom = slot.AddComponent<CardLongPressZoom>();
            zoom.Data = card;
        }
    }

    // 외부(PlayerController)에서 대미지 발생 시 즉시 호출
    public static void NotifyDamageChanged()
    {
        foreach (var view in FindObjectsByType<DamageZoneView>(FindObjectsSortMode.None))
            view.Refresh();
    }
}
