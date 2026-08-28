using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manipulador de entrada móvel para o GarraMania.
/// Responsável por mapear a barra de força, botões de ação e integração com o ClawController e GameSession.
/// </summary>
public class InputHandler : MonoBehaviour
{
    private static InputHandler _instance;
    public static InputHandler Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<InputHandler>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("InputHandler");
                    _instance = go.AddComponent<InputHandler>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Eventos")]
    public UnityEvent<float> OnForceChanged = new UnityEvent<float>();
    public UnityEvent OnActionTriggered = new UnityEvent();
    public UnityEvent OnResetTriggered = new UnityEvent();

    public float CurrentForce { get; private set; } = 1.0f;

    private ClawController clawController;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (OnForceChanged == null) OnForceChanged = new UnityEvent<float>();
        if (OnActionTriggered == null) OnActionTriggered = new UnityEvent();
        if (OnResetTriggered == null) OnResetTriggered = new UnityEvent();
    }

    void Start()
    {
        FindClaw();
        SetForce(UISettings.Instance != null ? UISettings.Instance.defaultClawForce : 1.0f);
    }

    private void FindClaw()
    {
        if (clawController == null)
        {
            clawController = FindFirstObjectByType<ClawController>();
        }
    }

    /// <summary>
    /// Ajusta a força da garra (0.1f a 1.0f) aplicando a curva configurada no UISettings.
    /// </summary>
    public void SetForce(float normalizedValue)
    {
        float evaluated = UISettings.Instance != null 
            ? UISettings.Instance.EvaluateClawForce(normalizedValue) 
            : Mathf.Clamp01(normalizedValue);

        CurrentForce = Mathf.Clamp(evaluated, 0.1f, 1.0f);

        FindClaw();
        if (clawController != null)
        {
            clawController.SetForce(CurrentForce);
        }

        OnForceChanged?.Invoke(CurrentForce);
    }

    /// <summary>
    /// Dispara a ação da garra (pegar/soltar).
    /// </summary>
    public void TriggerAction()
    {
        if (InputRouter.Instance != null && !InputRouter.Instance.IsBlocked)
        {
            InputRouter.Instance.TriggerTouchAction();
        }
        OnActionTriggered?.Invoke();
    }

    /// <summary>
    /// Dispara o início do jogo / inserção de ficha.
    /// </summary>
    public void TriggerStart()
    {
        if (GameSession.Instance != null)
        {
            GameSession.Instance.StartGame();
        }
    }

    /// <summary>
    /// Reseta a garra e reinicia a sessão.
    /// </summary>
    public void TriggerReset()
    {
        if (GameSession.Instance != null)
        {
            GameSession.Instance.ResetSession();
        }

        FindClaw();
        if (clawController != null)
        {
            clawController.ResetarGarra();
        }

        OnResetTriggered?.Invoke();
    }
}
