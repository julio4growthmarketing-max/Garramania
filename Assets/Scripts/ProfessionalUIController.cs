using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI principal do GarraMania.
/// A hierarquia é criada uma única vez, mas o gameplay não é criado aqui:
/// a UI apenas apresenta a sessão e envia comandos ao InputRouter.
/// Esta versão usa uGUI nativo para funcionar mesmo antes da importação do TMP Essentials.
/// </summary>
public sealed class ProfessionalUIController : MonoBehaviour
{
    private static readonly Color NeonCyan = new Color(0.00f, 0.93f, 1.00f, 1f);
    private static readonly Color NeonMagenta = new Color(1.00f, 0.08f, 0.58f, 1f);
    private static readonly Color NeonGold = new Color(1.00f, 0.83f, 0.12f, 1f);
    private static readonly Color NeonGreen = new Color(0.12f, 1.00f, 0.52f, 1f);
    private static readonly Color NeonRed = new Color(1.00f, 0.18f, 0.28f, 1f);
    private static readonly Color Panel = new Color(0.025f, 0.045f, 0.09f, 0.92f);
    private static readonly Color PanelLight = new Color(0.07f, 0.12f, 0.22f, 0.95f);

    private GameObject canvasRoot;
    private GameObject menuPanel;
    private GameObject hudPanel;
    private GameObject controlsPanel;
    private GameObject resultPanel;
    private GameObject gameOverPanel;
    private Text creditsText;
    private Text prizesText;
    private Text timerText;
    private Text highScoreText;
    private Text forceText;
    private Image timerFill;
    private RectTransform timerFillRect;
    private Text statusText;
    private Text actionText;
    private Text actionSubText;
    private Text cameraText;
    private Text resultNameText;
    private Text resultMessageText;
    private Text gameOverMessageText;
    private Button actionButton;
    private Button cameraButton;
    private ClawController claw;
    private GameSession session;
    private ClawCameraController cameraController;
    private Coroutine popupRoutine;
    private GameObject prizePopup;
    private Font uiFont;
    private bool built;
    private float lastStartRequestTime = -10f;

    private void Start()
    {
        if (built) return;
        built = true;

        session = GameSession.Instance;
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
            HandleHighScoreChanged(session.HighScore);
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
    }

    private void OnDestroy()
    {
        if (session != null)
        {
            session.OnStateChanged.RemoveListener(HandleStateChanged);
            session.OnTimeChanged.RemoveListener(HandleTimeChanged);
            session.OnCreditsChanged.RemoveListener(HandleCreditsChanged);
            session.OnHighScoreChanged.RemoveListener(HandleHighScoreChanged);
            session.OnPrizeDelivered.RemoveListener(HandlePrizeDelivered);
            session.OnPrizeWonShowResult.RemoveListener(HandlePrizeResult);
            session.OnGameOver.RemoveListener(HandleGameOver);
        }
        if (claw != null) claw.OnClawStateChanged.RemoveListener(HandleClawStateChanged);
        if (cameraController != null) cameraController.OnCameraAngleChanged.RemoveListener(HandleCameraAngleChanged);
    }

    private void ConnectSession()
    {
        if (session == null) return;
        session.OnStateChanged.AddListener(HandleStateChanged);
        session.OnTimeChanged.AddListener(HandleTimeChanged);
        session.OnCreditsChanged.AddListener(HandleCreditsChanged);
        session.OnHighScoreChanged.AddListener(HandleHighScoreChanged);
        session.OnPrizeDelivered.AddListener(HandlePrizeDelivered);
        session.OnPrizeWonShowResult.AddListener(HandlePrizeResult);
        session.OnGameOver.AddListener(HandleGameOver);
    }

    private void HandleStateChanged(GameState state)
    {
        bool playing = state == GameState.Playing;
        bool busy = state == GameState.Capturing || state == GameState.Returning || state == GameState.Delivering;
        bool showMenu = state == GameState.Idle;
        bool showGameOver = state == GameState.GameOver;

        if (menuPanel != null) menuPanel.SetActive(showMenu);
        if (hudPanel != null) hudPanel.SetActive(playing || busy);
        // Os controles permanecem na tela durante a partida inteira!
        if (controlsPanel != null) controlsPanel.SetActive(playing || busy);
        if (gameOverPanel != null) gameOverPanel.SetActive(showGameOver);

        if (actionButton != null)
        {
            actionButton.interactable = playing;
        }

        if (playing)
        {
            if (resultPanel != null) resultPanel.SetActive(false);
            SetStatus("MOVA A GARRA E APERTE PARA DESCER", NeonCyan);
        }
        else if (busy)
        {
            string msg = "AGUARDE A GARRA";
            ClawController c = FindFirstObjectByType<ClawController>();
            bool hasPrize = c != null && c.HasPrize;

            if (state == GameState.Capturing) msg = "DESCENDO GARRA...";
            else if (state == GameState.Returning) msg = hasPrize ? "SUBINDO COM O PRÊMIO!" : "RECOLHENDO GARRA...";
            else if (state == GameState.Delivering) msg = "ENTREGANDO NA CALHA!";
            SetStatus(msg, hasPrize ? NeonGreen : NeonGold);
        }
        else if (showMenu)
        {
            if (resultPanel != null) resultPanel.SetActive(false);
            SetStatus(session != null && session.Credits > 0 ? "FICHAS PRONTAS" : "SEM FICHAS DISPONÍVEIS", session != null && session.Credits > 0 ? NeonGreen : NeonRed);
        }
    }

