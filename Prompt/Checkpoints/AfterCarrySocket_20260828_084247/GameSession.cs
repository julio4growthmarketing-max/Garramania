using UnityEngine;
using UnityEngine.Events;

public enum GameState
{
    Idle,
    Playing,
    Capturing,
    Returning,
    Delivering,
    GameOver
}

public class GameSession : MonoBehaviour
{
    private static GameSession _instance;
    public static GameSession Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GameSession>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameSession");
                    _instance = go.AddComponent<GameSession>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Configurações da Sessão")]
    [SerializeField] private int initialCredits = 3;
    [SerializeField] private float maxSessionTime = 45f;
    [Tooltip("Mantém o protótipo jogável mesmo se um playtest anterior salvou zero fichas. Desative quando a economia estiver pronta.")]
    [SerializeField] private bool prototypeInfiniteRetries = true;

    [Header("Referências")]
    [SerializeField] private ClawController clawController;

    public GameState CurrentState { get; private set; } = GameState.Idle;
    public int Credits { get; private set; }
    public float TimeRemaining { get; private set; }
    public float MaxSessionTime => maxSessionTime;
    public int PrizesWon { get; private set; }
    public int HighScore { get; private set; }

    private const string PREFS_CREDITS = "GarraMania_Credits";
    private const string PREFS_HIGHSCORE = "GarraMania_HighScore";

    public void AddCredits(int amount)
    {
        Credits += amount;
        SaveData();
        OnCreditsChanged?.Invoke(Credits);
    }

    public void ResetCredits(int amount = 3)
    {
        Credits = amount;
        SaveData();
        OnCreditsChanged?.Invoke(Credits);
    }

    private void LoadData()
    {
        Credits = PlayerPrefs.GetInt(PREFS_CREDITS, initialCredits);
        if (prototypeInfiniteRetries && Credits <= 0)
        {
            Credits = Mathf.Max(1, initialCredits);
            SaveData();
        }
        HighScore = PlayerPrefs.GetInt(PREFS_HIGHSCORE, 0);
    }

    private void SaveData()
    {
        PlayerPrefs.SetInt(PREFS_CREDITS, Credits);
        PlayerPrefs.SetInt(PREFS_HIGHSCORE, HighScore);
        PlayerPrefs.Save();
    }

    public void CheckHighScore()
    {
        if (PrizesWon > HighScore)
        {
            HighScore = PrizesWon;
            SaveData();
            OnHighScoreChanged?.Invoke(HighScore);
        }
    }

    [Header("Eventos da Sessão")]
    public UnityEvent<GameState> OnStateChanged = new UnityEvent<GameState>();
    public UnityEvent<float, float> OnTimeChanged = new UnityEvent<float, float>(); // (remainingTime, maxTime)
    public UnityEvent<int> OnCreditsChanged = new UnityEvent<int>();
    public UnityEvent<int> OnHighScoreChanged = new UnityEvent<int>();
    public UnityEvent<Prize, int> OnPrizeDelivered = new UnityEvent<Prize, int>(); // (prize, totalPrizes)
    public UnityEvent OnGameOver = new UnityEvent();
    public UnityEvent<Prize> OnPrizeWonShowResult = new UnityEvent<Prize>(); // Para a tela de resultado

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (OnStateChanged == null) OnStateChanged = new UnityEvent<GameState>();
        if (OnTimeChanged == null) OnTimeChanged = new UnityEvent<float, float>();
        if (OnCreditsChanged == null) OnCreditsChanged = new UnityEvent<int>();
        if (OnHighScoreChanged == null) OnHighScoreChanged = new UnityEvent<int>();
        if (OnPrizeDelivered == null) OnPrizeDelivered = new UnityEvent<Prize, int>();
        if (OnGameOver == null) OnGameOver = new UnityEvent();
        if (OnPrizeWonShowResult == null) OnPrizeWonShowResult = new UnityEvent<Prize>();

        LoadData();
        TimeRemaining = maxSessionTime;

        if (clawController == null)
        {
            clawController = FindFirstObjectByType<ClawController>();
        }

