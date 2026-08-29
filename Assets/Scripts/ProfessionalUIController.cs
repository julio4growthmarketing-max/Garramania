using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Efeito físico arcade tátil: faz o botão afundar 4px ao toque com micro-vibração háptica.
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
            rect.anchoredPosition += new Vector2(0f, -4f);
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
            rect.anchoredPosition -= new Vector2(0f, -4f);
            rect.localScale = Vector3.one;
        }
    }
}

/// <summary>
/// UI Arcade Profissional do GarraMania (Design System 2026).
/// Inclui Álbum de Coleção de 6 Slots interativo com pedestais de raridade,
/// botões físicos arcade com press-down, HUD em pílulas de vidro e transições suaves.
/// </summary>
public sealed class ProfessionalUIController : MonoBehaviour
{
    // Paleta Oficial Arcade Neon
    public static readonly Color NeonCyan = new Color(0.00f, 0.93f, 1.00f, 1f);
    public static readonly Color NeonMagenta = new Color(1.00f, 0.10f, 0.45f, 1f);
    public static readonly Color NeonGold = new Color(1.00f, 0.82f, 0.10f, 1f);
    public static readonly Color NeonGreen = new Color(0.12f, 1.00f, 0.52f, 1f);
    public static readonly Color NeonRed = new Color(1.00f, 0.18f, 0.28f, 1f);
    public static readonly Color NeonPurple = new Color(0.78f, 0.25f, 1.00f, 1f);

    public static readonly Color GlassDark = new Color(0.03f, 0.05f, 0.10f, 0.88f);
    public static readonly Color GlassPill = new Color(0.04f, 0.08f, 0.16f, 0.75f);
    public static readonly Color GlassCard = new Color(0.05f, 0.09f, 0.18f, 0.94f);

    private static Sprite roundedRectSprite;
    private static Sprite circleSprite;

    private GameObject canvasRoot;
    private GameObject menuPanel;
    private GameObject hudPanel;
    private GameObject controlsPanel;
    private GameObject resultPanel;
    private GameObject gameOverPanel;
    private GameObject albumPanel;
    private GameObject inspectModal;

    private Text creditsText;
    private Text prizesText;
    private Text timerText;
    private Image timerFill;
    private RectTransform timerFillRect;
    private Text statusText;
    private Text actionText;
    private Text actionSubText;
    private Text cameraText;
    private Text albumHudButtonText;
    private Text menuAlbumButtonText;

    private Text resultTitleText;
    private Text resultNameText;
    private Text resultBadgeText;
    private Text resultMessageText;
    private Text gameOverMessageText;

    // Modal de Inspeção
    private Text inspectNameText;
    private Text inspectRarityText;
    private Text inspectLoreText;
    private Text inspectStatsText;

    private Button actionButton;
    private Image actionButtonCore;

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

