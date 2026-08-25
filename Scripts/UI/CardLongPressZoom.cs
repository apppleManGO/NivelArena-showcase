// Assets/Scripts/UI/CardLongPressZoom.cs
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// 카드 이미지를 표시하는 오브젝트에 붙여서 2초 이상 누르면 CardZoomView로 확대 표시.
// 대미지 존처럼 동적으로 생성되는 카드 슬롯에서 사용 (드래그/클릭 로직이 따로 없는 곳).
public class CardLongPressZoom : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public const float HoldDuration = 1f;

    public CardData Data;

    private Coroutine _holdCoroutine;

    public void OnPointerDown(PointerEventData eventData)
    {
        CancelHold();
        _holdCoroutine = StartCoroutine(HoldTimer());
    }

    public void OnPointerUp(PointerEventData eventData)   => CancelHold();
    public void OnPointerExit(PointerEventData eventData) => CancelHold();

    private void OnDisable() => CancelHold();

    private void CancelHold()
    {
        if (_holdCoroutine != null)
        {
            StopCoroutine(_holdCoroutine);
            _holdCoroutine = null;
        }
    }

    private IEnumerator HoldTimer()
    {
        yield return new WaitForSeconds(HoldDuration);
        _holdCoroutine = null;
        if (Data != null)
            CardZoomView.Instance?.Show(Data);
    }
}
