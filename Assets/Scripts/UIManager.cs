using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// UIManager do GarraMania com Estética Neon Arcade e Layout Responsivo (Landscape & Portrait).
/// Utiliza componentes nativos seguros (UnityEngine.UI.Text + Outline + Shadow) com renderização
/// garantida em 100% das plataformas, sem dependência de modais ou pacotes adicionais.
/// </summary>
public class UIManager : MonoBehaviour
{
    private float tempoPremio = 0f;

    // Componentes de Texto
    private Text txtCreditos, txtPremiosGanhos, txtNumTimer, txtPopup, txtDicaFicha, txtHighScore, txtForcaVal;
    private Image barraForcaFill;
    private Image barraTimer;
    private Image bordaTimer;
    private GameObject painelInicio;
    private GameObject painelControles;

    // ===== BOTÃO DE AÇÃO DINÂMICO (PEGAR ⇄ SOLTAR) =====
    private Text txtBtnAcaoPrincipal;
    private Text txtBtnAcaoSub;
    private Image imgBtnAcaoCore;
    private Image imgBtnAcaoGlow;

    // ===== BOTÃO DE CÂMERA (FRENTE ⇄ LADO) =====
    private Text txtBtnCam;

    // ===== TELA DE RESULTADO =====
    private GameObject painelResultado;
    private Text txtResultadoTitulo;
    private Text txtResultadoNome;
    private Text txtResultadoMsg;

    // ===== COUNTDOWN PULSE =====
    private RectTransform timerRT;
    private Coroutine timerPulseCoroutine;
    private bool isTimerPulsing = false;

    // ===== CORES DO DESIGN SYSTEM NEON ARCADE =====
    private static readonly Color NEON_CYAN = new Color(0f, 0.95f, 1f, 1f);
    private static readonly Color NEON_MAGENTA = new Color(1f, 0.08f, 0.58f, 1f);
    private static readonly Color NEON_GOLD = new Color(1f, 0.88f, 0.12f, 1f);
    private static readonly Color NEON_GREEN = new Color(0.1f, 1f, 0.55f, 1f);
    private static readonly Color NEON_RED = new Color(1f, 0.15f, 0.25f, 1f);
    private static readonly Color BG_DARK_GLASS = new Color(0.04f, 0.05f, 0.09f, 0.85f);
    private static readonly Color BG_CARD_SLATE = new Color(0.08f, 0.11f, 0.18f, 0.92f);
    private static readonly Color BORDER_CYAN_GLOW = new Color(0f, 0.95f, 1f, 0.55f);
    private static readonly Color BORDER_GOLD_GLOW = new Color(1f, 0.85f, 0.1f, 0.65f);

    private Font fontArcade;

    void Awake()
    {
        fontArcade = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fontArcade == null) fontArcade = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    void Start()
    {
        ConstruirUI();
        ConectarGameSession();
    }

    void ConectarGameSession()
    {
        if (GameSession.Instance != null)
        {
            if (GameSession.Instance.OnStateChanged != null)
                GameSession.Instance.OnStateChanged.AddListener(OnGameStateChanged);
            if (GameSession.Instance.OnTimeChanged != null)
                GameSession.Instance.OnTimeChanged.AddListener(AtualizarTimer);
            if (GameSession.Instance.OnCreditsChanged != null)
                GameSession.Instance.OnCreditsChanged.AddListener(AtualizarCreditos);
            if (GameSession.Instance.OnPrizeDelivered != null)
                GameSession.Instance.OnPrizeDelivered.AddListener(OnPrizeDelivered);
            if (GameSession.Instance.OnHighScoreChanged != null)
                GameSession.Instance.OnHighScoreChanged.AddListener(AtualizarHighScore);

            if (GameSession.Instance.OnPrizeWonShowResult != null)
                GameSession.Instance.OnPrizeWonShowResult.AddListener(MostrarTelaResultado);

            // Conectar estado da garra para o botão inteligente PEGAR ⇄ SOLTAR
            ClawController claw = FindAnyObjectByType<ClawController>();
            if (claw != null)
            {
                claw.OnClawStateChanged.AddListener(AtualizarBotaoAcao);
                AtualizarBotaoAcao(claw.IsClosed);
            }

            // Conectar estado da câmera para o botão FRENTE ⇄ LADO
            if (ClawCameraController.Instance != null)
            {
                ClawCameraController.Instance.OnCameraAngleChanged.AddListener(AtualizarBotaoCamera);
                AtualizarBotaoCamera(ClawCameraController.Instance.CurrentAngle);
            }

            AtualizarCreditos(GameSession.Instance.Credits);
            AtualizarHighScore(GameSession.Instance.HighScore);
            AtualizarTimer(GameSession.Instance.TimeRemaining, GameSession.Instance.MaxSessionTime);
            OnGameStateChanged(GameSession.Instance.CurrentState);
        }
        else
        {
            MostrarTelaInicio();
        }
    }

