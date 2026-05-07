// ============================================================
// 文件: LanguageHelper.cs
// 作用: 多语言运行时切换 — 支持 zh-CN（中文）、en（英文）、ru（俄文）
// 原理: 修改当前线程的 UI 文化，让 RESX 资源自动切换语言
// ============================================================

using System.Globalization;

namespace ModbusSCADA.Helpers;

/// <summary>
/// 语言助手 — 管理多语言切换和当前语言状态
/// </summary>
public static class LanguageHelper
{
    // ---------- 支持的语言列表 ----------

    /// <summary>支持的语言字典 — key=语言代码, value=显示名称</summary>
    public static readonly Dictionary<string, string> SupportedLanguages = new()
    {
        { "zh-CN", "中文" },
        { "en",    "English" },
        { "ru",    "Русский" }
    };

    /// <summary>当前使用的语言代码</summary>
    public static string CurrentLanguage { get; private set; } = "zh-CN";

    // ================================================================
    // 语言切换
    // ================================================================

    /// <summary>
    /// 切换应用程序语言
    /// </summary>
    /// <param name="cultureCode">语言代码，例如 "zh-CN" / "en" / "ru"</param>
    public static void SetLanguage(string cultureCode)
    {
        // 只处理支持的语言
        if (!SupportedLanguages.ContainsKey(cultureCode)) return;

        // 设置当前线程的 UI 文化 → 会影响 ResourceManager 的 GetString 行为
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureCode);
        // 设置当前线程的文化 → 影响日期、数字格式等
        Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureCode);

        // 更新当前语言记录
        CurrentLanguage = cultureCode;
    }

    /// <summary>
    /// 获取当前语言的显示名称（用于状态栏显示）
    /// </summary>
    public static string GetCurrentLanguageDisplay()
    {
        return SupportedLanguages.TryGetValue(CurrentLanguage, out var name)
            ? name
            : CurrentLanguage;
    }

    /// <summary>
    /// 获取指定语言代码对应的显示名称
    /// </summary>
    public static string GetDisplayName(string cultureCode)
    {
        return SupportedLanguages.TryGetValue(cultureCode, out var name)
            ? name
            : cultureCode;
    }
}
