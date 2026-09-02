using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gerenciador leve de Internacionalização (i18n) do GarraMania.
/// Suporta carregamento instantâneo de strings via JSON (PT-BR, EN-US, ES-ES) sem dependências externas.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    public const string PREF_LANG = "GarraMania_Language";
    public const string LANG_PT = "pt-BR";
    public const string LANG_EN = "en-US";
    public const string LANG_ES = "es-ES";


    private string currentLanguage = LANG_PT;
    private readonly Dictionary<string, Dictionary<string, string>> stringTable = new Dictionary<string, Dictionary<string, string>>();

    public static event Action OnLanguageChanged;

    public string CurrentLanguage => currentLanguage;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadLanguagePacks();
        currentLanguage = PlayerPrefs.GetString(PREF_LANG, LANG_PT);
    }

    private void LoadLanguagePacks()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>("Localization/strings");
        if (jsonAsset == null)
        {
            Debug.LogWarning("[LocalizationManager] Arquivo Resources/Localization/strings.json não encontrado!");
            return;
        }

        try
        {
            // Parse estruturado de chave-valor simples
            string json = jsonAsset.text;
            ParseJsonTable(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalizationManager] Erro ao carregar strings: {ex.Message}");
        }
    }

    private void ParseJsonTable(string json)
    {
        stringTable.Clear();
        string[] supportedLangs = new string[] { LANG_PT, LANG_EN, LANG_ES };

        foreach (string lang in supportedLangs)
        {
            string marker = $"\"{lang}\"";
            int langIdx = json.IndexOf(marker, StringComparison.Ordinal);
            if (langIdx == -1) continue;

            int braceStart = json.IndexOf('{', langIdx);
            if (braceStart == -1) continue;

            int braceEnd = json.IndexOf('}', braceStart);
            if (braceEnd == -1) continue;

            string block = json.Substring(braceStart + 1, braceEnd - braceStart - 1);
            Dictionary<string, string> dict = new Dictionary<string, string>();

            string[] lines = block.Split(new char[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                int colon = line.IndexOf(':');
                if (colon == -1) continue;

                string k = line.Substring(0, colon).Trim().Trim('"');
                string v = line.Substring(colon + 1).Trim().Trim('"');
                if (!string.IsNullOrEmpty(k))
                {
                    dict[k] = v;
                }
            }

            stringTable[lang] = dict;
        }
    }

    public void SetLanguage(string langCode)
    {
        if (currentLanguage == langCode) return;
        currentLanguage = langCode;
        PlayerPrefs.SetString(PREF_LANG, currentLanguage);
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke();
    }

    public static string Get(string key, string defaultValue = null)
    {
        if (Instance == null || Instance.stringTable == null) return defaultValue ?? key;

        if (Instance.stringTable.TryGetValue(Instance.currentLanguage, out var dict) && dict.TryGetValue(key, out string val))
        {
            return val;
        }

        // Fallback para Português se não encontrar no idioma atual
        if (Instance.stringTable.TryGetValue(LANG_PT, out var ptDict) && ptDict.TryGetValue(key, out string ptVal))
        {
            return ptVal;
        }

        return defaultValue ?? key;
    }

    public static string Format(string key, params object[] args)
    {
        string raw = Get(key);
        try
        {
            return string.Format(raw, args);
        }
        catch
        {
            return raw;
        }
    }
}