    void Update()
    {
        if (tempoPremio > 0f)
        {
            tempoPremio -= Time.deltaTime;
            if (tempoPremio <= 0f && txtPopup != null) txtPopup.gameObject.SetActive(false);
        }

        // Permite iniciar pelo teclado (Espaço ou Enter)
        if (painelInicio != null && painelInicio.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || (InputRouter.Instance != null && InputRouter.Instance.ActionTriggered))
            {
                IniciarJogo();
            }
        }
    }

    public void IniciarJogo()
    {
        if (painelInicio != null) painelInicio.SetActive(false);
        if (painelResultado != null) painelResultado.SetActive(false);
        if (painelControles != null) painelControles.SetActive(true);

        if (GameSession.Instance != null)
        {
            if (GameSession.Instance.Credits <= 0)
            {
                GameSession.Instance.ResetCredits(3);
            }
            GameSession.Instance.StartGame();
        }
    }

    void OnGameStateChanged(GameState state)
    {
        // Controles arcade sempre visíveis na tela para o jogador!
        if (painelControles != null) painelControles.SetActive(true);
        if (painelInicio != null) painelInicio.SetActive(false);

        if (state == GameState.Playing)
        {
            if (painelResultado != null) painelResultado.SetActive(false);
            isTimerPulsing = false;
        }
        else if (state == GameState.GameOver)
        {
            isTimerPulsing = false;
            StartCoroutine(MostrarTelaGameOverComDelay());
        }
    }

    IEnumerator MostrarTelaGameOverComDelay()
    {
        yield return new WaitForSecondsRealtime(0.6f);

        if (painelResultado == null || !painelResultado.activeSelf)
        {
            MostrarTelaInicio();
            if (txtDicaFicha != null)
            {
                txtDicaFicha.text = "⏰ O tempo acabou! Tente novamente!";
                txtDicaFicha.color = NEON_RED;
            }
        }
    }

    void AtualizarTimer(float remaining, float max)
    {
        if (barraTimer == null || txtNumTimer == null) return;
        float fill = max > 0f ? remaining / max : 0f;
        barraTimer.fillAmount = fill;
        txtNumTimer.text = Mathf.CeilToInt(remaining).ToString();

        if (remaining > 15f)
        {
            barraTimer.color = Color.Lerp(NEON_GREEN, NEON_CYAN, fill);
            txtNumTimer.color = Color.white;
            txtNumTimer.fontSize = 30;
            if (bordaTimer != null) bordaTimer.color = BORDER_CYAN_GLOW;
        }
        else if (remaining > 5f)
        {
            barraTimer.color = new Color(1f, 0.65f, 0.1f);
            txtNumTimer.color = NEON_GOLD;
            txtNumTimer.fontSize = 32;
            if (bordaTimer != null) bordaTimer.color = new Color(1f, 0.65f, 0.1f, 0.7f);
        }
        else
        {
            barraTimer.color = NEON_RED;
            txtNumTimer.color = new Color(1f, 0.3f, 0.4f);
            txtNumTimer.fontSize = 36;
            if (bordaTimer != null) bordaTimer.color = new Color(1f, 0.15f, 0.25f, 0.95f);

            if (!isTimerPulsing && timerRT != null)
            {
                isTimerPulsing = true;
                if (timerPulseCoroutine != null) StopCoroutine(timerPulseCoroutine);
                timerPulseCoroutine = StartCoroutine(PulseTimer());
            }
        }
    }

    IEnumerator PulseTimer()
    {
        Vector3 originalScale = timerRT != null ? timerRT.localScale : Vector3.one;
        while (isTimerPulsing && timerRT != null)
        {
            float t = 0f;
            while (t < 0.15f && timerRT != null)
            {
                t += Time.unscaledDeltaTime;
                timerRT.localScale = Vector3.Lerp(originalScale, originalScale * 1.25f, t / 0.15f);
                yield return null;
            }
            t = 0f;
            while (t < 0.15f && timerRT != null)
            {
                t += Time.unscaledDeltaTime;
                timerRT.localScale = Vector3.Lerp(originalScale * 1.25f, originalScale, t / 0.15f);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.2f);
        }
        if (timerRT != null) timerRT.localScale = originalScale;
    }

    void AtualizarCreditos(int credits)
    {
        if (txtCreditos != null) txtCreditos.text = "🪙 " + credits + " FICHAS";
        if (txtDicaFicha != null)
        {
            txtDicaFicha.text = credits > 0 ? "🪙 Fichas prontas: " + credits : "Sem fichas!";
            txtDicaFicha.color = credits > 0 ? new Color(0.85f, 0.9f, 1f) : NEON_RED;
        }
    }

    void AtualizarHighScore(int score)
    {
        if (txtHighScore != null) txtHighScore.text = "⭐ RECORDE: " + score;
    }

    public void AjustarForca(float delta)
    {
        float current = InputHandler.Instance != null ? InputHandler.Instance.CurrentForce : 1.0f;
        float nova = Mathf.Clamp(current + delta, 0.1f, 1.0f);
        if (InputHandler.Instance != null) InputHandler.Instance.SetForce(nova);
        AtualizarBarraForca(nova);
    }

    void AtualizarBarraForca(float force)
    {
        if (barraForcaFill != null) barraForcaFill.fillAmount = force;
        if (txtForcaVal != null) txtForcaVal.text = Mathf.RoundToInt(force * 100f) + "%";
    }

    void OnPrizeDelivered(Prize prize, int totalPrizes)
    {
        if (txtPremiosGanhos != null) txtPremiosGanhos.text = "🏆 " + totalPrizes;
        if (txtPopup != null)
        {
            txtPopup.gameObject.SetActive(true);
            tempoPremio = 2.5f;
        }
    }

    // ===== TELA DE RESULTADO CELEBRAÇÃO =====
    void MostrarTelaResultado(Prize prize)
    {
        if (painelResultado == null) return;

        if (painelControles != null) painelControles.SetActive(false);
        if (painelInicio != null) painelInicio.SetActive(false);

        string nomeDoPremio = prize != null ? prize.prizeId.Replace("_", " ").ToUpper() : "PELÚCIA";
        if (txtResultadoNome != null) txtResultadoNome.text = "🧸 " + nomeDoPremio;
        if (txtResultadoMsg != null)
        {
            int total = GameSession.Instance != null ? GameSession.Instance.PrizesWon : 1;
            txtResultadoMsg.text = $"Adicionado à sua coleção!\nTotal de troféus: {total}";
        }

        painelResultado.SetActive(true);
        StartCoroutine(AnimarTelaResultado());
    }

    // ===== BOTÃO INTELIGENTE DE AÇÃO (PEGAR ⇄ SOLTAR) =====
    public void AtualizarBotaoAcao(bool isClosed)
    {
        if (txtBtnAcaoPrincipal == null) return;

        if (isClosed)
        {
            txtBtnAcaoPrincipal.text = "CAPTURANDO...";
            if (txtBtnAcaoSub != null) txtBtnAcaoSub.text = "AGUARDE A ENTREGA";
            if (imgBtnAcaoCore != null) imgBtnAcaoCore.color = new Color(0.95f, 0.58f, 0.05f, 0.96f); // Dourado Âmbar
            if (imgBtnAcaoGlow != null) imgBtnAcaoGlow.color = new Color(1f, 0.85f, 0.1f, 0.55f);
        }
        else
        {
            txtBtnAcaoPrincipal.text = "PEGAR";
            if (txtBtnAcaoSub != null) txtBtnAcaoSub.text = "TOQUE P/ CAPTURAR";
            if (imgBtnAcaoCore != null) imgBtnAcaoCore.color = new Color(0.85f, 0.08f, 0.35f, 0.96f); // Carmesim Neon
            if (imgBtnAcaoGlow != null) imgBtnAcaoGlow.color = new Color(1f, 0.08f, 0.58f, 0.45f);
        }
    }

    // ===== BOTÃO DE CÂMERA (FRENTE ⇄ DIREITA ⇄ ESQUERDA) =====
    public void AtualizarBotaoCamera(ClawCameraController.CameraViewAngle angle)
    {
        if (txtBtnCam == null) return;
        switch (angle)
        {
            case ClawCameraController.CameraViewAngle.Front:
                txtBtnCam.text = "CÂMERA: FRENTE";
                break;
            case ClawCameraController.CameraViewAngle.Right:
                txtBtnCam.text = "CÂMERA: DIREITA";
                break;
            case ClawCameraController.CameraViewAngle.Left:
                txtBtnCam.text = "CÂMERA: ESQUERDA";
                break;
        }
    }

    IEnumerator AnimarTelaResultado()
    {
        if (painelResultado == null) yield break;

        CanvasGroup cg = painelResultado.GetComponent<CanvasGroup>();
        if (cg == null) cg = painelResultado.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(0f, 1f, t / 0.4f);
            yield return null;
        }
        cg.alpha = 1f;
    }

    void MostrarTelaInicio()
    {
        if (painelInicio != null) painelInicio.SetActive(true);
        int credits = GameSession.Instance != null ? GameSession.Instance.Credits : 3;
        AtualizarCreditos(credits);
    }

    // ======================== CONSTRUÇÃO DA UI NEON ARCADE ========================

    void ConstruirUI()
    {
        GameObject canvasObj = new GameObject("UICanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler cs = canvasObj.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // Ajuste adaptativo para Landscape vs Portrait
        bool isLandscape = Screen.width >= Screen.height;
        cs.referenceResolution = isLandscape ? new Vector2(1920, 1080) : new Vector2(1080, 1920);
        cs.matchWidthOrHeight = isLandscape ? 1.0f : 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        Transform ct = canvas.transform;
        int credsIniciais = GameSession.Instance != null ? GameSession.Instance.Credits : 3;

        // ======================== 1. BARRA DO TOPO (FLOATING CYBER BAR) ========================
        // Ancorada no topo com stretch horizontal
        GameObject barTopo = Painel(ct, "BarraTopo", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -45), new Vector2(0, 90), BG_DARK_GLASS);

        // Linha neon decorativa inferior da barra
        Painel(barTopo.transform, "NeonLine_Bottom", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(0, 3), NEON_CYAN);

        // Logo Neon Arcade (Centro)
        TextoNeon(barTopo.transform, "Logo", "🕹️ GARRAMANIA", Centro, Centro, Vector2.zero, new Vector2(360, 60), 28, NEON_GOLD, FontStyle.Bold, NEON_MAGENTA);

        // Capsule Fichas (Esquerda)
        GameObject capCred = PainelComBorda(barTopo.transform, "CapCreditos", EsqCentro, EsqCentro, new Vector2(110, 0), new Vector2(180, 52), BG_CARD_SLATE, BORDER_GOLD_GLOW, 2f);
        txtCreditos = TextoNeon(capCred.transform, "CredText", "🪙 " + credsIniciais + " FICHAS", Centro, Centro, Vector2.zero, new Vector2(170, 46), 18, NEON_GOLD, FontStyle.Bold);

        // Capsule Recorde (Centro Esquerda)
        int hsInicial = GameSession.Instance != null ? GameSession.Instance.HighScore : 0;
        GameObject capHS = PainelComBorda(barTopo.transform, "CapHighScore", EsqCentro, EsqCentro, new Vector2(300, 0), new Vector2(180, 52), BG_CARD_SLATE, BORDER_GOLD_GLOW, 2f);
        txtHighScore = TextoNeon(capHS.transform, "HSText", "⭐ RECORDE: " + hsInicial, Centro, Centro, Vector2.zero, new Vector2(170, 46), 18, NEON_GOLD, FontStyle.Bold);

        // Capsule Prêmios (Direita)
        GameObject capPrem = PainelComBorda(barTopo.transform, "CapPremios", DirCentro, DirCentro, new Vector2(-130, 0), new Vector2(190, 52), BG_CARD_SLATE, new Color(0f, 1f, 0.55f, 0.45f), 2f);
        txtPremiosGanhos = TextoNeon(capPrem.transform, "PremText", "🏆 0 PRÊMIOS", Centro, Centro, Vector2.zero, new Vector2(180, 46), 20, NEON_GREEN, FontStyle.Bold);

        // ======================== 2. RADIAL NEON TIMER ========================
        // Ancorado no canto superior direito
        GameObject timerObj = Painel(ct, "TimerHUD", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-75, -155), new Vector2(110, 110), Color.clear);
        timerRT = timerObj.GetComponent<RectTransform>();

        // Anel de borda
        bordaTimer = Painel(timerObj.transform, "TimerBorda", Centro, Centro, Vector2.zero, new Vector2(108, 108), BORDER_CYAN_GLOW).GetComponent<Image>();
        // Fundo escuro
        Painel(timerObj.transform, "TimerFundo", Centro, Centro, Vector2.zero, new Vector2(100, 100), BG_DARK_GLASS);
        // Barra radial
        barraTimer = Painel(timerObj.transform, "TimerFill", Centro, Centro, Vector2.zero, new Vector2(100, 100), NEON_CYAN).GetComponent<Image>();
        barraTimer.type = Image.Type.Filled;
        barraTimer.fillMethod = Image.FillMethod.Radial360;
        barraTimer.fillClockwise = true;
        // Núcleo escuro
        Painel(timerObj.transform, "TimerCore", Centro, Centro, Vector2.zero, new Vector2(76, 76), new Color(0.06f, 0.08f, 0.12f, 0.95f));
        // Texto com número
        txtNumTimer = TextoNeon(timerObj.transform, "TimerNum", "45", Centro, Centro, Vector2.zero, new Vector2(70, 70), 30, Color.white, FontStyle.Bold);

        // ======================== 3. POPUP DE PRÊMIO RÁPIDO ========================
        txtPopup = TextoNeon(ct, "PopupPremio", "✨ PRÊMIO CAPTURADO! ✨", new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(800, 80), 40, NEON_GOLD, FontStyle.Bold, Color.black);
        txtPopup.gameObject.SetActive(false);

        // ======================== 4. CONTROLES MOBILE ARCADE ========================
        painelControles = Painel(ct, "ControlesMobile", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
        painelControles.SetActive(true);

        // --- D-PAD ARCADE (ANCORADO NO CANTO INFERIOR ESQUERDO) ---
        GameObject dpadRoot = Painel(painelControles.transform, "DPad_Root", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(170, 160), new Vector2(240, 240), Color.clear);
        // Base escura do D-Pad
        Painel(dpadRoot.transform, "DPad_Plate", Centro, Centro, Vector2.zero, new Vector2(230, 230), new Color(0.04f, 0.06f, 0.1f, 0.5f));

        CriarBotaoArcade(dpadRoot.transform, "BtnCima", "▲", new Vector2(0, 75), new Vector2(75, 70),
            () => InputRouter.Instance.SetTouchZ(1), () => InputRouter.Instance.SetTouchZ(0));
        CriarBotaoArcade(dpadRoot.transform, "BtnBaixo", "▼", new Vector2(0, -75), new Vector2(75, 70),
            () => InputRouter.Instance.SetTouchZ(-1), () => InputRouter.Instance.SetTouchZ(0));
        CriarBotaoArcade(dpadRoot.transform, "BtnEsq", "◄", new Vector2(-75, 0), new Vector2(70, 75),
            () => InputRouter.Instance.SetTouchX(-1), () => InputRouter.Instance.SetTouchX(0));
        CriarBotaoArcade(dpadRoot.transform, "BtnDir", "►", new Vector2(75, 0), new Vector2(70, 75),
            () => InputRouter.Instance.SetTouchX(1), () => InputRouter.Instance.SetTouchX(0));

        // --- DECK DE AÇÃO (ANCORADO NO CANTO INFERIOR DIREITO) ---
        // Isso garante que NUNCA ficará no meio da tela no modo Landscape!
        GameObject actionRoot = Painel(painelControles.transform, "Action_Root", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-160, 160), new Vector2(220, 320), Color.clear);

        // Botão grande inteligente "PEGAR ⇄ SOLTAR"
        CriarBotaoAcaoArcade(actionRoot.transform, "BtnPegar", "PEGAR", new Vector2(0, -30), new Vector2(160, 160),
            () => InputRouter.Instance.TriggerTouchAction());

        // Botões de elevação vertical (Subir e Descer)
        CriarBotaoArcade(actionRoot.transform, "BtnSubir", "▲ SUBIR", new Vector2(0, 145), new Vector2(140, 56),
            () => InputRouter.Instance.SetTouchY(1), () => InputRouter.Instance.SetTouchY(0));
        CriarBotaoArcade(actionRoot.transform, "BtnDescer", "▼ DESCER", new Vector2(0, 80), new Vector2(140, 56),
            () => InputRouter.Instance.SetTouchY(-1), () => InputRouter.Instance.SetTouchY(0));

        // Botão de Alternância de Câmera "FRENTE / LADO"
        CriarBotaoCameraArcade(actionRoot.transform, "BtnCamera", "VISÃO LADO", new Vector2(0, 215), new Vector2(150, 50),
            () => {
                if (ClawCameraController.Instance != null) ClawCameraController.Instance.ToggleCameraAngle();
            });

        // Botão Reset Garra
        CriarBotaoArcade(actionRoot.transform, "BtnReset", "↺ RESET", new Vector2(0, 280), new Vector2(150, 48),
            () => {
                if (InputHandler.Instance != null) InputHandler.Instance.TriggerReset();
                else {
                    ClawController cc = FindAnyObjectByType<ClawController>();
                    if (cc != null) cc.ResetarGarra();
                }
            }, () => {});

        // --- BARRA DE FORÇA AJUSTÁVEL MOBILE ---
        GameObject forceRoot = PainelComBorda(painelControles.transform, "ForceBar_Root", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-160, 490), new Vector2(180, 75), BG_CARD_SLATE, BORDER_CYAN_GLOW, 2f);
        TextoNeon(forceRoot.transform, "ForceTitle", "FORÇA DA GARRA", new Vector2(0.5f, 0.8f), new Vector2(0.5f, 0.8f), Vector2.zero, new Vector2(170, 22), 12, NEON_CYAN, FontStyle.Bold);
        
        GameObject bgBar = Painel(forceRoot.transform, "BarBG", new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(100, 18), new Color(0.04f, 0.06f, 0.1f, 0.9f));
        GameObject fillBarObj = Painel(bgBar.transform, "BarFill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, NEON_GREEN);
        barraForcaFill = fillBarObj.GetComponent<Image>();
        barraForcaFill.type = Image.Type.Filled;
        barraForcaFill.fillMethod = Image.FillMethod.Horizontal;
        barraForcaFill.fillAmount = 1.0f;

        txtForcaVal = TextoNeon(forceRoot.transform, "ForceVal", "100%", new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(80, 20), 12, Color.white, FontStyle.Bold);

        CriarBotaoArcade(forceRoot.transform, "BtnForceDown", "-", new Vector2(-65, -3), new Vector2(28, 28), () => AjustarForca(-0.1f), () => {});
        CriarBotaoArcade(forceRoot.transform, "BtnForceUp", "+", new Vector2(65, -3), new Vector2(28, 28), () => AjustarForca(0.1f), () => {});

        // ======================== 5. TELA DE INÍCIO (MARQUEE ARCADE) ========================
        ConstruirTelaInicio(ct, credsIniciais);
        if (painelInicio != null) painelInicio.SetActive(false);

        // ======================== 6. TELA DE RESULTADO (MODAL DE CELEBRAÇÃO) ========================
        ConstruirTelaResultado(ct);
    }

    void ConstruirTelaInicio(Transform canvasTransform, int credsIniciais)
    {
        painelInicio = Painel(canvasTransform, "TelaInicio", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.06f, 0.88f));

        // Card central arcade
        GameObject cardMarquee = PainelComBorda(painelInicio.transform, "CardMarquee", Centro, Centro, Vector2.zero, new Vector2(680, 640), BG_DARK_GLASS, BORDER_CYAN_GLOW, 3f);

        // Faixa decorativa superior neon
        Painel(cardMarquee.transform, "TopNeonStripe", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -5), new Vector2(0, 6), NEON_MAGENTA);

        // Título Principal
        TextoNeon(cardMarquee.transform, "TituloPrincipal", "🕹️ GARRAMANIA", new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), Vector2.zero, new Vector2(640, 80), 44, NEON_GOLD, FontStyle.Bold, NEON_MAGENTA);
        TextoNeon(cardMarquee.transform, "Subtitulo", "― ARCADE CLAW EXPERIENCE ―", new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(550, 40), 18, NEON_CYAN);

        // Ícone decorativo / Pelúcia de destaque
        TextoNeon(cardMarquee.transform, "IconeCentral", "🧸  🦊  🐨", new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(450, 60), 40, Color.white);

        // Status de Fichas
        txtDicaFicha = TextoNeon(cardMarquee.transform, "DicaFicha", "🪙 Fichas prontas: " + credsIniciais, new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(450, 45), 22, new Color(0.85f, 0.9f, 1f));

        // Botão JOGAR (Verde Neon)
        GameObject btnJogar = PainelComBorda(cardMarquee.transform, "BtnJogar", new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(360, 90), new Color(0.05f, 0.5f, 0.25f, 0.95f), NEON_GREEN, 3f);
        TextoNeon(btnJogar.transform, "TxtBtnJogar", "▶ JOGAR AGORA", Centro, Centro, Vector2.zero, new Vector2(340, 80), 30, Color.white, FontStyle.Bold, new Color(0f, 0.4f, 0.15f));

        EventTrigger trigger = btnJogar.AddComponent<EventTrigger>();
        EventTrigger.Entry entryDown = new EventTrigger.Entry();
        entryDown.eventID = EventTriggerType.PointerDown;
        entryDown.callback.AddListener((data) => IniciarJogo());
        trigger.triggers.Add(entryDown);

        Button btn = btnJogar.AddComponent<Button>();
        btn.onClick.AddListener(IniciarJogo);

        // Rodapé de instruções
        TextoNeon(cardMarquee.transform, "Rodape", "Mova a garra, mire na pelúcia e aperte PEGAR!", new Vector2(0.5f, 0.09f), new Vector2(0.5f, 0.09f), Vector2.zero, new Vector2(600, 40), 16, new Color(0.5f, 0.6f, 0.75f));
    }

    void ConstruirTelaResultado(Transform canvasTransform)
    {
        painelResultado = Painel(canvasTransform, "TelaResultado", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.07f, 0.92f));

        // Card central de celebração
        GameObject cardVitoria = PainelComBorda(painelResultado.transform, "CardVitoria", Centro, Centro, Vector2.zero, new Vector2(680, 700), BG_DARK_GLASS, BORDER_GOLD_GLOW, 3f);

        // Faixa superior dourada
        Painel(cardVitoria.transform, "StripeGold", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -5), new Vector2(0, 6), NEON_GOLD);

        // Título "🎉 VITÓRIA ÉPICA!"
        txtResultadoTitulo = TextoNeon(cardVitoria.transform, "TituloResultado", "🎉 VITÓRIA ÉPICA! 🎉",
            new Vector2(0.5f, 0.85f), new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(620, 70), 40,
            NEON_GOLD, FontStyle.Bold, new Color(0.5f, 0.3f, 0f));

        // Estrelas
        TextoNeon(cardVitoria.transform, "Estrelas", "⭐  ⭐  ⭐",
            new Vector2(0.5f, 0.74f), new Vector2(0.5f, 0.74f), Vector2.zero, new Vector2(360, 50), 34,
            new Color(1f, 0.92f, 0.3f));

        // Banner com nome da pelúcia
        GameObject bannerNome = PainelComBorda(cardVitoria.transform, "BannerNome", new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(560, 90), BG_CARD_SLATE, BORDER_CYAN_GLOW, 2f);
        txtResultadoNome = TextoNeon(bannerNome.transform, "NomePremio", "🧸 KOALA",
            Centro, Centro, Vector2.zero, new Vector2(540, 80), 38,
            Color.white, FontStyle.Bold, NEON_CYAN);

        // Mensagem de coleção
        txtResultadoMsg = TextoNeon(cardVitoria.transform, "MsgColecao", "Adicionado com sucesso ao seu inventário!\nTotal de prêmios: 1",
            new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(580, 60), 20,
            new Color(0.7f, 0.85f, 0.95f));

        // Separador neon
        Painel(cardVitoria.transform, "Separador", new Vector2(0.15f, 0.36f), new Vector2(0.85f, 0.36f), Vector2.zero, new Vector2(0, 2), new Color(1f, 0.85f, 0.1f, 0.4f));

        // Botão "JOGAR DE NOVO"
        GameObject btnJogarDeNovo = PainelComBorda(cardVitoria.transform, "BtnJogarDeNovo",
            new Vector2(0.5f, 0.24f), new Vector2(0.5f, 0.24f), Vector2.zero, new Vector2(380, 80),
            new Color(0.05f, 0.5f, 0.25f, 0.95f), NEON_GREEN, 3f);
        TextoNeon(btnJogarDeNovo.transform, "TxtBtnJDN", "▶ JOGAR DE NOVO", Centro, Centro, Vector2.zero, new Vector2(360, 70), 26, Color.white, FontStyle.Bold);

        EventTrigger triggerJDN = btnJogarDeNovo.AddComponent<EventTrigger>();
        EventTrigger.Entry entryJDN = new EventTrigger.Entry();
        entryJDN.eventID = EventTriggerType.PointerDown;
        entryJDN.callback.AddListener((data) => {
            if (GameSession.Instance != null) GameSession.Instance.ResetSession();
            IniciarJogo();
        });
        triggerJDN.triggers.Add(entryJDN);
        Button btnCompJDN = btnJogarDeNovo.AddComponent<Button>();
        btnCompJDN.onClick.AddListener(() => {
            if (GameSession.Instance != null) GameSession.Instance.ResetSession();
            IniciarJogo();
        });

        // Botão "COMPARTILHAR"
        GameObject btnCompartilhar = PainelComBorda(cardVitoria.transform, "BtnCompartilhar",
            new Vector2(0.5f, 0.11f), new Vector2(0.5f, 0.11f), Vector2.zero, new Vector2(300, 56),
            new Color(0.1f, 0.3f, 0.65f, 0.85f), new Color(0.2f, 0.55f, 1f, 0.7f), 2f);
        TextoNeon(btnCompartilhar.transform, "TxtBtnShare", "📤 COMPARTILHAR", Centro, Centro, Vector2.zero, new Vector2(280, 50), 20, Color.white, FontStyle.Bold);

        EventTrigger triggerShare = btnCompartilhar.AddComponent<EventTrigger>();
        EventTrigger.Entry entryShare = new EventTrigger.Entry();
        entryShare.eventID = EventTriggerType.PointerDown;
        entryShare.callback.AddListener((data) => {
            Debug.Log("[UIManager] Compartilhar conquista - Screenshot/Share Intent!");
        });
        triggerShare.triggers.Add(entryShare);

        painelResultado.SetActive(false);
    }

    // ======================== HELPERS DE CRIAÇÃO VISUAL ========================

    static Vector2 Centro = new Vector2(0.5f, 0.5f);
    static Vector2 EsqCentro = new Vector2(0.0f, 0.5f);
    static Vector2 DirCentro = new Vector2(1.0f, 0.5f);

    GameObject Painel(Transform pai, string nome, Vector2 ancMin, Vector2 ancMax, Vector2 ancPos, Vector2 sizeDelta, Color cor)
    {
        GameObject obj = new GameObject(nome);
        obj.transform.SetParent(pai, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = ancMin;
        rt.anchorMax = ancMax;
        rt.anchoredPosition = ancPos;
        rt.sizeDelta = sizeDelta;
        Image img = obj.AddComponent<Image>();
        img.color = cor;
        return obj;
    }

    GameObject PainelComBorda(Transform pai, string nome, Vector2 ancMin, Vector2 ancMax, Vector2 ancPos, Vector2 sizeDelta, Color corFundo, Color corBorda, float espessuraBorda = 2f)
    {
        GameObject borda = Painel(pai, nome + "_Borda", ancMin, ancMax, ancPos, sizeDelta, corBorda);
        Painel(borda.transform, nome + "_Fundo", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-espessuraBorda * 2f, -espessuraBorda * 2f), corFundo);
        return borda;
    }

    Text TextoNeon(Transform pai, string nome, string cont, Vector2 ancMin, Vector2 ancMax, Vector2 ancPos, Vector2 sizeDelta, int fontSize, Color cor, FontStyle estilo = FontStyle.Normal, Color? glowColor = null)
    {
        GameObject obj = new GameObject(nome);
        obj.transform.SetParent(pai, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = ancMin;
        rt.anchorMax = ancMax;
        rt.anchoredPosition = ancPos;
        rt.sizeDelta = sizeDelta;

        Text txt = obj.AddComponent<Text>();
        txt.text = cont;
        if (fontArcade == null) fontArcade = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (fontArcade == null) fontArcade = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.font = fontArcade;
        txt.fontSize = fontSize;
        txt.color = cor;
        txt.fontStyle = estilo;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;

        // Efeito Neon Glow com Outline
        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = glowColor ?? new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // Sombra de profundidade arcade
        Shadow shadow = obj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(2.5f, -2.5f);

        return txt;
    }

    void CriarBotaoArcade(Transform pai, string nome, string texto, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onDown, UnityEngine.Events.UnityAction onUp)
    {
        GameObject btnObj = PainelComBorda(pai, nome, Centro, Centro, pos, size, BG_CARD_SLATE, BORDER_CYAN_GLOW, 2f);
        TextoNeon(btnObj.transform, "txt", texto, Centro, Centro, Vector2.zero, size, 24, NEON_CYAN, FontStyle.Bold, new Color(0f, 0.4f, 0.6f));

        Image imgBorda = btnObj.GetComponent<Image>();
        EventTrigger trigger = btnObj.AddComponent<EventTrigger>();

        EventTrigger.Entry entryDown = new EventTrigger.Entry();
        entryDown.eventID = EventTriggerType.PointerDown;
        entryDown.callback.AddListener((data) => {
            if (imgBorda != null) imgBorda.color = NEON_CYAN;
            onDown();
        });
        trigger.triggers.Add(entryDown);

        EventTrigger.Entry entryUp = new EventTrigger.Entry();
        entryUp.eventID = EventTriggerType.PointerUp;
        entryUp.callback.AddListener((data) => {
            if (imgBorda != null) imgBorda.color = BORDER_CYAN_GLOW;
            onUp();
        });
        trigger.triggers.Add(entryUp);
    }

    void CriarBotaoAcaoArcade(Transform pai, string nome, string texto, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        // Anel externo de glow
        GameObject btnGlow = Painel(pai, nome + "_Glow", Centro, Centro, pos, size, new Color(1f, 0.08f, 0.58f, 0.45f));
        imgBtnAcaoGlow = btnGlow.GetComponent<Image>();

        // Botão principal
        GameObject btnCore = Painel(btnGlow.transform, nome + "_Core", Centro, Centro, Vector2.zero, new Vector2(size.x - 12f, size.y - 12f), new Color(0.85f, 0.08f, 0.35f, 0.96f));
        imgBtnAcaoCore = btnCore.GetComponent<Image>();

        txtBtnAcaoPrincipal = TextoNeon(btnCore.transform, "txtPrincipal", texto, Centro, Centro, new Vector2(0, 14), new Vector2(size.x, 38), 28, Color.white, FontStyle.Bold, new Color(0.4f, 0f, 0.1f));
        txtBtnAcaoSub = TextoNeon(btnCore.transform, "txtSub", "TOQUE P/ BAIXAR", Centro, Centro, new Vector2(0, -18), new Vector2(size.x, 24), 13, new Color(1f, 0.85f, 0.9f));

        RectTransform rtCore = btnCore.GetComponent<RectTransform>();

        EventTrigger trigger = btnCore.AddComponent<EventTrigger>();
        EventTrigger.Entry entryDown = new EventTrigger.Entry();
        entryDown.eventID = EventTriggerType.PointerDown;
        entryDown.callback.AddListener((data) => {
            if (imgBtnAcaoGlow != null) imgBtnAcaoGlow.color = NEON_MAGENTA;
            if (rtCore != null) rtCore.localScale = Vector3.one * 0.92f;
            onClick();
        });
        trigger.triggers.Add(entryDown);

        EventTrigger.Entry entryUp = new EventTrigger.Entry();
        entryUp.eventID = EventTriggerType.PointerUp;
        entryUp.callback.AddListener((data) => {
            if (imgBtnAcaoGlow != null) imgBtnAcaoGlow.color = new Color(1f, 0.08f, 0.58f, 0.45f);
            if (rtCore != null) rtCore.localScale = Vector3.one;
        });
        trigger.triggers.Add(entryUp);
    }

    void CriarBotaoCameraArcade(Transform pai, string nome, string texto, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = PainelComBorda(pai, nome, Centro, Centro, pos, size, BG_CARD_SLATE, BORDER_CYAN_GLOW, 2f);
        txtBtnCam = TextoNeon(btnObj.transform, "txtCam", texto, Centro, Centro, Vector2.zero, size, 20, NEON_CYAN, FontStyle.Bold, new Color(0f, 0.4f, 0.6f));

        Image imgBorda = btnObj.GetComponent<Image>();
        EventTrigger trigger = btnObj.AddComponent<EventTrigger>();

        EventTrigger.Entry entryDown = new EventTrigger.Entry();
        entryDown.eventID = EventTriggerType.PointerDown;
        entryDown.callback.AddListener((data) => {
            if (imgBorda != null) imgBorda.color = NEON_CYAN;
            onClick();
        });
        trigger.triggers.Add(entryDown);

        EventTrigger.Entry entryUp = new EventTrigger.Entry();
        entryUp.eventID = EventTriggerType.PointerUp;
        entryUp.callback.AddListener((data) => {
            if (imgBorda != null) imgBorda.color = BORDER_CYAN_GLOW;
        });
        trigger.triggers.Add(entryUp);
    }
}