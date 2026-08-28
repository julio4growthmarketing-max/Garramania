using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InputRouter : MonoBehaviour
{
    private static InputRouter _instance;
    public static InputRouter Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<InputRouter>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("InputRouter");
                    _instance = go.AddComponent<InputRouter>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    public Vector3 Movement { get; private set; }
    public bool ActionTriggered { get; private set; }
    public bool IsBlocked { get; private set; }

    private float touchX = 0f;
    private float touchY = 0f;
    private float touchZ = 0f;
    private bool touchAction = false;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    public void SetBlocked(bool blocked)
    {
        IsBlocked = blocked;
        if (blocked)
        {
            touchX = 0f;
            touchY = 0f;
            touchZ = 0f;
            touchAction = false;
            Movement = Vector3.zero;
            ActionTriggered = false;
        }
    }

    void Update()
    {
        if (IsBlocked)
        {
            Movement = Vector3.zero;
            ActionTriggered = false;
            touchAction = false;
            return;
        }

        float x = touchX;
        float y = touchY;
        float z = touchZ;
        bool action = touchAction;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) x = -1f;
            if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) x = 1f;
            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed) z = 1f;
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed) z = -1f;
            if (Keyboard.current.eKey.isPressed) y = -1f; // E para Descer
            if (Keyboard.current.qKey.isPressed) y = 1f;  // Q para Subir
            if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) action = true;
        }
#endif

        Movement = new Vector3(x, y, z);
        ActionTriggered = action;
        touchAction = false;
    }

    public void SetTouchX(float val) { if (!IsBlocked) touchX = val; }
    public void SetTouchY(float val) { if (!IsBlocked) touchY = val; }
    public void SetTouchZ(float val) { if (!IsBlocked) touchZ = val; }
    public void TriggerTouchAction() { if (!IsBlocked) touchAction = true; }
}