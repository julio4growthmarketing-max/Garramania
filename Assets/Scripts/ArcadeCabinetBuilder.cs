using UnityEngine;

/// <summary>
/// Construtor Arquitetural Modular de Alta Fidelidade do Gabinete Arcade.
/// Reproduz a estrutura de um autêntico UFO Catcher japonês (SEGA/Namco):
/// 1. Pedestal inferior com porta de cofre de moedas e pés niveladores.
/// 2. Balcão / Console de comando físico inclinado com ranhura de fichas (100 YEN), joystick e botões arcade.
/// 3. Colunas estruturais chanfradas com tubos de neon RGB difuso.
/// 4. Calha de entrega com portinhola "PUSH", iluminação interna e cerca de acrílico protetor.
/// 5. Sistema de Trilhos de Guindaste (Gantry Móvel) no teto com carro motorizado e polias.
/// 6. Dossel superior inclinado com letreiro luminoso retroiluminado e caixas de som estéreo.
/// 7. Painel traseiro cósmico Galaxy Wallpaper com iluminação volumétrica por spotlights.
/// </summary>
public class ArcadeCabinetBuilder
{
    public GameObject RootCabinet { get; private set; }
    public GameObject GantryTrolley { get; private set; }
    public Transform GantryCrossbar { get; private set; }

    public static ArcadeCabinetBuilder Build(Transform parent = null)
    {
        ArcadeCabinetBuilder builder = new ArcadeCabinetBuilder();
        builder.Construct(parent);
        return builder;
    }

