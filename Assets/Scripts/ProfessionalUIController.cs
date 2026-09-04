using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Efeito físico arcade tátil: faz o botão afundar ao toque com micro-vibração háptica e clique sonoro.
/// </summary>
public sealed class ArcadePressEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private RectTransform rect;
    private bool isDown;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isDown) return;
        isDown = true;
        if (rect != null)
        {
            rect.anchoredPosition += new Vector2(0f, -3f);
            rect.localScale = Vector3.one * 0.96f;
        }
        GameJuice.Instance?.HapticsLight();
        AudioFeedbackController.Instance?.PlayClank();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDown) return;
        isDown = false;
        if (rect != null)
        {
            rect.anchoredPosition -= new Vector2(0f, -3f);
            rect.localScale = Vector3.one;
        }
    }
}

public enum Button3DTheme
{
    Emerald,       // Verde Compra / Conversão / Play
    Sapphire,      // Ciano / Azul Arcade
    Gold,          // Dourado
    SanwaRed,      // Vermelho Alerta / Fechar
    PurplePink,    // Gradiente Magenta / Roxo estilo ClawCrazy
    YellowDrop,    // Amarelo Neon para o botão DROP
    WhiteGhost     // Branco translúcido para botões secundários
}

/// <summary>
/// UI Arcade Mobile Adaptativa do GarraMania (Estilo ClawCrazy 2026).
/// Tipografia viva, de alto contraste e legibilidade, modais de 7 dias, sets de prêmios e loja VIP.
/// </summary>
public sealed class ProfessionalUIController : MonoBehaviour
{
    public static ProfessionalUIController Instance { get; private set; }

    // Paleta de Cores Oficial GarraMania & Cyber-Arcade (Consolidada em UITheme)
    public static readonly Color ColorBgDeepNavy   = UITheme.ColorBgDeepNavy;
    public static readonly Color ColorCardDark     = UITheme.ColorCardDark;
    public static readonly Color ColorCardSlot     = UITheme.ColorCardSlot;
    public static readonly Color ColorNeonGold     = UITheme.ColorNeonGold;
    public static readonly Color ColorNeonCyan     = UITheme.ColorNeonCyan;
    public static readonly Color ColorNeonPink     = UITheme.ColorNeonPink;
    public static readonly Color ColorNeonPurple   = UITheme.ColorNeonPurple;
    public static readonly Color ColorNeonGreen    = UITheme.ColorNeonGreen;
    public static readonly Color ColorNeonRed      = UITheme.ColorNeonRed;
    public static readonly Color ColorTextOutline  = UITheme.ColorTextOutline;


    private GameObject canvasRoot;
    private GameObject menuPanel;
    private GameObject hudPanel;
    private GameObject controlsPanel;
    private GameObject resultPanel;
    private GameObject gameOverPanel;
    private GameObject albumPanel;
    private GameObject inspectModal;
    private GameObject dailyRewardModal;
    private GameObject setsModal;
    private GameObject vipShopModal;
    private readonly Dictionary<GameObject, Coroutine> activePanelTransitions = new Dictionary<GameObject, Coroutine>();

    // Elementos de HUD
    private Text creditsText;
    private Text timerText;
    private Image timerFill;
    private RectTransform timerFillRect;
    private Text spectatorCountText;
    private Text albumHudButtonText;
    private Text menuAlbumButtonText;
    private GameObject dailyRewardHudBadge;

    // Elementos do Álbum de Coleção
    private Text albumProgressText;
    private Transform albumGridContainer;

    // Elementos do Result Modal (Vitória)
    private Image resultPortraitImage;
    private Text resultTitleText;
    private Text resultNameText;
    private Text resultBadgeText;
    private Text resultMessageText;

    // Elementos de Game Over
    private Text gameOverTitleText;
    private Text gameOverMessageText;
    private Text gameOverButtonText;
    private Action gameOverAction;

    // Elementos do Modal de Inspeção
    private Image inspectPortraitImage;
    private Text inspectNameText;
    private Text inspectRarityText;
    private Text inspectLoreText;
    private Text inspectStatsText;

    // Controles Físicos In-Game (Joystick Virtual + Botão Sanwa Vermelho)
    private Button actionButton;
    private Image actionButtonCore;
    private Text actionText;
    private Image goldenBtnBg;
    private Text goldenBtnText;
    private Text camButtonText;
    private Text themeSelectorText;
    private CabinetThemeType albumActiveTab = CabinetThemeType.CyberNeon;
    private Image[] albumTabBgs;
    private Text[] albumTabTexts;

    private ClawController claw;
    private GameSession session;
    private ClawCameraController cameraController;
    private CollectionManager collection;

    private Font uiFont;
    private bool built;
    private float lastStartRequestTime = -10f;
    private GameObject previousPanelBeforeModal;
    private int simulatedSpectators = 2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (built) return;
        built = true;

        session = GameSession.Instance;
        collection = CollectionManager.Instance;
        claw = FindAnyObjectByType<ClawController>();
        cameraController = ClawCameraController.Instance != null
            ? ClawCameraController.Instance
            : FindAnyObjectByType<ClawCameraController>();

        PreloadPortraits();
        BuildInterface();
        ConnectSession();
        InputRouter.Instance?.SetBlocked(true);

        if (session != null)
        {
            HandleCreditsChanged(session.Credits);
            HandleTimeChanged(session.TimeRemaining, session.MaxSessionTime);
            HandleStateChanged(session.CurrentState);
        }

        if (claw != null)
        {
            claw.OnClawStateChanged.AddListener(HandleClawStateChanged);
            HandleClawStateChanged(claw.IsClosed);
        }

