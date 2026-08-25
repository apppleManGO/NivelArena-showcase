// Assets/Scripts/UI/DeckBuilder/DeckListItem.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class DeckListItem : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    public Image    CardImage;
    public TMP_Text CardNameText;

    CardData           _data;
    DeckBuilderManager _manager;

    public void Setup(CardData data, DeckBuilderManager manager)
    {
        _data    = data;
        _manager = manager;

        // 프리팹 루트의 HorizontalLayoutGroup이 CardImage를 (0,1) 앵커+0크기로 강제해 이미지가 안 보임 → 끔.
        //  (덱 아이템은 사실상 '카드 이미지 한 장'이라 레이아웃 그룹 불필요, 이미지가 셀을 꽉 채우게)
        var hlg = GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        if (CardImage != null)
        {
            // 카드 이미지를 셀(그리드가 정한 크기)에 꽉 차게 stretch — 프리팹은 60x84 고정이라 셀이 커져도 작게 남았음.
            //  offsetMin/Max 대신 anchor+sizeDelta+anchoredPosition을 명시 세팅(더 확실).
            var rt = CardImage.rectTransform;
            rt.anchorMin        = new Vector2(0f, 0f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = Vector2.zero;      // stretch 상태에서 (0,0)=부모 꽉 채움
            rt.anchoredPosition = Vector2.zero;
            CardImage.preserveAspect = true;         // 셀 비율=카드 비율이라 꽉 차면서 왜곡 없음

            CardImage.sprite  = data.Artwork;
            CardImage.enabled = data.Artwork != null;
        }
        if (CardNameText != null) CardNameText.text = data.CardName;
    }

    // 클릭 시 이 카드 1장 덱에서 제거
    public void OnPointerClick(PointerEventData e)
    {
        if (_data != null) _manager?.RemoveCard(_data.CardId);
    }
}