    private void Construct(Transform parent)
    {
        RootCabinet = new GameObject("Gabinete_Arcade_Modular");
        if (parent != null) RootCabinet.transform.SetParent(parent);

        // ======================== MATERIAIS URP PBR ========================
        // Chassis Preto Meia-Noite / Grafite
        Material mChassis = CriarMaterialURP(new Color(0.09f, 0.09f, 0.12f), 0.82f, 0.45f);
        // Aço Inox / Metal Escovado
        Material mAcoInox = CriarMaterialURP(new Color(0.82f, 0.85f, 0.90f), 0.92f, 0.85f);
        // Metal Dourado / Latão
        Material mDourado = CriarMaterialURP(new Color(0.95f, 0.78f, 0.20f), 0.88f, 0.75f);
        // Piso Xadrez Branco
        Material mPisoBranco = CriarMaterialURP(new Color(0.92f, 0.94f, 0.98f), 0.85f, 0.1f);
        // Piso Xadrez Preto
        Material mPisoPreto = CriarMaterialURP(new Color(0.06f, 0.07f, 0.09f), 0.85f, 0.2f);
        
        // Neon Cyan Emissivo HDR (Layout 2)
        Material mNeonCyan = CriarEmissivo(new Color(0.25f, 1.7f, 2.1f), 1.2f);
        // Neon Magenta Emissivo HDR (Layout 2)
        Material mNeonMagenta = CriarEmissivo(new Color(2.5f, 0.35f, 1.3f), 1.2f);
        // Neon Amarelo / Dourado Letreiro
        Material mNeonGold = CriarEmissivo(new Color(1.8f, 1.5f, 0.3f), 1.2f);
        // Luz Verde Status
        Material mNeonGreen = CriarEmissivo(new Color(0.1f, 1.8f, 0.4f), 1.2f);

        // Vidro Cristalino PBR (Layout 2: Reflexo Nítido e Limpo com Reflection Probe)
        Material mVidroGabinete = CriarVidro(new Color(1f, 1f, 1f, 0.12f), 0.96f);
        // Acrílico / Vidro Translúcido URP
        Material mVidroAcrilico = CriarVidro(new Color(0.85f, 0.95f, 1.0f, 0.18f), 0.96f);
        // Vidro Fumê da Portinhola
        Material mVidroFume = CriarVidro(new Color(0.12f, 0.14f, 0.18f, 0.55f), 0.85f);

        // Painel Galáxia
        Material mGalaxy = CriarMaterialURP(Color.white, 0.70f, 0.15f);
        Texture2D texGalaxy = Resources.Load<Texture2D>("Textures/GalaxyWallpaper");
        if (texGalaxy != null) mGalaxy.mainTexture = texGalaxy;

        // Arte Gráfica do Letreiro Marquee (GARRAMANIA Neon)
        Material mMarquee = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Texture2D texMarquee = Resources.Load<Texture2D>("Textures/MarqueeBanner");
        if (texMarquee != null)
        {
            mMarquee.mainTexture = texMarquee;
            mMarquee.EnableKeyword("_EMISSION");
            mMarquee.SetTexture("_EmissionMap", texMarquee);
            mMarquee.SetColor("_EmissionColor", Color.white * 1.8f);
        }
        else
        {
            mMarquee = mNeonGold;
        }

        // Adesivos e Decalques de Instruções Arcade
        Material mDecals = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Texture2D texDecals = Resources.Load<Texture2D>("Textures/ArcadeDecals");
        if (texDecals != null)
        {
            mDecals.mainTexture = texDecals;
            mDecals.EnableKeyword("_EMISSION");
            mDecals.SetTexture("_EmissionMap", texDecals);
            mDecals.SetColor("_EmissionColor", Color.white * 1.2f);
        }
        else
        {
            mDecals = mChassis;
        }

        // Cenário Tokyo Game Center (Fundo do Fliperama)
        Material mArcadeBackdrop = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Texture2D texArcadeRoom = Resources.Load<Texture2D>("Textures/ArcadeGameCenter");
        if (texArcadeRoom != null)
        {
            mArcadeBackdrop.mainTexture = texArcadeRoom;
            mArcadeBackdrop.color = new Color(0.9f, 0.9f, 0.9f);
            mArcadeBackdrop.SetFloat("_Smoothness", 0.25f);
            mArcadeBackdrop.EnableKeyword("_EMISSION");
            mArcadeBackdrop.SetTexture("_EmissionMap", texArcadeRoom);
            mArcadeBackdrop.SetColor("_EmissionColor", Color.white * 0.4f);
        }

        // ======================== 1. PEDESTAL INFERIOR & PÉS ========================
        Transform rootBase = CriarSubGrupo("01_Base_Pedestal");
        
        // Base Pesada Principal
        Cubo("Plinto_Inferior", new Vector3(0, -2.65f, 0), new Vector3(5.3f, 0.7f, 5.3f), mChassis, rootBase);
        // Moldura em Aço Inox
        Cubo("Moldura_Base_Inox", new Vector3(0, -2.32f, 0), new Vector3(5.35f, 0.06f, 5.35f), mAcoInox, rootBase);

        // Pés Niveladores nos 4 cantos
        Vector3[] cantosPes = new Vector3[] {
            new Vector3(-2.4f, -2.95f, -2.4f),
            new Vector3(2.4f, -2.95f, -2.4f),
            new Vector3(-2.4f, -2.95f, 2.4f),
            new Vector3(2.4f, -2.95f, 2.4f)
        };
        foreach (var cp in cantosPes)
        {
            Cubo("Pe_Nivelador", cp, new Vector3(0.35f, 0.25f, 0.35f), mChassis, rootBase);
            Cubo("Sapata_Borracha", cp + new Vector3(0, -0.1f, 0), new Vector3(0.42f, 0.08f, 0.42f), mPisoPreto, rootBase);
        }

        // Porta de Manutenção / Cofre de Moedas na Frente
        Cubo("Porta_Cofre", new Vector3(0.6f, -2.62f, -2.67f), new Vector3(1.6f, 0.55f, 0.04f), mChassis, rootBase);
        Cubo("Fechadura_Chave", new Vector3(1.25f, -2.62f, -2.70f), new Vector3(0.08f, 0.08f, 0.05f), mAcoInox, rootBase);
        Cubo("Grade_Ventilacao", new Vector3(-0.6f, -2.62f, -2.67f), new Vector3(1.0f, 0.35f, 0.02f), mPisoPreto, rootBase);

        // ======================== 2. PISO XADREZ GLOSSY ========================
        Transform rootPiso = CriarSubGrupo("02_Piso_Arena");
        Cubo("Subpiso_Isolante", new Vector3(0, -2.05f, 0), new Vector3(5.0f, 0.1f, 5.0f), mPisoPreto, rootPiso);
        for (int x = -2; x <= 2; x++)
        {
            for (int z = -2; z <= 2; z++)
            {
                // Evita colocar tile no buraco exato da calha de prêmio
                if (x <= -1 && z <= -1) continue;
                Material mTile = (x + z) % 2 == 0 ? mPisoPreto : mPisoBranco;
                Cubo($"Tile_{x}_{z}", new Vector3(x, -1.98f, z), new Vector3(0.98f, 0.05f, 0.98f), mTile, rootPiso);
            }
        }

        // Platô Elevado da Arena (aproxima o monte de pelúcias do vidro frontal)
        Cubo("Plato_Elevado_Arena", new Vector3(0.35f, -1.65f, 0.35f), new Vector3(3.6f, 0.65f, 3.6f), mChassis, rootPiso);

        // ======================== 3. COLUNAS CHANFRADAS & NEON RGB ========================
        Transform rootColunas = CriarSubGrupo("03_Colunas_Estruturais");
        Vector3[] colPos = new Vector3[] {
            new Vector3(-2.55f, 0.5f, -2.55f),
            new Vector3(2.55f, 0.5f, -2.55f),
            new Vector3(-2.55f, 0.5f, 2.55f),
            new Vector3(2.55f, 0.5f, 2.55f)
        };
        for (int i = 0; i < colPos.Length; i++)
        {
            // Coluna Externa Estrutural
            Cubo($"Coluna_Corpo_{i}", colPos[i], new Vector3(0.32f, 5.0f, 0.32f), mChassis, rootColunas);
            // Chanfro Metálico Inox
            Cubo($"Coluna_Inox_{i}", colPos[i] + new Vector3(0, 0, 0), new Vector3(0.35f, 0.15f, 0.35f), mAcoInox, rootColunas);
            
            // Tubo Vertical Neon Cyan Brilhante
            Vector3 offsetNeon = new Vector3(colPos[i].x > 0 ? -0.16f : 0.16f, 0, colPos[i].z > 0 ? -0.16f : 0.16f);
            Cubo($"Neon_Coluna_{i}", colPos[i] + offsetNeon, new Vector3(0.06f, 4.92f, 0.06f), mNeonCyan, rootColunas);
        }

        // ======================== 4. PAREDES, VIDROS & GALAXY MURAL ========================
        Transform rootParedes = CriarSubGrupo("04_Paredes_Vidros");
        
        // Fundo com Arte Galaxy Neon Arcade
        Cubo("Mural_Galaxy_Fundo", new Vector3(0, 0.5f, 2.58f), new Vector3(5.0f, 5.0f, 0.10f), mGalaxy, rootParedes);
        // Moldura do Mural
        Cubo("Moldura_Top_Fundo", new Vector3(0, 2.95f, 2.54f), new Vector3(5.1f, 0.12f, 0.06f), mAcoInox, rootParedes);

        // Vidros Físicos PBR Transparentes (Reflexos nítidos da garra, pelúcias e neons)
        // Cubo("Vidro_Frontal", new Vector3(0, 0.5f, -2.55f), new Vector3(4.8f, 4.8f, 0.02f), mVidroGabinete, rootParedes); // Omitido na frente para visibilidade cristalina
        Cubo("Vidro_Esquerdo", new Vector3(-2.55f, 0.5f, 0), new Vector3(0.02f, 4.8f, 4.8f), mVidroGabinete, rootParedes);
        Cubo("Vidro_Direito", new Vector3(2.55f, 0.5f, 0), new Vector3(0.02f, 4.8f, 4.8f), mVidroGabinete, rootParedes);

        // Adesivo de Instruções e Decalques Arcade no Vidro.
        // A face frontal do cubo recebia o UV invertido; a rotação no próprio plano
        // corrige a placa sem trocar a textura nem afetar o restante do gabinete.
        GameObject placaInstrucoes = Cubo("Adesivo_Instrucoes_Arcade", new Vector3(1.85f, -0.6f, -2.52f), new Vector3(0.9f, 1.15f, 0.01f), mDecals, rootParedes);
        placaInstrucoes.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);

