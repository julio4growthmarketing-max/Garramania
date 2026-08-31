using UnityEngine;
using UnityEngine.EventSystems;

public sealed class HoldInputButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum InputAxis
    {
        HorizontalX,
        VerticalZ,
        VerticalY
    }

    [SerializeField] private InputAxis axis = InputAxis.VerticalZ;
    [SerializeField] private float axisValue = 1f;

    public void Configure(InputAxis targetAxis, float value)
    {
        axis = targetAxis;
        axisValue = Mathf.Clamp(value, -1f, 1f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ApplyAxis(axisValue);
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

    private void ApplyAxis(float val)
    {
        if (InputRouter.Instance == null) return;
        switch (axis)
        {
            case InputAxis.HorizontalX:
                InputRouter.Instance.SetTouchX(val);
                break;
            case InputAxis.VerticalZ:
                InputRouter.Instance.SetTouchZ(val);
                break;
            case InputAxis.VerticalY:
                InputRouter.Instance.SetTouchY(val);
                break;
        }
    }

    private void ClearAxis()
    {
        ApplyAxis(0f);
    }
}

