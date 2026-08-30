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
            rect.localScale = Vector3.one * 0.965f;
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

/// <summary>
/// UI Arcade Mobile Adaptativa do GarraMania.
/// Princípio de Design: A máquina é o app. A UI é uma folha inferior (Bottom Sheet) translúcida,
/// mantendo a cabine 3D sempre viva e visível.
/// Inclui retratos ilustrados dos 6 bichinhos, Safe Area para iPhone, e botões táteis com chanfro 3D.
/// </summary>
public sealed class ProfessionalUIController : MonoBehaviour
{
    // Paleta de Cores Oficial GarraMania 2026
    public static readonly Color EmeraldPrimary = new Color(0.06f, 0.73f, 0.51f, 1f);     // #10B981 - Ação Primária
    public static readonly Color EmeraldPressed = new Color(0.02f, 0.59f, 0.41f, 1f);     // #059669
    public static readonly Color SapphireDark   = new Color(0.04f, 0.07f, 0.16f, 0.95f);    // #0B132B - Vidro Escuro
    public static readonly Color SapphireSheet  = new Color(0.05f, 0.08f, 0.17f, 0.96f);    // Fundo do Bottom Sheet
    public static readonly Color NeonCyan       = new Color(0.22f, 0.74f, 0.97f, 1f);     // #38BDF8 - Ciano Suave
    public static readonly Color NeonGold       = new Color(0.96f, 0.62f, 0.07f, 1f);     // #F59E0B - Ouro 24k
    public static readonly Color NeonMagenta    = new Color(0.96f, 0.15f, 0.45f, 1f);     // #F43F5E - Rosa Arcade
    public static readonly Color NeonRed        = new Color(0.94f, 0.27f, 0.24f, 1f);     // Alerta / Tempo Esgotando
    public static readonly Color GlassOutline   = new Color(0.35f, 0.55f, 0.85f, 0.25f);   // Borda sutil de vidro

    private static Sprite roundedRectSprite;
    private static Sprite circleSprite;
    private static readonly Dictionary<string, Sprite> portraitCache = new Dictionary<string, Sprite>();

    private GameObject canvasRoot;
    private GameObject menuPanel;
    private GameObject hudPanel;
    private GameObject controlsPanel;
    private GameObject resultPanel;
    private GameObject gameOverPanel;
    private GameObject albumPanel;
    private GameObject inspectModal;

    // Elementos de HUD
    private Text creditsText;
    private Text timerText;
    private Image timerFill;
    private RectTransform timerFillRect;
    private Text albumHudButtonText;
    private Text menuAlbumButtonText;

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

