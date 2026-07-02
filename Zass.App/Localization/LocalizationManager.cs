using System.Globalization;
using System.Threading;
using Zass.Core.Settings;

namespace Zass.App.Localization;

/// <summary>
/// Applies the UI language at runtime (RF-20) by switching the current UI culture,
/// which the <see cref="Resources.Strings"/> resource manager reads on every lookup.
/// Live windows refresh their text in response to <see cref="LanguageChanged"/>;
/// the overlay is recreated per capture, so it always picks up the current language.
/// </summary>
/// <remarks>
/// The architecture (§9) suggests swappable <c>ResourceDictionary</c> files, but the
/// codebase committed to satellite <c>.resx</c> assemblies in Increment 0. Switching
/// <see cref="CultureInfo.CurrentUICulture"/> plus a refresh event achieves the same
/// no-restart behavior without rewriting the validated localization layer.
/// </remarks>
internal static class LocalizationManager
{
    // Captured at type load, before any Apply override, so "System" always resolves
    // against the real OS UI language rather than a culture we set ourselves.
    private static readonly string SystemTwoLetterLanguage =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    /// <summary>Raised after the UI language changes so live UI can refresh its text.</summary>
    public static event EventHandler? LanguageChanged;

    /// <summary>The language currently applied to the process.</summary>
    public static LanguageOption Current { get; private set; } = LanguageOption.System;

    /// <summary>
    /// Applies <paramref name="option"/> to the current and default-for-new-threads UI
    /// culture, then raises <see cref="LanguageChanged"/>. Idempotent re-applies still
    /// raise the event so callers can force a refresh.
    /// </summary>
    public static void Apply(LanguageOption option)
    {
        Current = option;
        CultureInfo culture = Resolve(option);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Resolves the concrete culture for a language preference.</summary>
    public static CultureInfo Resolve(LanguageOption option) => option switch
    {
        LanguageOption.Spanish => new CultureInfo("es"),
        LanguageOption.English => new CultureInfo("en"),
        _ => SystemTwoLetterLanguage == "es" ? new CultureInfo("es") : new CultureInfo("en"),
    };
}
