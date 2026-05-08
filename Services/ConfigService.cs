// ============================================================
// 文件: ConfigService.cs
// 作用: 处理 JSON 配置文件的读写 — 连接设置、变量列表、语言偏好
// 使用: Newtonsoft.Json 序列化
// ============================================================

using Newtonsoft.Json;
using ModbusSCADA.Models;

namespace ModbusSCADA.Services;

/// <summary>
/// 配置服务 — 负责将连接设置和变量列表保存到 JSON 文件，或从 JSON 文件加载
/// </summary>
public static class ConfigService
{
    // ---------- 配置文件路径 ----------

    private const string AppDataFolderName = "Modbus-SCADA";
    private static string? _configDirectoryOverride;

    /// <summary>配置文件目录，默认位于当前用户的 AppData。</summary>
    public static string ConfigDirectory =>
        _configDirectoryOverride ??
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppDataFolderName);

    /// <summary>连接设置配置文件的完整路径（conn-settings.json）</summary>
    private static string ConnSettingsPath =>
        Path.Combine(ConfigDirectory, "conn-settings.json");

    /// <summary>变量列表配置文件的完整路径（variables.json）</summary>
    private static string VariablesPath =>
        Path.Combine(ConfigDirectory, "variables.json");

    /// <summary>
    /// 测试专用：注入配置目录。传入 null 可恢复默认 AppData 目录。
    /// </summary>
    public static void SetConfigDirectoryForTesting(string? directory)
    {
        _configDirectoryOverride = string.IsNullOrWhiteSpace(directory)
            ? null
            : Path.GetFullPath(directory);
    }

    // ================================================================
    // 连接设置 → 读写 conn-settings.json
    // ================================================================

    /// <summary>
    /// 从 conn-settings.json 加载连接设置
    /// 如果文件不存在，返回一个默认设置对象
    /// </summary>
    public static ConnectionSettings LoadConnectionSettings()
    {
        var path = ConnSettingsPath;
        if (!File.Exists(path))
        {
            return new ConnectionSettings();
        }

        try
        {
            var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<ConnectionSettings>(json)
                   ?? new ConnectionSettings();
        }
        catch (Exception ex) when (IsConfigReadException(ex))
        {
            BackupBadConfig(path);
            return new ConnectionSettings();
        }
    }

    /// <summary>
    /// 保存连接设置到 conn-settings.json
    /// </summary>
    /// <param name="settings">需要保存的连接设置对象</param>
    public static void SaveConnectionSettings(ConnectionSettings settings)
    {
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        WriteAllTextAtomically(ConnSettingsPath, json);
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
        var path = VariablesPath;
        if (!File.Exists(path))
        {
            return new List<ModbusVariable>();
        }

        try
        {
            var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<ModbusVariable>>(json)?
                .Where(v => v != null)
                .ToList() ?? new List<ModbusVariable>();
        }
        catch (Exception ex) when (IsConfigReadException(ex))
        {
            BackupBadConfig(path);
            return new List<ModbusVariable>();
        }
    }

    /// <summary>
    /// 保存变量列表到 variables.json
    /// </summary>
    /// <param name="variables">需要保存的变量列表</param>
    public static void SaveVariables(List<ModbusVariable> variables)
    {
        var json = JsonConvert.SerializeObject(variables, Formatting.Indented);
        WriteAllTextAtomically(VariablesPath, json);
    }

    private static bool IsConfigReadException(Exception ex)
    {
        return ex is JsonException or IOException or UnauthorizedAccessException or System.Security.SecurityException;
    }

    private static void WriteAllTextAtomically(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, content, System.Text.Encoding.UTF8);

        try
        {
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void BackupBadConfig(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            var backupPath = $"{path}.bad-{DateTime.Now:yyyyMMddHHmmssfff}";
            File.Move(path, backupPath);
        }
        catch
        {
            // 配置已损坏时，备份失败也不能阻止应用使用默认值启动。
        }
    }
}
