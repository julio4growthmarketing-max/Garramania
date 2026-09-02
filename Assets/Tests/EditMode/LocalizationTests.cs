using NUnit.Framework;
using UnityEngine;

public class LocalizationTests
{
    private GameObject locObj;
    private LocalizationManager locManager;

    [SetUp]
    public void Setup()
    {
        locObj = new GameObject("Test_LocalizationManager");
        locManager = locObj.AddComponent<LocalizationManager>();
    }

    [TearDown]
    public void Teardown()
    {
        if (locObj != null)
        {
            Object.DestroyImmediate(locObj);
        }
    }

    [Test]
    public void LocalizationManager_ReturnsDefaultKeyIfNotFound()
    {
        string result = LocalizationManager.Get("CHAVE_INEXISTENTE_XYZ", "FallbackText");
        Assert.AreEqual("FallbackText", result);
    }

    [Test]
    public void LocalizationManager_FormatStringWorks()
    {
        string formatted = LocalizationManager.Format("ALBUM_PROGRESS", 2, 6, 33);
        Assert.IsNotNull(formatted);
        Assert.IsTrue(formatted.Contains("2") && formatted.Contains("6"), "String formatada deve conter os parâmetros numéricos");
    }

    [Test]
    public void LocalizationManager_CanSwitchLanguages()
    {
        locManager.SetLanguage(LocalizationManager.LANG_EN);
        Assert.AreEqual(LocalizationManager.LANG_EN, locManager.CurrentLanguage);

        locManager.SetLanguage(LocalizationManager.LANG_PT);
        Assert.AreEqual(LocalizationManager.LANG_PT, locManager.CurrentLanguage);
    }
}
