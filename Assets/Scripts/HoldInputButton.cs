using UnityEngine;
using UnityEngine.EventSystems;

public sealed class HoldInputButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private float axisValue;

    public void Configure(float value)
    {
        axisValue = Mathf.Clamp(value, -1f, 1f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        InputRouter.Instance?.SetTouchY(axisValue);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ClearAxis();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ClearAxis();
    }

    private void OnDisable()
    {
        ClearAxis();
    }

    private void ClearAxis()
    {
        InputRouter.Instance?.SetTouchY(0f);
    }
}