        // Colisores Físicos Invisíveis (Bloqueiam as pelúcias mas deixam a câmera 100% livre)
        CriarColisorInvisivel("Colisor_Frontal", new Vector3(0, 0.5f, -2.55f), new Vector3(5.0f, 5.0f, 0.1f), rootParedes);
        CriarColisorInvisivel("Colisor_Esquerdo", new Vector3(-2.55f, 0.5f, 0), new Vector3(0.1f, 5.0f, 5.0f), rootParedes);
        CriarColisorInvisivel("Colisor_Direito", new Vector3(2.55f, 0.5f, 0), new Vector3(0.1f, 5.0f, 5.0f), rootParedes);
        // Iluminação direta de vitrine de shopping nas pelúcias (cristalina, quente e brilhante)
        CriarSpotlight(new Vector3(0, 2.70f, 0), new Color(1f, 0.96f, 0.90f), 9f, 80f, 5.5f, rootParedes);
        CriarSpotlight(new Vector3(0, 2.70f, -0.8f), new Color(1f, 0.95f, 0.85f), 9f, 75f, 4.5f, rootParedes);

        // ======================== 5. BALCÃO FÍSICO / CONSOLE DO JOGADOR ========================
        Transform rootConsole = CriarSubGrupo("05_Console_De_Comando");

