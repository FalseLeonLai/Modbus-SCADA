// ============================================================
// 文件: ConnectionSettings.cs
// 作用: 定义 Modbus TCP 连接参数，支持 JSON 保存与加载
// ============================================================

namespace ModbusSCADA.Models;

/// <summary>
/// 连接设置 — Modbus TCP 设备连接所需的所有参数
/// </summary>
public class ConnectionSettings
{
    /// <summary>PLC 或 Modbus 设备的 IP 地址，例如 "192.168.1.100"</summary>
    public string IPAddress { get; set; } = "127.0.0.1";

    /// <summary>Modbus TCP 端口，标准端口为 502</summary>
    public int Port { get; set; } = 502;

    /// <summary>从站地址（站号），范围 1-247</summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>连接超时时间，单位毫秒（默认 3000ms = 3秒）</summary>
    public int Timeout { get; set; } = 3000;

    /// <summary>重连间隔，单位毫秒（连接断开后自动重连的等待时间）</summary>
    public int ReconnectInterval { get; set; } = 5000;

    /// <summary>当前界面语言代码：zh-CN / en / ru</summary>
    public string Language { get; set; } = "zh-CN";
}
