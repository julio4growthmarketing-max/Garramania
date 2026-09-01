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

    // Paleta de Cores Oficial GarraMania & ClawCrazy
    public static readonly Color ColorBgDeepNavy   = new Color(0.08f, 0.09f, 0.18f, 0.95f);  // #14172E
    public static readonly Color ColorCardDark     = new Color(0.11f, 0.13f, 0.25f, 0.96f);  // #1C2140
    public static readonly Color ColorCardSlot     = new Color(0.15f, 0.18f, 0.32f, 0.90f);  // #262E52
    public static readonly Color ColorNeonGold     = new Color(1.00f, 0.85f, 0.12f, 1.00f);  // #FFD91F
    public static readonly Color ColorNeonCyan     = new Color(0.20f, 0.88f, 1.00f, 1.00f);  // #33E0FF
    public static readonly Color ColorNeonPink     = new Color(1.00f, 0.25f, 0.60f, 1.00f);  // #FF4099
    public static readonly Color ColorNeonPurple   = new Color(0.60f, 0.25f, 1.00f, 1.00f);  // #9940FF
    public static readonly Color ColorNeonGreen    = new Color(0.12f, 0.92f, 0.45f, 1.00f);  // #1FEB73
    public static readonly Color ColorNeonRed      = new Color(1.00f, 0.22f, 0.25f, 1.00f);  // #FF3840
    public static readonly Color ColorTextOutline  = new Color(0.04f, 0.05f, 0.12f, 0.98f);  // Contorno escuro pesado

    private static Sprite roundedRectSprite;
    private static Sprite circleSprite;
    private static Sprite gradientPinkPurpleSprite;
    private static Sprite yellowDropSprite;
    private static Sprite greenBuySprite;
    private static Sprite whiteGhostSprite;

    private static readonly Dictionary<string, Sprite> portraitCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite> uiSpriteCache = new Dictionary<string, Sprite>();

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
    private Text actionSubText;
    private Image goldenBtnBg;
    private Text goldenBtnText;

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
            if (spectatorCountText != null) spectatorCountText.text = $"{simulatedSpectators}";
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
        session.OnGameOver.AddListener(HandleGameOver);

        if (collection != null)
        {
            collection.OnCollectionUpdated.AddListener(UpdateAlbumBadges);
        }

        if (PlayerEconomyManager.Instance != null)
        {
            PlayerEconomyManager.Instance.OnEconomyUpdated.AddListener(UpdateDailyBadgeStatus);
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
            if (resultPanel != null) resultPanel.SetActive(false);
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
        if (creditsText != null) creditsText.text = $"{value}";
    }

    private void HandlePrizeDelivered(Prize prize, int total) { }

    private void HandlePrizeResult(Prize prize)
    {
        if (resultPanel == null) return;
        string stockId = prize != null ? (!string.IsNullOrEmpty(prize.StockId) ? prize.StockId : prize.prizeId) : "Fox";
        CaptureResult res = CollectionManager.Instance.RegisterCapture(stockId);

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
                    OpenDailyReward();
                });
            }
        };

        PopIn(gameOverPanel);
    }

    private void HandleClawStateChanged(bool closed)
    {
        if (actionText == null) return;
        actionText.text = closed ? "SOLTAR" : "AGARRAR";
        if (actionSubText != null) actionSubText.text = closed ? "LIBERAR" : "DESCER";
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
            OpenVipShop();
            return;
        }

        PlayerEconomyManager.Instance?.RegisterGameStarted();
        session.StartGame();
    }

    public void ContinueAfterResult()
    {
        PopOut(resultPanel, () => {
            if (session != null && session.Credits > 0)
            {
                session.SetState(GameState.Playing);
                if (controlsPanel != null) controlsPanel.SetActive(true);
                InputRouter.Instance?.SetBlocked(false);
            }
            else
            {
                session?.ResetSession();
                InputRouter.Instance?.SetBlocked(true);
            }
        });
    }

    public void OpenAlbum()
    {
        previousPanelBeforeModal = menuPanel != null && menuPanel.activeSelf ? menuPanel : hudPanel;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);

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
        BuildDailyRewardModal();
        PopIn(dailyRewardModal);
    }

    public void CloseDailyReward()
    {
        PopOut(dailyRewardModal);
        UpdateDailyBadgeStatus();
    }

    public void OpenSetsShowcase()
    {
        BuildSetsModal();
        PopIn(setsModal);
    }

    public void CloseSetsShowcase()
    {
        PopOut(setsModal);
    }

    public void OpenVipShop()
    {
        BuildVipShopModal();
        PopIn(vipShopModal);
    }

    public void CloseVipShop()
    {
        PopOut(vipShopModal);
    }

    private void UpdateAlbumBadges()
    {
        int unlocked = CollectionManager.Instance.GetUnlockedCount();
        int total = CollectionManager.Instance.GetTotalCount();
        string hudBadge = $"🏆 ({unlocked}/{total})";
        if (albumHudButtonText != null) albumHudButtonText.text = hudBadge;
        if (menuAlbumButtonText != null) menuAlbumButtonText.text = $"🏆 COLEÇÃO ({unlocked}/{total})";
    }

    private void UpdateDailyBadgeStatus()
    {
        bool available = PlayerEconomyManager.Instance != null && PlayerEconomyManager.Instance.IsDailyRewardAvailable();
        if (dailyRewardHudBadge != null) dailyRewardHudBadge.SetActive(available);
    }

    private void BuildInterface()
    {
        uiFont = Resources.Load<Font>("Fonts/LilitaOne-Regular")
              ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") 
              ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

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
        Vector2 backMax = isP ? new Vector2(0.15f, 0.985f) : new Vector2(0.08f, 0.988f);
        CreateArcadeButton(parent, "BackBtn", backMin, backMax, Button3DTheme.WhiteGhost, () => {
            if (session != null && session.CurrentState == GameState.Playing)
            {
                session.ResetSession();
                InputRouter.Instance?.SetBlocked(true);
            }
        }, "←", 22);

        Vector2 specMin = isP ? new Vector2(0.17f, 0.925f) : new Vector2(0.09f, 0.920f);
        Vector2 specMax = isP ? new Vector2(0.33f, 0.985f) : new Vector2(0.18f, 0.988f);
        GameObject specPill = CreateGlassPill(parent, "SpecPill", specMin, specMax, ColorNeonCyan);
        CreateText(specPill.transform, "SpecIcon", "👥", new Vector2(0.05f, 0.05f), new Vector2(0.40f, 0.95f), Vector2.zero, Vector2.zero, 18, Color.white, TextAnchor.MiddleCenter, false);
        spectatorCountText = CreateText(specPill.transform, "SpecCount", "2", new Vector2(0.40f, 0.05f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero, 17, Color.white, TextAnchor.MiddleCenter, true);

        Vector2 tokMin = isP ? new Vector2(0.35f, 0.925f) : new Vector2(0.20f, 0.920f);
        Vector2 tokMax = isP ? new Vector2(0.65f, 0.985f) : new Vector2(0.38f, 0.988f);
        GameObject tokPill = CreateGlassPill(parent, "TokenPill", tokMin, tokMax, ColorNeonGold);

        CreatePanel(tokPill.transform, "CoinIcon", Color.white, new Vector2(0.04f, 0.15f), new Vector2(0.26f, 0.85f), Vector2.zero, Vector2.zero, false, GetUISprite("icon_gold_coin")).GetComponent<Image>().preserveAspect = true;
        creditsText = CreateText(tokPill.transform, "TokenCount", "160", new Vector2(0.26f, 0.05f), new Vector2(0.72f, 0.95f), Vector2.zero, Vector2.zero, 18, ColorNeonGold, TextAnchor.MiddleCenter, true);

        CreateArcadeButton(tokPill.transform, "PlusBtn", new Vector2(0.74f, 0.10f), new Vector2(0.96f, 0.90f), Button3DTheme.PurplePink, OpenVipShop, "+", 18);

        Vector2 giftMin = isP ? new Vector2(0.67f, 0.925f) : new Vector2(0.78f, 0.920f);
        Vector2 giftMax = isP ? new Vector2(0.81f, 0.985f) : new Vector2(0.87f, 0.988f);
        GameObject giftBtn = CreateArcadeButton(parent, "DailyGiftBtn", giftMin, giftMax, Button3DTheme.PurplePink, OpenDailyReward, "🎁", 18);
        dailyRewardHudBadge = CreatePanel(giftBtn.transform, "AlertDot", ColorNeonRed, new Vector2(0.65f, 0.65f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero, false, GetCircleSprite());

        Vector2 albMin = isP ? new Vector2(0.83f, 0.925f) : new Vector2(0.89f, 0.920f);
        Vector2 albMax = isP ? new Vector2(0.97f, 0.985f) : new Vector2(0.98f, 0.988f);
        GameObject albBtn = CreateArcadeButton(parent, "AlbumHudBtn", albMin, albMax, Button3DTheme.PurplePink, OpenAlbum, "🏆", 18);
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

        // 2. BIG RED ARCADE PUSH-BUTTON (Sanwa 3D Cherry Red Dome na Direita)
        Vector2 actMin = isP ? new Vector2(0.67f, 0.035f) : new Vector2(0.82f, 0.035f);
        Vector2 actMax = isP ? new Vector2(0.97f, 0.185f) : new Vector2(0.97f, 0.235f);

        GameObject actionButtonObj = CreatePanel(parent, "ActionButton_Core", Color.white, actMin, actMax, Vector2.zero, Vector2.zero, true, GetUISprite("btn_sanwa_red_3d"));
        actionButtonCore = actionButtonObj.GetComponent<Image>();
        actionButton = actionButtonObj.AddComponent<Button>();
        actionButton.onClick.AddListener(() => InputRouter.Instance?.TriggerTouchAction());
        actionButtonObj.AddComponent<ArcadePressEffect>();

        ColorBlock actCb = actionButton.colors;
        actCb.normalColor = Color.white;
        actCb.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        actCb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        actCb.fadeDuration = 0.04f;
        actionButton.colors = actCb;

        actionText = CreateText(actionButtonObj.transform, "ActionText", "AGARRAR", new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.82f), Vector2.zero, Vector2.zero, isP ? 20 : 22, Color.white, TextAnchor.MiddleCenter, true);
        actionSubText = CreateText(actionButtonObj.transform, "ActionSubText", "DESCER", new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.42f), Vector2.zero, Vector2.zero, 12, new Color(1f, 1f, 1f, 0.90f), TextAnchor.MiddleCenter, false);

        // 3. Botão Alternador de Câmera (Topo Direito)
        Vector2 camMin = isP ? new Vector2(0.80f, 0.850f) : new Vector2(0.90f, 0.840f);
        Vector2 camMax = isP ? new Vector2(0.98f, 0.915f) : new Vector2(0.99f, 0.910f);
        CreateArcadeButton(parent, "CamToggleBtn", camMin, camMax, Button3DTheme.Sapphire, () => cameraController?.ToggleCameraAngle(), "ÂNGULO", 12, "icon_rotate_camera");

        // 4. Botão Ficha Dourada (Docked Acima do Botão Sanwa na Base)
        Vector2 goldMin = isP ? new Vector2(0.67f, 0.200f) : new Vector2(0.82f, 0.250f);
        Vector2 goldMax = isP ? new Vector2(0.97f, 0.250f) : new Vector2(0.97f, 0.300f);
        GameObject goldBtn = CreateArcadeButton(parent, "GoldenTokenBtn", goldMin, goldMax, Button3DTheme.Gold, () => {
            if (PlayerEconomyManager.Instance != null)
            {
                bool active = PlayerEconomyManager.Instance.ToggleGoldenClaw();
                if (actionButtonCore != null) actionButtonCore.color = active ? new Color(1f, 0.9f, 0.4f, 1f) : Color.white;
                if (goldenBtnText != null) goldenBtnText.text = active ? "100% ATIVO" : "GARRA 100%";
            }
        }, "GARRA 100%", 13, "icon_gold_coin");
        goldenBtnBg = goldBtn.GetComponent<Image>();
        goldenBtnText = goldBtn.GetComponentInChildren<Text>();

        // 5. Timer Centralizado
        Vector2 timeMin = isP ? new Vector2(0.38f, 0.045f) : new Vector2(0.44f, 0.040f);
        Vector2 timeMax = isP ? new Vector2(0.62f, 0.145f) : new Vector2(0.56f, 0.130f);
        GameObject timerPill = CreateGlassPill(parent, "TimerPill", timeMin, timeMax, ColorNeonCyan);
        timerFill = CreatePanel(timerPill.transform, "TimerFill", ColorNeonCyan, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.85f), Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).GetComponent<Image>();
        timerFill.type = Image.Type.Sliced;
        timerFillRect = timerFill.rectTransform;
        timerText = CreateText(timerPill.transform, "TimerText", "45s", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 20, Color.white, TextAnchor.MiddleCenter, true);
    }

    private void BuildMenu(Transform parent)
    {
        bool isP = Screen.width < Screen.height;

        Vector2 logoMin = isP ? new Vector2(0.06f, 0.84f) : new Vector2(0.30f, 0.86f);
        Vector2 logoMax = isP ? new Vector2(0.94f, 0.92f) : new Vector2(0.70f, 0.94f);
        GameObject logoGroup = CreatePanel(parent, "HeaderLogo", Color.clear, logoMin, logoMax, Vector2.zero, Vector2.zero, false);
        CreateText(logoGroup.transform, "LogoTitle", "GARRAMANIA", new Vector2(0f, 0.35f), Vector2.one, Vector2.zero, Vector2.zero, isP ? 42 : 46, ColorNeonGold, TextAnchor.MiddleCenter, true);
        CreateText(logoGroup.transform, "LogoSub", "FLIPERAMA & REAL CLAW MACHINE", Vector2.zero, new Vector2(1f, 0.38f), Vector2.zero, Vector2.zero, 14, ColorNeonCyan, TextAnchor.MiddleCenter, true);

        Vector2 sheetMin = isP ? new Vector2(0.04f, 0.02f) : new Vector2(0.24f, 0.02f);
        Vector2 sheetMax = isP ? new Vector2(0.96f, 0.40f) : new Vector2(0.76f, 0.44f);
        GameObject sheet = CreatePanel(parent, "MenuSheet", ColorCardDark, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetUISprite("card_dialog_blue", new Vector4(60, 60, 60, 60)));

        CreateArcadeButton(sheet.transform, "PlayBtn", new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.94f), Button3DTheme.PurplePink, StartGame, "JOGAR AGORA! 🕹️", isP ? 24 : 26);
        CreateArcadeButton(sheet.transform, "SetsBtn", new Vector2(0.06f, 0.37f), new Vector2(0.48f, 0.63f), Button3DTheme.Sapphire, OpenSetsShowcase, "TRIOS DA VITRINE 🎁", 14);
        CreateArcadeButton(sheet.transform, "DailyBtn", new Vector2(0.52f, 0.37f), new Vector2(0.94f, 0.63f), Button3DTheme.Gold, OpenDailyReward, "DIÁRIO (7 DIAS) 🪙", 14);
        CreateArcadeButton(sheet.transform, "VipBtn", new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.32f), Button3DTheme.Emerald, OpenVipShop, "SEJA VIP • FICHAS & BÔNUS 👑", 16);
    }

    private void BuildDailyRewardModal()
    {
        if (dailyRewardModal == null) return;
        foreach (Transform child in dailyRewardModal.transform) Destroy(child.gameObject);

        bool isP = Screen.width < Screen.height;
        Vector2 min = isP ? new Vector2(0.04f, 0.04f) : new Vector2(0.20f, 0.04f);
        Vector2 max = isP ? new Vector2(0.96f, 0.92f) : new Vector2(0.80f, 0.96f);

        GameObject win = CreatePanel(dailyRewardModal.transform, "DailyWindow", ColorBgDeepNavy, min, max, Vector2.zero, Vector2.zero, true, GetUISprite("card_dialog_blue", new Vector4(60, 60, 60, 60)));

        GameObject banner = CreatePanel(win.transform, "Banner", Color.white, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero, false, GetUISprite("banner_ribbon_orange", new Vector4(40, 20, 40, 20)));
        CreateText(banner.transform, "Title", "RECOMPENSA DIÁRIA!", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, isP ? 24 : 28, Color.white, TextAnchor.MiddleCenter, true);

        CreateText(win.transform, "Sub", "RESETA A CADA 24H! COLETE SEU PRÊMIO DIÁRIO!", new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.87f), Vector2.zero, Vector2.zero, 13, ColorNeonCyan, TextAnchor.MiddleCenter, true);

        int currentStreak = PlayerEconomyManager.Instance != null ? PlayerEconomyManager.Instance.CurrentStreakDay : 1;
        bool isReady = PlayerEconomyManager.Instance != null && PlayerEconomyManager.Instance.IsDailyRewardAvailable();

        int[] rewards = PlayerEconomyManager.DailyTokenRewards;

        float gridTop = 0.80f;
        float gridBottom = 0.28f;
        float cardH = (gridTop - gridBottom) / 3f;

        for (int i = 0; i < 6; i++)
        {
            int dayNum = i + 1;
            int col = i % 2;
            int row = i / 2;

            float xMin = col == 0 ? 0.06f : 0.52f;
            float xMax = col == 0 ? 0.48f : 0.94f;
            float yMax = gridTop - (row * cardH);
            float yMin = yMax - cardH + 0.015f;

            bool isToday = (dayNum == currentStreak);
            bool isClaimed = (dayNum < currentStreak) || (isToday && !isReady);

            Color cardBg = isToday && isReady ? ColorCardDark : ColorCardSlot;
            GameObject dayCard = CreatePanel(win.transform, $"Day_{dayNum}", cardBg, new Vector2(xMin, yMin), new Vector2(xMax, yMax), Vector2.zero, Vector2.zero, true, GetUISprite("slot_pedestal_3d", new Vector4(20, 20, 20, 20)));

            if (isToday && isReady)
            {
                CreatePanel(dayCard.transform, "GlowRim", ColorNeonCyan * 0.7f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite());
            }

            CreateText(dayCard.transform, "DayLabel", $"DIA {dayNum}", new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero, 16, isToday ? ColorNeonGold : Color.white, TextAnchor.MiddleCenter, true);
            CreateText(dayCard.transform, "PrizeLabel", $"🪙 {rewards[i]}", new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.65f), Vector2.zero, Vector2.zero, 18, ColorNeonGold, TextAnchor.MiddleCenter, true);

            string statusText = isClaimed ? "✓ COLETADO" : (isToday && isReady ? "RESGATAR!" : "BLOQUEADO");
            Color statusColor = isClaimed ? Color.gray : (isToday && isReady ? ColorNeonGreen : new Color(0.6f, 0.7f, 0.9f, 0.7f));
            CreateText(dayCard.transform, "Status", statusText, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.32f), Vector2.zero, Vector2.zero, 13, statusColor, TextAnchor.MiddleCenter, true);
        }

        bool isDay7 = (currentStreak == 7);
        bool isDay7Claimed = (currentStreak == 7 && !isReady);
        GameObject day7Card = CreatePanel(win.transform, "Day_7", isDay7 && isReady ? ColorCardDark : ColorCardSlot, new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.27f), Vector2.zero, Vector2.zero, true, GetUISprite("slot_pedestal_3d", new Vector4(20, 20, 20, 20)));
        CreateText(day7Card.transform, "Day7Label", "⭐ DIA 7 • GRANDE PRÊMIO ⭐", new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero, 17, ColorNeonGold, TextAnchor.MiddleCenter, true);
        CreateText(day7Card.transform, "Day7Prize", "🪙 25 FICHAS + 1 FICHA DOURADA (FORÇA 100%)", new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.55f), Vector2.zero, Vector2.zero, 14, Color.white, TextAnchor.MiddleCenter, true);

        if (isReady)
        {
            CreateArcadeButton(win.transform, "ClaimBtn", new Vector2(0.06f, 0.04f), new Vector2(0.60f, 0.13f), Button3DTheme.Emerald, () => {
                PlayerEconomyManager.Instance?.ClaimDailyReward();
                BuildDailyRewardModal();
            }, "RESGATAR RECOMPENSA! 🪙", 18);

            CreateArcadeButton(win.transform, "CloseBtn", new Vector2(0.64f, 0.04f), new Vector2(0.94f, 0.13f), Button3DTheme.WhiteGhost, CloseDailyReward, "FECHAR", 16);
        }
        else
        {
            TimeSpan rem = PlayerEconomyManager.Instance != null ? PlayerEconomyManager.Instance.GetTimeUntilNextDailyReward() : TimeSpan.Zero;
            string waitStr = rem > TimeSpan.Zero ? $"Próximo em: {rem.Hours:D2}h {rem.Minutes:D2}m" : "Recompensa já resgatada hoje!";

            CreateArcadeButton(win.transform, "CloseBtn", new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.13f), Button3DTheme.PurplePink, CloseDailyReward, $"VOLTAR ({waitStr})", 16);
        }
    }

    private void BuildSetsModal()
    {
        if (setsModal == null) return;
        foreach (Transform child in setsModal.transform) Destroy(child.gameObject);

        bool isP = Screen.width < Screen.height;
        Vector2 min = isP ? new Vector2(0.04f, 0.05f) : new Vector2(0.24f, 0.05f);
        Vector2 max = isP ? new Vector2(0.96f, 0.92f) : new Vector2(0.76f, 0.95f);

        GameObject win = CreatePanel(setsModal.transform, "SetsWindow", ColorBgDeepNavy, min, max, Vector2.zero, Vector2.zero, true, GetUISprite("card_dialog_blue", new Vector4(60, 60, 60, 60)));

        var sets = CollectionManager.Instance.GetAllSets();
        var currentSet = sets.Count > 0 ? sets[0] : null;

        if (currentSet != null)
        {
            float heroY = 0.58f;
            float heroW = 0.28f;

            for (int i = 0; i < currentSet.itemIds.Length; i++)
            {
                string id = currentSet.itemIds[i];
                var it = CollectionManager.Instance.GetItem(id);
                Sprite portrait = GetPlushiePortrait(id);

                float xCenter = 0.20f + (i * 0.30f);
                Vector2 pMin = new Vector2(xCenter - heroW * 0.5f, heroY);
                Vector2 pMax = new Vector2(xCenter + heroW * 0.5f, heroY + 0.28f);

                GameObject frame = CreatePanel(win.transform, $"Hero_{id}", ColorCardDark, pMin, pMax, Vector2.zero, Vector2.zero, false, GetCircleSprite());
                if (it != null && it.IsUnlocked)
                {
                    CreatePanel(frame.transform, "Border", ColorNeonGold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
                    GameObject img = CreatePanel(frame.transform, "Portrait", Color.white, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero, false, portrait);
                    CreateText(frame.transform, "Count", $"×{it.count}", new Vector2(0.55f, 0.75f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero, 13, ColorNeonGold, TextAnchor.MiddleCenter, true);
                }
                else
                {
                    GameObject img = CreatePanel(frame.transform, "Portrait", new Color(0.1f, 0.15f, 0.25f, 0.85f), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero, false, portrait);
                    CreateText(frame.transform, "Lock", "?", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 28, Color.gray, TextAnchor.MiddleCenter, true);
                }
            }

            int prog = CollectionManager.Instance.GetSetProgress(currentSet);
            int total = currentSet.itemIds.Length;

            CreateText(win.transform, "SetTitle", $"COLECIONE OS {total}!", new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.54f), Vector2.zero, Vector2.zero, isP ? 28 : 32, Color.white, TextAnchor.MiddleCenter, true);
            CreateText(win.transform, "SetSub", $"{currentSet.title}\n{currentSet.subtitle}", new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.44f), Vector2.zero, Vector2.zero, 15, ColorNeonCyan, TextAnchor.MiddleCenter, true);

            CreateText(win.transform, "SetReward", $"🎁 BÔNUS DE CONCLUSÃO: +{currentSet.rewardTokens} FICHAS\nProgresso: {prog}/{total} Capturados", new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.32f), Vector2.zero, Vector2.zero, 14, ColorNeonGold, TextAnchor.MiddleCenter, true);

            if (CollectionManager.Instance.IsSetComplete(currentSet) && !currentSet.hasClaimedSetReward)
            {
                CreateArcadeButton(win.transform, "ClaimSetBtn", new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.22f), Button3DTheme.Emerald, () => {
                    CollectionManager.Instance.ClaimSetReward(currentSet);
                    BuildSetsModal();
                }, "RESGATAR BÔNUS DO TRIO! 🎁", 18);
            }
            else
            {
                CreateArcadeButton(win.transform, "PlaySetBtn", new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.22f), Button3DTheme.PurplePink, () => {
                    CloseSetsShowcase();
                    StartGame();
                }, "JOGAR AGORA! 🕹️", 20);
            }

            CreateArcadeButton(win.transform, "DismissBtn", new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.10f), Button3DTheme.WhiteGhost, CloseSetsShowcase, "FECHAR", 16);
        }
    }

    private void BuildVipShopModal()
    {
        if (vipShopModal == null) return;
        foreach (Transform child in vipShopModal.transform) Destroy(child.gameObject);

        bool isP = Screen.width < Screen.height;
        Vector2 min = isP ? new Vector2(0.04f, 0.04f) : new Vector2(0.24f, 0.04f);
        Vector2 max = isP ? new Vector2(0.96f, 0.92f) : new Vector2(0.76f, 0.95f);

        GameObject win = CreatePanel(vipShopModal.transform, "VipWindow", ColorBgDeepNavy, min, max, Vector2.zero, Vector2.zero, true, GetUISprite("card_dialog_blue", new Vector4(60, 60, 60, 60)));

        GameObject banner = CreatePanel(win.transform, "Banner", Color.white, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero, false, GetUISprite("banner_ribbon_orange", new Vector4(40, 20, 40, 20)));
        CreateText(banner.transform, "Title", "DESBLOQUEAR TUDO", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, isP ? 24 : 28, Color.white, TextAnchor.MiddleCenter, true);

        CreateText(win.transform, "Sub", "SEJA VIP • ACESSO EXCLUSIVO, FICHAS BÔNUS & FRETE GRÁTIS!", new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.87f), Vector2.zero, Vector2.zero, 13, ColorNeonCyan, TextAnchor.MiddleCenter, true);

        GameObject perks = CreatePanel(win.transform, "Perks", ColorCardDark, new Vector2(0.06f, 0.38f), new Vector2(0.94f, 0.78f), Vector2.zero, Vector2.zero, true, GetUISprite("slot_pedestal_3d", new Vector4(20, 20, 20, 20)));

        CreateText(perks.transform, "P1", "🔓 TODAS AS MÁQUINAS LIBERADAS", new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero, 17, Color.white, TextAnchor.MiddleLeft, true);
        CreateText(perks.transform, "P2", "🪙 125 FICHAS + 3 FICHAS DOURADAS (100% GRIP)", new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.65f), Vector2.zero, Vector2.zero, 17, ColorNeonGold, TextAnchor.MiddleLeft, true);
        CreateText(perks.transform, "P3", "🚚 FRETE GRÁTIS POR 14 DIAS", new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.35f), Vector2.zero, Vector2.zero, 17, ColorNeonGreen, TextAnchor.MiddleLeft, true);

        GameObject packBtn1 = CreateArcadeButton(win.transform, "Pack1", new Vector2(0.06f, 0.25f), new Vector2(0.48f, 0.35f), Button3DTheme.Sapphire, () => {
            PlayerEconomyManager.Instance?.PurchaseTokenPack(40);
            BuildVipShopModal();
        }, "40 FICHAS • R$ 4,90", 13);

        GameObject packBtn2 = CreateArcadeButton(win.transform, "Pack2", new Vector2(0.52f, 0.25f), new Vector2(0.94f, 0.35f), Button3DTheme.Gold, () => {
            PlayerEconomyManager.Instance?.PurchaseTokenPack(100, 1);
            BuildVipShopModal();
        }, "100 FICHAS + 1 ⭐ • R$ 9,90", 13);

        CreateArcadeButton(win.transform, "BuyVipBtn", new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.23f), Button3DTheme.Emerald, () => {
            PlayerEconomyManager.Instance?.PurchaseVIP();
            BuildVipShopModal();
        }, "ASSINAR VIP • R$ 24,90", 22);

        CreateArcadeButton(win.transform, "CloseBtn", new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.10f), Button3DTheme.WhiteGhost, CloseVipShop, "FECHAR", 16);
    }

    private void BuildResult(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 sheetMin = isP ? new Vector2(0.04f, 0.02f) : new Vector2(0.24f, 0.02f);
        Vector2 sheetMax = isP ? new Vector2(0.96f, 0.46f) : new Vector2(0.76f, 0.52f);

        GameObject sheet = CreatePanel(parent, "ResultSheet", ColorCardDark, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetUISprite("card_dialog_blue", new Vector4(60, 60, 60, 60)));

        GameObject bannerObj = CreatePanel(sheet.transform, "CelebrationBanner", Color.white, new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.98f), Vector2.zero, Vector2.zero, false, GetUISprite("banner_ribbon_orange", new Vector4(40, 20, 40, 20)));
        resultTitleText = CreateText(bannerObj.transform, "Title", "🎉 PRÊMIO CAPTURADO!", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 22, Color.white, TextAnchor.MiddleCenter, true);

        GameObject portraitContainer = CreatePanel(sheet.transform, "PortraitContainer", Color.clear, new Vector2(0.06f, 0.32f), new Vector2(0.38f, 0.80f), Vector2.zero, Vector2.zero, false);
        GameObject portraitImgObj = CreatePanel(portraitContainer.transform, "PortraitImg", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        resultPortraitImage = portraitImgObj.GetComponent<Image>();
        if (resultPortraitImage != null) resultPortraitImage.preserveAspect = true;

        resultNameText = CreateText(sheet.transform, "PrizeName", "RAPOSA ASTUTA", new Vector2(0.40f, 0.60f), new Vector2(0.95f, 0.78f), Vector2.zero, Vector2.zero, 24, Color.white, TextAnchor.MiddleLeft, true);
        resultBadgeText = CreateText(sheet.transform, "Badge", "★ COMUM ★", new Vector2(0.40f, 0.44f), new Vector2(0.95f, 0.60f), Vector2.zero, Vector2.zero, 16, ColorNeonCyan, TextAnchor.MiddleLeft, true);
        resultMessageText = CreateText(sheet.transform, "Message", "Adicionado ao seu álbum de coleção!", new Vector2(0.08f, 0.22f), new Vector2(0.95f, 0.36f), Vector2.zero, Vector2.zero, 13, new Color(0.9f, 0.95f, 1f, 0.9f), TextAnchor.MiddleCenter, false);

        CreateArcadeButton(sheet.transform, "ContinueBtn", new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.20f), Button3DTheme.PurplePink, ContinueAfterResult, "CONTINUAR JOGANDO ▶", 20);

        parent.gameObject.SetActive(false);
    }

    private void BuildGameOver(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 sheetMin = isP ? new Vector2(0.04f, 0.02f) : new Vector2(0.25f, 0.02f);
        Vector2 sheetMax = isP ? new Vector2(0.96f, 0.34f) : new Vector2(0.75f, 0.38f);

        GameObject sheet = CreatePanel(parent, "GameOverSheet", ColorCardDark, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetUISprite("card_dialog_blue", new Vector4(60, 60, 60, 60)));

        gameOverTitleText = CreateText(sheet.transform, "Title", "FIM DA JOGADA", new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.90f), Vector2.zero, Vector2.zero, 26, Color.white, TextAnchor.MiddleCenter, true);
        gameOverMessageText = CreateText(sheet.transform, "Msg", "Você ainda tem fichas restantes!", new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.66f), Vector2.zero, Vector2.zero, 15, ColorNeonCyan, TextAnchor.MiddleCenter, false);

        GameObject btn = CreateArcadeButton(sheet.transform, "GameOverBtn", new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.38f), Button3DTheme.PurplePink, () => gameOverAction?.Invoke(), "JOGAR NOVAMENTE", 20, "icon_gold_coin");
        gameOverButtonText = btn.GetComponentInChildren<Text>();

        parent.gameObject.SetActive(false);
    }

    private void BuildAlbumContainer(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 sheetMin = isP ? new Vector2(0.04f, 0.02f) : new Vector2(0.18f, 0.02f);
        Vector2 sheetMax = isP ? new Vector2(0.96f, 0.88f) : new Vector2(0.82f, 0.94f);

        GameObject window = CreatePanel(parent, "AlbumWindow", ColorBgDeepNavy, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetUISprite("card_dialog_blue", new Vector4(60, 60, 60, 60)));

        CreateText(window.transform, "Header", "🏆 ÁLBUM DE COLEÇÃO", new Vector2(0.05f, 0.915f), new Vector2(0.95f, 0.985f), Vector2.zero, Vector2.zero, isP ? 28 : 32, ColorNeonGold, TextAnchor.MiddleCenter, true);
        albumProgressText = CreateText(window.transform, "ProgressPill", "COLEÇÃO: 0 / 6 DESBLOQUEADOS (0%)", new Vector2(0.05f, 0.855f), new Vector2(0.95f, 0.910f), Vector2.zero, Vector2.zero, 15, ColorNeonCyan, TextAnchor.MiddleCenter, true);

        GameObject gridObj = CreatePanel(window.transform, "GridContainer", Color.clear, new Vector2(0.04f, 0.11f), new Vector2(0.96f, 0.84f), Vector2.zero, Vector2.zero, false);
        albumGridContainer = gridObj.transform;

        CreateArcadeButton(window.transform, "CloseAlbumBtn", new Vector2(0.15f, 0.020f), new Vector2(0.85f, 0.095f), Button3DTheme.WhiteGhost, CloseAlbum, "VOLTAR À MÁQUINA", 18);

        BuildInspectModal(parent);
    }

    private void BuildAlbumGrid()
    {
        if (albumGridContainer == null) return;
        foreach (Transform child in albumGridContainer) Destroy(child.gameObject);

        bool isP = Screen.width < Screen.height;
        var allItems = CollectionManager.Instance.GetAllItems();
        int unlocked = CollectionManager.Instance.GetUnlockedCount();
        int total = CollectionManager.Instance.GetTotalCount();

        if (albumProgressText != null)
        {
            int pct = Mathf.RoundToInt((float)unlocked / total * 100f);
            albumProgressText.text = $"COLEÇÃO: {unlocked} / {total} DESBLOQUEADOS ({pct}%)";
        }

        int cols = isP ? 2 : 3;
        int rows = isP ? 3 : 2;

        for (int i = 0; i < allItems.Count; i++)
        {
            var item = allItems[i];
            int c = i % cols;
            int r = rows - 1 - (i / cols);

            float cellW = 1.0f / cols;
            float cellH = 1.0f / rows;
            Vector2 aMin = new Vector2(c * cellW + 0.02f, r * cellH + 0.02f);
            Vector2 aMax = new Vector2((c + 1) * cellW - 0.02f, (r + 1) * cellH - 0.02f);

            Color rarityColor = item.themeColor;

            GameObject card = CreatePanel(albumGridContainer, $"Slot_{item.id}", ColorCardSlot, aMin, aMax, Vector2.zero, Vector2.zero, true, GetUISprite("slot_pedestal_3d", new Vector4(20, 20, 20, 20)));

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

                GameObject countPill = CreateGlassPill(card.transform, "CountPill", new Vector2(0.60f, 0.78f), new Vector2(0.96f, 0.95f), ColorNeonGold);
                CreateText(countPill.transform, "CountText", $"×{item.count}", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13, ColorNeonGold, TextAnchor.MiddleCenter, true);

                CreateText(card.transform, "Name", item.displayName.ToUpperInvariant(), new Vector2(0.04f, 0.20f), new Vector2(0.96f, 0.40f), Vector2.zero, Vector2.zero, 16, Color.white, TextAnchor.MiddleCenter, true);

                string tag = item.rarity == PrizeRarity.Rare ? "★ RARO" : item.rarity == PrizeRarity.Uncommon ? "★ INCOMUM" : "COMUM";
                CreateText(card.transform, "RarityTag", tag, new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.20f), Vector2.zero, Vector2.zero, 14, rarityColor, TextAnchor.MiddleCenter, true);
            }
            else
            {
                GameObject cardMystery = CreatePanel(card.transform, "MysteryCard", Color.white, new Vector2(0.12f, 0.30f), new Vector2(0.88f, 0.92f), Vector2.zero, Vector2.zero, false, GetUISprite("card_mystery_rainbow"));
                cardMystery.GetComponent<Image>().preserveAspect = true;

                CreateText(card.transform, "Name", "???", new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.28f), Vector2.zero, Vector2.zero, 15, new Color(0.85f, 0.9f, 1f, 0.95f), TextAnchor.MiddleCenter, true);
                CreateText(card.transform, "Hint", "BLOQUEADO", new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.16f), Vector2.zero, Vector2.zero, 13, new Color(0.6f, 0.7f, 0.85f, 0.8f), TextAnchor.MiddleCenter, false);
            }
        }
    }

    private void BuildInspectModal(Transform parent)
    {
        inspectModal = CreatePanel(parent, "InspectModal", new Color(0.01f, 0.02f, 0.04f, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
        inspectModal.SetActive(false);

        bool isP = Screen.width < Screen.height;
        Vector2 min = isP ? new Vector2(0.08f, 0.22f) : new Vector2(0.28f, 0.18f);
        Vector2 max = isP ? new Vector2(0.92f, 0.78f) : new Vector2(0.72f, 0.82f);

        GameObject card = CreatePanel(inspectModal.transform, "InspectCard", ColorBgDeepNavy, min, max, Vector2.zero, Vector2.zero, true, GetUISprite("card_dialog_blue", new Vector4(60, 60, 60, 60)));

        GameObject imgFrame = CreatePanel(card.transform, "InspectFrame", ColorCardDark, new Vector2(0.35f, 0.64f), new Vector2(0.65f, 0.92f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
        CreatePanel(imgFrame.transform, "FrameBorder", ColorNeonGold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
        GameObject imgObj = CreatePanel(imgFrame.transform, "InspectImg", Color.white, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
        inspectPortraitImage = imgObj.GetComponent<Image>();

        inspectNameText = CreateText(card.transform, "Name", "RAPOSA ASTUTA", new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.64f), Vector2.zero, Vector2.zero, 24, Color.white, TextAnchor.MiddleCenter, true);
        inspectRarityText = CreateText(card.transform, "Rarity", "★ COMUM ★", new Vector2(0.05f, 0.46f), new Vector2(0.95f, 0.54f), Vector2.zero, Vector2.zero, 16, ColorNeonCyan, TextAnchor.MiddleCenter, true);
        inspectLoreText = CreateText(card.transform, "Lore", "Descrição da pelúcia...", new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.46f), Vector2.zero, Vector2.zero, 14, new Color(0.9f, 0.95f, 1f, 0.90f), TextAnchor.MiddleCenter, false);
        inspectStatsText = CreateText(card.transform, "Stats", "Capturas: 0", new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.28f), Vector2.zero, Vector2.zero, 14, ColorNeonGold, TextAnchor.MiddleCenter, false);

        CreateArcadeButton(card.transform, "CloseInspectBtn", new Vector2(0.18f, 0.04f), new Vector2(0.82f, 0.16f), Button3DTheme.WhiteGhost, () => inspectModal.SetActive(false), "FECHAR", 16);
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

    public static Sprite GetPlushiePortrait(string id)
    {
        if (string.IsNullOrEmpty(id)) id = "fox";
        string key = id.ToLowerInvariant();
        if (key.Contains("fox")) key = "fox";
        else if (key.Contains("green") || key.Contains("bear")) key = "greenbear";
        else if (key.Contains("fish") || key.Contains("balloon")) key = "balloonfish";
        else if (key.Contains("koala")) key = "koala";
        else if (key.Contains("badger")) key = "badger";
        else if (key.Contains("pork") || key.Contains("pig")) key = "porky";
        else key = "fox";

        if (portraitCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        Texture2D tex = Resources.Load<Texture2D>($"Textures/Portraits/portrait_{key}");
        if (tex != null)
        {
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            portraitCache[key] = sp;
            return sp;
        }
        return null;
    }

    private void PopIn(GameObject panel)
    {
        if (panel == null) return;
        panel.SetActive(true);
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        StartCoroutine(PopInRoutine(panel.GetComponent<RectTransform>(), cg));
    }

    private IEnumerator PopInRoutine(RectTransform rect, CanvasGroup cg)
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
    }

    private void PopOut(GameObject panel, Action onComplete = null)
    {
        if (panel == null) return;
        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null) cg = panel.AddComponent<CanvasGroup>();
        StartCoroutine(PopOutRoutine(panel, panel.GetComponent<RectTransform>(), cg, onComplete));
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
        onComplete?.Invoke();
    }

    public static Sprite GetUISprite(string name, Vector4 border = default)
    {
        if (uiSpriteCache.TryGetValue(name, out Sprite cached) && cached != null) return cached;
        Sprite sp = Resources.Load<Sprite>($"KitUI/{name}");
        if (sp == null) sp = Resources.Load<Sprite>($"UI/{name}");
        if (sp != null)
        {
            uiSpriteCache[name] = sp;
            return sp;
        }
        return GetRoundedRectSprite();
    }

    private GameObject CreateArcadeButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Button3DTheme theme, Action onClick, string label = null, int fontSize = 16, string iconName = null)
    {
        Sprite btnSprite;
        Color labelColor = Color.white;
        Color outlineColor = ColorTextOutline;

        switch (theme)
        {
            case Button3DTheme.Emerald:
                btnSprite = GetGreenBuySprite();
                labelColor = Color.white;
                break;
            case Button3DTheme.Gold:
                btnSprite = GetUISprite("btn_candy_gold", new Vector4(70, 35, 70, 28));
                labelColor = Color.white;
                break;
            case Button3DTheme.SanwaRed:
                btnSprite = GetUISprite("btn_candy_red", new Vector4(70, 35, 70, 28));
                labelColor = Color.white;
                break;
            case Button3DTheme.PurplePink:
                btnSprite = GetGradientPinkPurpleSprite();
                labelColor = Color.white;
                break;
            case Button3DTheme.YellowDrop:
                btnSprite = GetYellowDropSprite();
                labelColor = new Color(0.08f, 0.09f, 0.18f, 1f);
                outlineColor = Color.clear;
                break;
            case Button3DTheme.WhiteGhost:
                btnSprite = GetWhiteGhostSprite();
                labelColor = Color.white;
                break;
            case Button3DTheme.Sapphire:
            default:
                btnSprite = GetUISprite("btn_candy_cyan", new Vector4(70, 35, 70, 28));
                labelColor = Color.white;
                break;
        }

        GameObject btnObj = CreatePanel(parent, name, Color.white, anchorMin, anchorMax, Vector2.zero, Vector2.zero, true, btnSprite);
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        btnObj.AddComponent<ArcadePressEffect>();

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        cb.fadeDuration = 0.04f;
        btn.colors = cb;

        if (!string.IsNullOrEmpty(iconName))
        {
            Sprite icSp = GetUISprite(iconName);
            if (icSp != null)
            {
                GameObject icObj = CreatePanel(btnObj.transform, "Icon", Color.white, new Vector2(0.06f, 0.16f), new Vector2(0.28f, 0.84f), Vector2.zero, Vector2.zero, false, icSp);
                icObj.GetComponent<Image>().preserveAspect = true;
                if (!string.IsNullOrEmpty(label))
                {
                    Text txt = CreateText(btnObj.transform, "Label", label, new Vector2(0.28f, 0.08f), new Vector2(0.96f, 0.92f), Vector2.zero, Vector2.zero, fontSize, labelColor, TextAnchor.MiddleCenter, true);
                    txt.resizeTextForBestFit = true;
                    txt.resizeTextMinSize = 11;
                    txt.resizeTextMaxSize = Mathf.Max(fontSize + 6, 26);
                }
                return btnObj;
            }
        }
        if (!string.IsNullOrEmpty(label))
        {
            Text txt = CreateText(btnObj.transform, "Label", label, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f), Vector2.zero, Vector2.zero, fontSize, labelColor, TextAnchor.MiddleCenter, true);
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 12;
            txt.resizeTextMaxSize = Mathf.Max(fontSize + 8, 32);
        }
        return btnObj;
    }

    private GameObject CreateGlassPill(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color glowBorder)
    {
        return CreatePanel(parent, name, ColorCardDark, aMin, aMax, Vector2.zero, Vector2.zero, true, GetUISprite("slot_pedestal_3d", new Vector4(20, 20, 20, 20)));
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

        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = ColorTextOutline;
        outline.effectDistance = new Vector2(2.2f, -2.2f);

        Shadow shadow = textObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(2.5f, -2.5f);
        return text;
    }

    public static Sprite GetGradientPinkPurpleSprite()
    {
        if (gradientPinkPurpleSprite != null) return gradientPinkPurpleSprite;
        int w = 64; int h = 64; int r = 18;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] cols = new Color[w * h];
        Color pink = new Color(1.0f, 0.25f, 0.62f, 1f); Color purple = new Color(0.55f, 0.25f, 0.98f, 1f);
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color rowCol = Color.Lerp(purple, pink, t);
            for (int x = 0; x < w; x++)
            {
                int dx = Mathf.Min(x, w - 1 - x); int dy = Mathf.Min(y, h - 1 - y);
                if (dx < r && dy < r)
                {
                    float dist = Vector2.Distance(new Vector2(dx, dy), new Vector2(r, r));
                    float alpha = Mathf.Clamp01(r - dist + 0.5f);
                    cols[y * w + x] = new Color(rowCol.r, rowCol.g, rowCol.b, alpha);
                }
                else cols[y * w + x] = rowCol;
            }
        }
        tex.SetPixels(cols); tex.Apply();
        gradientPinkPurpleSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return gradientPinkPurpleSprite;
    }

    public static Sprite GetGreenBuySprite()
    {
        if (greenBuySprite != null) return greenBuySprite;
        int w = 64; int h = 64; int r = 18;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] cols = new Color[w * h];
        Color greenTop = new Color(0.15f, 0.95f, 0.48f, 1f); Color greenBot = new Color(0.08f, 0.75f, 0.35f, 1f);
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color rowCol = Color.Lerp(greenBot, greenTop, t);
            for (int x = 0; x < w; x++)
            {
                int dx = Mathf.Min(x, w - 1 - x); int dy = Mathf.Min(y, h - 1 - y);
                if (dx < r && dy < r)
                {
                    float dist = Vector2.Distance(new Vector2(dx, dy), new Vector2(r, r));
                    float alpha = Mathf.Clamp01(r - dist + 0.5f);
                    cols[y * w + x] = new Color(rowCol.r, rowCol.g, rowCol.b, alpha);
                }
                else cols[y * w + x] = rowCol;
            }
        }
        tex.SetPixels(cols); tex.Apply();
        greenBuySprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return greenBuySprite;
    }

    public static Sprite GetYellowDropSprite()
    {
        if (yellowDropSprite != null) return yellowDropSprite;
        int size = 96; float r = size * 0.5f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] cols = new Color[size * size];
        Vector2 center = new Vector2(r - 0.5f, r - 0.5f);
        Color goldTop = new Color(1.0f, 0.92f, 0.20f, 1f); Color goldBot = new Color(0.98f, 0.75f, 0.05f, 1f);
        for (int y = 0; y < size; y++)
        {
            float t = (float)y / (size - 1);
            Color baseCol = Color.Lerp(goldBot, goldTop, t);
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(r - dist + 0.5f);
                cols[y * size + x] = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);
            }
        }
        tex.SetPixels(cols); tex.Apply();
        yellowDropSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return yellowDropSprite;
    }

    public static Sprite GetWhiteGhostSprite()
    {
        if (whiteGhostSprite != null) return whiteGhostSprite;
        int size = 64; float r = size * 0.5f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] cols = new Color[size * size];
        Vector2 center = new Vector2(r - 0.5f, r - 0.5f);
        Color ghost = new Color(0.92f, 0.94f, 1.0f, 0.95f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(r - dist + 0.5f) * 0.95f;
                cols[y * size + x] = new Color(ghost.r, ghost.g, ghost.b, alpha);
            }
        }
        tex.SetPixels(cols); tex.Apply();
        whiteGhostSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return whiteGhostSprite;
    }

    public static Sprite GetRoundedRectSprite()
    {
        if (roundedRectSprite != null) return roundedRectSprite;
        int size = 32; int r = 10;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] cols = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = Mathf.Min(x, size - 1 - x); int dy = Mathf.Min(y, size - 1 - y);
                if (dx < r && dy < r)
                {
                    float dist = Vector2.Distance(new Vector2(dx, dy), new Vector2(r, r));
                    float alpha = Mathf.Clamp01(r - dist + 0.5f);
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else cols[y * size + x] = Color.white;
            }
        }
        tex.SetPixels(cols); tex.Apply();
        roundedRectSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return roundedRectSprite;
    }

    public static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        int size = 64; float r = size * 0.5f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] cols = new Color[size * size];
        Vector2 center = new Vector2(r - 0.5f, r - 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(r - dist + 0.5f);
                cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(cols); tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }
}