        // Balcão Inclinado Físico
        GameObject balcao = Cubo("Balcao_Mesa", new Vector3(0, -1.88f, -2.85f), new Vector3(4.8f, 0.22f, 0.70f), mChassis, rootConsole);
        balcao.transform.rotation = Quaternion.Euler(15f, 0f, 0f); // Levemente inclinado para o jogador

        // Faixa de Neon Frontal do Balcão
        Cubo("Neon_Borda_Balcao", new Vector3(0, -1.95f, -3.22f), new Vector3(4.8f, 0.05f, 0.05f), mNeonMagenta, rootConsole);

        // Console de Moedas (Coin Mech)
        Vector3 posMoeda = new Vector3(-1.2f, -1.82f, -2.90f);
        Cubo("Placa_Moeda", posMoeda, new Vector3(0.35f, 0.42f, 0.04f), mAcoInox, rootConsole);
        Cubo("Ranhura_Moeda", posMoeda + new Vector3(0, 0.08f, -0.03f), new Vector3(0.04f, 0.16f, 0.02f), mPisoPreto, rootConsole);
        Cubo("Botao_Ejetar_Moeda", posMoeda + new Vector3(0.09f, -0.06f, -0.03f), new Vector3(0.07f, 0.07f, 0.03f), mNeonGold, rootConsole);

        // Joystick Arcade Físico Decorativo no Balcão
        Vector3 posJoy = new Vector3(0f, -1.82f, -2.88f);
        Cubo("Base_Joystick", posJoy, new Vector3(0.25f, 0.04f, 0.25f), mPisoPreto, rootConsole);
        Cubo("Haste_Joystick", posJoy + new Vector3(0, 0.15f, 0), new Vector3(0.03f, 0.26f, 0.03f), mAcoInox, rootConsole);
        GameObject topoJoy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        topoJoy.name = "Bola_Joystick";
        topoJoy.transform.parent = rootConsole;
        topoJoy.transform.position = posJoy + new Vector3(0, 0.28f, 0);
        topoJoy.transform.localScale = Vector3.one * 0.14f;
        topoJoy.GetComponent<MeshRenderer>().material = mNeonMagenta;
        Object.Destroy(topoJoy.GetComponent<Collider>());

        // Botões Arcade Iluminados Decorativos no Balcão
        Vector3[] btnPos = new Vector3[] {
            new Vector3(1.0f, -1.80f, -2.85f),
            new Vector3(1.3f, -1.80f, -2.85f)
        };
        for (int b = 0; b < btnPos.Length; b++)
        {
            GameObject btnArcade = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            btnArcade.name = $"Botao_Fisico_{b}";
            btnArcade.transform.parent = rootConsole;
            btnArcade.transform.position = btnPos[b];
            btnArcade.transform.localScale = new Vector3(0.14f, 0.04f, 0.14f);
            btnArcade.transform.rotation = Quaternion.Euler(15f, 0, 0);
            btnArcade.GetComponent<MeshRenderer>().material = b == 0 ? mNeonCyan : mNeonGold;
            Object.Destroy(btnArcade.GetComponent<Collider>());
        }

        // ======================== 6. CALHA DE PRÊMIOS & PORTINHOLA PUSH ========================
        Transform rootCalha = CriarSubGrupo("06_Calha_De_Premios");
        
