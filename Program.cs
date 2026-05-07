// ============================================================
// 文件: Program.cs
// 作用: 应用程序入口 — 启动 WinForms 应用并加载配置
// ============================================================

using System.Text;
using Modbus上位机.Forms;
using Modbus上位机.Helpers;
using Modbus上位机.Services;

namespace Modbus上位机;

/// <summary>
/// 应用程序入口类
/// </summary>
internal static class Program
{
    /// <summary>
    /// 应用程序主入口点
    /// </summary>
    [STAThread] // STA = 单线程套间，WinForms 必须
    static void Main()
    {
        // 全局 UTF-8 编码设置 — 确保所有文件读写、网络通信使用 UTF-8
        // CodePagesEncodingProvider 提供额外的编码支持（中文 GB2312 等）
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 加载保存的连接配置，从中获取上次使用的语言设置
        var savedSettings = ConfigService.LoadConnectionSettings();
        LanguageHelper.SetLanguage(savedSettings.Language);

        // WinForms 高 DPI 配置
        Application.EnableVisualStyles();             // 启用视觉样式（WinXP 风格）
        Application.SetCompatibleTextRenderingDefault(false); // 使用 GDI+ 渲染文字
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);  // 每显示器独立 DPI 缩放

        // 启动主窗口
        Application.Run(new MainForm());
    }
}
