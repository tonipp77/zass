namespace Zass.Core.Settings;

/// <summary>
/// UI language preference (RF-20). <see cref="System"/> follows the OS UI language
/// when it is Spanish or English, falling back to English in any other case.
/// </summary>
public enum LanguageOption
{
    /// <summary>Follow the operating system UI language (es/en, else English).</summary>
    System,

    /// <summary>Force English.</summary>
    English,

    /// <summary>Force Spanish.</summary>
    Spanish,
}