        // Fundo e Rampa da Calha
        Cubo("Calha_Piso", new Vector3(-1.8f, -2.02f, -1.8f), new Vector3(1.15f, 0.08f, 1.15f), mChassis, rootCalha);
        // Bordas com Neon Cyan
        Cubo("Calha_Borda_X", new Vector3(-1.8f, -1.90f, -1.22f), new Vector3(1.2f, 0.20f, 0.08f), mNeonCyan, rootCalha);
        Cubo("Calha_Borda_Z", new Vector3(-1.22f, -1.90f, -1.8f), new Vector3(0.08f, 0.20f, 1.2f), mNeonCyan, rootCalha);

        // Cerca Protetora de Acrílico (impede as pelúcias de caírem sozinhas na calha)
        Cubo("Acrilico_Barreira_X", new Vector3(-1.8f, -1.58f, -1.22f), new Vector3(1.2f, 0.45f, 0.03f), mVidroAcrilico, rootCalha);
        Cubo("Acrilico_Barreira_Z", new Vector3(-1.22f, -1.58f, -1.8f), new Vector3(0.03f, 0.45f, 1.2f), mVidroAcrilico, rootCalha);

        // Placa Indicadora Luminosa da Calha (SAÍDA DE PRÊMIOS / DROP ZONE)
        Cubo("Placa_Sinalizadora_Calha", new Vector3(-1.8f, -1.25f, -1.22f), new Vector3(1.15f, 0.20f, 0.05f), mNeonGold, rootCalha);
        Cubo("Seta_Neon_Calha", new Vector3(-1.8f, -1.40f, -1.22f), new Vector3(0.18f, 0.16f, 0.06f), mNeonCyan, rootCalha);

        // Portinhola de Coleta "PUSH" na parte inferior frontal
        Vector3 posPush = new Vector3(-1.8f, -2.48f, -2.67f);
        Cubo("Moldura_Saida_Premios", posPush, new Vector3(1.15f, 0.70f, 0.06f), mAcoInox, rootCalha);
        Cubo("Portinhola_PUSH", posPush + new Vector3(0, 0, -0.02f), new Vector3(0.95f, 0.52f, 0.03f), mVidroFume, rootCalha);
        Cubo("Texto_PUSH_Inox", posPush + new Vector3(0, 0, -0.04f), new Vector3(0.40f, 0.12f, 0.01f), mNeonGold, rootCalha);

        // Luz Interna e Spotlight da Calha (Brilha intensamente no duto de queda)
        CriarLuz(new Vector3(-1.8f, -1.85f, -1.8f), new Color(1f, 0.9f, 0.5f), 4.5f, 3.2f, rootCalha);
        CriarSpotlight(new Vector3(-1.8f, -1.15f, -1.8f), new Color(1f, 0.88f, 0.4f), 5f, 65f, 3.5f, rootCalha);

        // Trigger de Entrega de Prêmios
        GameObject zona = new GameObject("ZonaDeEntrega_Invisivel");
        zona.transform.parent = rootCalha;
        zona.transform.position = new Vector3(-1.8f, -1.8f, -1.8f);
        zona.transform.localScale = new Vector3(1.2f, 1.0f, 1.2f);
        BoxCollider zbc = zona.AddComponent<BoxCollider>();
        zbc.isTrigger = true;
        PrizeDeliveryZone pdz = zona.AddComponent<PrizeDeliveryZone>();
        if (pdz.OnPrizeDelivered == null) pdz.OnPrizeDelivered = new UnityEngine.Events.UnityEvent<Prize>();
        pdz.OnPrizeDelivered.AddListener((prize) => {
            if (GameSession.Instance != null) GameSession.Instance.RegisterPrizeDelivered(prize);
        });

        // ======================== 7. SISTEMA DE TRILHOS E GANTRY NO TETO ========================
        Transform rootGantry = CriarSubGrupo("07_Gantry_Guindaste");
        
        // Trilhos Longitudinais em Z (fixos nas laterais do teto)
        Cubo("Trilho_Z_Esq", new Vector3(-2.1f, 2.85f, 0), new Vector3(0.12f, 0.12f, 4.8f), mAcoInox, rootGantry);
        Cubo("Trilho_Z_Dir", new Vector3(2.1f, 2.85f, 0), new Vector3(0.12f, 0.12f, 4.8f), mAcoInox, rootGantry);