    private void HandleTimeChanged(float remaining, float maximum)
    {
        if (timerText == null) return;
        float normalized = maximum > 0f ? Mathf.Clamp01(remaining / maximum) : 0f;
        timerText.text = Mathf.CeilToInt(remaining).ToString();
        if (timerFillRect != null)
        {
            timerFillRect.anchorMax = new Vector2(Mathf.Lerp(0.04f, 0.96f, normalized), 0.80f);
        }

        if (remaining <= 5f)
        {
            timerText.color = NeonRed;
            if (timerFill != null) timerFill.color = NeonRed;
        }
        else if (remaining <= 15f)
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
        if (creditsText != null) creditsText.text = value + " FICHAS";
    }

    private void HandleHighScoreChanged(int value)
    {
        if (highScoreText != null) highScoreText.text = "RECORDE: " + value;
    }

    private void HandlePrizeDelivered(Prize prize, int total)
    {
        if (prizesText != null) prizesText.text = total + " PRÊMIOS";
        ShowPrizePopup(prize);
    }

    private void HandlePrizeResult(Prize prize)
    {
        if (resultPanel == null) return;
        if (resultNameText != null) resultNameText.text = GetPrizeName(prize);
        if (resultMessageText != null)
        {
            int total = session != null ? session.PrizesWon : 1;
            resultMessageText.text = "ADICIONADO À COLEÇÃO\nTOTAL: " + total + " PRÊMIOS";
        }
        resultPanel.SetActive(true);
        if (controlsPanel != null) controlsPanel.SetActive(false);
    }

    private void HandleGameOver()
    {
        if (controlsPanel != null) controlsPanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(false);
        if (gameOverMessageText != null)
            gameOverMessageText.text = session != null && session.Credits > 0 ? "TENTE NOVAMENTE" : "SEM FICHAS DISPONÍVEIS";
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    private void HandleClawStateChanged(bool closed)
    {
        if (actionText == null) return;
        actionText.text = closed ? "SOLTAR" : "AGARRAR";
        if (actionSubText != null) actionSubText.text = closed ? "LIBERAR PRÊMIO" : "CAPTURAR BICHINHO";
        if (actionButton != null)
        {
            ColorBlock colors = actionButton.colors;
            colors.normalColor = closed ? new Color(0.98f, 0.58f, 0.08f, 1f) : NeonMagenta;
            colors.highlightedColor = Color.white;
            colors.pressedColor = closed ? NeonGold : new Color(1f, 0.32f, 0.72f, 1f);
            actionButton.colors = colors;
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
        if (session == null)
        {
            Debug.LogError("[ProfessionalUI] StartGame sem GameSession disponível.");
            return;
        }
        Debug.Log($"[ProfessionalUI] INICIAR JOGADA acionado. Estado: {session.CurrentState}; fichas: {session.Credits}");
        if (session.Credits <= 0)
        {
            session.ResetCredits(3);
        }
        session.StartGame();
    }

    public void ContinueAfterResult()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
        session?.ResetSession();
        InputRouter.Instance?.SetBlocked(true);
    }

    public void RestartAfterGameOver()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        session?.ResetSession();
        InputRouter.Instance?.SetBlocked(true);
    }

    private void SetStatus(string value, Color color)
    {
        if (statusText == null) return;
        statusText.text = value;
        statusText.color = color;
    }

    private string GetPrizeName(Prize prize)
    {
        if (prize == null || string.IsNullOrEmpty(prize.prizeId)) return "PRÊMIO SURPRESA";
        string rarity = prize.Rarity == PrizeRarity.Rare ? " • RARO" : prize.Rarity == PrizeRarity.Uncommon ? " • INCOMUM" : prize.Rarity == PrizeRarity.Legendary ? " • LENDÁRIO" : " • COMUM";
        return prize.prizeId.Replace("_", " ").ToUpperInvariant() + rarity;
    }

