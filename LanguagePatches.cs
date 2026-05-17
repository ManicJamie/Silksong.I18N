using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using Newtonsoft.Json;
using TeamCherry.Localization;

namespace Silksong.I18N;

[HarmonyPatch]
internal static class LanguagePatches
{
    [HarmonyPatch(typeof(Language), nameof(Language.DoSwitch))]
    [HarmonyPostfix]
    private static void OnLanguageSwitched() => LoadAllModSheets();

    public static void LoadAllModSheets()
    {
        LanguageCode lang = I18NPlugin.Instance.LanguageOverride ?? Language._currentLanguage;
        if (I18NPlugin.Instance.UseLanguageOverride)
        {
            I18NPlugin.Instance.Logger.LogDebug($"using language override {lang}");
        }

        foreach ((string? id, PluginInfo? info) in Chainloader.PluginInfos)
        {
            BaseUnityPlugin mod = info.Instance;
            if (!mod)
            {
                continue;
            }

            Assembly modAsm = mod.GetType().Assembly;
            if (modAsm.Location.IsNullOrWhiteSpace())
            {
                I18NPlugin.Instance.Logger.LogDebug(
                    $"mod {id} assembly has no location, "
                        + $"if you are using ScriptEngine, "
                        + $"please enable DumpedAssemblies of ScriptEngine "
                        + $"and place the languages folder in BepInEx\\ScriptEngineDumpedAssemblies"
                );
                continue;
            }

            var modDir = Path.GetDirectoryName(modAsm.Location);
            if (!Directory.Exists(modDir))
            {
                continue;
            }

            var isPluginsDir = string.Equals(
                Path.GetFullPath(modDir).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(Paths.PluginPath).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.InvariantCultureIgnoreCase
            );

            if (isPluginsDir)
            {
                I18NPlugin.Instance.Logger.LogInfo(
                    $"mod {id} installed directly in plugins dir, not loading languages"
                );
                continue;
            }

            Dictionary<string, string>? fallbackSheet = null;
            NeutralResourcesLanguageAttribute? langAttr =
                modAsm.GetCustomAttribute<NeutralResourcesLanguageAttribute>();
            if (langAttr is not null)
            {
                // We do effectively `.ToUpper().ToLower()` here to maintain semantic parity with
                // the subsequent call to `LoadModSheet` and avoid making assumptions about the
                // Unicode behavior of the `NeutralResourcesLanguageAttribute` string.
                var fallbackLang = langAttr.CultureName.ToUpper();
                fallbackSheet = LoadModSheet(modDir, fallbackLang.ToLower());
                if (fallbackSheet is not null)
                {
                    I18NPlugin.Instance.Logger.LogDebug(
                        $"loaded fallback sheet in language {fallbackLang} for mod {id}"
                    );
                }
            }

            Dictionary<string, string>? sheet = LoadModSheet(
                modDir,
                lang.ToString().ToLower(),
                fallbackSheet
            );
            if (sheet is not null)
            {
                Language._currentEntrySheets[$"Mods.{id}"] = sheet;
                I18NPlugin.Instance.Logger.LogDebug(
                    $"loaded sheet in language {lang} for mod {id}"
                );
            }
        }
    }

    private static Dictionary<string, string>? LoadModSheet(
        string modDir,
        string lang,
        Dictionary<string, string>? fallback = null
    )
    {
        var opts = new EnumerationOptions();
        opts.MatchCasing = MatchCasing.CaseInsensitive;

        var hit = false;
        Dictionary<string, string> modSheet = fallback ?? new Dictionary<string, string>();

        try
        {
            IEnumerable<Dictionary<string, string>> modSheets = Directory
                .EnumerateDirectories(modDir, "languages", opts)
                .SelectMany(dir => Directory.EnumerateFiles(dir, $"{lang}.json", opts))
                .OrderBy(p => p)
                .Select(p => ReadSheetFile(p))
                .OfType<Dictionary<string, string>>();

            foreach (Dictionary<string, string> sheet in modSheets)
            {
                if (hit)
                {
                    I18NPlugin.Instance.Logger.LogWarning(
                        $"multiple casings found for language {lang.ToUpper()} in: {modDir}"
                    );
                }

                hit = true;
                foreach ((string? k, string? v) in sheet)
                {
                    modSheet[k] = v;
                }
            }
        }
        catch (Exception ex)
        {
            I18NPlugin.Instance.Logger.LogError($"unable to load mod sheets: {modDir}\n{ex}");
            return null;
        }

        if (hit || fallback is not null)
        {
            return modSheet;
        }
        else
        {
            return null;
        }
    }

    private static Dictionary<string, string>? ReadSheetFile(string path)
    {
        try
        {
            using var s = new StreamReader(File.OpenRead(path), Encoding.UTF8, false);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(s.ReadToEnd());
        }
        catch (Exception ex)
        {
            I18NPlugin.Instance.Logger.LogError($"unable to read language file: {path}\n{ex}");
            return null;
        }
    }

    [HarmonyPatch(typeof(Language), nameof(Language.Get), [typeof(string), typeof(string)])]
    [HarmonyPostfix]
    private static void OnGetLocalizedText(string? key, string? sheetTitle) =>
        WarnIfModKeyMissing(sheetTitle, key);

#pragma warning disable Harmony003
    [HarmonyPatch(typeof(LocalisedString), nameof(LocalisedString.ToString), [typeof(bool)])]
    [HarmonyPostfix]
    private static void OnGetLocalizedString(LocalisedString __instance, bool allowBlankText) =>
        WarnIfModKeyMissing(__instance.Sheet, __instance.Key, allowBlankText);
#pragma warning restore Harmony003

    private static void WarnIfModKeyMissing(string? sheet, string? key, bool allowBlankText = true)
    {
        if (!string.IsNullOrEmpty(sheet) && !string.IsNullOrEmpty(key) && sheet.StartsWith("Mods."))
        {
            if (!Language.Has(key, sheet))
            {
                LanguageCode lang = Language.CurrentLanguage();
                var modId = sheet.Substring("Mods.".Length);
                I18NPlugin.Instance.Logger.LogWarning(
                    $"language {lang} for mod {modId} missing: {key}"
                );
            }
            else if (!allowBlankText)
            {
                var text = LocalisedString.ReplaceTags(Language.Get(key, sheet));
                if (string.IsNullOrWhiteSpace(text))
                {
                    LanguageCode lang = Language.CurrentLanguage();
                    var modId = sheet.Substring("Mods.".Length);
                    I18NPlugin.Instance.Logger.LogWarning(
                        $"language {lang} for mod {modId} is blank at: {key}"
                    );
                }
            }
        }
    }
}