        // Barra Transversal Móvel em X (Viga que desliza em Z)
        GameObject crossbarObj = Cubo("Viga_Gantry_X", new Vector3(0, 2.86f, 0), new Vector3(4.3f, 0.14f, 0.14f), mAcoInox, rootGantry);
        GantryCrossbar = crossbarObj.transform;

        // Carro Motorizado do Guindaste (Trolley móvel que desliza em X e segura o cabo da garra)
        GantryTrolley = new GameObject("Trolley_Motor_Guindaste");
        GantryTrolley.transform.parent = rootGantry;
        GantryTrolley.transform.position = new Vector3(0, 2.85f, 0);

        Cubo("Motor_Box", Vector3.zero, new Vector3(0.48f, 0.22f, 0.48f), mChassis, GantryTrolley.transform);
        Cubo("Polia_Esquerda", new Vector3(-0.25f, 0, 0), new Vector3(0.06f, 0.16f, 0.16f), mAcoInox, GantryTrolley.transform);
        Cubo("Polia_Direita", new Vector3(0.25f, 0, 0), new Vector3(0.06f, 0.16f, 0.16f), mAcoInox, GantryTrolley.transform);
        Cubo("Spool_Cabo", new Vector3(0, -0.08f, 0), new Vector3(0.14f, 0.08f, 0.14f), mDourado, GantryTrolley.transform);
        Cubo("LED_Status_Gantry", new Vector3(0, 0.12f, -0.22f), new Vector3(0.06f, 0.06f, 0.04f), mNeonGreen, GantryTrolley.transform);

        // Adiciona seguidor inteligente do Gantry para acompanhar a garra em tempo real
        GantryTrolley.AddComponent<GantryFollower>();

        // ======================== 8. DOSSEL SUPERIOR & LIGHTBOX MARQUEE ========================
        Transform rootDossel = CriarSubGrupo("08_Dossel_Marquee");
        
        // Caixa do Dossel Inclinada
        Cubo("Dossel_Teto", new Vector3(0, 3.25f, 0), new Vector3(5.4f, 0.65f, 5.4f), mChassis, rootDossel);
        Cubo("Moldura_Dossel_Inox", new Vector3(0, 2.95f, 0), new Vector3(5.45f, 0.06f, 5.45f), mAcoInox, rootDossel);

        // Letreiro Marquee Retroiluminado com Arte Gráfica
        GameObject marquee = Cubo("Letreiro_Marquee_Box", new Vector3(0, 3.28f, -2.76f), new Vector3(4.8f, 0.50f, 0.10f), mChassis, rootDossel);
        marquee.transform.rotation = Quaternion.Euler(-8f, 0, 0); // Inclinado ligeiramente para baixo em direção ao jogador
        Cubo("Letreiro_Luminoso_Face", new Vector3(0, 3.28f, -2.82f), new Vector3(4.5f, 0.38f, 0.03f), mMarquee, rootDossel);
        Cubo("Friso_Neon_Top_Marquee", new Vector3(0, 3.52f, -2.82f), new Vector3(4.7f, 0.04f, 0.04f), mNeonCyan, rootDossel);
        Cubo("Friso_Neon_Bot_Marquee", new Vector3(0, 3.04f, -2.82f), new Vector3(4.7f, 0.04f, 0.04f), mNeonMagenta, rootDossel);

        // Caixas de Som Estéreo no Dossel
        Cubo("Caixa_Som_Esq", new Vector3(-2.1f, 3.28f, -2.74f), new Vector3(0.42f, 0.42f, 0.08f), mPisoPreto, rootDossel);
        Cubo("Caixa_Som_Dir", new Vector3(2.1f, 3.28f, -2.74f), new Vector3(0.42f, 0.42f, 0.08f), mPisoPreto, rootDossel);

        // Iluminação Principal de Vitrine Arcade (Spotlights no teto focando as pelúcias e a garra)
        CriarSpotlight(new Vector3(0.35f, 2.85f, 0.35f), new Color(1.0f, 0.96f, 0.90f), 8f, 75f, 4.5f, rootDossel);
        CriarSpotlight(new Vector3(-0.85f, 2.85f, 0.85f), new Color(0.85f, 0.95f, 1.0f), 7f, 65f, 2.5f, rootDossel);
        CriarLuz(new Vector3(0.35f, 2.60f, 0.35f), new Color(1f, 0.98f, 0.92f), 6.5f, 2.0f, rootDossel);

