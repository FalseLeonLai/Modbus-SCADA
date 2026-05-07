// ============================================================
// 文件: ConfigService.cs
// 作用: 处理 JSON 配置文件的读写 — 连接设置、变量列表、语言偏好
// 使用: Newtonsoft.Json 序列化
// ============================================================

using Newtonsoft.Json;
using Modbus上位机.Models;

namespace Modbus上位机.Services;

/// <summary>
/// 配置服务 — 负责将连接设置和变量列表保存到 JSON 文件，或从 JSON 文件加载
/// </summary>
public static class ConfigService
{
    // ---------- 配置文件路径 ----------

    /// <summary>连接设置配置文件的完整路径（conn-settings.json）</summary>
    private static readonly string ConnSettingsPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "conn-settings.json");

    /// <summary>变量列表配置文件的完整路径（variables.json）</summary>
    private static readonly string VariablesPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "variables.json");

    // ================================================================
    // 连接设置 → 读写 conn-settings.json
    // ================================================================

    /// <summary>
    /// 从 conn-settings.json 加载连接设置
    /// 如果文件不存在，返回一个默认设置对象
    /// </summary>
    public static ConnectionSettings LoadConnectionSettings()
    {
        // 检查配置文件是否存在
        if (!File.Exists(ConnSettingsPath))
        {
            // 不存在就返回默认值（127.0.0.1:502，站号1，中文）
            return new ConnectionSettings();
        }

        // 读取 JSON 文本
        var json = File.ReadAllText(ConnSettingsPath);
        // 反序列化为 ConnectionSettings 对象
        // ?? 后面的意思是：如果反序列化结果是 null，就用默认值
        return JsonConvert.DeserializeObject<ConnectionSettings>(json)
               ?? new ConnectionSettings();
    }

    /// <summary>
    /// 保存连接设置到 conn-settings.json
    /// </summary>
    /// <param name="settings">需要保存的连接设置对象</param>
    public static void SaveConnectionSettings(ConnectionSettings settings)
    {
        // 序列化为格式化的 JSON（缩进易读）
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        // 以 UTF-8 编码写入文件
        File.WriteAllText(ConnSettingsPath, json, System.Text.Encoding.UTF8);
    }

    // ================================================================
    // 变量列表 → 读写 variables.json
    // ================================================================

    /// <summary>
    /// 从 variables.json 加载变量列表
    /// 如果文件不存在，返回一个空列表
    /// </summary>
    public static List<ModbusVariable> LoadVariables()
    {
        // 检查配置文件是否存在
        if (!File.Exists(VariablesPath))
        {
            // 不存在就返回空列表（程序首次运行，用户自己添加变量）
            return new List<ModbusVariable>();
        }

        // 读取 JSON 并反序列化为变量列表
        var json = File.ReadAllText(VariablesPath);
        return JsonConvert.DeserializeObject<List<ModbusVariable>>(json)
               ?? new List<ModbusVariable>();
    }

    /// <summary>
    /// 保存变量列表到 variables.json
    /// </summary>
    /// <param name="variables">需要保存的变量列表</param>
    public static void SaveVariables(List<ModbusVariable> variables)
    {
        // 序列化为格式化的 JSON
        var json = JsonConvert.SerializeObject(variables, Formatting.Indented);
        // 以 UTF-8 编码写入文件
        File.WriteAllText(VariablesPath, json, System.Text.Encoding.UTF8);
    }
}
