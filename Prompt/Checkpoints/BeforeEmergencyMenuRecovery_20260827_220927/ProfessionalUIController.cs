using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

    private void Update()
    {
        // Fallback defensivo para o menu: em algumas combinações do Input System
        // e do Game View o Button recebe foco visual, mas não recebe o evento Click.
        // Mantemos o listener normal e somente acionamos o fallback dentro do retângulo.
        if (menuPanel == null || !menuPanel.activeInHierarchy || actionButton == null) return;

        bool pressed = false;
        Vector2 screenPosition = default;
        if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
        {
            StartGame();
            return;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            pressed = true;
            screenPosition = Mouse.current.position.ReadValue();
        }
        else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            pressed = true;
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
        }

        if (pressed && RectTransformUtility.RectangleContainsScreenPoint(actionButton.GetComponent<RectTransform>(), screenPosition, null))
        {
            StartGame();
        }
    }

    private void OnGUI()
    {
        if (menuPanel == null || !menuPanel.activeInHierarchy || actionButton == null) return;

        // O menu uGUI continua sendo a interface principal. Este botão IMGUI
        // transparente cobre exatamente a área do botão visual e funciona mesmo
        // quando o módulo de UI do Input System não entrega PointerClick.
        // O Game View do Editor pode estar em escala 0.39x, enquanto Screen.width
        // continua reportando a resolução interna. Para não errar o retângulo por
        // causa dessa diferença, o fallback cobre o menu inteiro: nesse estado,
        // qualquer clique é semanticamente um pedido para iniciar a jogada.
        Rect menuFallbackRect = new Rect(0f, 0f, Screen.width, Screen.height);
        if (Event.current.type == EventType.MouseUp && menuFallbackRect.Contains(Event.current.mousePosition))
        {
            StartGame();
            Event.current.Use();
            return;
        }

        GUIStyle invisibleButton = GUIStyle.none;
        if (GUI.Button(menuFallbackRect, GUIContent.none, invisibleButton)) StartGame();
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
        if (controlsPanel != null) controlsPanel.SetActive(playing);
        if (gameOverPanel != null) gameOverPanel.SetActive(showGameOver);

        if (playing)
        {
            if (resultPanel != null) resultPanel.SetActive(false);
            SetStatus("MOVA A GARRA E ESCOLHA UM PRÊMIO", NeonCyan);
        }
        else if (busy)
        {
            SetStatus(state == GameState.Delivering ? "ENTREGA EM ANDAMENTO" : "AGUARDE A GARRA", NeonGold);
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
        actionText.text = closed ? "SOLTAR" : "FECHAR GARRA";
        if (actionSubText != null) actionSubText.text = closed ? "LIBERAR PRÊMIO" : "ACIONAR CAPTURA";
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
            SetStatus("SEM FICHAS — REINICIE PARA TESTE", NeonRed);
            return;
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
        CreateText(parent, "GameTitle", "GARRAMANIA", new Vector2(0.38f, 0.91f), new Vector2(0.62f, 0.99f), Vector2.zero, Vector2.zero, 32, NeonGold, TextAnchor.MiddleCenter, true);
        GameObject credits = CreatePanel(parent, "Credits", Panel, new Vector2(0.03f, 0.90f), new Vector2(0.20f, 0.985f), Vector2.zero, Vector2.zero, true);
        creditsText = CreateText(credits.transform, "CreditsText", "3 FICHAS", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 20, NeonGold, TextAnchor.MiddleCenter, true);
        GameObject hsPanel = CreatePanel(parent, "HighScorePanel", Panel, new Vector2(0.21f, 0.90f), new Vector2(0.37f, 0.985f), Vector2.zero, Vector2.zero, true);
        highScoreText = CreateText(hsPanel.transform, "HighScoreText", "RECORDE: 0", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18, NeonGold, TextAnchor.MiddleCenter, true);
        GameObject prizes = CreatePanel(parent, "Prizes", Panel, new Vector2(0.77f, 0.90f), new Vector2(0.97f, 0.985f), Vector2.zero, Vector2.zero, true);
        prizesText = CreateText(prizes.transform, "PrizesText", "0 PRÊMIOS", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 22, NeonGreen, TextAnchor.MiddleCenter, true);

        GameObject timer = CreatePanel(parent, "Timer", Panel, new Vector2(0.40f, 0.77f), new Vector2(0.60f, 0.88f), Vector2.zero, Vector2.zero, true);
        timerFill = CreatePanel(timer.transform, "TimerFill", NeonCyan, new Vector2(0.04f, 0.20f), new Vector2(0.96f, 0.80f), Vector2.zero, Vector2.zero, false).GetComponent<Image>();
        timerFillRect = timerFill.rectTransform;
        timerFill.type = Image.Type.Simple;
        CreatePanel(timer.transform, "TimerCore", new Color(0.015f, 0.025f, 0.055f, 0.96f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false).transform.SetAsFirstSibling();
        timerText = CreateText(timer.transform, "TimerText", "45", new Vector2(0.42f, 0.04f), new Vector2(0.58f, 0.96f), Vector2.zero, Vector2.zero, 28, Color.white, TextAnchor.MiddleCenter, true);
        statusText = CreateText(parent, "Status", "FICHAS PRONTAS", new Vector2(0.25f, 0.075f), new Vector2(0.75f, 0.135f), Vector2.zero, Vector2.zero, 16, NeonCyan, TextAnchor.MiddleCenter, true);
    }

    private void BuildControls(Transform parent)
    {
        GameObject joystick = CreatePanel(parent, "VirtualJoystick", new Color(0.02f, 0.06f, 0.13f, 0.88f), new Vector2(0.035f, 0.035f), new Vector2(0.28f, 0.30f), Vector2.zero, Vector2.zero, true);
        GameObject handle = CreatePanel(joystick.transform, "Handle", NeonCyan, new Vector2(0.34f, 0.34f), new Vector2(0.66f, 0.66f), Vector2.zero, Vector2.zero, true);
        handle.GetComponent<Image>().raycastTarget = false;
        VirtualJoystickView joystickView = joystick.AddComponent<VirtualJoystickView>();
        joystickView.Configure(joystick.GetComponent<RectTransform>(), handle.GetComponent<RectTransform>(), 72f);
        CreateText(joystick.transform, "JoystickLabel", "MOVER", new Vector2(0.12f, 0.04f), new Vector2(0.88f, 0.18f), Vector2.zero, Vector2.zero, 14, Color.white, TextAnchor.MiddleCenter, true);

        GameObject action = CreateButton(parent, "ActionButton", NeonMagenta, new Vector2(0.77f, 0.035f), new Vector2(0.965f, 0.25f));
        actionButton = action.GetComponent<Button>();
        actionButton.onClick.AddListener(() => InputRouter.Instance?.TriggerTouchAction());
        actionText = CreateText(action.transform, "ActionText", "FECHAR GARRA", new Vector2(0.04f, 0.42f), new Vector2(0.96f, 0.86f), Vector2.zero, Vector2.zero, 26, Color.white, TextAnchor.MiddleCenter, true);
        actionSubText = CreateText(action.transform, "ActionSubText", "ACIONAR CAPTURA", new Vector2(0.04f, 0.12f), new Vector2(0.96f, 0.40f), Vector2.zero, Vector2.zero, 13, Color.white, TextAnchor.MiddleCenter, false);

        GameObject up = CreateButton(parent, "UpButton", NeonCyan, new Vector2(0.625f, 0.18f), new Vector2(0.755f, 0.27f));
        up.GetComponent<Button>().interactable = true;
        HoldInputButton upHold = up.AddComponent<HoldInputButton>();
        upHold.Configure(1f);
        CreateText(up.transform, "UpText", "SUBIR", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 16, Color.white, TextAnchor.MiddleCenter, true);
        GameObject down = CreateButton(parent, "DownButton", NeonGold, new Vector2(0.625f, 0.07f), new Vector2(0.755f, 0.16f));
        HoldInputButton downHold = down.AddComponent<HoldInputButton>();
        downHold.Configure(-1f);
        CreateText(down.transform, "DownText", "DESCER", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 16, Color.white, TextAnchor.MiddleCenter, true);

        GameObject camera = CreateButton(parent, "CameraButton", PanelLight, new Vector2(0.78f, 0.27f), new Vector2(0.96f, 0.33f));
        cameraButton = camera.GetComponent<Button>();
        cameraButton.onClick.AddListener(() => cameraController?.ToggleCameraAngle());
        cameraText = CreateText(camera.transform, "CameraText", "CÂMERA: FRENTE", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 14, NeonCyan, TextAnchor.MiddleCenter, true);

        GameObject resetBtn = CreateButton(parent, "ResetButton", PanelLight, new Vector2(0.78f, 0.34f), new Vector2(0.96f, 0.40f));
        resetBtn.GetComponent<Button>().onClick.AddListener(() => {
            if (InputHandler.Instance != null) InputHandler.Instance.TriggerReset();
            else FindAnyObjectByType<ClawController>()?.ResetarGarra();
        });
        CreateText(resetBtn.transform, "ResetText", "RESET GARRA", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 13, Color.white, TextAnchor.MiddleCenter, true);

        GameObject forceBtn = CreateButton(parent, "ForceButton", PanelLight, new Vector2(0.625f, 0.28f), new Vector2(0.755f, 0.34f));
        forceText = CreateText(forceBtn.transform, "ForceText", "FORÇA: 100%", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 12, NeonGreen, TextAnchor.MiddleCenter, true);
        forceBtn.GetComponent<Button>().onClick.AddListener(() => {
            float cur = InputHandler.Instance != null ? InputHandler.Instance.CurrentForce : 1f;
            float next = cur > 0.4f ? cur - 0.2f : 1.0f;
            if (InputHandler.Instance != null) InputHandler.Instance.SetForce(next);
            if (forceText != null) forceText.text = $"FORÇA: {Mathf.RoundToInt(next * 100)}%";
        });
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