        UpdateAlbumBadges();
        UpdateDailyBadgeStatus();
    }

    private Vector2Int lastScreenDim = Vector2Int.zero;
    private float spectatorTimer = 0f;

    private void Update()
    {
        if (canvasRoot != null && (Screen.width != lastScreenDim.x || Screen.height != lastScreenDim.y))
        {
            lastScreenDim = new Vector2Int(Screen.width, Screen.height);
            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                bool isPortrait = Screen.width < Screen.height;
                scaler.referenceResolution = isPortrait ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f);
            }
        }

        spectatorTimer += Time.unscaledDeltaTime;
        if (spectatorTimer > 8.0f)
        {
            spectatorTimer = 0f;
            simulatedSpectators = Mathf.Clamp(simulatedSpectators + UnityEngine.Random.Range(-1, 2), 1, 6);
            if (spectatorCountText != null) spectatorCountText.text = $"AO VIVO: {simulatedSpectators}";
        }
    }

    private void ConnectSession()
    {
        if (session == null) return;
        session.OnStateChanged.AddListener(HandleStateChanged);
        session.OnTimeChanged.AddListener(HandleTimeChanged);
        session.OnCreditsChanged.AddListener(HandleCreditsChanged);
        session.OnPrizeDelivered.AddListener(HandlePrizeDelivered);
        session.OnPrizeWonShowResult.AddListener(HandlePrizeResult);

        if (collection != null)
        {
            collection.OnCollectionUpdated.AddListener(UpdateAlbumBadges);
        }

        if (PlayerEconomyManager.Instance != null)
        {
            PlayerEconomyManager.Instance.OnEconomyUpdated.AddListener(UpdateDailyBadgeStatus);
        }

        if (cameraController != null)
        {
            cameraController.OnCameraAngleChanged.AddListener(HandleCameraAngleChanged);
            HandleCameraAngleChanged(cameraController.CurrentAngle);
        }
    }

    private void HandleCameraAngleChanged(ClawCameraController.CameraViewAngle angle)
    {
        if (camButtonText != null && cameraController != null)
        {
            camButtonText.text = $"📹 {cameraController.CurrentAngleDisplayName}";
        }
    }

    private void HandleStateChanged(GameState state)
    {
        bool playing = state == GameState.Playing;
        bool idle = state == GameState.Idle;
        bool gameOver = state == GameState.GameOver;

        if (idle)
        {
            PopIn(menuPanel);
            if (hudPanel != null) hudPanel.SetActive(true);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
        }
        else if (playing)
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (resultPanel != null && !resultPanel.activeSelf) resultPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(true);
            if (controlsPanel != null) controlsPanel.SetActive(true);
        }
        else if (gameOver)
        {
            HandleGameOver();
        }
    }

    private void HandleTimeChanged(float remaining, float max)
    {
        if (timerText == null) return;
        int seconds = Mathf.CeilToInt(remaining);
        timerText.text = $"{seconds}s";

        float normalized = max > 0.01f ? Mathf.Clamp01(remaining / max) : 0f;
        if (timerFillRect != null)
        {
            timerFillRect.anchorMax = new Vector2(Mathf.Lerp(0.04f, 0.96f, normalized), 0.85f);
        }

        if (remaining <= 10f)
        {
            timerText.color = ColorNeonRed;
            if (timerFill != null) timerFill.color = ColorNeonRed;
        }
        else if (remaining <= 20f)
        {
            timerText.color = ColorNeonGold;
            if (timerFill != null) timerFill.color = ColorNeonGold;
        }
        else
        {
            timerText.color = Color.white;
            if (timerFill != null) timerFill.color = ColorNeonCyan;
        }
    }

    private void HandleCreditsChanged(int value)
    {
        if (creditsText != null) creditsText.text = $"FICHAS: {value}";
    }

    private void HandlePrizeDelivered(Prize prize, int total) { }

    private void HandlePrizeResult(Prize prize)
    {
        if (resultPanel == null) return;
        string stockId = prize != null ? (!string.IsNullOrEmpty(prize.StockId) ? prize.StockId : prize.prizeId) : "Fox";
        
        CaptureResult res = CollectionManager.Instance != null ? CollectionManager.Instance.LastCaptureResult : default;
        if (res.item == null)
        {
            res = CollectionManager.Instance != null 
                ? CollectionManager.Instance.RegisterCapture(stockId) 
                : new CaptureResult { item = new CollectionItem { id = stockId, displayName = stockId, rarity = PrizeRarity.Common } };
        }

        if (resultNameText != null) resultNameText.text = res.item.displayName.ToUpperInvariant();

        if (resultPortraitImage != null)
        {
            Sprite portrait = GetPlushiePortrait(res.item.id);
            if (portrait != null)
            {
                resultPortraitImage.sprite = portrait;
                resultPortraitImage.color = Color.white;
            }
        }

        if (resultBadgeText != null)
        {
            resultBadgeText.text = res.item.rarity == PrizeRarity.Rare ? "★★★ RARO ★★★" : res.item.rarity == PrizeRarity.Uncommon ? "★★ INCOMUM ★★" : "★ COMUM ★";
            resultBadgeText.color = res.item.themeColor;
        }

        if (resultMessageText != null)
        {
            if (res.isFirstTime)
            {
                resultMessageText.text = $"✨ NOVO NO ÁLBUM! ✨\nColeção: {res.totalUniqueUnlocked}/{CollectionManager.Instance.GetTotalCount()} bichinhos";
                resultMessageText.color = ColorNeonGold;
                GameJuice.Instance?.PlaySparkles(Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 2f : Vector3.zero);
            }
            else
            {
                resultMessageText.text = $"Adicionado à coleção! Você tem ×{res.totalOfThisType} deste modelo.\nÁlbum: {res.totalUniqueUnlocked}/{CollectionManager.Instance.GetTotalCount()} catalogados";
                resultMessageText.color = new Color(0.9f, 0.95f, 1.0f, 0.95f);
            }
        }

        UpdateAlbumBadges();
        PopIn(resultPanel);
        if (controlsPanel != null) controlsPanel.SetActive(false);
    }

    private void HandleGameOver()
    {
        if (controlsPanel != null) controlsPanel.SetActive(false);

        bool hasCredits = session != null && session.Credits > 0;
        if (gameOverTitleText != null) gameOverTitleText.text = "FIM DA JOGADA";
        if (gameOverMessageText != null)
        {
            gameOverMessageText.text = hasCredits
                ? $"Você ainda tem {session.Credits} ficha(s) restante(s)!"
                : "Fichas esgotadas!\nResgate fichas grátis ou desbloqueie mais na loja.";
        }

        if (gameOverButtonText != null)
        {
            gameOverButtonText.text = hasCredits ? "JOGAR NOVAMENTE 🪙" : "RESGATAR FICHAS 🎁";
        }

        gameOverAction = () => {
            if (hasCredits)
            {
                PopOut(gameOverPanel, () => {
                    StartGame();
                });
            }
            else
            {
                PopOut(gameOverPanel, () => {
                    session?.AddCredits(3);
                    StartGame();
                });
            }
        };

        PopIn(gameOverPanel);
    }

    private void HandleClawStateChanged(bool closed)
    {
        if (actionText == null) return;
        actionText.text = closed ? "SOLTAR" : "AGARRAR";
        if (actionButtonCore != null)
        {
            bool isGold = PlayerEconomyManager.Instance != null && PlayerEconomyManager.Instance.IsGoldenClawActive;
            actionButtonCore.color = isGold ? new Color(1f, 0.9f, 0.4f, 1f) : (closed ? new Color(1f, 0.7f, 0.7f, 1f) : Color.white);
        }
    }

    public void StartGame()
    {
        if (Time.unscaledTime - lastStartRequestTime < 0.15f) return;
        lastStartRequestTime = Time.unscaledTime;
        if (session == null) session = GameSession.Instance;
        if (session == null) return;

        if (session.Credits <= 0)
        {
            session.AddCredits(3);
        }

        PlayerEconomyManager.Instance?.RegisterGameStarted();
        session.StartGame();
    }

    public void ContinueAfterResult()
    {
        PopOut(resultPanel, () => {
            if (session != null)
            {
                if (session.Credits > 0)
                {
                    StartGame();
                }
                else
                {
                    HandleGameOver();
                }
            }
        });
    }

    public void OpenAlbum()
    {
        previousPanelBeforeModal = menuPanel != null && menuPanel.activeSelf ? menuPanel : hudPanel;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);

        if (CabinetThemeManager.Instance != null)
        {
            albumActiveTab = CabinetThemeManager.Instance.CurrentThemeType;
        }

        BuildAlbumGrid();
        PopIn(albumPanel);
    }

    public void CloseAlbum()
    {
        PopOut(albumPanel, () => {
            if (previousPanelBeforeModal == menuPanel)
            {
                PopIn(menuPanel);
            }
            else
            {
                if (hudPanel != null) hudPanel.SetActive(true);
                if (controlsPanel != null && session != null && session.CurrentState == GameState.Playing)
                {
                    controlsPanel.SetActive(true);
                }
            }
        });
    }

    public void OpenDailyReward()
    {
        session?.AddCredits(5);
    }

    public void CloseDailyReward() { }

    public void OpenSetsShowcase()
    {
        OpenAlbum();
    }

    public void CloseSetsShowcase() { }

    public void OpenVipShop()
    {
        session?.AddCredits(5);
    }

    public void CloseVipShop() { }

    private void UpdateAlbumBadges()
    {
        var theme = CabinetThemeManager.Instance != null ? CabinetThemeManager.Instance.CurrentTheme : null;
        int themeUnlocked = 0;
        int themeTotal = 6;
        if (theme != null && theme.exclusivePrizeIds != null)
        {
            themeTotal = theme.exclusivePrizeIds.Count;
            for (int i = 0; i < theme.exclusivePrizeIds.Count; i++)
            {
                var it = CollectionManager.Instance.GetItem(theme.exclusivePrizeIds[i]);
                if (it != null && it.IsUnlocked) themeUnlocked++;
            }
        }
        else
        {
            themeUnlocked = CollectionManager.Instance.GetUnlockedCount();
            themeTotal = CollectionManager.Instance.GetTotalCount();
        }

        string hudBadge = $"ÁLBUM: {themeUnlocked}/{themeTotal}";
        if (albumHudButtonText != null) albumHudButtonText.text = hudBadge;
        if (menuAlbumButtonText != null) menuAlbumButtonText.text = $"ÁLBUM DE COLEÇÃO ({themeUnlocked}/{themeTotal})";
    }

    private void UpdateDailyBadgeStatus()
    {
        bool available = PlayerEconomyManager.Instance != null && PlayerEconomyManager.Instance.IsDailyRewardAvailable();
        if (dailyRewardHudBadge != null) dailyRewardHudBadge.SetActive(available);
    }

    private void BuildInterface()
    {
        uiFont = UITheme.GetArcadeFont();

        canvasRoot = new GameObject("GarraManiaUI_MobileSystem");
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        bool isPortrait = Screen.width < Screen.height;
        scaler.referenceResolution = isPortrait ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = isPortrait ? 0f : 0.65f;
        canvasRoot.AddComponent<GraphicRaycaster>();

        GameObject safeArea = CreatePanel(canvas.transform, "SafeArea", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        safeArea.AddComponent<SafeAreaFitter>();
        Transform root = safeArea.transform;

        hudPanel = CreatePanel(root, "HUD", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildHud(hudPanel.transform);

        controlsPanel = CreatePanel(root, "Controls", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildControls(controlsPanel.transform);

        menuPanel = CreatePanel(root, "Menu", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildMenu(menuPanel.transform);

        resultPanel = CreatePanel(root, "Result", new Color(0.02f, 0.03f, 0.08f, 0.60f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildResult(resultPanel.transform);

        gameOverPanel = CreatePanel(root, "GameOver", new Color(0.02f, 0.03f, 0.08f, 0.65f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildGameOver(gameOverPanel.transform);

        albumPanel = CreatePanel(root, "AlbumPanel", new Color(0.02f, 0.03f, 0.08f, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildAlbumContainer(albumPanel.transform);
        albumPanel.SetActive(false);

        dailyRewardModal = CreatePanel(root, "DailyRewardModal", new Color(0.02f, 0.03f, 0.08f, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        dailyRewardModal.SetActive(false);

        setsModal = CreatePanel(root, "SetsModal", new Color(0.02f, 0.03f, 0.08f, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        setsModal.SetActive(false);

        vipShopModal = CreatePanel(root, "VipShopModal", new Color(0.02f, 0.03f, 0.08f, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        vipShopModal.SetActive(false);
    }

    private void BuildHud(Transform parent)
    {
        bool isP = Screen.width < Screen.height;

        Vector2 backMin = isP ? new Vector2(0.03f, 0.925f) : new Vector2(0.02f, 0.920f);
        Vector2 backMax = isP ? new Vector2(0.12f, 0.985f) : new Vector2(0.07f, 0.988f);
        GameObject backBtn = CreateArcadeButton(parent, "BackBtn", backMin, backMax, Button3DTheme.WhiteGhost, () => {
            if (session != null && session.CurrentState == GameState.Playing)
            {
                session.ResetSession();
                InputRouter.Instance?.SetBlocked(true);
            }
            else if (albumPanel != null && albumPanel.activeSelf)
            {
                CloseAlbum();
            }
            else if (inspectModal != null && inspectModal.activeSelf)
            {
                inspectModal.SetActive(false);
            }
        }, "X", 26);
        Text backTxt = backBtn.GetComponentInChildren<Text>();
        if (backTxt != null)
        {
            backTxt.text = "X";
            backTxt.color = new Color(0.90f, 0.15f, 0.20f);
            backTxt.fontStyle = FontStyle.Bold;
            backTxt.fontSize = isP ? 26 : 28;
        }

        Vector2 specMin = isP ? new Vector2(0.13f, 0.925f) : new Vector2(0.08f, 0.920f);
        Vector2 specMax = isP ? new Vector2(0.36f, 0.985f) : new Vector2(0.20f, 0.988f);
        GameObject specPill = CreateGlassPill(parent, "SpecPill", specMin, specMax, ColorNeonCyan);
        spectatorCountText = CreateText(specPill.transform, "SpecCount", "AO VIVO: 2", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, isP ? 16 : 18, ColorNeonCyan, TextAnchor.MiddleCenter, true);

        Vector2 tokMin = isP ? new Vector2(0.38f, 0.925f) : new Vector2(0.22f, 0.920f);
        Vector2 tokMax = isP ? new Vector2(0.66f, 0.985f) : new Vector2(0.42f, 0.988f);
        GameObject tokPill = CreateGlassPill(parent, "TokenPill", tokMin, tokMax, ColorNeonGold);
        creditsText = CreateText(tokPill.transform, "TokenCount", "FICHAS: 3", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, isP ? 18 : 20, ColorNeonGold, TextAnchor.MiddleCenter, true);

        Vector2 albMin = isP ? new Vector2(0.68f, 0.925f) : new Vector2(0.78f, 0.920f);
        Vector2 albMax = isP ? new Vector2(0.97f, 0.985f) : new Vector2(0.98f, 0.988f);
        GameObject albBtn = CreateArcadeButton(parent, "AlbumHudBtn", albMin, albMax, Button3DTheme.PurplePink, OpenAlbum, "ÁLBUM: 0/6", isP ? 16 : 18);
        albumHudButtonText = albBtn.GetComponentInChildren<Text>();
    }

    private void BuildControls(Transform parent)
    {
        bool isP = Screen.width < Screen.height;

        // 1. Joystick Virtual (Círculo Translúcido Ergonômico na Esquerda)
        Vector2 joyMin = isP ? new Vector2(0.05f, 0.035f) : new Vector2(0.04f, 0.035f);
        Vector2 joyMax = isP ? new Vector2(0.35f, 0.185f) : new Vector2(0.18f, 0.235f);
        GameObject joystick = CreatePanel(parent, "VirtualJoystick", new Color(0.06f, 0.10f, 0.22f, 0.80f), joyMin, joyMax, Vector2.zero, Vector2.zero, true, GetCircleSprite());
        CreatePanel(joystick.transform, "JoyRim", ColorNeonCyan * 0.35f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
        GameObject handle = CreatePanel(joystick.transform, "Handle", ColorNeonCyan, new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f), Vector2.zero, Vector2.zero, true, GetCircleSprite());
        VirtualJoystickView joystickView = joystick.AddComponent<VirtualJoystickView>();
        joystickView.Configure(joystick.GetComponent<RectTransform>(), handle.GetComponent<RectTransform>(), isP ? 55f : 60f);

        // 2. BIG RED ARCADE PUSH-BUTTON (Procedural Sanwa 3D Dome)
        Vector2 actMin = isP ? new Vector2(0.67f, 0.035f) : new Vector2(0.82f, 0.035f);
        Vector2 actMax = isP ? new Vector2(0.97f, 0.185f) : new Vector2(0.97f, 0.235f);

        GameObject actionRing = CreatePanel(parent, "ActionButton_Ring", new Color(0.10f, 0.14f, 0.24f, 0.95f), actMin, actMax, Vector2.zero, Vector2.zero, true, GetCircleSprite());
        CreatePanel(actionRing.transform, "RingBevel", new Color(1f, 1f, 1f, 0.18f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
        GameObject actionButtonObj = CreatePanel(actionRing.transform, "ActionButton_Core", ColorNeonPink, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero, true, GetCircleSprite());
        CreatePanel(actionButtonObj.transform, "DomeHighlight", new Color(1f, 1f, 1f, 0.25f), new Vector2(0.15f, 0.52f), new Vector2(0.85f, 0.92f), Vector2.zero, Vector2.zero, false, GetCircleSprite());

        actionButtonCore = actionButtonObj.GetComponent<Image>();
        actionButton = actionButtonObj.AddComponent<Button>();
        actionButton.onClick.AddListener(() => InputRouter.Instance?.TriggerTouchAction());
        actionButtonObj.AddComponent<ArcadePressEffect>();

        ColorBlock actCb = actionButton.colors;
        actCb.normalColor = ColorNeonPink;
        actCb.highlightedColor = Color.white;
        actCb.pressedColor = ColorNeonPink * 0.82f;
        actCb.fadeDuration = 0.04f;
        actionButton.colors = actCb;

        actionText = CreateText(actionButtonObj.transform, "ActionText", "AGARRAR", new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero, isP ? 34 : 36, Color.white, TextAnchor.MiddleCenter, true);

        // 3. Botão Alternador de Câmera (Topo Direito)
        Vector2 camMin = isP ? new Vector2(0.66f, 0.845f) : new Vector2(0.85f, 0.840f);
        Vector2 camMax = isP ? new Vector2(0.98f, 0.915f) : new Vector2(0.99f, 0.910f);
        string currentCamName = cameraController != null ? cameraController.CurrentAngleDisplayName : "FRENTE";
        GameObject camBtn = CreateArcadeButton(parent, "CamToggleBtn", camMin, camMax, Button3DTheme.Sapphire, () => {
            if (cameraController != null)
            {
                cameraController.ToggleCameraAngle();
                HandleCameraAngleChanged(cameraController.CurrentAngle);
            }
        }, $"📹 {currentCamName}", isP ? 20 : 22);
        camButtonText = camBtn.GetComponentInChildren<Text>();

        // 4. Botão Ficha Dourada (Docked Acima do Botão Sanwa na Base)
        Vector2 goldMin = isP ? new Vector2(0.67f, 0.200f) : new Vector2(0.82f, 0.250f);
        Vector2 goldMax = isP ? new Vector2(0.97f, 0.255f) : new Vector2(0.97f, 0.305f);
        GameObject goldBtn = CreateArcadeButton(parent, "GoldenTokenBtn", goldMin, goldMax, Button3DTheme.Gold, () => {
            if (PlayerEconomyManager.Instance != null)
            {
                bool active = PlayerEconomyManager.Instance.ToggleGoldenClaw();
                if (actionButtonCore != null) actionButtonCore.color = active ? ColorNeonGold : ColorNeonPink;
                if (goldenBtnText != null) goldenBtnText.text = active ? "⭐ 100% ATIVO" : "⭐ GARRA 100%";
            }
        }, "⭐ GARRA 100%", isP ? 20 : 22);
        goldenBtnBg = goldBtn.GetComponent<Image>();
        goldenBtnText = goldBtn.GetComponentInChildren<Text>();

        // 5. Timer Centralizado
        Vector2 timeMin = isP ? new Vector2(0.38f, 0.045f) : new Vector2(0.44f, 0.040f);
        Vector2 timeMax = isP ? new Vector2(0.62f, 0.145f) : new Vector2(0.56f, 0.130f);
        GameObject timerPill = CreateGlassPill(parent, "TimerPill", timeMin, timeMax, ColorNeonCyan);
        timerFill = CreatePanel(timerPill.transform, "TimerFill", ColorNeonCyan, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.85f), Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).GetComponent<Image>();
        timerFill.type = Image.Type.Sliced;
        timerFillRect = timerFill.rectTransform;
        timerText = CreateText(timerPill.transform, "TimerText", "45s", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 28, Color.white, TextAnchor.MiddleCenter, true);
    }

    private void BuildMenu(Transform parent)
    {
        bool isP = Screen.width < Screen.height;

        Vector2 logoMin = isP ? new Vector2(0.06f, 0.78f) : new Vector2(0.30f, 0.82f);
        Vector2 logoMax = isP ? new Vector2(0.94f, 0.94f) : new Vector2(0.70f, 0.94f);
        GameObject logoGroup = CreatePanel(parent, "HeaderLogo", Color.clear, logoMin, logoMax, Vector2.zero, Vector2.zero, false);
        CreateText(logoGroup.transform, "LogoTitle", "GARRAMANIA", new Vector2(0f, 0.35f), Vector2.one, Vector2.zero, Vector2.zero, isP ? 48 : 52, ColorNeonGold, TextAnchor.MiddleCenter, true);
        CreateText(logoGroup.transform, "LogoSub", "FLIPERAMA & REAL CLAW MACHINE", Vector2.zero, new Vector2(1f, 0.38f), Vector2.zero, Vector2.zero, 22, ColorNeonCyan, TextAnchor.MiddleCenter, true);

        Vector2 sheetMin = isP ? new Vector2(0.06f, 0.04f) : new Vector2(0.25f, 0.04f);
        Vector2 sheetMax = isP ? new Vector2(0.94f, 0.42f) : new Vector2(0.75f, 0.46f);
        GameObject sheet = CreatePanel(parent, "MenuSheet", ColorBgDeepNavy, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(sheet.transform, "SheetBorder", ColorNeonCyan * 0.45f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        // 1. Botão Jogar
        CreateArcadeButton(sheet.transform, "PlayBtn", new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.94f), Button3DTheme.PurplePink, StartGame, "JOGAR AGORA! 🕹️", isP ? 34 : 36);

        // 2. Seletor de Cabine Temática (◀ TEMA ▶)
        Vector2 themeMin = new Vector2(0.06f, 0.38f);
        Vector2 themeMax = new Vector2(0.94f, 0.62f);
        GameObject themeSelPanel = CreatePanel(sheet.transform, "ThemeSelector", ColorCardDark, themeMin, themeMax, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite());
        CreatePanel(themeSelPanel.transform, "ThemeBorder", ColorNeonGold * 0.40f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        string initialThemeName = CabinetThemeManager.Instance != null && CabinetThemeManager.Instance.CurrentTheme != null 
            ? CabinetThemeManager.Instance.CurrentTheme.displayName 
            : "CYBER NEON 🕹️";
        themeSelectorText = CreateText(themeSelPanel.transform, "ThemeName", initialThemeName, new Vector2(0.20f, 0f), new Vector2(0.80f, 1f), Vector2.zero, Vector2.zero, isP ? 20 : 22, ColorNeonGold, TextAnchor.MiddleCenter, true);

        GameObject prevBtn = CreateArcadeButton(themeSelPanel.transform, "PrevThemeBtn", new Vector2(0.02f, 0.10f), new Vector2(0.18f, 0.90f), Button3DTheme.Sapphire, () => {
            if (CabinetThemeManager.Instance != null)
            {
                CabinetThemeManager.Instance.PreviousTheme();
                if (themeSelectorText != null) themeSelectorText.text = CabinetThemeManager.Instance.CurrentTheme.displayName;
                UpdateAlbumBadges();
            }
        }, "<", 28);
        Text prevTxt = prevBtn.GetComponentInChildren<Text>();
        if (prevTxt != null)
        {
            prevTxt.text = "<";
            prevTxt.fontStyle = FontStyle.Bold;
            prevTxt.fontSize = isP ? 26 : 30;
            prevTxt.color = ColorNeonCyan;
        }

        GameObject nextBtn = CreateArcadeButton(themeSelPanel.transform, "NextThemeBtn", new Vector2(0.82f, 0.10f), new Vector2(0.98f, 0.90f), Button3DTheme.Sapphire, () => {
            if (CabinetThemeManager.Instance != null)
            {
                CabinetThemeManager.Instance.NextTheme();
                if (themeSelectorText != null) themeSelectorText.text = CabinetThemeManager.Instance.CurrentTheme.displayName;
                UpdateAlbumBadges();
            }
        }, ">", 28);
        Text nextTxt = nextBtn.GetComponentInChildren<Text>();
        if (nextTxt != null)
        {
            nextTxt.text = ">";
            nextTxt.fontStyle = FontStyle.Bold;
            nextTxt.fontSize = isP ? 26 : 30;
            nextTxt.color = ColorNeonCyan;
        }

        // 3. Botão Álbum de Prêmios
        CreateArcadeButton(sheet.transform, "AlbumBtn", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.32f), Button3DTheme.Emerald, OpenAlbum, "🏆 ÁLBUM DE COLEÇÃO", isP ? 24 : 26);
    }

    private void BuildDailyRewardModal() { }
    private void BuildSetsModal() { }
    private void BuildVipShopModal() { }

    private void BuildResult(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 sheetMin = isP ? new Vector2(0.04f, 0.04f) : new Vector2(0.24f, 0.04f);
        Vector2 sheetMax = isP ? new Vector2(0.96f, 0.58f) : new Vector2(0.76f, 0.64f);

        GameObject sheet = CreatePanel(parent, "ResultSheet", ColorBgDeepNavy, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(sheet.transform, "SheetBorder", ColorNeonGold * 0.60f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        GameObject bannerObj = CreatePanel(sheet.transform, "CelebrationBanner", ColorCardDark, new Vector2(0.06f, 0.84f), new Vector2(0.94f, 0.98f), Vector2.zero, Vector2.zero, false, GetRoundedRectSprite());
        CreatePanel(bannerObj.transform, "BannerGlow", ColorNeonGold * 0.6f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();
        resultTitleText = CreateText(bannerObj.transform, "Title", "🎉 PRÊMIO CAPTURADO!", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 34, ColorNeonGold, TextAnchor.MiddleCenter, true);

        GameObject portraitContainer = CreatePanel(sheet.transform, "PortraitContainer", Color.clear, new Vector2(0.06f, 0.44f), new Vector2(0.40f, 0.82f), Vector2.zero, Vector2.zero, false);
        GameObject portraitImgObj = CreatePanel(portraitContainer.transform, "PortraitImg", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        resultPortraitImage = portraitImgObj.GetComponent<Image>();
        if (resultPortraitImage != null) resultPortraitImage.preserveAspect = true;

        resultNameText = CreateText(sheet.transform, "PrizeName", "RAPOSA ASTUTA", new Vector2(0.42f, 0.64f), new Vector2(0.95f, 0.82f), Vector2.zero, Vector2.zero, 32, Color.white, TextAnchor.MiddleLeft, true);
        resultBadgeText = CreateText(sheet.transform, "Badge", "★ COMUM ★", new Vector2(0.42f, 0.48f), new Vector2(0.95f, 0.64f), Vector2.zero, Vector2.zero, 24, ColorNeonCyan, TextAnchor.MiddleLeft, true);
        resultMessageText = CreateText(sheet.transform, "Message", "Adicionado ao seu álbum de coleção!", new Vector2(0.06f, 0.33f), new Vector2(0.94f, 0.45f), Vector2.zero, Vector2.zero, 22, new Color(0.9f, 0.95f, 1f, 0.95f), TextAnchor.MiddleCenter, false);

        // BOTÃO SOCIAL VIRAL: Compartilhar no WhatsApp
        CreateArcadeButton(sheet.transform, "ShareBtn", new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.31f), Button3DTheme.Emerald, ShareVictoryOnWhatsApp, "COMPARTILHAR NO WHATSAPP 💬", 24);

        // BOTÃO JOGAR NOVAMENTE / CONTINUAR
        CreateArcadeButton(sheet.transform, "ContinueBtn", new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.16f), Button3DTheme.PurplePink, ContinueAfterResult, "JOGAR NOVAMENTE 🕹️", 28);

        // BOTÃO FECHAR '✕' NO CANTO SUPERIOR DIREITO
        CreateArcadeButton(sheet.transform, "CloseCornerBtn", new Vector2(0.86f, 0.86f), new Vector2(0.97f, 0.97f), Button3DTheme.WhiteGhost, ContinueAfterResult, "✕", 22);

        parent.gameObject.SetActive(false);
    }

    private void ShareVictoryOnWhatsApp()
    {
        string prize = resultNameText != null ? resultNameText.text : "uma pelúcia rara";
        string msg = $"🎉 Acabei de capturar {prize} na máquina GarraMania! 🕹️🧸\nConsegue pegar também? Jogue online grátis:";
        string link = "https://garramania.vercel.app";
        string fullUrl = $"https://api.whatsapp.com/send?text={UnityEngine.Networking.UnityWebRequest.EscapeURL(msg + " " + link)}";
        Application.OpenURL(fullUrl);
    }

    private void BuildGameOver(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 sheetMin = isP ? new Vector2(0.04f, 0.04f) : new Vector2(0.25f, 0.04f);
        Vector2 sheetMax = isP ? new Vector2(0.96f, 0.38f) : new Vector2(0.75f, 0.42f);

        GameObject sheet = CreatePanel(parent, "GameOverSheet", ColorBgDeepNavy, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(sheet.transform, "Border", ColorNeonCyan * 0.45f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        gameOverTitleText = CreateText(sheet.transform, "Title", "FIM DA JOGADA", new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.90f), Vector2.zero, Vector2.zero, 36, Color.white, TextAnchor.MiddleCenter, true);
        gameOverMessageText = CreateText(sheet.transform, "Msg", "Você ainda tem fichas restantes!", new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.66f), Vector2.zero, Vector2.zero, 26, ColorNeonCyan, TextAnchor.MiddleCenter, false);

        GameObject btn = CreateArcadeButton(sheet.transform, "GameOverBtn", new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.38f), Button3DTheme.PurplePink, () => gameOverAction?.Invoke(), "JOGAR NOVAMENTE 🕹️", 30);
        gameOverButtonText = btn.GetComponentInChildren<Text>();

        parent.gameObject.SetActive(false);
    }

    private void BuildAlbumContainer(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 sheetMin = isP ? new Vector2(0.04f, 0.02f) : new Vector2(0.18f, 0.02f);
        Vector2 sheetMax = isP ? new Vector2(0.96f, 0.94f) : new Vector2(0.82f, 0.96f);

        GameObject window = CreatePanel(parent, "AlbumWindow", ColorBgDeepNavy, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(window.transform, "Border", ColorNeonCyan * 0.50f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        CreateText(window.transform, "Header", "🏆 ÁLBUM DE COLEÇÃO", new Vector2(0.05f, 0.925f), new Vector2(0.95f, 0.985f), Vector2.zero, Vector2.zero, isP ? 36 : 40, ColorNeonGold, TextAnchor.MiddleCenter, true);

        // 3 ABAS DE CABINES INDEPENDENTES
        Vector2 tabsMin = new Vector2(0.04f, 0.855f);
        Vector2 tabsMax = new Vector2(0.96f, 0.915f);
        GameObject tabsBar = CreatePanel(window.transform, "TabsBar", ColorCardDark, tabsMin, tabsMax, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite());

        albumTabBgs = new Image[3];
        albumTabTexts = new Text[3];

        string[] tabLabels = { "🕹️ NEON", "🌸 KAWAII", "👑 VIP" };
        CabinetThemeType[] tabTypes = { CabinetThemeType.CyberNeon, CabinetThemeType.KawaiiPastel, CabinetThemeType.GoldCasino };

        for (int i = 0; i < 3; i++)
        {
            int tabIdx = i;
            float step = 1.0f / 3.0f;
            Vector2 tMin = new Vector2(i * step + 0.01f, 0.08f);
            Vector2 tMax = new Vector2((i + 1) * step - 0.01f, 0.92f);

            GameObject tabBtn = CreateArcadeButton(tabsBar.transform, $"Tab_{i}", tMin, tMax, Button3DTheme.Sapphire, () => {
                albumActiveTab = tabTypes[tabIdx];
                BuildAlbumGrid();
            }, tabLabels[i], isP ? 18 : 20);

            albumTabBgs[i] = tabBtn.GetComponent<Image>();
            albumTabTexts[i] = tabBtn.GetComponentInChildren<Text>();
        }

        albumProgressText = CreateText(window.transform, "ProgressPill", "COLEÇÃO: 0 / 6 DESBLOQUEADOS (0%)", new Vector2(0.05f, 0.795f), new Vector2(0.95f, 0.845f), Vector2.zero, Vector2.zero, 22, ColorNeonCyan, TextAnchor.MiddleCenter, true);

        GameObject gridObj = CreatePanel(window.transform, "GridContainer", Color.clear, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.785f), Vector2.zero, Vector2.zero, false);
        albumGridContainer = gridObj.transform;

        CreateArcadeButton(window.transform, "CloseAlbumBtn", new Vector2(0.10f, 0.015f), new Vector2(0.90f, 0.085f), Button3DTheme.WhiteGhost, CloseAlbum, "VOLTAR À MÁQUINA", 24);

        BuildInspectModal(parent);
    }

    private void BuildAlbumGrid()
    {
        if (albumGridContainer == null) return;
        foreach (Transform child in albumGridContainer) Destroy(child.gameObject);

        bool isP = Screen.width < Screen.height;

        // Atualiza botões das abas
        if (albumTabBgs != null)
        {
            CabinetThemeType[] tabTypes = { CabinetThemeType.CyberNeon, CabinetThemeType.KawaiiPastel, CabinetThemeType.GoldCasino };
            for (int t = 0; t < 3; t++)
            {
                bool active = (albumActiveTab == tabTypes[t]);
                if (albumTabBgs[t] != null) albumTabBgs[t].color = active ? ColorNeonGold * 0.85f : ColorCardSlot;
                if (albumTabTexts[t] != null) albumTabTexts[t].color = active ? Color.white : new Color(0.7f, 0.8f, 0.95f);
            }
        }

        List<string> exclusiveIds = GetPrizesForTheme(albumActiveTab);
        string themeTitle = albumActiveTab == CabinetThemeType.CyberNeon ? "CYBER NEON" : albumActiveTab == CabinetThemeType.KawaiiPastel ? "KAWAII CANDY" : "GOLD CASINO VIP";

        int unlocked = 0;
        for (int i = 0; i < exclusiveIds.Count; i++)
        {
            var it = CollectionManager.Instance.GetItem(exclusiveIds[i]);
            if (it != null && it.IsUnlocked) unlocked++;
        }

        int total = exclusiveIds.Count;
        int pct = Mathf.RoundToInt((float)unlocked / Mathf.Max(1, total) * 100f);

        if (albumProgressText != null)
        {
            albumProgressText.text = $"COLEÇÃO {themeTitle}: {unlocked} / {total} DESBLOQUEADOS ({pct}%)";
        }

        int cols = isP ? 2 : 3;
        int rows = isP ? 3 : 2;

        for (int i = 0; i < exclusiveIds.Count; i++)
        {
            var item = CollectionManager.Instance.GetItem(exclusiveIds[i]);
            if (item == null) continue;

            int c = i % cols;
            int r = rows - 1 - (i / cols);

            float cellW = 1.0f / cols;
            float cellH = 1.0f / rows;
            Vector2 aMin = new Vector2(c * cellW + 0.02f, r * cellH + 0.02f);
            Vector2 aMax = new Vector2((c + 1) * cellW - 0.02f, (r + 1) * cellH - 0.02f);

            Color rarityColor = item.themeColor;

            GameObject card = CreatePanel(albumGridContainer, $"Slot_{item.id}", ColorCardSlot, aMin, aMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
            CreatePanel(card.transform, "CardBorder", item.IsUnlocked ? rarityColor * 0.70f : Color.white * 0.12f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

            Button btn = card.AddComponent<Button>();
            card.AddComponent<ArcadePressEffect>();
            btn.onClick.AddListener(() => OpenInspect(item));

            Sprite portrait = GetPlushiePortrait(item.id);

            if (item.IsUnlocked)
            {
                GameObject imgFrame = CreatePanel(card.transform, "Frame", ColorBgDeepNavy, new Vector2(0.20f, 0.42f), new Vector2(0.80f, 0.88f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
                CreatePanel(imgFrame.transform, "FrameBorder", rarityColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
                GameObject imgObj = CreatePanel(imgFrame.transform, "Portrait", Color.white, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
                if (portrait != null)
                {
                    imgObj.GetComponent<Image>().sprite = portrait;
                }

                GameObject countPill = CreateGlassPill(card.transform, "CountPill", new Vector2(0.55f, 0.74f), new Vector2(0.96f, 0.95f), ColorNeonGold);
                CreateText(countPill.transform, "CountText", $"×{item.count}", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 20, ColorNeonGold, TextAnchor.MiddleCenter, true);

                CreateText(card.transform, "Name", item.displayName.ToUpperInvariant(), new Vector2(0.04f, 0.20f), new Vector2(0.96f, 0.40f), Vector2.zero, Vector2.zero, 24, Color.white, TextAnchor.MiddleCenter, true);

                string tag = item.rarity == PrizeRarity.Legendary ? "★ LENDÁRIO" : item.rarity == PrizeRarity.Rare ? "★ RARO" : item.rarity == PrizeRarity.Uncommon ? "★ INCOMUM" : "COMUM";
                CreateText(card.transform, "RarityTag", tag, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.20f), Vector2.zero, Vector2.zero, 20, rarityColor, TextAnchor.MiddleCenter, true);
            }
            else
            {
                GameObject lockCircle = CreatePanel(card.transform, "LockCircle", new Color(0.08f, 0.11f, 0.20f, 0.9f), new Vector2(0.24f, 0.42f), new Vector2(0.76f, 0.88f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
                CreatePanel(lockCircle.transform, "LockRim", Color.white * 0.15f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
                CreateText(lockCircle.transform, "LockMark", "?", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 34, new Color(0.5f, 0.65f, 0.85f, 0.6f), TextAnchor.MiddleCenter, true);

                CreateText(card.transform, "Name", "???", new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.28f), Vector2.zero, Vector2.zero, 22, new Color(0.85f, 0.9f, 1f, 0.95f), TextAnchor.MiddleCenter, true);
                CreateText(card.transform, "Hint", "BLOQUEADO", new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.16f), Vector2.zero, Vector2.zero, 20, new Color(0.6f, 0.7f, 0.85f, 0.8f), TextAnchor.MiddleCenter, false);
            }
        }
    }

    private List<string> GetPrizesForTheme(CabinetThemeType type)
    {
        switch (type)
        {
            case CabinetThemeType.KawaiiPastel:
                return new List<string> { "Fox_Arctic", "Bear_Polar", "Bear_Panda", "Koala_Eucalyptus", "Fish_Clown", "Porky_Classic" };
            case CabinetThemeType.GoldCasino:
                return new List<string> { "Fish_Gold", "Badger_Honey", "Fox_Shadow", "Bear_Galaxy", "Koala_King", "Porky_Diamond" };
            case CabinetThemeType.CyberNeon:
            default:
                return new List<string> { "Fox", "GreenBear", "BalloonFish", "Koala", "Badger", "Porky" };
        }
    }

    private void BuildInspectModal(Transform parent)
    {
        inspectModal = CreatePanel(parent, "InspectModal", new Color(0.01f, 0.02f, 0.04f, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
        inspectModal.SetActive(false);

        bool isP = Screen.width < Screen.height;
        Vector2 min = isP ? new Vector2(0.08f, 0.18f) : new Vector2(0.28f, 0.16f);
        Vector2 max = isP ? new Vector2(0.92f, 0.82f) : new Vector2(0.72f, 0.84f);

        GameObject card = CreatePanel(inspectModal.transform, "InspectCard", ColorBgDeepNavy, min, max, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(card.transform, "Border", ColorNeonGold * 0.45f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        GameObject imgFrame = CreatePanel(card.transform, "InspectFrame", ColorCardDark, new Vector2(0.35f, 0.64f), new Vector2(0.65f, 0.92f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
        CreatePanel(imgFrame.transform, "FrameBorder", ColorNeonGold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
        GameObject imgObj = CreatePanel(imgFrame.transform, "InspectImg", Color.white, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
        inspectPortraitImage = imgObj.GetComponent<Image>();

        inspectNameText = CreateText(card.transform, "Name", "RAPOSA ASTUTA", new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.64f), Vector2.zero, Vector2.zero, 34, Color.white, TextAnchor.MiddleCenter, true);
        inspectRarityText = CreateText(card.transform, "Rarity", "★ COMUM ★", new Vector2(0.05f, 0.46f), new Vector2(0.95f, 0.54f), Vector2.zero, Vector2.zero, 26, ColorNeonCyan, TextAnchor.MiddleCenter, true);
        inspectLoreText = CreateText(card.transform, "Lore", "Descrição da pelúcia...", new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.46f), Vector2.zero, Vector2.zero, 24, new Color(0.9f, 0.95f, 1f, 0.90f), TextAnchor.MiddleCenter, false);
        inspectStatsText = CreateText(card.transform, "Stats", "Capturas: 0", new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.28f), Vector2.zero, Vector2.zero, 24, ColorNeonGold, TextAnchor.MiddleCenter, false);

        CreateArcadeButton(card.transform, "CloseInspectBtn", new Vector2(0.18f, 0.04f), new Vector2(0.82f, 0.16f), Button3DTheme.WhiteGhost, () => inspectModal.SetActive(false), "FECHAR", 26);
    }

    private void OpenInspect(CollectionItem item)
    {
        if (inspectModal == null || item == null) return;
        Sprite portrait = GetPlushiePortrait(item.id);

        if (!item.IsUnlocked)
        {
            if (inspectPortraitImage != null)
            {
                inspectPortraitImage.sprite = portrait;
                inspectPortraitImage.color = new Color(0.05f, 0.07f, 0.12f, 0.85f);
            }
            inspectNameText.text = "??? (BLOQUEADO)";
            inspectRarityText.text = "Procure na vitrine do fliperama!";
            inspectRarityText.color = Color.gray;
            inspectLoreText.text = "Este bichinho ainda não foi capturado. Use as pinças da garra para agarrá-lo!";
            inspectStatsText.text = "Ainda não catalogado";
            inspectStatsText.color = Color.gray;
        }
        else
        {
            if (inspectPortraitImage != null)
            {
                inspectPortraitImage.sprite = portrait;
                inspectPortraitImage.color = Color.white;
            }
            inspectNameText.text = item.displayName.ToUpperInvariant();
            inspectRarityText.text = item.rarity == PrizeRarity.Rare ? "★★★ RARIDADE: RARO ★★★" : item.rarity == PrizeRarity.Uncommon ? "★★ RARIDADE: INCOMUM ★★" : "★ RARIDADE: COMUM ★";
            inspectRarityText.color = item.themeColor;
            inspectLoreText.text = item.lore;
            inspectStatsText.text = $"Total Capturado: ×{item.count} | Primeiro em: {item.firstCapturedAt}";
            inspectStatsText.color = ColorNeonGold;
        }
        inspectModal.SetActive(true);
    }

    private void PreloadPortraits()
    {
        string[] ids = { "fox", "greenbear", "balloonfish", "koala", "badger", "porky" };
        foreach (string id in ids) GetPlushiePortrait(id);
    }

    private void PopIn(GameObject panel)
    {
        if (panel == null) return;
        panel.SetActive(true);
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        if (activePanelTransitions.TryGetValue(panel, out var running) && running != null)
        {
            StopCoroutine(running);
        }

        activePanelTransitions[panel] = StartCoroutine(PopInRoutine(panel, panel.GetComponent<RectTransform>(), cg));
    }

    private IEnumerator PopInRoutine(GameObject panel, RectTransform rect, CanvasGroup cg)
    {
        float duration = 0.20f;
        float elapsed = 0f;
        cg.alpha = 0f;
        Vector2 startPos = new Vector2(0f, -40f);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = Mathf.Sin(t * Mathf.PI * 0.5f);
            rect.anchoredPosition = Vector2.Lerp(startPos, Vector2.zero, ease);
            cg.alpha = ease;
            yield return null;
        }
        rect.anchoredPosition = Vector2.zero;
        cg.alpha = 1f;
        activePanelTransitions.Remove(panel);
    }

    private void PopOut(GameObject panel, Action onComplete = null)
    {
        if (panel == null) return;
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();

        if (activePanelTransitions.TryGetValue(panel, out var running) && running != null)
        {
            StopCoroutine(running);
        }

        activePanelTransitions[panel] = StartCoroutine(PopOutRoutine(panel, panel.GetComponent<RectTransform>(), cg, onComplete));
    }

    private IEnumerator PopOutRoutine(GameObject panel, RectTransform rect, CanvasGroup cg, Action onComplete)
    {
        float duration = 0.15f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(0f, -30f), t);
            cg.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }
        panel.SetActive(false);
        activePanelTransitions.Remove(panel);
        onComplete?.Invoke();
    }


    private GameObject CreateArcadeButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Button3DTheme theme, Action onClick, string label = null, int fontSize = 16, string iconName = null)
    {
        UITheme.ButtonStyle style = UITheme.GetButtonStyle(theme);
        Color bgColor = style.BgColor;
        Color labelColor = style.LabelColor;
        Color bevelColor = style.BevelColor;

        GameObject btnObj = CreatePanel(parent, name, bgColor, anchorMin, anchorMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        btnObj.AddComponent<ArcadePressEffect>();

        ColorBlock cb = btn.colors;
        cb.normalColor = bgColor;
        cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
        cb.pressedColor = bgColor * 0.80f;
        cb.fadeDuration = 0.04f;
        btn.colors = cb;

        // Borda sutil chanfrada 3D procedural
        CreatePanel(btnObj.transform, "BevelBorder", bevelColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        if (!string.IsNullOrEmpty(label))
        {
            Text txt = CreateText(btnObj.transform, "Label", label, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), Vector2.zero, Vector2.zero, fontSize, labelColor, TextAnchor.MiddleCenter, true);
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = Mathf.Max(18, fontSize - 6);
            txt.resizeTextMaxSize = Mathf.Max(fontSize + 6, 38);
        }
        return btnObj;
    }

    private GameObject CreateGlassPill(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color glowBorder)
    {
        GameObject pill = CreatePanel(parent, name, ColorBgDeepNavy, aMin, aMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(pill.transform, "GlowBorder", glowBorder * 0.40f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();
        return pill;
    }

    private GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, bool raycast, Sprite sprite = null)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.offsetMin = size == Vector2.zero ? Vector2.zero : -size * 0.5f;
        rect.offsetMax = size == Vector2.zero ? Vector2.zero : size * 0.5f;
        Image img = obj.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        return obj;
    }

    private Text CreateText(Transform parent, string name, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment, bool bold)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.offsetMin = size == Vector2.zero ? Vector2.zero : -size * 0.5f;
        rect.offsetMax = size == Vector2.zero ? Vector2.zero : size * 0.5f;
        Text text = textObj.AddComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        // Efeito refinado: apenas títulos grandes ganham contorno suave para evitar esmagar as letras de menus
        if (fontSize >= 28)
        {
            Outline outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.04f, 0.05f, 0.10f, 0.85f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }

    public static Sprite GetGradientPinkPurpleSprite() => UITheme.GetGradientPinkPurpleSprite();
    public static Sprite GetGreenBuySprite() => UITheme.GetGreenBuySprite();
    public static Sprite GetYellowDropSprite() => UITheme.GetYellowDropSprite();
    public static Sprite GetWhiteGhostSprite() => UITheme.GetWhiteGhostSprite();
    public static Sprite GetUISprite(string name, Vector4 border = default) => UITheme.GetUISprite(name, border);
    public static Sprite GetPlushiePortrait(string prizeName) => UITheme.GetPlushiePortrait(prizeName);
    public static Sprite GetRoundedRectSprite() => UITheme.GetRoundedRectSprite();
    public static Sprite GetCircleSprite() => UITheme.GetCircleSprite();
}