        if (cameraController != null)
        {
            cameraController.OnCameraAngleChanged.AddListener(HandleCameraAngleChanged);
            HandleCameraAngleChanged(cameraController.CurrentAngle);
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
        bool delivering = state == GameState.Delivering;

        if (idle)
        {
            PopIn(menuPanel);
            if (hudPanel != null) hudPanel.SetActive(false);
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            SetStatus("INICIE UMA JOGADA PARA CONTROLAR", NeonCyan);
        }
        else if (playing)
        {
            if (menuPanel != null) menuPanel.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (hudPanel != null) hudPanel.SetActive(true);
            if (controlsPanel != null) controlsPanel.SetActive(true);
            SetStatus("USE O JOYSTICK E O BOTÃO PARA AGARRAR", NeonCyan);
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
        timerText.text = seconds.ToString();

        float normalized = max > 0.01f ? Mathf.Clamp01(remaining / max) : 0f;
        if (timerFillRect != null)
        {
            timerFillRect.anchorMax = new Vector2(Mathf.Lerp(0.04f, 0.96f, normalized), 0.82f);
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
        if (creditsText != null) creditsText.text = $"🪙 {value} FICHAS";
    }

    private void HandlePrizeDelivered(Prize prize, int total)
    {
        if (prizesText != null) prizesText.text = $"🏆 {total} PRÊMIOS";
    }

    private void HandlePrizeResult(Prize prize)
    {
        if (resultPanel == null) return;
        string stockId = prize != null ? (!string.IsNullOrEmpty(prize.StockId) ? prize.StockId : prize.prizeId) : "Fox";
        CaptureResult res = CollectionManager.Instance.RegisterCapture(stockId);

        if (resultNameText != null) resultNameText.text = res.item.displayName.ToUpperInvariant();

        if (resultBadgeText != null)
        {
            resultBadgeText.text = res.item.rarity == PrizeRarity.Rare ? "★ RARO ★" : res.item.rarity == PrizeRarity.Uncommon ? "★ INCOMUM ★" : "COMUM";
            resultBadgeText.color = res.item.themeColor;
        }

        if (resultMessageText != null)
        {
            if (res.isFirstTime)
            {
                resultMessageText.text = $"✨ NOVO NO ÁLBUM! ✨\nDesbloqueado pela primeira vez!\nColeção: {res.totalUniqueUnlocked}/{CollectionManager.Instance.GetTotalCount()} bichinhos";
                resultMessageText.color = NeonGold;
                GameJuice.Instance?.PlaySparkles(Camera.main != null ? Camera.main.transform.position + Camera.main.transform.forward * 2f : Vector3.zero);
            }
            else
            {
                resultMessageText.text = $"Adicionado à coleção! Você agora tem {res.totalOfThisType} deste modelo.\nColeção: {res.totalUniqueUnlocked}/{CollectionManager.Instance.GetTotalCount()} desbloqueados";
                resultMessageText.color = Color.white;
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
        if (gameOverMessageText != null)
            gameOverMessageText.text = session != null && session.Credits > 0 ? "FIM DA JOGADA!\nUSE SUAS FICHAS PARA TENTAR NOVAMENTE." : "FICHAS ESGOTADAS!\nOBRIGADO POR JOGAR.";
        PopIn(gameOverPanel);
    }

    private void HandleClawStateChanged(bool closed)
    {
        if (actionText == null) return;
        actionText.text = closed ? "SOLTAR" : "AGARRAR";
        if (actionSubText != null) actionSubText.text = closed ? "LIBERAR" : "DESCER";
        if (actionButtonCore != null)
        {
            actionButtonCore.color = closed ? new Color(1.0f, 0.55f, 0.08f, 1f) : NeonMagenta;
        }
    }

    private void HandleCameraAngleChanged(ClawCameraController.CameraViewAngle angle)
    {
        if (cameraText == null) return;
        cameraText.text = "CÂMERA: " + angle.ToString().ToUpperInvariant();
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

    public void RestartAfterGameOver()
    {
        PopOut(gameOverPanel, () => {
            session?.ResetSession();
            InputRouter.Instance?.SetBlocked(true);
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
        string text = $"🏆 ÁLBUM ({unlocked}/{total})";
        if (albumHudButtonText != null) albumHudButtonText.text = text;
        if (menuAlbumButtonText != null) menuAlbumButtonText.text = $"VER ÁLBUM ({unlocked}/{total})";
    }

    private void SetStatus(string value, Color color)
    {
        if (statusText == null) return;
        statusText.text = value;
        statusText.color = color;
    }

    // ==================== CONSTRUÇÃO DA INTERFACE UGUI ARCADE ====================
    private void BuildInterface()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") 
              ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        canvasRoot = new GameObject("GarraManiaUI_ArcadeSystem");
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

        menuPanel = CreatePanel(root, "Menu", new Color(0.01f, 0.02f, 0.05f, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildMenu(menuPanel.transform);

        resultPanel = CreatePanel(root, "Result", new Color(0.01f, 0.02f, 0.05f, 0.90f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildResult(resultPanel.transform);

        gameOverPanel = CreatePanel(root, "GameOver", new Color(0.01f, 0.02f, 0.05f, 0.92f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildGameOver(gameOverPanel.transform);

        albumPanel = CreatePanel(root, "AlbumPanel", new Color(0.01f, 0.02f, 0.05f, 0.94f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildAlbumContainer(albumPanel.transform);
        albumPanel.SetActive(false);
    }

    private void BuildHud(Transform parent)
    {
        bool isP = Screen.width < Screen.height;

        // Pílula 1: Fichas (Esquerda)
        Vector2 credMin = isP ? new Vector2(0.03f, 0.930f) : new Vector2(0.025f, 0.925f);
        Vector2 credMax = isP ? new Vector2(0.33f, 0.985f) : new Vector2(0.18f, 0.985f);
        GameObject credits = CreateGlassPill(parent, "CreditsPill", credMin, credMax, NeonCyan);
        creditsText = CreateText(credits.transform, "CreditsText", "🪙 3 FICHAS", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 16, Color.white, TextAnchor.MiddleCenter, true);

        // Pílula 2: Timer Central
        Vector2 timeMin = isP ? new Vector2(0.37f, 0.920f) : new Vector2(0.44f, 0.920f);
        Vector2 timeMax = isP ? new Vector2(0.63f, 0.988f) : new Vector2(0.56f, 0.988f);
        GameObject timer = CreateGlassPill(parent, "TimerPill", timeMin, timeMax, NeonGold);
        timerFill = CreatePanel(timer.transform, "TimerFill", NeonCyan, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.82f), Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).GetComponent<Image>();
        timerFill.type = Image.Type.Sliced;
        timerFillRect = timerFill.rectTransform;
        timerText = CreateText(timer.transform, "TimerText", "45", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 24, Color.white, TextAnchor.MiddleCenter, true);

        // Pílula 3: Botão Álbum no HUD (Direita)
        Vector2 albMin = isP ? new Vector2(0.67f, 0.930f) : new Vector2(0.82f, 0.925f);
        Vector2 albMax = isP ? new Vector2(0.97f, 0.985f) : new Vector2(0.975f, 0.985f);
        GameObject albumBtn = CreateArcadeButton(parent, "AlbumHudBtn", albMin, albMax, GlassPill, NeonGold, OpenAlbum);
        albumHudButtonText = CreateText(albumBtn.transform, "AlbumHudBtnText", "🏆 ÁLBUM (0/6)", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 15, NeonGold, TextAnchor.MiddleCenter, true);

        // Status Discreto
        Vector2 stMin = isP ? new Vector2(0.10f, 0.880f) : new Vector2(0.30f, 0.875f);
        Vector2 stMax = isP ? new Vector2(0.90f, 0.915f) : new Vector2(0.70f, 0.915f);
        statusText = CreateText(parent, "Status", "PRONTO PARA JOGAR", stMin, stMax, Vector2.zero, Vector2.zero, 12, NeonCyan, TextAnchor.MiddleCenter, false);
    }

    private void BuildControls(Transform parent)
    {
        bool isP = Screen.width < Screen.height;

        // 1. Joystick Virtual
        Vector2 joyMin = isP ? new Vector2(0.04f, 0.03f) : new Vector2(0.03f, 0.03f);
        Vector2 joyMax = isP ? new Vector2(0.36f, 0.19f) : new Vector2(0.18f, 0.23f);
        GameObject joystick = CreateGlassPill(parent, "VirtualJoystick", joyMin, joyMax, NeonCyan);
        GameObject handle = CreatePanel(joystick.transform, "Handle", NeonCyan, new Vector2(0.28f, 0.28f), new Vector2(0.72f, 0.72f), Vector2.zero, Vector2.zero, true, GetCircleSprite());
        VirtualJoystickView joystickView = joystick.AddComponent<VirtualJoystickView>();
        joystickView.Configure(joystick.GetComponent<RectTransform>(), handle.GetComponent<RectTransform>(), isP ? 55f : 60f);
        CreateText(joystick.transform, "JoyLabel", "MOVER", new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.22f), Vector2.zero, Vector2.zero, 11, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, true);

        // 2. BIG RED ARCADE PUSH-BUTTON (Botão Redondo Japonês Sanwa)
        Vector2 actMin = isP ? new Vector2(0.66f, 0.03f) : new Vector2(0.82f, 0.03f);
        Vector2 actMax = isP ? new Vector2(0.96f, 0.19f) : new Vector2(0.97f, 0.23f);

        GameObject actionRing = CreatePanel(parent, "ActionButton_Ring", new Color(0.18f, 0.22f, 0.32f, 0.95f), actMin, actMax, Vector2.zero, Vector2.zero, true, GetCircleSprite());
        GameObject actionButtonObj = CreatePanel(actionRing.transform, "ActionButton_Core", NeonMagenta, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero, true, GetCircleSprite());
        actionButtonCore = actionButtonObj.GetComponent<Image>();
        actionButton = actionButtonObj.AddComponent<Button>();
        actionButton.onClick.AddListener(() => InputRouter.Instance?.TriggerTouchAction());
        actionButtonObj.AddComponent<ArcadePressEffect>();

        actionText = CreateText(actionButtonObj.transform, "ActionText", "AGARRAR", new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.86f), Vector2.zero, Vector2.zero, isP ? 20 : 22, Color.white, TextAnchor.MiddleCenter, true);
        actionSubText = CreateText(actionButtonObj.transform, "ActionSubText", "DESCER", new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.44f), Vector2.zero, Vector2.zero, 12, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, false);

        // 3. Controles de Câmera (Espiar)
        Vector2 pLeftMin = isP ? new Vector2(0.04f, 0.205f) : new Vector2(0.38f, 0.035f);
        Vector2 pLeftMax = isP ? new Vector2(0.32f, 0.255f) : new Vector2(0.445f, 0.085f);
        CreateArcadeButton(parent, "PeekLeftBtn", pLeftMin, pLeftMax, GlassPill, NeonCyan, () => cameraController?.LeanLeft(), "◄ ESPIAR");

        Vector2 camMin = isP ? new Vector2(0.36f, 0.205f) : new Vector2(0.455f, 0.035f);
        Vector2 camMax = isP ? new Vector2(0.64f, 0.255f) : new Vector2(0.545f, 0.085f);
        GameObject camBtn = CreateArcadeButton(parent, "CamBtn", camMin, camMax, GlassPill, NeonGold, () => cameraController?.ToggleCameraAngle());
        cameraText = CreateText(camBtn.transform, "CamText", "CÂMERA", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13, NeonGold, TextAnchor.MiddleCenter, true);

        Vector2 pRightMin = isP ? new Vector2(0.68f, 0.205f) : new Vector2(0.555f, 0.035f);
        Vector2 pRightMax = isP ? new Vector2(0.96f, 0.255f) : new Vector2(0.62f, 0.085f);
        CreateArcadeButton(parent, "PeekRightBtn", pRightMin, pRightMax, GlassPill, NeonCyan, () => cameraController?.LeanRight(), "ESPIAR ►");
    }

    private void BuildMenu(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 min = isP ? new Vector2(0.08f, 0.18f) : new Vector2(0.28f, 0.16f);
        Vector2 max = isP ? new Vector2(0.92f, 0.82f) : new Vector2(0.72f, 0.84f);

        GameObject card = CreatePanel(parent, "MenuCard", GlassCard, min, max, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(card.transform, "CardBorder", NeonGold * 0.4f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        CreateText(card.transform, "Logo", "GARRAMANIA", new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.88f), Vector2.zero, Vector2.zero, isP ? 42 : 46, NeonGold, TextAnchor.MiddleCenter, true);
        CreateText(card.transform, "Subtitle", "ARCADE CLAW MACHINE • FLIPERAMA", new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.72f), Vector2.zero, Vector2.zero, 14, NeonCyan, TextAnchor.MiddleCenter, true);
        CreateText(card.transform, "Instruction", "Mire a garra cromada sobre a pelúcia,\naperte o botão para descer e leve para a calha!", new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.58f), Vector2.zero, Vector2.zero, 16, Color.white, TextAnchor.MiddleCenter, false);

        // Botão Play 3D Verde
        CreateArcadeButton(card.transform, "PlayBtn", new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.40f), NeonGreen, Color.black, StartGame, "INICIAR JOGADA");

        // Botão Álbum de Coleção
        GameObject albumBtn = CreateArcadeButton(card.transform, "MenuAlbumBtn", new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.20f), GlassPill, NeonCyan, OpenAlbum);
        menuAlbumButtonText = CreateText(albumBtn.transform, "MenuAlbumBtnText", "VER ÁLBUM (0/6)", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 16, NeonCyan, TextAnchor.MiddleCenter, true);
    }

    private void BuildResult(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 min = isP ? new Vector2(0.08f, 0.16f) : new Vector2(0.26f, 0.16f);
        Vector2 max = isP ? new Vector2(0.92f, 0.84f) : new Vector2(0.74f, 0.84f);

        GameObject card = CreatePanel(parent, "ResultCard", GlassCard, min, max, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        resultTitleText = CreateText(card.transform, "Title", "🎉 PRÊMIO CAPTURADO!", new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.90f), Vector2.zero, Vector2.zero, 30, NeonGold, TextAnchor.MiddleCenter, true);
        resultNameText = CreateText(card.transform, "PrizeName", "RAPOSA ASTUTA", new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.74f), Vector2.zero, Vector2.zero, 28, Color.white, TextAnchor.MiddleCenter, true);
        resultBadgeText = CreateText(card.transform, "Badge", "★ COMUM ★", new Vector2(0.20f, 0.50f), new Vector2(0.80f, 0.58f), Vector2.zero, Vector2.zero, 16, NeonCyan, TextAnchor.MiddleCenter, true);
        resultMessageText = CreateText(card.transform, "Message", "✨ NOVO NO ÁLBUM! ✨", new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.48f), Vector2.zero, Vector2.zero, 17, NeonGold, TextAnchor.MiddleCenter, false);

        CreateArcadeButton(card.transform, "ContinueBtn", new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.24f), NeonGreen, Color.black, ContinueAfterResult, "CONTINUAR JOGANDO");
        parent.gameObject.SetActive(false);
    }

    private void BuildGameOver(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 min = isP ? new Vector2(0.08f, 0.22f) : new Vector2(0.26f, 0.22f);
        Vector2 max = isP ? new Vector2(0.92f, 0.78f) : new Vector2(0.74f, 0.78f);

        GameObject card = CreatePanel(parent, "GameOverCard", GlassCard, min, max, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreateText(card.transform, "Title", "FIM DA JOGADA", new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.86f), Vector2.zero, Vector2.zero, 36, NeonRed, TextAnchor.MiddleCenter, true);
        gameOverMessageText = CreateText(card.transform, "Msg", "USE SUAS FICHAS PARA TENTAR NOVAMENTE", new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.62f), Vector2.zero, Vector2.zero, 18, Color.white, TextAnchor.MiddleCenter, false);
        CreateArcadeButton(card.transform, "RetryBtn", new Vector2(0.12f, 0.16f), new Vector2(0.88f, 0.32f), NeonCyan, Color.black, RestartAfterGameOver, "VOLTAR AO MENU");
        parent.gameObject.SetActive(false);
    }

    // ==================== VITRINE DE COLEÇÃO & ÁLBUM 2x3 ====================
    private Transform albumGridContainer;
    private Text albumProgressText;

    private void BuildAlbumContainer(Transform parent)
    {
        bool isP = Screen.width < Screen.height;
        Vector2 min = isP ? new Vector2(0.04f, 0.05f) : new Vector2(0.15f, 0.06f);
        Vector2 max = isP ? new Vector2(0.96f, 0.95f) : new Vector2(0.85f, 0.94f);

        GameObject window = CreatePanel(parent, "AlbumWindow", GlassCard, min, max, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        CreatePanel(window.transform, "Border", NeonCyan * 0.4f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

        CreateText(window.transform, "Header", "🏆 ÁLBUM DE COLEÇÃO", new Vector2(0.05f, 0.91f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero, isP ? 26 : 30, NeonGold, TextAnchor.MiddleCenter, true);
        albumProgressText = CreateText(window.transform, "ProgressPill", "COLEÇÃO: 0 / 6 DESBLOQUEADOS (0%)", new Vector2(0.10f, 0.845f), new Vector2(0.90f, 0.905f), Vector2.zero, Vector2.zero, 14, NeonCyan, TextAnchor.MiddleCenter, true);

        // Contêiner dos 6 slots
        GameObject gridObj = CreatePanel(window.transform, "GridContainer", Color.clear, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.83f), Vector2.zero, Vector2.zero, false);
        albumGridContainer = gridObj.transform;

        // Botão Fechar / Voltar
        CreateArcadeButton(window.transform, "CloseAlbumBtn", new Vector2(0.20f, 0.03f), new Vector2(0.80f, 0.11f), GlassPill, NeonCyan, CloseAlbum, "VOLTAR À MÁQUINA");

        // Modal de Inspeção Individual
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

        // Em portrait: 2 colunas x 3 linhas
        // Em landscape: 3 colunas x 2 linhas
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
            Color pedestalColor = item.IsUnlocked ? new Color(0.07f, 0.14f, 0.25f, 0.95f) : new Color(0.03f, 0.05f, 0.09f, 0.85f);

            GameObject card = CreatePanel(albumGridContainer, $"Slot_{item.id}", pedestalColor, aMin, aMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
            // Borda colorida de raridade
            CreatePanel(card.transform, "PedestalBorder", item.IsUnlocked ? rarityColor * 0.8f : Color.gray * 0.35f, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false, GetRoundedRectSprite()).transform.SetAsFirstSibling();

            Button btn = card.AddComponent<Button>();
            card.AddComponent<ArcadePressEffect>();
            btn.onClick.AddListener(() => OpenInspect(item));

            if (item.IsUnlocked)
            {
                // Unlocked State
                CreateText(card.transform, "Icon", GetPlushieEmoji(item.id), new Vector2(0.1f, 0.44f), new Vector2(0.9f, 0.88f), Vector2.zero, Vector2.zero, isP ? 38 : 42, Color.white, TextAnchor.MiddleCenter, true);
                CreateText(card.transform, "Name", item.displayName.ToUpperInvariant(), new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.44f), Vector2.zero, Vector2.zero, isP ? 13 : 15, Color.white, TextAnchor.MiddleCenter, true);
                
                // Badge xN
                GameObject countPill = CreateGlassPill(card.transform, "CountPill", new Vector2(0.60f, 0.74f), new Vector2(0.95f, 0.94f), NeonGold);
                CreateText(countPill.transform, "CountText", $"×{item.count}", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 12, NeonGold, TextAnchor.MiddleCenter, true);

                // Rarity Tag
                string tag = item.rarity == PrizeRarity.Rare ? "RARO" : item.rarity == PrizeRarity.Uncommon ? "INCOMUM" : "COMUM";
                CreateText(card.transform, "RarityTag", tag, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.22f), Vector2.zero, Vector2.zero, 11, rarityColor, TextAnchor.MiddleCenter, true);
            }
            else
            {
                // Locked Silhouette State
                CreateText(card.transform, "Silh", "?", new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.86f), Vector2.zero, Vector2.zero, isP ? 42 : 46, new Color(0.4f, 0.4f, 0.5f, 0.6f), TextAnchor.MiddleCenter, true);
                CreateText(card.transform, "Name", "???", new Vector2(0.05f, 0.22f), new Vector2(0.95f, 0.42f), Vector2.zero, Vector2.zero, 14, new Color(0.6f, 0.6f, 0.7f, 0.8f), TextAnchor.MiddleCenter, true);
                CreateText(card.transform, "Hint", "BLOQUEADO", new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.22f), Vector2.zero, Vector2.zero, 11, new Color(0.5f, 0.5f, 0.5f, 0.8f), TextAnchor.MiddleCenter, false);
            }
        }
    }

    private string GetPlushieEmoji(string id)
    {
        switch (id)
        {
            case "Fox": return "🦊";
            case "GreenBear": return "🧸";
            case "BalloonFish": return "🐡";
            case "Koala": return "🐨";
            case "Badger": return "🦡";
            case "Porky": return "👑🐷";
            default: return "🧸";
        }
    }

    private void BuildInspectModal(Transform parent)
    {
        inspectModal = CreatePanel(parent, "InspectModal", new Color(0.01f, 0.02f, 0.04f, 0.96f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
        inspectModal.SetActive(false);

        bool isP = Screen.width < Screen.height;
        Vector2 min = isP ? new Vector2(0.10f, 0.22f) : new Vector2(0.28f, 0.18f);
        Vector2 max = isP ? new Vector2(0.90f, 0.78f) : new Vector2(0.72f, 0.82f);

        GameObject card = CreatePanel(inspectModal.transform, "InspectCard", GlassCard, min, max, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
        inspectNameText = CreateText(card.transform, "Name", "RAPOSA ASTUTA", new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.92f), Vector2.zero, Vector2.zero, 28, NeonGold, TextAnchor.MiddleCenter, true);
        inspectRarityText = CreateText(card.transform, "Rarity", "COMUM", new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.78f), Vector2.zero, Vector2.zero, 15, NeonCyan, TextAnchor.MiddleCenter, true);
        inspectLoreText = CreateText(card.transform, "Lore", "Descrição aqui...", new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.68f), Vector2.zero, Vector2.zero, 16, Color.white, TextAnchor.MiddleCenter, false);
        inspectStatsText = CreateText(card.transform, "Stats", "Capturas: 3\nPrimeira vez: 29/08/2026", new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.36f), Vector2.zero, Vector2.zero, 14, NeonCyan, TextAnchor.MiddleCenter, false);

        CreateArcadeButton(card.transform, "CloseInspectBtn", new Vector2(0.20f, 0.05f), new Vector2(0.80f, 0.16f), NeonGreen, Color.black, () => inspectModal.SetActive(false), "FECHAR");
    }