        // Auto-criar sistemas de Juice e UI se não existirem
        if (FindFirstObjectByType<AudioFeedbackController>() == null)
        {
            new GameObject("AudioFeedbackController").AddComponent<AudioFeedbackController>();
        }
        if (FindFirstObjectByType<GameJuice>() == null)
        {
            new GameObject("GameJuice").AddComponent<GameJuice>();
        }
        if (FindFirstObjectByType<ProfessionalUIController>() == null)
        {
            new GameObject("ProfessionalUIController").AddComponent<ProfessionalUIController>();
        }
        if (InputRouter.Instance != null)
        {
            InputRouter.Instance.SetBlocked(true);
        }
        if (Camera.main != null && Camera.main.GetComponent<ClawCameraController>() == null)
        {
            Camera.main.gameObject.AddComponent<ClawCameraController>();
        }
    }

    void Start()
    {
        SetState(GameState.Idle);
        OnTimeChanged?.Invoke(TimeRemaining, maxSessionTime);
        OnCreditsChanged?.Invoke(Credits);
        OnHighScoreChanged?.Invoke(HighScore);
    }

    // Tracking para warning sonoro
    private int lastWarningSecond = -1;

    void Update()
    {
        if (CurrentState == GameState.Playing)
        {
            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining < 0f) TimeRemaining = 0f;

            OnTimeChanged?.Invoke(TimeRemaining, maxSessionTime);

            // 🔊 JUICE: Warning nos últimos 5 segundos
            if (TimeRemaining <= 5f && TimeRemaining > 0f)
            {
                int currentSecond = Mathf.CeilToInt(TimeRemaining);
                if (currentSecond != lastWarningSecond)
                {
                    lastWarningSecond = currentSecond;
                    AudioFeedbackController.Instance?.PlayWarning();
                    GameJuice.Instance?.FlashScreen(new Color(1f, 0f, 0f, 0.25f), 0.15f);
                }
            }

            if (TimeRemaining <= 0f)
            {
                lastWarningSecond = -1;
                TimeoutGameOver();
            }
        }
    }

    public bool CanMoveClaw()
    {
        return CurrentState == GameState.Playing;
    }

    public void StartGame()
    {
        if (CurrentState != GameState.Idle && CurrentState != GameState.GameOver)
        {
            Debug.LogWarning($"[GameSession] Não é possível iniciar uma partida no estado {CurrentState}.");
            return;
        }

        if (Credits <= 0)
        {
            Credits = 5;
            OnCreditsChanged?.Invoke(Credits);
            if (false)
            {
                Debug.Log("[GameSession] Fichas esgotadas. A partida não foi iniciada.");
                OnCreditsChanged?.Invoke(Credits);
                return;
            }

            // Modo protótipo: uma sessão anterior não pode deixar o botão
            // inicial permanentemente sem ação durante os testes.
            Credits = Mathf.Max(1, initialCredits);
            Debug.Log($"[GameSession] Playtest: fichas restauradas para {Credits}.");
            OnCreditsChanged?.Invoke(Credits);
        }

        Credits--;
        SaveData();
        OnCreditsChanged?.Invoke(Credits);

        TimeRemaining = maxSessionTime;
        lastWarningSecond = -1;
        OnTimeChanged?.Invoke(TimeRemaining, maxSessionTime);

        SetState(GameState.Playing);

        if (InputRouter.Instance != null)
        {
            InputRouter.Instance.SetBlocked(false);
        }

        // 🔊 JUICE: Som da moeda ao iniciar partida
        AudioFeedbackController.Instance?.PlayCoin();

        Debug.Log($"[GameSession] Partida iniciada! Fichas restantes: {Credits}, Tempo: {maxSessionTime}s");
    }

    public void TimeoutGameOver()
    {
        Debug.Log("[GameSession] Tempo esgotado! Bloqueando InputRouter e resetando garra.");

        SetState(GameState.GameOver);

        // 1. Bloqueia imediatamente os controles
        if (InputRouter.Instance != null)
        {
            InputRouter.Instance.SetBlocked(true);
        }

        // 2. Garante referência ao ClawController e aciona ResetarGarra()
        if (clawController == null)
        {
            clawController = FindFirstObjectByType<ClawController>();
        }

        if (clawController != null)
        {
            clawController.ResetarGarra();
        }

        OnGameOver?.Invoke();
    }

    // Referência ao último prêmio ganho para a tela de resultado
    public Prize LastPrizeWon { get; private set; }

    public void RegisterPrizeDelivered(Prize prize)
    {
        if (prize == null || prize.State != PrizeState.Delivered)
        {
            Debug.LogWarning("[GameSession] Entrega ignorada: prêmio inválido ou não marcado como entregue.");
            return;
        }

        if (CurrentState != GameState.Playing && CurrentState != GameState.Delivering)
        {
            Debug.LogWarning($"[GameSession] Entrega ignorada no estado {CurrentState}.");
            return;
        }

        SetState(GameState.Delivering);
        PrizesWon++;
        CheckHighScore();
        LastPrizeWon = prize;
        Debug.Log($"[GameSession] Prêmio capturado com sucesso! Total: {PrizesWon} (Recorde: {HighScore}) (ID: {prize?.prizeId})");
        OnPrizeDelivered?.Invoke(prize, PrizesWon);

        // 🔊 JUICE: Celebração completa ao entregar prêmio!
        AudioFeedbackController.Instance?.PlayFanfare();
        GameJuice.Instance?.PlayConfetti(Vector3.up * 2f);
        GameJuice.Instance?.ScreenShake(0.3f, 0.2f);
        GameJuice.Instance?.Haptics();
        GameJuice.Instance?.FlashScreen(new Color(1f, 0.95f, 0.3f, 0.35f), 0.3f);

        // Dispara evento para tela de resultado
        OnPrizeWonShowResult?.Invoke(prize);
    }

    public void SetState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"[GameSession] Estado alterado para: {newState}");
        OnStateChanged?.Invoke(newState);
    }

    public void ResetSession()
    {
        if (prototypeInfiniteRetries && Credits <= 0)
        {
            Credits = Mathf.Max(1, initialCredits);
            SaveData();
        }

        TimeRemaining = maxSessionTime;
        lastWarningSecond = -1;
        SetState(GameState.Idle);
        OnTimeChanged?.Invoke(TimeRemaining, maxSessionTime);
        OnCreditsChanged?.Invoke(Credits);

        if (InputRouter.Instance != null)
        {
            InputRouter.Instance.SetBlocked(true);
        }

        if (clawController == null)
        {
            clawController = FindFirstObjectByType<ClawController>();
        }

        if (clawController != null)
        {
            clawController.ResetarGarra();
        }
    }
}
