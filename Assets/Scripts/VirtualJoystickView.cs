using UnityEngine;
using UnityEngine.EventSystems;

public sealed class VirtualJoystickView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private RectTransform area;
    private RectTransform handle;
    private float radius = 72f;
    private Camera eventCamera;

    public void Configure(RectTransform joystickArea, RectTransform joystickHandle, float handleRadius)
    {
        area = joystickArea;
        handle = joystickHandle;
        radius = Mathf.Max(20f, handleRadius);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        eventCamera = eventData.pressEventCamera;
        UpdatePosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdatePosition(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        InputRouter.Instance?.SetTouchX(0f);
        InputRouter.Instance?.SetTouchZ(0f);
    }

    private void UpdatePosition(PointerEventData eventData)
    {
        if (area == null) area = transform as RectTransform;
        if (area == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventCamera, out Vector2 local)) return;

        Vector2 centered = local - area.rect.center;
        Vector2 half = area.rect.size * 0.5f;
        Vector2 normalized = new Vector2(
            half.x > 0.01f ? centered.x / half.x : 0f,
            half.y > 0.01f ? centered.y / half.y : 0f);
        normalized = Vector2.ClampMagnitude(normalized, 1f);

        if (handle != null) handle.anchoredPosition = normalized * radius;
        InputRouter.Instance?.SetTouchX(normalized.x);
        InputRouter.Instance?.SetTouchZ(normalized.y);
    }
}