    private void OpenInspect(CollectionItem item)
    {
        if (inspectModal == null || item == null) return;
        if (!item.IsUnlocked)
        {
            inspectNameText.text = "??? (BLOQUEADO)";
            inspectRarityText.text = "Procure na vitrine do fliperama!";
            inspectRarityText.color = Color.gray;
            inspectLoreText.text = "Este bichinho ainda não foi capturado. Use as pinças de aço e mire com precisão no monte para conquistá-lo!";
            inspectStatsText.text = "Ainda não catalogado";
            inspectStatsText.color = Color.gray;
        }
        else
        {
            inspectNameText.text = $"{GetPlushieEmoji(item.id)} {item.displayName.ToUpperInvariant()}";
            inspectRarityText.text = item.rarity == PrizeRarity.Rare ? "★ RARIDADE: RARO ★" : item.rarity == PrizeRarity.Uncommon ? "★ RARIDADE: INCOMUM ★" : "RARIDADE: COMUM";
            inspectRarityText.color = item.themeColor;
            inspectLoreText.text = item.lore;
            inspectStatsText.text = $"Total Capturado: {item.count} vezes\nPrimeira Captura: {item.firstCapturedAt}";
            inspectStatsText.color = NeonCyan;
        }
        inspectModal.SetActive(true);
    }

    // ==================== TWEENS & ANIMAÇÕES UGUI ====================
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
        float duration = 0.18f;
        float elapsed = 0f;
        rect.localScale = Vector3.one * 0.90f;
        cg.alpha = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = Mathf.Sin(t * Mathf.PI * 0.5f);
            rect.localScale = Vector3.Lerp(Vector3.one * 0.90f, Vector3.one, ease);
            cg.alpha = Mathf.Lerp(0f, 1f, ease);
            yield return null;
        }
        rect.localScale = Vector3.one;
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
        float duration = 0.14f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rect.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.92f, t);
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

        if (!string.IsNullOrEmpty(label))
        {
            CreateText(btnObj.transform, "Label", label, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18, textColor, TextAnchor.MiddleCenter, true);
        }

        return btnObj;
    }

    private GameObject CreateGlassPill(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color glowBorder)
    {
        GameObject pill = CreatePanel(parent, name, GlassPill, aMin, aMax, Vector2.zero, Vector2.zero, true, GetRoundedRectSprite());
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
        outline.effectColor = new Color(0f, 0f, 0f, 0.78f);
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
