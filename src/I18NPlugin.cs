using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GlobalEnums;
using HarmonyLib;
using TeamCherry.Localization;

namespace Silksong.I18N;

[BepInAutoPlugin(id: "org.silksong-modding.i18n")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public sealed partial class I18NPlugin : BaseUnityPlugin
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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

    /// <summary>
    /// The language used by I18N for modded text.
    /// </summary>
    /// <remarks>
    /// If this property returns null, then the base game language (see <see cref="Language.CurrentLanguage"/>) will be used.
    /// </remarks>
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

    /// <summary>
    /// Boolean value indicating whether or not the modded language is overriding the base game language.
    /// </summary>
    public bool UseLanguageOverride =>
        useLanguageOverride is not null ? useLanguageOverride.Value : false;
}
