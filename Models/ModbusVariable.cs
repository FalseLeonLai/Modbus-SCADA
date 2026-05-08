// ============================================================
// 文件: ModbusVariable.cs
// 作用: 定义一个 Modbus 变量的所有属性，用于存储变量配置和实时数据
// 适用: 完全不懂英文和C#的小白 — 每个英文名都有中文注释
// ============================================================

using Newtonsoft.Json;

namespace ModbusSCADA.Models;

/// <summary>
/// Modbus 数据类型 — 决定读写权限和使用的功能码
/// </summary>
public enum ModbusDataType
{
    /// <summary>线圈：可读可写 BOOL — 功能码 01(读) / 05(写)</summary>
    Coil,

    /// <summary>离散输入：只读 BOOL — 功能码 02(读)</summary>
    DiscreteInput,

    /// <summary>输入寄存器：只读 16位整数 — 功能码 04(读)</summary>
    InputRegister,

    /// <summary>保持寄存器：可读可写 16位整数 — 功能码 03(读) / 06(写)</summary>
    HoldingRegister
}

/// <summary>
/// 变量模型 — 描述一个 Modbus 监控点的完整信息
/// </summary>
public class ModbusVariable
{
    // ---------- 基本配置 ----------

    /// <summary>变量名称，用户自定义，例如 "电机1启动信号"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Modbus 寄存器地址（从 0 开始的绝对地址）</summary>
    public ushort Address { get; set; }

    /// <summary>数据类型：线圈/离散输入/输入寄存器/保持寄存器</summary>
    public ModbusDataType DataType { get; set; } = ModbusDataType.Coil;

    // ---------- 读写与刷新 ----------

    /// <summary>是否允许写入值（只读变量此项为 false）</summary>
    public bool CanWrite { get; set; }

    /// <summary>轮询间隔，单位毫秒（默认 1000ms = 1秒）</summary>
    public int PollInterval { get; set; } = 1000;

    // ---------- 运行时状态 ----------

    /// <summary>上一次读取到的值（object 类型，可存储 BOOL 或 ushort）</summary>
    [JsonIgnore]
    public object? CurrentValue { get; set; }

    /// <summary>最后一次读取时间</summary>
    [JsonIgnore]
    public DateTime LastReadTime { get; set; } = DateTime.MinValue;

    /// <summary>通信是否正常（读写成功设为 true，失败设为 false）</summary>
    [JsonIgnore]
    public bool IsConnected { get; set; }
}