    private void ShowPrizePopup(Prize prize)
    {
        if (prizePopup == null) return;
        Text popupText = prizePopup.GetComponentInChildren<Text>();
        if (popupText != null) popupText.text = "PRÊMIO ENTREGUE: " + GetPrizeName(prize);
        prizePopup.SetActive(true);
        if (popupRoutine != null) StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(HidePopup());
    }

    private IEnumerator HidePopup()
    {
        yield return new WaitForSecondsRealtime(2.4f);
        if (prizePopup != null) prizePopup.SetActive(false);
    }

    private void BuildInterface()
    {
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        canvasRoot = new GameObject("GarraManiaUI_Runtime");
        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.65f;
        canvasRoot.AddComponent<GraphicRaycaster>();

        GameObject safeArea = CreatePanel(canvas.transform, "SafeArea", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        safeArea.AddComponent<SafeAreaFitter>();
        Transform root = safeArea.transform;

        GameObject background = CreatePanel(root, "OverlayTint", new Color(0.015f, 0.02f, 0.05f, 0.10f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        background.transform.SetAsFirstSibling();

        hudPanel = CreatePanel(root, "HUD", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildHud(hudPanel.transform);
        controlsPanel = CreatePanel(root, "Controls", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildControls(controlsPanel.transform);
        menuPanel = CreatePanel(root, "Menu", new Color(0.015f, 0.025f, 0.06f, 0.88f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildMenu(menuPanel.transform);
        resultPanel = CreatePanel(root, "Result", new Color(0.01f, 0.02f, 0.05f, 0.94f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildResult(resultPanel.transform);
        gameOverPanel = CreatePanel(root, "GameOver", new Color(0.01f, 0.02f, 0.05f, 0.92f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        BuildGameOver(gameOverPanel.transform);
        prizePopup = CreatePanel(root, "PrizePopup", new Color(0.06f, 0.16f, 0.28f, 0.96f), new Vector2(0.28f, 0.64f), new Vector2(0.72f, 0.74f), Vector2.zero, Vector2.zero, true);
        CreateText(prizePopup.transform, "PrizePopupText", "PRÊMIO ENTREGUE", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 22, NeonGold, TextAnchor.MiddleCenter, true);
        prizePopup.SetActive(false);
    }

    private void BuildHud(Transform parent)
    {
        // HUD Minimalista Superior (Transparente estilo vitrine arcade)
        Color barBg = new Color(0.02f, 0.04f, 0.08f, 0.55f);

        // Fichas (Canto Superior Esquerdo)
        GameObject credits = CreatePanel(parent, "Credits", barBg, new Vector2(0.025f, 0.915f), new Vector2(0.18f, 0.98f), Vector2.zero, Vector2.zero, true);
        creditsText = CreateText(credits.transform, "CreditsText", "3 FICHAS", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18, NeonGold, TextAnchor.MiddleCenter, true);

        // Timer Central Digital
        GameObject timer = CreatePanel(parent, "Timer", barBg, new Vector2(0.42f, 0.905f), new Vector2(0.58f, 0.985f), Vector2.zero, Vector2.zero, true);
        timerFill = CreatePanel(timer.transform, "TimerFill", NeonCyan, new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero, false).GetComponent<Image>();
        timerFillRect = timerFill.rectTransform;
        timerFill.type = Image.Type.Simple;
        CreatePanel(timer.transform, "TimerCore", new Color(0.01f, 0.02f, 0.04f, 0.85f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false).transform.SetAsFirstSibling();
        timerText = CreateText(timer.transform, "TimerText", "45", new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero, 26, Color.white, TextAnchor.MiddleCenter, true);

        // Prêmios Coletados (Canto Superior Direito)
        GameObject prizes = CreatePanel(parent, "Prizes", barBg, new Vector2(0.82f, 0.915f), new Vector2(0.975f, 0.98f), Vector2.zero, Vector2.zero, true);
        prizesText = CreateText(prizes.transform, "PrizesText", "0 PRÊMIOS", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18, NeonGreen, TextAnchor.MiddleCenter, true);

        // Status Discreto
        statusText = CreateText(parent, "Status", "PRONTO PARA JOGAR", new Vector2(0.28f, 0.85f), new Vector2(0.72f, 0.90f), Vector2.zero, Vector2.zero, 14, NeonCyan, TextAnchor.MiddleCenter, false);
    }

    private void BuildControls(Transform parent)
    {
        // 1. Joystick Virtual Estilo Arcade Clássico (Canto Inferior Esquerdo)
        Color joyBg = new Color(0.02f, 0.05f, 0.12f, 0.55f);
        GameObject joystick = CreatePanel(parent, "VirtualJoystick", joyBg, new Vector2(0.03f, 0.04f), new Vector2(0.24f, 0.29f), Vector2.zero, Vector2.zero, true);
        GameObject handle = CreatePanel(joystick.transform, "Handle", new Color(0f, 0.95f, 1f, 0.90f), new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f), Vector2.zero, Vector2.zero, true);
        handle.GetComponent<Image>().raycastTarget = false;
        VirtualJoystickView joystickView = joystick.AddComponent<VirtualJoystickView>();
        joystickView.Configure(joystick.GetComponent<RectTransform>(), handle.GetComponent<RectTransform>(), 70f);
        CreateText(joystick.transform, "JoystickLabel", "JOYSTICK ARCADE", new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.18f), Vector2.zero, Vector2.zero, 12, new Color(1f, 1f, 1f, 0.90f), TextAnchor.MiddleCenter, false);

        // 2. Botão Arcade Único de Ação (Canto Inferior Direito) - Padrão de Máquina Real
        GameObject action = CreateButton(parent, "ActionButton", NeonMagenta, new Vector2(0.74f, 0.04f), new Vector2(0.97f, 0.29f));
        actionButton = action.GetComponent<Button>();
        actionButton.onClick.AddListener(() => InputRouter.Instance?.TriggerTouchAction());
        actionText = CreateText(action.transform, "ActionText", "DESCER GARRA", new Vector2(0.04f, 0.44f), new Vector2(0.96f, 0.88f), Vector2.zero, Vector2.zero, 26, Color.white, TextAnchor.MiddleCenter, true);
        actionSubText = CreateText(action.transform, "ActionSubText", "CAPTURAR BICHINHO", new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.44f), Vector2.zero, Vector2.zero, 13, new Color(1f, 1f, 1f, 0.90f), TextAnchor.MiddleCenter, false);

        // 3. Botões de Espiada Rápida (Peeking) e Câmera no centro inferior
        Color peekBtnColor = new Color(0.03f, 0.08f, 0.16f, 0.65f);
        GameObject peekLeft = CreateButton(parent, "PeekLeftButton", peekBtnColor, new Vector2(0.33f, 0.04f), new Vector2(0.46f, 0.12f));
        peekLeft.GetComponent<Button>().onClick.AddListener(() => cameraController?.LeanLeft());
        CreateText(peekLeft.transform, "PeekLeftText", "◄ ESPIAR", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13, NeonCyan, TextAnchor.MiddleCenter, true);

        GameObject peekRight = CreateButton(parent, "PeekRightButton", peekBtnColor, new Vector2(0.54f, 0.04f), new Vector2(0.67f, 0.12f));
        peekRight.GetComponent<Button>().onClick.AddListener(() => cameraController?.LeanRight());
        CreateText(peekRight.transform, "PeekRightText", "ESPIAR ►", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13, NeonCyan, TextAnchor.MiddleCenter, true);

        GameObject camera = CreateButton(parent, "CameraButton", peekBtnColor, new Vector2(0.435f, 0.13f), new Vector2(0.565f, 0.19f));
        cameraButton = camera.GetComponent<Button>();
        cameraButton.onClick.AddListener(() => cameraController?.ToggleCameraAngle());
        cameraText = CreateText(camera.transform, "CameraText", "CÂMERA", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13, NeonGold, TextAnchor.MiddleCenter, true);

        // 4. Faixa de Dicas de Teclado PC estilo Arcade
        GameObject hints = CreatePanel(parent, "KeyboardHints", new Color(0.01f, 0.03f, 0.08f, 0.75f), new Vector2(0.24f, 0.005f), new Vector2(0.76f, 0.035f), Vector2.zero, Vector2.zero, false);
        CreateText(hints.transform, "HintsText", "[WASD / Setas] Mover  •  [Espaço] Descer Garra / Agarrar  •  [Q / E] Espiar", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 12, new Color(0.85f, 0.95f, 1f, 0.95f), TextAnchor.MiddleCenter, false);
    }

    private void BuildMenu(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "MenuPanel", Panel, new Vector2(0.24f, 0.16f), new Vector2(0.76f, 0.84f), Vector2.zero, Vector2.zero, true);
        CreateText(panel.transform, "Logo", "GARRAMANIA", new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.86f), Vector2.zero, Vector2.zero, 48, NeonGold, TextAnchor.MiddleCenter, true);
        CreateText(panel.transform, "Subtitle", "CAPTURE. COLECIONE. VOLTE A JOGAR.", new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.68f), Vector2.zero, Vector2.zero, 15, NeonCyan, TextAnchor.MiddleCenter, true);
        CreateText(panel.transform, "Instruction", "MOVA A GARRA, MIRE NO BICHINHO E FECHE A GARRA", new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.52f), Vector2.zero, Vector2.zero, 17, Color.white, TextAnchor.MiddleCenter, false);
        GameObject play = CreateButton(panel.transform, "PlayButton", NeonGreen, new Vector2(0.22f, 0.17f), new Vector2(0.78f, 0.34f));
        play.GetComponent<Button>().onClick.AddListener(StartGame);
        CreateText(play.transform, "PlayText", "INICIAR JOGADA", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 25, Color.white, TextAnchor.MiddleCenter, true);
    }

    private void BuildResult(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "ResultPanel", Panel, new Vector2(0.22f, 0.14f), new Vector2(0.78f, 0.86f), Vector2.zero, Vector2.zero, true);
        CreateText(panel.transform, "ResultTitle", "PRÊMIO CAPTURADO!", new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.90f), Vector2.zero, Vector2.zero, 34, NeonGold, TextAnchor.MiddleCenter, true);
        resultNameText = CreateText(panel.transform, "ResultName", "PRÊMIO SURPRESA", new Vector2(0.05f, 0.54f), new Vector2(0.95f, 0.70f), Vector2.zero, Vector2.zero, 28, Color.white, TextAnchor.MiddleCenter, true);
        resultMessageText = CreateText(panel.transform, "ResultMessage", "ADICIONADO À COLEÇÃO", new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.54f), Vector2.zero, Vector2.zero, 18, NeonCyan, TextAnchor.MiddleCenter, false);
        GameObject continueButton = CreateButton(panel.transform, "ContinueButton", NeonGreen, new Vector2(0.22f, 0.14f), new Vector2(0.78f, 0.29f));
        continueButton.GetComponent<Button>().onClick.AddListener(ContinueAfterResult);
        CreateText(continueButton.transform, "ContinueText", "CONTINUAR", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 22, Color.white, TextAnchor.MiddleCenter, true);
        parent.gameObject.SetActive(false);
    }

    private void BuildGameOver(Transform parent)
    {
        GameObject panel = CreatePanel(parent, "GameOverPanel", Panel, new Vector2(0.22f, 0.20f), new Vector2(0.78f, 0.80f), Vector2.zero, Vector2.zero, true);
        CreateText(panel.transform, "GameOverTitle", "FIM DA JOGADA", new Vector2(0.05f, 0.67f), new Vector2(0.95f, 0.83f), Vector2.zero, Vector2.zero, 38, NeonRed, TextAnchor.MiddleCenter, true);
        gameOverMessageText = CreateText(panel.transform, "GameOverMessage", "TENTE NOVAMENTE", new Vector2(0.05f, 0.49f), new Vector2(0.95f, 0.63f), Vector2.zero, Vector2.zero, 20, Color.white, TextAnchor.MiddleCenter, false);
        GameObject retry = CreateButton(panel.transform, "RetryButton", NeonCyan, new Vector2(0.22f, 0.18f), new Vector2(0.78f, 0.34f));
        retry.GetComponent<Button>().onClick.AddListener(RestartAfterGameOver);
        CreateText(retry.transform, "RetryText", "VOLTAR AO MENU", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 21, Color.white, TextAnchor.MiddleCenter, true);
        parent.gameObject.SetActive(false);
    }

    private GameObject CreateButton(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject buttonObject = CreatePanel(parent, name, color, anchorMin, anchorMax, Vector2.zero, Vector2.zero, true);
        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.white;
        colors.pressedColor = color * 0.75f;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(color.r, color.g, color.b, 0.35f);
        colors.fadeDuration = 0.04f;
        button.colors = colors;
        return buttonObject;
    }

    private GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, bool raycast)
    {
        GameObject objectToCreate = new GameObject(name);
        objectToCreate.transform.SetParent(parent, false);
        RectTransform rect = objectToCreate.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.offsetMin = size == Vector2.zero ? Vector2.zero : -size * 0.5f;
        rect.offsetMax = size == Vector2.zero ? Vector2.zero : size * 0.5f;
        Image image = objectToCreate.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return objectToCreate;
    }

    private Text CreateText(Transform parent, string name, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment, bool bold)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.offsetMin = size == Vector2.zero ? Vector2.zero : -size * 0.5f;
        rect.offsetMax = size == Vector2.zero ? Vector2.zero : size * 0.5f;
        Text text = textObject.AddComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        return text;
    }
}
