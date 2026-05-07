// ============================================================
// 文件: VariableManager.cs
// 作用: 管理变量列表的增删改查，并提供数据变更事件通知 UI
// 设计: 使用 绑定列表(BindingList) 让 DataGridView 自动同步数据
// ============================================================

using System.ComponentModel;
using ModbusSCADA.Models;

namespace ModbusSCADA.Services;

/// <summary>
/// 变量管理器 — 维护变量列表，支持增删改查，数据变更时通知 UI 刷新
/// </summary>
public class VariableManager
{
    // ---------- 属性 ----------

    /// <summary>
    /// 变量列表 — 使用 BindingList 绑定到 DataGridView
    /// 当列表内容变化时，DataGridView 会自动更新显示
    /// </summary>
    public BindingList<ModbusVariable> Variables { get; } = new BindingList<ModbusVariable>();

    // ================================================================
    // CRUD 操作（增删改查）
    // ================================================================

    /// <summary>
    /// 添加一个变量到列表末尾
    /// </summary>
    /// <param name="variable">要添加的变量对象</param>
    public void Add(ModbusVariable variable)
    {
        // BindingList.Add 会自动通知 DataGridView 刷新
        Variables.Add(variable);
    }

    /// <summary>
    /// 删除指定索引位置的变量
    /// </summary>
    /// <param name="index">要删除的变量在列表中的索引（从0开始）</param>
    public void RemoveAt(int index)
    {
        // 索引越界检查
        if (index >= 0 && index < Variables.Count)
        {
            Variables.RemoveAt(index);
        }
    }

    /// <summary>
    /// 更新指定变量的值（用于实时数据刷新后写入）
    /// </summary>
    /// <param name="index">变量在列表中的索引</param>
    /// <param name="value">新的值</param>
    /// <param name="connected">通信状态是否正常</param>
    public void UpdateValue(int index, object? value, bool connected = true)
    {
        // 索引越界检查
        if (index < 0 || index >= Variables.Count) return;

        // 获取对应变量
        var variable = Variables[index];
        // 更新当前值
        variable.CurrentValue = value;
        // 更新通信状态
        variable.IsConnected = connected;
        // 记录更新时间
        variable.LastReadTime = DateTime.Now;

        // 手动触发列表项变更事件，通知 DataGridView 刷新该行
        // ResetItem 会让 DataGridView 重新读取该行的所有属性值
        Variables.ResetItem(index);
    }

    /// <summary>
    /// 获取 "名称 + 地址" 的简短文本，用于 ComboBox 下拉显示
    /// 例如: "电机1启动 (Coil:00001)"
    /// </summary>
    public string GetDisplayText(int index)
    {
        if (index < 0 || index >= Variables.Count) return string.Empty;

        var v = Variables[index];
        var typeName = v.DataType switch
        {
            ModbusDataType.Coil => "Coil",
            ModbusDataType.DiscreteInput => "DI",
            ModbusDataType.InputRegister => "IR",
            ModbusDataType.HoldingRegister => "HR",
            _ => "??"
        };
        return $"{v.Name} ({typeName}:{v.Address:D5})";
    }

    /// <summary>
    /// 从 JSON 文件加载变量列表到内存
    /// </summary>
    public void LoadFromFile()
    {
        var list = ConfigService.LoadVariables();
        // 清空现有列表
        Variables.Clear();
        // 逐个添加（每次 Add 都会通知 UI，所以先全加再统一通知效率更高）
        foreach (var item in list)
        {
            Variables.Add(item);
        }
    }

    /// <summary>
    /// 保存当前变量列表到 JSON 文件
    /// </summary>
    public void SaveToFile()
    {
        ConfigService.SaveVariables(Variables.ToList());
    }
}
