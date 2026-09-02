using NUnit.Framework;
using UnityEngine;

public class UIThemeTests
{
    [Test]
    public void UITheme_ColorsAreDefinedAndNonZeroAlpha()
    {
        Assert.Greater(UITheme.ColorBgDeepNavy.a, 0.5f, "Alpha do fundo Deep Navy deve ser visível");
        Assert.AreEqual(1f, UITheme.ColorNeonGold.a, "Alpha do Neon Gold deve ser 1.0");
        Assert.AreEqual(1f, UITheme.ColorNeonCyan.a, "Alpha do Neon Cyan deve ser 1.0");
        Assert.AreEqual(1f, UITheme.ColorNeonRed.a, "Alpha do Neon Red deve ser 1.0");
    }

    [Test]
    public void UITheme_RoundedRectSprite_IsCachedAndReused()
    {
        Sprite s1 = UITheme.GetRoundedRectSprite();
        Sprite s2 = UITheme.GetRoundedRectSprite();

        Assert.IsNotNull(s1, "GetRoundedRectSprite não deve ser nulo");
        Assert.AreSame(s1, s2, "Chamadas subsequentes devem retornar a mesma instância em cache");
        Assert.AreEqual(new Vector4(10, 10, 10, 10), s1.border, "Sprite 9-slice deve ter bordas corretas");
    }

    [Test]
    public void UITheme_CircleSprite_IsCachedAndReused()
    {
        Sprite c1 = UITheme.GetCircleSprite();
        Sprite c2 = UITheme.GetCircleSprite();

        Assert.IsNotNull(c1, "GetCircleSprite não deve ser nulo");
        Assert.AreSame(c1, c2, "Círculo deve ser mantido em cache");
    }

    [Test]
    public void UITheme_GetButtonStyle_ResolvesAllThemes()
    {
        foreach (Button3DTheme theme in System.Enum.GetValues(typeof(Button3DTheme)))
        {
            UITheme.ButtonStyle style = UITheme.GetButtonStyle(theme);
            Assert.Greater(style.BgColor.a, 0f, $"Tema {theme} deve ter cor de fundo com alpha > 0");
            Assert.Greater(style.LabelColor.a, 0f, $"Tema {theme} deve ter cor de texto com alpha > 0");
        }
    }
}
