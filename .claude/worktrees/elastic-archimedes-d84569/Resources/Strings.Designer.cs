// ============================================================
// 文件: Strings.Designer.cs
// 作用: 资源文件的强类型访问器 — 让代码可以通过
//       Resources.Strings.AppTitle 等方式获取多语言文本
// 说明: 正常情况下由 Visual Studio 自动生成，本项目手动维护
// ============================================================

#nullable enable

using System.Resources;
using System.Globalization;

namespace ModbusSCADA.Resources;

/// <summary>
/// 多语言字符串资源访问器
/// </summary>
internal static class Strings
{
    private static ResourceManager? _resourceManager;

    /// <summary>资源管理器 — 根据线程文化自动选择对应语言的 RESX 文件</summary>
    internal static ResourceManager ResourceManager
    {
        get
        {
            if (_resourceManager == null)
            {
                // "ModbusSCADA.Resources.Strings" = 命名空间.目录名.文件名
                // 对应 Resources/Strings.resx
                // Assembly 对象用于找到嵌入的资源
                _resourceManager = new ResourceManager(
                    "ModbusSCADA.Resources.Strings",
                    typeof(Strings).Assembly);
            }
            return _resourceManager;
        }
    }

    /// <summary>获取当前文化下的字符串资源</summary>
    private static string GetString(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
    }

    // ---------- 应用标题 ----------
    internal static string AppTitle => GetString("AppTitle");

    // ---------- 连接 ----------
    internal static string Connect => GetString("Connect");
    internal static string Disconnect => GetString("Disconnect");
    internal static string ConnectionStatus => GetString("ConnectionStatus");
    internal static string Connected => GetString("Connected");
    internal static string NotConnected => GetString("NotConnected");
    internal static string ConfigConnection => GetString("ConfigConnection");

    // ---------- 变量管理 ----------
    internal static string VariableMonitor => GetString("VariableMonitor");
    internal static string AddVariable => GetString("AddVariable");
    internal static string EditVariable => GetString("EditVariable");
    internal static string DeleteVariable => GetString("DeleteVariable");
    internal static string ImportVariables => GetString("ImportVariables");
    internal static string ExportVariables => GetString("ExportVariables");
    internal static string Refresh => GetString("Refresh");

    // ---------- 变量操作 ----------
    internal static string VariableOperation => GetString("VariableOperation");
    internal static string SelectedVariable => GetString("SelectedVariable");
    internal static string CurrentValue => GetString("CurrentValue");
    internal static string WriteNewValue => GetString("WriteNewValue");
    internal static string Write => GetString("Write");

    // ---------- 表格列头 ----------
    internal static string ColName => GetString("ColName");
    internal static string ColAddress => GetString("ColAddress");
    internal static string ColDataType => GetString("ColDataType");
    internal static string ColValue => GetString("ColValue");
    internal static string ColStatus => GetString("ColStatus");
    internal static string ColInterval => GetString("ColInterval");
    internal static string ColCanWrite => GetString("ColCanWrite");

    // ---------- 通信状态 ----------
    internal static string StatusGood => GetString("StatusGood");
    internal static string StatusError => GetString("StatusError");

    // ---------- 语言 ----------
    internal static string SwitchLanguage => GetString("SwitchLanguage");

    // ---------- 消息 ----------
    internal static string MsgDisconnectFirst => GetString("MsgDisconnectFirst");
    internal static string MsgConnectSuccess => GetString("MsgConnectSuccess");
    internal static string MsgConnectFail => GetString("MsgConnectFail");
    internal static string MsgWriteSuccess => GetString("MsgWriteSuccess");
    internal static string MsgWriteFail => GetString("MsgWriteFail");
    internal static string MsgConfirmDelete => GetString("MsgConfirmDelete");
    internal static string MsgSaveSuccess => GetString("MsgSaveSuccess");
    internal static string MsgSelectVariable => GetString("MsgSelectVariable");
    internal static string MsgVarNameEmpty => GetString("MsgVarNameEmpty");
    internal static string MsgVarNameDuplicate => GetString("MsgVarNameDuplicate");
    internal static string MsgVarAddrRange => GetString("MsgVarAddrRange");
    internal static string MsgVarIntervalRange => GetString("MsgVarIntervalRange");
    internal static string MsgCantWriteReadOnly => GetString("MsgCantWriteReadOnly");

    // ---------- 数据类型 ----------
    internal static string DT_Coil => GetString("DT_Coil");
    internal static string DT_DiscreteInput => GetString("DT_DiscreteInput");
    internal static string DT_InputRegister => GetString("DT_InputRegister");
    internal static string DT_HoldingRegister => GetString("DT_HoldingRegister");

    // ---------- 连接设置弹窗 ----------
    internal static string ConnSettingsTitle => GetString("ConnSettingsTitle");
    internal static string IPAddress => GetString("IPAddress");
    internal static string Port => GetString("Port");
    internal static string SlaveId => GetString("SlaveId");
    internal static string Timeout => GetString("Timeout");
    internal static string ReconnectInterval => GetString("ReconnectInterval");
    internal static string Save => GetString("Save");
    internal static string Cancel => GetString("Cancel");

    // ---------- 变量配置弹窗 ----------
    internal static string VarConfigTitle => GetString("VarConfigTitle");
    internal static string VarName => GetString("VarName");
    internal static string VarAddress => GetString("VarAddress");
    internal static string VarDataType => GetString("VarDataType");
    internal static string VarPollInterval => GetString("VarPollInterval");
    internal static string VarCanWrite => GetString("VarCanWrite");
}
