using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        if (rectTransform == null) return;
        if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        if (safeArea.width <= 0 || safeArea.height <= 0)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return;
        }

        Vector2 min = safeArea.position;
        Vector2 max = safeArea.position + safeArea.size;
        min.x /= Mathf.Max(1, Screen.width);
        min.y /= Mathf.Max(1, Screen.height);
        max.x /= Mathf.Max(1, Screen.width);
        max.y /= Mathf.Max(1, Screen.height);

        // No WebGL / iOS Safari, se o safeArea vier colado na borda, garante o respiro do notch e da barra de navegação
        bool isPortrait = Screen.height > Screen.width;
        if (isPortrait)
        {
            if (max.y > 0.965f) max.y = 0.955f; // Respiro no topo (notch / dynamic island)
            if (min.y < 0.030f) min.y = 0.020f; // Respiro na base (home bar)
        }

        rectTransform.anchorMin = min;
        rectTransform.anchorMax = max;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
