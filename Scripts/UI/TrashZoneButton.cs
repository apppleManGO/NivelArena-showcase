// Assets/Scripts/UI/TrashZoneButton.cs
using UnityEngine;
using UnityEngine.EventSystems;

// HUD의 PlayerTrashText / AITrashText 오브젝트에 붙여서 클릭하면 TrashZoneView가 열리게 함.
public class TrashZoneButton : MonoBehaviour, IPointerClickHandler
{
    public bool IsPlayer = true;

    public void OnPointerClick(PointerEventData eventData)
    {
        TrashZoneView.Instance?.Show(IsPlayer);
    }
}