    // Controles Físicos
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
    private GameObject previousPanelBeforeAlbum;

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
    }

    private Vector2Int lastScreenDim = Vector2Int.zero;
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
    }

    private void HandleStateChanged(GameState state)
    {
        bool playing = state == GameState.Playing;
        bool idle = state == GameState.Idle;
        bool gameOver = state == GameState.GameOver;

        if (idle)
        {
            PopIn(menuPanel);
            if (hudPanel != null) hudPanel.SetActive(false);
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
            timerText.color = NeonRed;
            if (timerFill != null) timerFill.color = NeonRed;
        }
        else if (remaining <= 20f)
        {
            timerText.color = NeonGold;
            if (timerFill != null) timerFill.color = NeonGold;
        }
        else
        {
            timerText.color = Color.white;
            if (timerFill != null) timerFill.color = NeonCyan;
        }
    }

    private void HandleCreditsChanged(int value)
    {
        if (creditsText != null) creditsText.text = $"🪙 {value} Fichas";
    }

    private void HandlePrizeDelivered(Prize prize, int total)
    {
        // Evento tratado no Result Modal
    }

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
                resultMessageText.color = NeonGold;
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
        if (hudPanel != null) hudPanel.SetActive(false);

        bool hasCredits = session != null && session.Credits > 0;
        if (gameOverTitleText != null) gameOverTitleText.text = "FIM DA JOGADA";
        if (gameOverMessageText != null)
        {
            gameOverMessageText.text = hasCredits
                ? $"Você ainda tem {session.Credits} ficha(s) restante(s)!"
                : "Fichas esgotadas!\nResgate fichas grátis ou tente novamente.";
        }

        if (gameOverButtonText != null)
        {
            gameOverButtonText.text = hasCredits ? "JOGAR NOVAMENTE 🪙" : "VOLTAR AO MENU";
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
                    session?.ResetSession();
                    InputRouter.Instance?.SetBlocked(true);
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
            actionButtonCore.color = isGold ? NeonGold : (closed ? new Color(1.0f, 0.55f, 0.08f, 1f) : NeonMagenta);
        }
    }

    public void StartGame()
    {
        if (Time.unscaledTime - lastStartRequestTime < 0.15f) return;
        lastStartRequestTime = Time.unscaledTime;
        if (session == null) session = GameSession.Instance;
        if (session == null) return;

        if (session.Credits <= 0) session.ResetCredits(3);
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
        previousPanelBeforeAlbum = menuPanel != null && menuPanel.activeSelf ? menuPanel : hudPanel;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);

        BuildAlbumGrid();
        PopIn(albumPanel);
    }

    public void CloseAlbum()
    {
        PopOut(albumPanel, () => {
            if (previousPanelBeforeAlbum == menuPanel)
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

    private void UpdateAlbumBadges()
    {
        int unlocked = CollectionManager.Instance.GetUnlockedCount();
        int total = CollectionManager.Instance.GetTotalCount();
        string hudBadge = $"🏆 Álbum ({unlocked}/{total})";
        if (albumHudButtonText != null) albumHudButtonText.text = hudBadge;
        if (menuAlbumButtonText != null) menuAlbumButtonText.text = $"🏆 ÁLBUM ({unlocked}/{total})";
    }

    // ==================== CONSTRUÇÃO DA INTERFACE UGUI ARCADE ====================
    private void BuildInterface()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") 
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

        // 1. HUD Superior (Em Jogo)
        hudPanel = CreatePanel(root, "HUD", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildHud(hudPanel.transform);

        // 2. Controles de Jogo (Joystick + Sanwa + Câmera + Ficha Dourada)
        controlsPanel = CreatePanel(root, "Controls", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildControls(controlsPanel.transform);

        // 3. Menu Inicial (Bottom Sheet)
        menuPanel = CreatePanel(root, "Menu", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildMenu(menuPanel.transform);

        // 4. Modal de Vitória / Prêmio Capturado (Bottom Sheet)
        resultPanel = CreatePanel(root, "Result", new Color(0.01f, 0.02f, 0.05f, 0.40f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildResult(resultPanel.transform);

        // 5. Modal de Fim de Jogada (Bottom Sheet)
        gameOverPanel = CreatePanel(root, "GameOver", new Color(0.01f, 0.02f, 0.05f, 0.45f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildGameOver(gameOverPanel.transform);

        // 6. Álbum de Coleção (Sheet 2x3 com Ilustrações)
        albumPanel = CreatePanel(root, "AlbumPanel", new Color(0.01f, 0.02f, 0.05f, 0.70f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildAlbumContainer(albumPanel.transform);
        albumPanel.SetActive(false);
    }

    // -------------------------------------------------------------
    // HUD: 3 Pílulas no Topo com Safe Area
    // -------------------------------------------------------------
    private void BuildHud(Transform parent)
    {
        bool isP = Screen.width < Screen.height;

        // Pílula 1: Fichas (Esquerda)
        Vector2 credMin = isP ? new Vector2(0.03f, 0.932f) : new Vector2(0.025f, 0.925f);
        Vector2 credMax = isP ? new Vector2(0.33f, 0.985f) : new Vector2(0.18f, 0.985f);
        GameObject credits = CreateGlassPill(parent, "CreditsPill", credMin, credMax, NeonCyan);
        creditsText = CreateText(credits.transform, "CreditsText", "🪙 3 Fichas", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 16, Color.white, TextAnchor.MiddleCenter, true);

        // Pílula 2: Timer Central
        Vector2 timeMin = isP ? new Vector2(0.37f, 0.924f) : new Vector2(0.44f, 0.920f);
        Vector2 timeMax = isP ? new Vector2(0.63f, 0.988f) : new Vector2(0.56f, 0.988f);
        GameObject timer = CreateGlassPill(parent, "TimerPill", timeMin, timeMax, NeonGold);
        timerFill = CreatePanel(timer.transform, "TimerFill", NeonCyan, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.85f), Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).GetComponent<Image>();
        timerFill.type = Image.Type.Sliced;
        timerFillRect = timerFill.rectTransform;
        timerText = CreateText(timer.transform, "TimerText", "45s", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 20, Color.white, TextAnchor.MiddleCenter, true);

        // Pílula 3: Botão Álbum no HUD (Direita)
        Vector2 albMin = isP ? new Vector2(0.67f, 0.932f) : new Vector2(0.82f, 0.925f);
        Vector2 albMax = isP ? new Vector2(0.97f, 0.985f) : new Vector2(0.975f, 0.985f);
        GameObject albumBtn = CreateArcadeButton(parent, "AlbumHudBtn", albMin, albMax, SapphireDark, NeonGold, OpenAlbum);
        albumHudButtonText = CreateText(albumBtn.transform, "AlbumHudBtnText", "🏆 Álbum (0/6)", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 15, NeonGold, TextAnchor.MiddleCenter, true);
    }

    // -------------------------------------------------------------
    // CONTROLES: Joystick Cilíndrico + Sanwa 3D + Toggle Câmera + Ouro
    // -------------------------------------------------------------
    private void BuildControls(Transform parent)
    {
        bool isP = Screen.width < Screen.height;

        // 1. Joystick Virtual (Círculo Translúcido Ergonômico)
        Vector2 joyMin = isP ? new Vector2(0.05f, 0.035f) : new Vector2(0.04f, 0.035f);
        Vector2 joyMax = isP ? new Vector2(0.35f, 0.185f) : new Vector2(0.18f, 0.235f);
        GameObject joystick = CreatePanel(parent, "VirtualJoystick", new Color(0.06f, 0.10f, 0.22f, 0.80f), joyMin, joyMax, Vector2.zero, Vector2.zero, true, GetCircleSprite());
        CreatePanel(joystick.transform, "JoyRim", NeonCyan * 0.35f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
        GameObject handle = CreatePanel(joystick.transform, "Handle", NeonCyan, new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f), Vector2.zero, Vector2.zero, true, GetCircleSprite());
        VirtualJoystickView joystickView = joystick.AddComponent<VirtualJoystickView>();
        joystickView.Configure(joystick.GetComponent<RectTransform>(), handle.GetComponent<RectTransform>(), isP ? 55f : 60f);

        // 2. BIG RED ARCADE PUSH-BUTTON (Sanwa 3D)
        Vector2 actMin = isP ? new Vector2(0.67f, 0.035f) : new Vector2(0.82f, 0.035f);
        Vector2 actMax = isP ? new Vector2(0.97f, 0.185f) : new Vector2(0.97f, 0.235f);

        GameObject actionRing = CreatePanel(parent, "ActionButton_Ring", new Color(0.12f, 0.16f, 0.26f, 0.95f), actMin, actMax, Vector2.zero, Vector2.zero, true, GetCircleSprite());
        GameObject actionButtonObj = CreatePanel(actionRing.transform, "ActionButton_Core", NeonMagenta, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero, true, GetCircleSprite());
        actionButtonCore = actionButtonObj.GetComponent<Image>();
        actionButton = actionButtonObj.AddComponent<Button>();
        actionButton.onClick.AddListener(() => InputRouter.Instance?.TriggerTouchAction());
        actionButtonObj.AddComponent<ArcadePressEffect>();

        actionText = CreateText(actionButtonObj.transform, "ActionText", "AGARRAR", new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.86f), Vector2.zero, Vector2.zero, isP ? 18 : 20, Color.white, TextAnchor.MiddleCenter, true);
        actionSubText = CreateText(actionButtonObj.transform, "ActionSubText", "DESCER", new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.44f), Vector2.zero, Vector2.zero, 11, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, false);

        // 3. Botão Alternador de Câmera (Ícone Circular 44pt no Canto Superior Direito - SEM TAPAR OS BICHOS)
        Vector2 camMin = isP ? new Vector2(0.84f, 0.850f) : new Vector2(0.92f, 0.840f);
        Vector2 camMax = isP ? new Vector2(0.97f, 0.915f) : new Vector2(0.98f, 0.910f);
        GameObject camBtn = CreateArcadeButton(parent, "CamToggleBtn", camMin, camMax, SapphireDark, NeonCyan, () => cameraController?.ToggleCameraAngle());
        CreateText(camBtn.transform, "CamIcon", "🎥 Ângulo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13, NeonCyan, TextAnchor.MiddleCenter, true);

        // 4. Botão Ficha Dourada (Docked Acima do Botão Sanwa na Base - NÃO Cobre os Bichos!)
        Vector2 goldMin = isP ? new Vector2(0.67f, 0.200f) : new Vector2(0.82f, 0.250f);
        Vector2 goldMax = isP ? new Vector2(0.97f, 0.245f) : new Vector2(0.97f, 0.295f);
        GameObject goldBtn = CreateArcadeButton(parent, "GoldenTokenBtn", goldMin, goldMax, SapphireDark, NeonGold, () => {
            if (PlayerEconomyManager.Instance != null)
            {
                bool active = PlayerEconomyManager.Instance.ToggleGoldenClaw();
                if (actionButtonCore != null) actionButtonCore.color = active ? NeonGold : NeonMagenta;
                if (actionText != null) actionText.text = active ? "AGARRAR ★" : "AGARRAR";
                if (goldenBtnBg != null) goldenBtnBg.color = active ? NeonGold : SapphireDark;
                if (goldenBtnText != null) goldenBtnText.color = active ? Color.black : NeonGold;
            }
        });
        goldenBtnBg = goldBtn.GetComponent<Image>();
        goldenBtnText = CreateText(goldBtn.transform, "GoldLabel", "★ FORÇA 100%", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13, NeonGold, TextAnchor.MiddleCenter, true);
    }

    // -------------------------------------------------------------
    // MENU INICIAL: Bottom Sheet Compacto (34% da tela)
    // -------------------------------------------------------------
    private void BuildMenu(Transform parent)
    {
        bool isP = Screen.width < Screen.height;

        // Logo Flutuante no Topo
        Vector2 logoMin = isP ? new Vector2(0.08f, 0.86f) : new Vector2(0.30f, 0.88f);
        Vector2 logoMax = isP ? new Vector2(0.92f, 0.96f) : new Vector2(0.70f, 0.98f);
        GameObject logoGroup = CreatePanel(parent, "HeaderLogo", Color.clear, logoMin, logoMax, Vector2.zero, Vector2.zero, false);
        CreateText(logoGroup.transform, "LogoTitle", "GARRAMANIA", new Vector2(0f, 0.38f), Vector2.one, Vector2.zero, Vector2.zero, isP ? 34 : 38, NeonGold, TextAnchor.MiddleCenter, true);
        CreateText(logoGroup.transform, "LogoSub", "FLIPERAMA • CLAW ARCADE", Vector2.zero, new Vector2(1f, 0.40f), Vector2.zero, Vector2.zero, 12, NeonCyan, TextAnchor.MiddleCenter, true);

        // Bottom Sheet (34% da altura da tela na base)
        Vector2 sheetMin = isP ? new Vector2(0.04f, 0.02f) : new Vector2(0.25f, 0.02f);
        Vector2 sheetMax = isP ? new Vector2(0.96f, 0.36f) : new Vector2(0.75f, 0.42f);

        GameObject sheet = CreatePanel(parent, "MenuSheet", SapphireSheet, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(sheet.transform, "SheetBorder", GlassOutline, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        // Botão Principal: INICIAR JOGADA (54pt, Arcade Emerald)
        CreateArcadeButton(sheet.transform, "PlayBtn", new Vector2(0.06f, 0.54f), new Vector2(0.94f, 0.88f), EmeraldPrimary, Color.white, StartGame, "INICIAR JOGADA ▶");

        // Linha de Botões Secundários: Álbum (50%) e +3 Fichas (50%)
        GameObject albumBtn = CreateArcadeButton(sheet.transform, "MenuAlbumBtn", new Vector2(0.06f, 0.20f), new Vector2(0.48f, 0.48f), SapphireDark, NeonCyan, OpenAlbum);
        menuAlbumButtonText = CreateText(albumBtn.transform, "AlbumText", "🏆 ÁLBUM (0/6)", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 14, NeonCyan, TextAnchor.MiddleCenter, true);

        CreateArcadeButton(sheet.transform, "DailyRewardBtn", new Vector2(0.52f, 0.20f), new Vector2(0.94f, 0.48f), SapphireDark, NeonGold, () => {
            if (PlayerEconomyManager.Instance != null)
            {
                if (PlayerEconomyManager.Instance.ClaimDailyReward())
                {
                    AudioFeedbackController.Instance?.PlayCoin();
                }
            }
        }, "🎁 +3 FICHAS");

        // Rodapé Sutil
        CreateText(sheet.transform, "Hint", "Mire com o joystick e aperte o botão para descer", new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.17f), Vector2.zero, Vector2.zero, 12, new Color(1f, 1f, 1f, 0.65f), TextAnchor.MiddleCenter, false);
    }

    // -------------------------------------------------------------
    // RESULT MODAL: Bottom Sheet de Premiação com Retrato Ilustrado
    // -------------------------------------------------------------
    private void BuildResult(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 sheetMin = isP ? new Vector2(0.04f, 0.02f) : new Vector2(0.24f, 0.02f);
        Vector2 sheetMax = isP ? new Vector2(0.96f, 0.44f) : new Vector2(0.76f, 0.50f);

        GameObject sheet = CreatePanel(parent, "ResultSheet", SapphireSheet, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(sheet.transform, "SheetBorder", NeonGold * 0.35f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        // Título de Celebração
        resultTitleText = CreateText(sheet.transform, "Title", "🎉 PRÊMIO CAPTURADO!", new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero, 20, NeonGold, TextAnchor.MiddleCenter, true);

        // Bloco Central: Retrato Ilustrado à Esquerda + Detalhes à Direita
        GameObject portraitContainer = CreatePanel(sheet.transform, "PortraitContainer", new Color(0.08f, 0.12f, 0.22f, 0.95f), new Vector2(0.08f, 0.40f), new Vector2(0.35f, 0.80f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
        CreatePanel(portraitContainer.transform, "PortraitBorder", NeonGold * 0.6f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
        GameObject portraitImgObj = CreatePanel(portraitContainer.transform, "PortraitImg", Color.white, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
        resultPortraitImage = portraitImgObj.GetComponent<Image>();

        // Textos à Direita do Retrato
        resultNameText = CreateText(sheet.transform, "PrizeName", "RAPOSA ASTUTA", new Vector2(0.38f, 0.62f), new Vector2(0.95f, 0.80f), Vector2.zero, Vector2.zero, 20, Color.white, TextAnchor.MiddleLeft, true);
        resultBadgeText = CreateText(sheet.transform, "Badge", "★ COMUM ★", new Vector2(0.38f, 0.48f), new Vector2(0.95f, 0.62f), Vector2.zero, Vector2.zero, 14, NeonCyan, TextAnchor.MiddleLeft, true);
        resultMessageText = CreateText(sheet.transform, "Message", "Adicionado ao seu álbum de coleção!", new Vector2(0.08f, 0.26f), new Vector2(0.95f, 0.40f), Vector2.zero, Vector2.zero, 13, new Color(0.9f, 0.95f, 1f, 0.9f), TextAnchor.MiddleCenter, false);

        // Botão Continuar (52pt, Emerald)
        CreateArcadeButton(sheet.transform, "ContinueBtn", new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.23f), EmeraldPrimary, Color.white, ContinueAfterResult, "CONTINUAR JOGANDO ▶");

        parent.gameObject.SetActive(false);
    }

    // -------------------------------------------------------------
    // GAME OVER: Bottom Sheet Compacto (32% da tela)
    // -------------------------------------------------------------
    private void BuildGameOver(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 sheetMin = isP ? new Vector2(0.04f, 0.02f) : new Vector2(0.25f, 0.02f);
        Vector2 sheetMax = isP ? new Vector2(0.96f, 0.32f) : new Vector2(0.75f, 0.38f);

        GameObject sheet = CreatePanel(parent, "GameOverSheet", SapphireSheet, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(sheet.transform, "Border", NeonCyan * 0.25f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        gameOverTitleText = CreateText(sheet.transform, "Title", "FIM DA JOGADA", new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.92f), Vector2.zero, Vector2.zero, 22, Color.white, TextAnchor.MiddleCenter, true);
        gameOverMessageText = CreateText(sheet.transform, "Msg", "Você ainda tem fichas restantes!", new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.68f), Vector2.zero, Vector2.zero, 14, NeonCyan, TextAnchor.MiddleCenter, false);

        GameObject btn = CreateArcadeButton(sheet.transform, "GameOverBtn", new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.38f), EmeraldPrimary, Color.white, () => gameOverAction?.Invoke());
        gameOverButtonText = CreateText(btn.transform, "BtnText", "JOGAR NOVAMENTE 🪙", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 16, Color.white, TextAnchor.MiddleCenter, true);

        parent.gameObject.SetActive(false);
    }

    // -------------------------------------------------------------
    // ÁLBUM DE COLEÇÃO: Sheet 2x3 com Ilustrações dos 6 Bichinhos
    // -------------------------------------------------------------
    private Transform albumGridContainer;
    private Text albumProgressText;

    private void BuildAlbumContainer(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 sheetMin = isP ? new Vector2(0.04f, 0.02f) : new Vector2(0.18f, 0.02f);
        Vector2 sheetMax = isP ? new Vector2(0.96f, 0.86f) : new Vector2(0.82f, 0.94f);

        GameObject window = CreatePanel(parent, "AlbumWindow", SapphireSheet, sheetMin, sheetMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(window.transform, "Border", NeonCyan * 0.4f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        CreateText(window.transform, "Header", "🏆 ÁLBUM DE COLEÇÃO", new Vector2(0.05f, 0.915f), new Vector2(0.95f, 0.985f), Vector2.zero, Vector2.zero, isP ? 24 : 28, NeonGold, TextAnchor.MiddleCenter, true);
        albumProgressText = CreateText(window.transform, "ProgressPill", "COLEÇÃO: 0 / 6 DESBLOQUEADOS (0%)", new Vector2(0.05f, 0.855f), new Vector2(0.95f, 0.910f), Vector2.zero, Vector2.zero, 14, NeonCyan, TextAnchor.MiddleCenter, true);

        // Grade 2x3
        GameObject gridObj = CreatePanel(window.transform, "GridContainer", Color.clear, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.84f), Vector2.zero, Vector2.zero, false);
        albumGridContainer = gridObj.transform;

        // Botão Fechar
        CreateArcadeButton(window.transform, "CloseAlbumBtn", new Vector2(0.15f, 0.025f), new Vector2(0.85f, 0.100f), SapphireDark, NeonCyan, CloseAlbum, "VOLTAR À MÁQUINA");

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
            Color pedestalColor = item.IsUnlocked ? new Color(0.07f, 0.11f, 0.22f, 0.95f) : new Color(0.03f, 0.05f, 0.10f, 0.85f);

            GameObject card = CreatePanel(albumGridContainer, $"Slot_{item.id}", pedestalColor, aMin, aMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
            CreatePanel(card.transform, "PedestalBorder", item.IsUnlocked ? rarityColor * 0.75f : Color.gray * 0.25f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

            Button btn = card.AddComponent<Button>();
            card.AddComponent<ArcadePressEffect>();
            btn.onClick.AddListener(() => OpenInspect(item));

            Sprite portrait = GetPlushiePortrait(item.id);

            if (item.IsUnlocked)
            {
                // 1. Retrato Ilustrado Oficial (64x64)
                GameObject imgFrame = CreatePanel(card.transform, "Frame", new Color(0.04f, 0.07f, 0.14f, 0.95f), new Vector2(0.20f, 0.42f), new Vector2(0.80f, 0.88f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
                CreatePanel(imgFrame.transform, "FrameBorder", rarityColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
                GameObject imgObj = CreatePanel(imgFrame.transform, "Portrait", Color.white, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
                if (portrait != null)
                {
                    imgObj.GetComponent<Image>().sprite = portrait;
                }

                // 2. Badge ×N no canto superior direito
                GameObject countPill = CreateGlassPill(card.transform, "CountPill", new Vector2(0.60f, 0.78f), new Vector2(0.96f, 0.95f), NeonGold);
                CreateText(countPill.transform, "CountText", $"×{item.count}", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 12, NeonGold, TextAnchor.MiddleCenter, true);

                // 3. Nome do Bichinho
                CreateText(card.transform, "Name", item.displayName.ToUpperInvariant(), new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.40f), Vector2.zero, Vector2.zero, 13, Color.white, TextAnchor.MiddleCenter, true);

                // 4. Tag de Raridade
                string tag = item.rarity == PrizeRarity.Rare ? "★ RARO" : item.rarity == PrizeRarity.Uncommon ? "★ INCOMUM" : "COMUM";
                CreateText(card.transform, "RarityTag", tag, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.22f), Vector2.zero, Vector2.zero, 11, rarityColor, TextAnchor.MiddleCenter, true);
            }
            else
            {
                // Silhueta Escura com '?'
                GameObject silhFrame = CreatePanel(card.transform, "SilhFrame", new Color(0.03f, 0.05f, 0.09f, 0.95f), new Vector2(0.22f, 0.44f), new Vector2(0.78f, 0.86f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
                CreatePanel(silhFrame.transform, "Border", Color.gray * 0.2f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
                GameObject silhImg = CreatePanel(silhFrame.transform, "SilhImg", new Color(0.12f, 0.16f, 0.24f, 0.6f), new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
                if (portrait != null)
                {
                    Image im = silhImg.GetComponent<Image>();
                    im.sprite = portrait;
                    im.color = new Color(0.05f, 0.07f, 0.12f, 0.75f); // Silhueta escura da própria arte
                }
                CreateText(silhFrame.transform, "QMark", "?", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 28, new Color(0.6f, 0.7f, 0.9f, 0.5f), TextAnchor.MiddleCenter, true);

                CreateText(card.transform, "Name", "???", new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.40f), Vector2.zero, Vector2.zero, 13, new Color(0.6f, 0.6f, 0.7f, 0.8f), TextAnchor.MiddleCenter, true);
                CreateText(card.transform, "Hint", "BLOQUEADO", new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.22f), Vector2.zero, Vector2.zero, 11, new Color(0.45f, 0.5f, 0.6f, 0.8f), TextAnchor.MiddleCenter, false);
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

        GameObject card = CreatePanel(inspectModal.transform, "InspectCard", SapphireSheet, min, max, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(card.transform, "Border", NeonGold * 0.35f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        // Retrato em Destaque
        GameObject imgFrame = CreatePanel(card.transform, "InspectFrame", new Color(0.08f, 0.12f, 0.22f, 0.95f), new Vector2(0.35f, 0.64f), new Vector2(0.65f, 0.92f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
        CreatePanel(imgFrame.transform, "FrameBorder", NeonGold * 0.6f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetCircleSprite());
        GameObject imgObj = CreatePanel(imgFrame.transform, "InspectImg", Color.white, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero, false, GetCircleSprite());
        inspectPortraitImage = imgObj.GetComponent<Image>();

        inspectNameText = CreateText(card.transform, "Name", "RAPOSA ASTUTA", new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.64f), Vector2.zero, Vector2.zero, 20, Color.white, TextAnchor.MiddleCenter, true);
        inspectRarityText = CreateText(card.transform, "Rarity", "★ COMUM ★", new Vector2(0.05f, 0.46f), new Vector2(0.95f, 0.54f), Vector2.zero, Vector2.zero, 14, NeonCyan, TextAnchor.MiddleCenter, true);
        inspectLoreText = CreateText(card.transform, "Lore", "Descrição da pelúcia...", new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.46f), Vector2.zero, Vector2.zero, 14, new Color(0.9f, 0.95f, 1f, 0.85f), TextAnchor.MiddleCenter, false);
        inspectStatsText = CreateText(card.transform, "Stats", "Capturas: 0", new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.28f), Vector2.zero, Vector2.zero, 13, NeonGold, TextAnchor.MiddleCenter, false);

        CreateArcadeButton(card.transform, "CloseInspectBtn", new Vector2(0.18f, 0.04f), new Vector2(0.82f, 0.16f), EmeraldPrimary, Color.white, () => inspectModal.SetActive(false), "FECHAR");
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
            inspectStatsText.color = NeonGold;
        }
        inspectModal.SetActive(true);
    }

    // ==================== LOADER DE RETRATOS DOS BICHINHOS ====================
    private void PreloadPortraits()
    {
        string[] ids = { "fox", "greenbear", "balloonfish", "koala", "badger", "porky" };
        foreach (string id in ids)
        {
            GetPlushiePortrait(id);
        }
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

        if (portraitCache.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Texture2D tex = Resources.Load<Texture2D>($"Textures/Portraits/portrait_{key}");
        if (tex != null)
        {
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            portraitCache[key] = sp;
            return sp;
        }

        return null;
    }

    // ==================== TRANSIÇÕES BOTTOM SHEET ====================
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

    // ==================== CRIADORES DE COMPONENTES ESTILIZADOS ====================
    private GameObject CreateArcadeButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor, Action onClick, string label = null)
    {
        GameObject btnObj = CreatePanel(parent, name, bgColor, anchorMin, anchorMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        btnObj.AddComponent<ArcadePressEffect>();

        ColorBlock cb = btn.colors;
        cb.normalColor = bgColor;
        cb.highlightedColor = Color.white;
        cb.pressedColor = bgColor * 0.85f;
        cb.fadeDuration = 0.05f;
        btn.colors = cb;

        // Borda sutil chanfrada 3D
        CreatePanel(btnObj.transform, "BevelBorder", new Color(1f, 1f, 1f, 0.18f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        if (!string.IsNullOrEmpty(label))
        {
            CreateText(btnObj.transform, "Label", label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 16, textColor, TextAnchor.MiddleCenter, true);
        }

        return btnObj;
    }

    private GameObject CreateGlassPill(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color glowBorder)
    {
        GameObject pill = CreatePanel(parent, name, SapphireDark, aMin, aMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(pill.transform, "GlowBorder", glowBorder * 0.35f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();
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

        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        return text;
    }

    // ==================== PROCEDURAL HIGH-DPI SPRITES ====================
    public static Sprite GetRoundedRectSprite()
    {
        if (roundedRectSprite != null) return roundedRectSprite;
        int size = 32;
        int r = 10;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] cols = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = Mathf.Min(x, size - 1 - x);
                int dy = Mathf.Min(y, size - 1 - y);
                if (dx < r && dy < r)
                {
                    float dist = Vector2.Distance(new Vector2(dx, dy), new Vector2(r, r));
                    float alpha = Mathf.Clamp01(r - dist + 0.5f);
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    cols[y * size + x] = Color.white;
                }
            }
        }
        tex.SetPixels(cols);
        tex.Apply();
        roundedRectSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return roundedRectSprite;
    }

    public static Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        int size = 64;
        float r = size * 0.5f;
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
        tex.SetPixels(cols);
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }
}
