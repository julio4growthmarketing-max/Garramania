using UnityEngine;

/// <summary>
/// Gerenciador central de persistência otimizado para WebGL e mobile.
/// Elimina travamentos causados por PlayerPrefs.Save() síncrono frequente,
/// agrupando gravações (debouncing) e gravando automaticamente nos momentos seguros.
/// </summary>
public class PersistentSaveManager : MonoBehaviour
{
    private static PersistentSaveManager instance;
    private static bool isDirty = false;
    private static float dirtyTimestamp = 0f;
    private const float DEBOUNCE_DELAY = 2.0f; // Salva 2 segundos após a última alteração

    public static PersistentSaveManager Instance
    {
        get
        {
            if (instance == null)
            {
                EnsureInitialized();
            }
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void EnsureInitialized()
    {
        if (instance != null) return;

        GameObject go = new GameObject("PersistentSaveManager");
        instance = go.AddComponent<PersistentSaveManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (isDirty && Time.unscaledTime - dirtyTimestamp >= DEBOUNCE_DELAY)
        {
            Flush();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Flush();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            Flush();
        }
    }

    private void OnApplicationQuit()
    {
        Flush();
    }

    /// <summary>
    /// Marca que há dados pendentes para salvar. A gravação física em disco / IndexedDB
    /// ocorrerá de forma agrupada após DEBOUNCE_DELAY segundos.
    /// </summary>
    public static void MarkDirty()
    {
        isDirty = true;
        dirtyTimestamp = Time.unscaledTime;
    }

    /// <summary>
    /// Força a gravação imediata no disco / IndexedDB. Usar apenas em encerramento ou saídas de tela.
    /// </summary>
    public static void Flush()
    {
        if (!isDirty) return;

        isDirty = false;
        try
        {
            PlayerPrefs.Save();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[PersistentSaveManager] Falha ao salvar PlayerPrefs: {ex.Message}");
        }
    }

    // ======================== MÉTODOS AUXILIARES SEGUROS ========================

    public static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        MarkDirty();
    }

    public static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        MarkDirty();
    }

    public static void SetString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        MarkDirty();
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(key, defaultValue);
    }

    public static float GetFloat(string key, float defaultValue = 0f)
    {
        return PlayerPrefs.GetFloat(key, defaultValue);
    }

    public static string GetString(string key, string defaultValue = "")
    {
        return PlayerPrefs.GetString(key, defaultValue);
    }

    public static bool HasKey(string key)
    {
        return PlayerPrefs.HasKey(key);
    }

    public static void DeleteKey(string key)
    {
        PlayerPrefs.DeleteKey(key);
        MarkDirty();
    }
}
