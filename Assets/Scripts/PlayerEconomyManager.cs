using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gerenciador de Economia e Retenção do Jogador (GarraMania).
/// Gerencia Fichas Diárias Gratuitas, Ficha Dourada (100% Grip sem escorregar) e Mini-Missões.
/// </summary>
public sealed class PlayerEconomyManager : MonoBehaviour
{
    public static PlayerEconomyManager Instance { get; private set; }

    private const string PREF_LAST_DAILY_CLAIM = "GarraMania_LastDailyClaim";
    private const string PREF_GOLDEN_TOKENS = "GarraMania_GoldenTokens";
    private const string PREF_GAMES_PLAYED_COUNT = "GarraMania_GamesPlayedCounter";

    public int GoldenTokens { get; private set; }
    public bool IsGoldenClawActive { get; private set; }
    public int GamesPlayedCounter { get; private set; }

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
        GoldenTokens = PlayerPrefs.GetInt(PREF_GOLDEN_TOKENS, 1); // Dá 1 Ficha Dourada de boas-vindas!
        GamesPlayedCounter = PlayerPrefs.GetInt(PREF_GAMES_PLAYED_COUNT, 0);
    }

    private void SaveData()
    {
        PlayerPrefs.SetInt(PREF_GOLDEN_TOKENS, GoldenTokens);
        PlayerPrefs.SetInt(PREF_GAMES_PLAYED_COUNT, GamesPlayedCounter);
        PlayerPrefs.Save();
        OnEconomyUpdated?.Invoke();
    }

    /// <summary>
    /// Verifica se a recompensa diária de +3 fichas já está disponível (a cada 24h)
    /// </summary>
    public bool IsDailyRewardAvailable()
    {
        string lastClaimStr = PlayerPrefs.GetString(PREF_LAST_DAILY_CLAIM, "");
        if (string.IsNullOrEmpty(lastClaimStr)) return true;

        if (DateTime.TryParse(lastClaimStr, out DateTime lastClaim))
        {
            return (DateTime.Now - lastClaim).TotalHours >= 20.0; // Disponível após 20 horas
        }
        return true;
    }

    /// <summary>
    /// Resgata o pacote diário de 3 fichas grátis
    /// </summary>
    public bool ClaimDailyReward()
    {
        if (!IsDailyRewardAvailable()) return false;

        PlayerPrefs.SetString(PREF_LAST_DAILY_CLAIM, DateTime.Now.ToString("o"));
        PlayerPrefs.Save();

        if (GameSession.Instance != null)
        {
            GameSession.Instance.AddCredits(3);
        }

        AudioFeedbackController.Instance?.PlayCoin();
        GameJuice.Instance?.HapticsSuccess();
        OnEconomyUpdated?.Invoke();

        Debug.Log("[PlayerEconomyManager] Recompensa diária resgatada: +3 Fichas!");
        return true;
    }

    /// <summary>
    /// Registra o início de uma partida para pontuar a cada 5 jogadas uma Ficha Dourada
    /// </summary>
    public void RegisterGameStarted()
    {
        GamesPlayedCounter++;
        if (GamesPlayedCounter >= 5)
        {
            GamesPlayedCounter = 0;
            GoldenTokens++;
            Debug.Log("[PlayerEconomyManager] Parabéns! Você ganhou +1 Ficha Dourada!");
        }
        SaveData();
    }

    /// <summary>
    /// Ativa a Ficha Dourada para a próxima jogada (Garra 100% calibrada)
    /// </summary>
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

    /// <summary>
    /// Consome a Ficha Dourada ao acionar a garra
    /// </summary>
    public void ConsumeGoldenToken()
    {
        if (IsGoldenClawActive)
        {
            GoldenTokens = Mathf.Max(0, GoldenTokens - 1);
            IsGoldenClawActive = false;
            SaveData();
        }
    }
}
