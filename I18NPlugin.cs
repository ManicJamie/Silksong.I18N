using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using TeamCherry.Localization;

namespace Silksong.I18N;

[BepInAutoPlugin(id: "org.silksong-modding.i18n")]
public sealed partial class I18NPlugin : BaseUnityPlugin
{
    internal new ManualLogSource Logger => base.Logger;

    private void Start()
    {
        _instance = this;
        new Harmony(Id).PatchAll();

        useLanguageOverride = Config.Bind<bool>(
            "General",
            "Use Language Override",
            false,
            "Whether to manually specify the language used for all modded text."
        );
        languageOverride = Config.Bind<SupportedLanguages>(
            "General",
            "Language Override",
            SupportedLanguages.EN,
            "Modded text will use this language if Use Language Override is enabled."
        );
        useLanguageOverride.SettingChanged += (_, _) => LanguagePatches.LoadAllModSheets();
        languageOverride.SettingChanged += (_, _) => LanguagePatches.LoadAllModSheets();

        LanguagePatches.LoadAllModSheets();
    }

    private static I18NPlugin? _instance = null;
    internal static I18NPlugin Instance =>
        _instance != null
            ? _instance
            : throw new NullReferenceException("I18N instance is not ready");

    private ConfigEntry<bool>? useLanguageOverride;
    private ConfigEntry<SupportedLanguages>? languageOverride;

    public LanguageCode? LanguageOverride
    {
        get
        {
            if (useLanguageOverride is not null && useLanguageOverride.Value)
            {
                return (LanguageCode?)languageOverride?.Value;
            }

            return null;
        }
    }

    public bool UseLanguageOverride =>
        useLanguageOverride is not null ? useLanguageOverride.Value : false;
}