        // ======================== 9. ILUMINAÇÃO VOLUMÉTRICA ARCADE ========================
        Transform rootLuzes = CriarSubGrupo("09_Iluminacao_Arcade");
        // Dois grandes holofotes (Spotlights) superiores focados no poço
        CriarSpotlight(new Vector3(0, 3.1f, 0.2f), new Color(1.0f, 0.98f, 0.90f), 12f, 75f, 4.5f, rootLuzes);
        CriarSpotlight(new Vector3(-0.7f, 3.1f, -0.6f), new Color(0.92f, 0.96f, 1.0f), 10f, 65f, 3.0f, rootLuzes);
        
        // Luzes neon ambiente de preenchimento
        CriarLuz(new Vector3(-2.1f, 2.7f, -2.1f), new Color(1f, 0.15f, 0.65f), 7f, 2.0f, rootLuzes);
        CriarLuz(new Vector3(2.1f, 2.7f, 2.1f), new Color(0f, 0.9f, 1f), 7f, 2.0f, rootLuzes);

        // ======================== 10. CENÁRIO DE FUNDO TOKYO ARCADE ========================
        Transform rootBackdrop = CriarSubGrupo("10_Cenario_Fundo_Tokyo");
        if (texArcadeRoom != null)
        {
            // Mural panorâmico em perspectiva profunda atrás da máquina
            Cubo("Backdrop_Tokyo_Arcade", new Vector3(0, 1.5f, 9.5f), new Vector3(28.0f, 15.0f, 0.1f), mArcadeBackdrop, rootBackdrop);
            // Piso estendido escuro do fliperama com reflexos
            Material mPisoFliperama = CriarMaterialURP(new Color(0.04f, 0.04f, 0.06f), 0.85f, 0.2f);
            Cubo("Chao_Fliperama_Lustroso", new Vector3(0, -2.95f, 6.0f), new Vector3(32.0f, 0.1f, 18.0f), mPisoFliperama, rootBackdrop);
        }
    }

    private Transform CriarSubGrupo(string nome)
    {
        GameObject go = new GameObject(nome);
        go.transform.SetParent(RootCabinet.transform, false);
        return go.transform;
    }

    private GameObject Cubo(string nome, Vector3 pos, Vector3 esc, Material mat, Transform pai = null)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = nome;
        if (pai != null) c.transform.SetParent(pai, false);
        c.transform.localPosition = pos;
        c.transform.localScale = esc;
        if (mat != null)
        {
            MeshRenderer mr = c.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = mat;
        }
        return c;
    }

    private void CriarColisorInvisivel(string nome, Vector3 pos, Vector3 esc, Transform pai)
    {
        GameObject c = new GameObject(nome);
        c.transform.parent = pai;
        c.transform.localPosition = pos;
        c.transform.localScale = esc;
        c.AddComponent<BoxCollider>();
    }

    private Material CriarMaterialURP(Color cor, float smoothness, float metallic)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = cor;
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        return m;
    }

    private Material CriarEmissivo(Color cor, float intensidade)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.color = cor;
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", cor * intensidade);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        return m;
    }

    private Material CriarVidro(Color cor, float smoothness)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", 0);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 3000;
        m.color = cor;
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return m;
    }

    private void CriarLuz(Vector3 pos, Color cor, float range, float intensidade, Transform pai)
    {
        GameObject l = new GameObject("Luz_Neon");
        l.transform.SetParent(pai, false);
        l.transform.localPosition = pos;
        Light light = l.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = cor;
        light.range = range;
        light.intensity = intensidade;
        #if UNITY_EDITOR
        light.lightmapBakeType = LightmapBakeType.Baked;
#endif
    }

    private void CriarSpotlight(Vector3 pos, Color cor, float range, float spotAngle, float intensidade, Transform pai)
    {
        GameObject l = new GameObject("Spotlight_Arcade");
        l.transform.SetParent(pai, false);
        l.transform.localPosition = pos;
        l.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Light light = l.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = cor;
        light.range = range;
        light.spotAngle = spotAngle;
        light.intensity = intensidade;
        #if UNITY_EDITOR
        light.lightmapBakeType = LightmapBakeType.Baked;
#endif
    }
}
