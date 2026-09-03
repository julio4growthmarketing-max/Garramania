using System;
using System.Collections.Generic;
using UnityEngine;

public enum CabinetThemeType
{
    CyberNeon,
    KawaiiPastel,
    GoldCasino
}

[Serializable]
public class CabinetThemeData
{
    public CabinetThemeType themeType;
    public string displayName;
    public string badgeIcon;

    // Cores do Chassis e Painéis
    public Color chassisColor;
    public Color accentMetalColor;
    public Color floorColorA;
    public Color floorColorB;

    // Iluminação e Neons Emissivos
    public Color neonColor1;
    public Color neonColor2;
    public Color marqueeColor;
    public Color spotlightColor;

    // Texto do Letreiro Superior
    public string marqueeTitle;
    public string wallpaperResourcePath;

    // Prêmios exclusivos desta cabine
    public List<string> exclusivePrizeIds;
    public List<string> featuredPrizeIds => exclusivePrizeIds;

    public static CabinetThemeData CreateCyberNeon()
    {
        return new CabinetThemeData
        {
            themeType = CabinetThemeType.CyberNeon,
            displayName = "CYBER NEON 🕹️",
            badgeIcon = "🕹️",
            chassisColor = new Color(0.09f, 0.09f, 0.12f),       // Grafite meia-noite
            accentMetalColor = new Color(0.82f, 0.85f, 0.90f),   // Aço inox escovado
            floorColorA = new Color(0.92f, 0.94f, 0.98f),        // Xadrez branco
            floorColorB = new Color(0.06f, 0.07f, 0.09f),        // Xadrez preto
            neonColor1 = new Color(0.20f, 1.60f, 2.20f),         // Ciano elétrico
            neonColor2 = new Color(2.40f, 0.30f, 1.40f),         // Magenta neon
            marqueeColor = new Color(1.80f, 1.40f, 0.30f),       // Dourado
            spotlightColor = new Color(0.90f, 0.95f, 1.00f),     // Luz branca pura
            marqueeTitle = "GARRAMANIA NEON",
            wallpaperResourcePath = "Textures/Wallpaper_CyberNeon",
            exclusivePrizeIds = new List<string> { "Fox", "GreenBear", "BalloonFish", "Koala", "Badger", "Porky" }
        };
    }

    public static CabinetThemeData CreateKawaiiPastel()
    {
        return new CabinetThemeData
        {
            themeType = CabinetThemeType.KawaiiPastel,
            displayName = "KAWAII CANDY 🌸",
            badgeIcon = "🌸",
            chassisColor = new Color(0.98f, 0.88f, 0.92f),       // Rosa pastel suave
            accentMetalColor = new Color(1.00f, 0.95f, 0.85f),   // Creme perolado
            floorColorA = new Color(1.00f, 0.92f, 0.96f),        // Rosa bebê
            floorColorB = new Color(1.00f, 1.00f, 1.00f),        // Branco puro
            neonColor1 = new Color(2.20f, 0.60f, 1.60f),         // Rosa chiclete vibrante
            neonColor2 = new Color(1.90f, 1.80f, 0.40f),         // Amarelo baunilha quente
            marqueeColor = new Color(2.20f, 0.80f, 1.50f),
            spotlightColor = new Color(1.00f, 0.92f, 0.95f),     // Luz quente rosada
            marqueeTitle = "SWEET CANDY CLAW",
            wallpaperResourcePath = "Textures/Wallpaper_KawaiiCandy",
            exclusivePrizeIds = new List<string> { "Fox_Arctic", "Bear_Polar", "Bear_Panda", "Koala_Eucalyptus", "Fish_Clown", "Porky_Classic" }
        };
    }

    public static CabinetThemeData CreateGoldCasino()
    {
        return new CabinetThemeData
        {
            themeType = CabinetThemeType.GoldCasino,
            displayName = "GOLD CASINO 👑",
            badgeIcon = "👑",
            chassisColor = new Color(0.05f, 0.05f, 0.06f),       // Preto fosco profundo
            accentMetalColor = new Color(0.95f, 0.78f, 0.20f),   // Ouro nobre / Latão polido
            floorColorA = new Color(0.12f, 0.12f, 0.14f),        // Mármore escuro
            floorColorB = new Color(0.85f, 0.70f, 0.20f),        // Detalhes em ouro
            neonColor1 = new Color(2.40f, 1.80f, 0.20f),         // Dourado radiante
            neonColor2 = new Color(2.00f, 1.00f, 0.10f),         // Âmbar
            marqueeColor = new Color(2.50f, 2.00f, 0.30f),
            spotlightColor = new Color(1.00f, 0.88f, 0.50f),     // Luz de cassino dourada
            marqueeTitle = "VIP HIGH ROLLER",
            wallpaperResourcePath = "Textures/Wallpaper_GoldCasino",
            exclusivePrizeIds = new List<string> { "Fish_Gold", "Badger_Honey", "Fox_Shadow", "Bear_Galaxy", "Koala_King", "Porky_Diamond" }
        };
    }
}
