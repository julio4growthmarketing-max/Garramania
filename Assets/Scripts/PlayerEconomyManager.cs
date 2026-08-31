using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gerenciador de Economia, Recompensa Diária (7 dias) e Retenção do Jogador (GarraMania).
/// </summary>
public sealed class PlayerEconomyManager : MonoBehaviour
{
    public static PlayerEconomyManager Instance { get; private set; }

    private const string PREF_LAST_DAILY_CLAIM = "GarraMania_LastDailyClaim";
    private const string PREF_DAILY_STREAK = "GarraMania_DailyStreakDay";
    private const string PREF_GOLDEN_TOKENS = "GarraMania_GoldenTokens";
    private const string PREF_GAMES_PLAYED_COUNT = "GarraMania_GamesPlayedCounter";
    private const string PREF_VIP_ACTIVE = "GarraMania_VIPActive";

    // Tabela oficial de 7 dias inspirada no ClawCrazy
    public static readonly int[] DailyTokenRewards = { 8, 10, 12, 14, 16, 18, 25 };

    public int GoldenTokens { get; private set; }
    public bool IsGoldenClawActive { get; private set; }
    public int GamesPlayedCounter { get; private set; }
    public int CurrentStreakDay { get; private set; } // 1 a 7
    public bool IsVIP { get; private set; }

    public UnityEvent OnEconomyUpdated = new UnityEvent();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadData();
    }

    private void LoadData()
    {
        GoldenTokens = PlayerPrefs.GetInt(PREF_GOLDEN_TOKENS, 1);
        GamesPlayedCounter = PlayerPrefs.GetInt(PREF_GAMES_PLAYED_COUNT, 0);
        CurrentStreakDay = PlayerPrefs.GetInt(PREF_DAILY_STREAK, 1);
        if (CurrentStreakDay < 1 || CurrentStreakDay > 7) CurrentStreakDay = 1;
        IsVIP = PlayerPrefs.GetInt(PREF_VIP_ACTIVE, 0) == 1;
    }

    private void SaveData()
    {
        PlayerPrefs.SetInt(PREF_GOLDEN_TOKENS, GoldenTokens);
        PlayerPrefs.SetInt(PREF_GAMES_PLAYED_COUNT, GamesPlayedCounter);
        PlayerPrefs.SetInt(PREF_DAILY_STREAK, CurrentStreakDay);
        PlayerPrefs.SetInt(PREF_VIP_ACTIVE, IsVIP ? 1 : 0);
        PlayerPrefs.Save();
        OnEconomyUpdated?.Invoke();
    }

    /// <summary>
    /// Retorna se a recompensa diária está pronta para ser resgatada
    /// </summary>
    public bool IsDailyRewardAvailable()
    {
        string lastClaimStr = PlayerPrefs.GetString(PREF_LAST_DAILY_CLAIM, "");
        if (string.IsNullOrEmpty(lastClaimStr)) return true;

        if (DateTime.TryParse(lastClaimStr, out DateTime lastClaim))
        {
            TimeSpan diff = DateTime.Now - lastClaim;
            // Disponível após 18 horas ou se mudou de dia
            return diff.TotalHours >= 18.0 || DateTime.Now.Date > lastClaim.Date;
        }
        return true;
    }

    /// <summary>
    /// Tempo restante até a próxima recompensa diária
    /// </summary>
    public TimeSpan GetTimeUntilNextDailyReward()
    {
        string lastClaimStr = PlayerPrefs.GetString(PREF_LAST_DAILY_CLAIM, "");
        if (string.IsNullOrEmpty(lastClaimStr)) return TimeSpan.Zero;

        if (DateTime.TryParse(lastClaimStr, out DateTime lastClaim))
        {
            DateTime nextReady = lastClaim.AddHours(18.0);
            if (DateTime.Now >= nextReady) return TimeSpan.Zero;
            return nextReady - DateTime.Now;
        }
        return TimeSpan.Zero;
    }

    /// <summary>
    /// Resgata o prêmio do dia atual (1 a 7)
    /// </summary>
    public int ClaimDailyReward()
    {
        if (!IsDailyRewardAvailable()) return 0;

        int dayIdx = Mathf.Clamp(CurrentStreakDay - 1, 0, DailyTokenRewards.Length - 1);
        int tokensReward = DailyTokenRewards[dayIdx];

        PlayerPrefs.SetString(PREF_LAST_DAILY_CLAIM, DateTime.Now.ToString("o"));

        if (GameSession.Instance != null)
        {
            GameSession.Instance.AddCredits(tokensReward);
        }

        // Bônus especial de 7º dia
        if (CurrentStreakDay >= 7)
        {
            GoldenTokens += 1;
            CurrentStreakDay = 1; // Reinicia ciclo de 7 dias
        }
        else
        {
            CurrentStreakDay++;
        }

        SaveData();

        AudioFeedbackController.Instance?.PlayCoin();
        GameJuice.Instance?.HapticsSuccess();

        Debug.Log($"[PlayerEconomyManager] Recompensa Diária (Dia {dayIdx + 1}) resgatada: +{tokensReward} Fichas!");
        return tokensReward;
    }

    public void RegisterGameStarted()
    {
        GamesPlayedCounter++;
        if (GamesPlayedCounter >= 5)
        {
            GamesPlayedCounter = 0;
            GoldenTokens++;
            Debug.Log("[PlayerEconomyManager] Bônus de fidelidade! +1 Ficha Dourada!");
        }
        SaveData();
    }

    public bool ToggleGoldenClaw()
    {
        if (IsGoldenClawActive)
        {
            IsGoldenClawActive = false;
            OnEconomyUpdated?.Invoke();
            return false;
        }

        if (GoldenTokens > 0)
        {
            IsGoldenClawActive = true;
            OnEconomyUpdated?.Invoke();
            AudioFeedbackController.Instance?.PlayCoin();
            return true;
        }

        return false;
    }

    public void ConsumeGoldenToken()
    {
        if (IsGoldenClawActive)
        {
            GoldenTokens = Mathf.Max(0, GoldenTokens - 1);
            IsGoldenClawActive = false;
            SaveData();
        }
    }

    public void PurchaseVIP()
    {
        IsVIP = true;
        GoldenTokens += 3;
        if (GameSession.Instance != null)
        {
            GameSession.Instance.AddCredits(125);
        }
        SaveData();
        AudioFeedbackController.Instance?.PlayCoin();
        GameJuice.Instance?.HapticsSuccess();
    }

    public void PurchaseTokenPack(int tokens, int goldBonus = 0)
    {
        if (GameSession.Instance != null)
        {
            GameSession.Instance.AddCredits(tokens);
        }
        if (goldBonus > 0)
        {
            GoldenTokens += goldBonus;
        }
        SaveData();
        AudioFeedbackController.Instance?.PlayCoin();
        GameJuice.Instance?.HapticsSuccess();
    }
}
