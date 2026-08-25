// Assets/Scripts/UI/SlotClickHandler.cs
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class SlotClickHandler : MonoBehaviour, IPointerClickHandler
{
    private int _index;
    private Action<int> _callback;

    public void Init(int index, Action<int> callback)
    {
        _index    = index;
        _callback = callback;

        // Raycast 가능하도록 투명 Image 보장
        var img = GetComponent<Image>();
        if (img.sprite == null) img.color = new Color(1, 1, 1, 0.01f);
        img.raycastTarget = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _callback?.Invoke(_index);
    }
}
