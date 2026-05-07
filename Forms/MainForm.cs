// ============================================================
// 文件: MainForm.cs
// 作用: 主窗口 — 实时变量监视、数据读写、变量管理
// 设计: 这是用户与 Modbus 设备交互的核心界面
// ============================================================

using System.ComponentModel;
using Modbus上位机.Models;
using Modbus上位机.Helpers;
using Modbus上位机.Resources;
using Modbus上位机.Services;

namespace Modbus上位机.Forms;

/// <summary>
/// 主窗口 — 上位机核心界面
/// </summary>
public partial class MainForm : Form
{
    // ================================================================
    // 服务对象
    // ================================================================

    /// <summary>Modbus 通信服务 — 负责连接、读写、后台轮询</summary>
    private readonly ModbusService _modbusService = new();

    /// <summary>变量管理器 — 维护变量列表，绑定到 DataGridView</summary>
    private readonly VariableManager _variableManager = new();

    /// <summary>当前连接设置（运行时可变）</summary>
    private ConnectionSettings _settings;

    // ================================================================
    // UI 控件
    // ================================================================

    // 顶部工具栏
    private ToolStrip _topToolbar;
    private ToolStripButton _btnConnect, _btnDisconnect, _btnConfigConn;
    private ToolStripLabel _lblStatusText, _lblStatusValue;
    private ToolStripComboBox _cboLanguage;

    // 变量监视表格
    private DataGridView _dgvVariables;

    // 变量操作面板（底部）
    private GroupBox _grpOperation;
    private Label _lblSelectedInfo;
    private Label _lblWriteValue;
    private TextBox _txtWriteValue;
    private Button _btnWrite;

    // 底部工具栏
    private ToolStrip _bottomToolbar;
    private ToolStripButton _btnAdd, _btnEdit, _btnDelete, _btnImport, _btnExport, _btnRefresh;

    /// <summary>
    /// 构造函数 — 初始化主窗口
    /// </summary>
    public MainForm()
    {
        // 加载连接设置
        _settings = ConfigService.LoadConnectionSettings();

        // 初始化 UI 控件
        InitializeMainForm();
        // 加载保存的变量列表
        _variableManager.LoadFromFile();
        // 绑定表格数据源
        BindDataGridView();
    }

    // ================================================================
    // 界面初始化
    // ================================================================

    /// <summary>
    /// 构建主窗口的所有 UI 控件
    /// </summary>
    private void InitializeMainForm()
    {
        // ---------- 窗口属性 ----------
        Text = Strings.AppTitle;
        Size = new Size(1100, 650);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei", 9F);

        // ---------- 顶部工具栏 ----------
        _topToolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };

        _btnConnect = new ToolStripButton(Strings.Connect) { ImageScaling = ToolStripItemImageScaling.None };
        _btnConnect.Click += BtnConnect_Click;

        _btnDisconnect = new ToolStripButton(Strings.Disconnect) { ImageScaling = ToolStripItemImageScaling.None, Enabled = false };
        _btnDisconnect.Click += BtnDisconnect_Click;

        _btnConfigConn = new ToolStripButton(Strings.ConfigConnection) { ImageScaling = ToolStripItemImageScaling.None };
        _btnConfigConn.Click += BtnConfigConn_Click;

        _lblStatusText = new ToolStripLabel($"{Strings.ConnectionStatus}: ");
        _lblStatusValue = new ToolStripLabel(Strings.NotConnected)
        {
            ForeColor = Color.Red,
            Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold)
        };

        _cboLanguage = new ToolStripComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 90
        };
        foreach (var lang in LanguageHelper.SupportedLanguages)
        {
            _cboLanguage.Items.Add(lang.Value); // "中文" / "English" / "Русский"
        }
        // 选中当前语言
        var currentDisplay = LanguageHelper.GetCurrentLanguageDisplay();
        for (int i = 0; i < _cboLanguage.Items.Count; i++)
        {
            if (_cboLanguage.Items[i].ToString() == currentDisplay)
            {
                _cboLanguage.SelectedIndex = i;
                break;
            }
        }
        _cboLanguage.SelectedIndexChanged += CboLanguage_SelectedIndexChanged;

        // 添加到工具栏
        _topToolbar.Items.Add(_btnConnect);
        _topToolbar.Items.Add(_btnDisconnect);
        _topToolbar.Items.Add(_btnConfigConn);
        _topToolbar.Items.Add(new ToolStripSeparator());
        _topToolbar.Items.Add(_lblStatusText);
        _topToolbar.Items.Add(_lblStatusValue);
        _topToolbar.Items.Add(new ToolStripSeparator());
        _topToolbar.Items.Add(new ToolStripLabel(Strings.SwitchLanguage + ": "));
        _topToolbar.Items.Add(_cboLanguage);

        // ---------- DataGridView 变量监视表格 ----------
        _dgvVariables = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,      // 禁止用户直接在表格中添加行
            AllowUserToDeleteRows = false,   // 禁止用户直接在表格中删除行
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, // 点击选中整行
            MultiSelect = false,
            ReadOnly = true,                 // 所有单元格只读（双击不能编辑）
            RowHeadersVisible = false,       // 隐藏左侧行头
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, // 列宽自动填充
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None
        };

        // 表格选择行变化时更新操作面板
        _dgvVariables.SelectionChanged += DgvVariables_SelectionChanged;

        // ---------- 底部操作面板 ----------
        _grpOperation = new GroupBox
        {
            Text = Strings.VariableOperation,
            Dock = DockStyle.Bottom,
            Height = 80
        };

        _lblSelectedInfo = new Label
        {
            Text = $"{Strings.SelectedVariable}: ---",
            Location = new Point(15, 25),
            AutoSize = true
        };

        _lblWriteValue = new Label
        {
            Text = Strings.WriteNewValue + ": ",
            Location = new Point(350, 25),
            AutoSize = true
        };
        _txtWriteValue = new TextBox
        {
            Location = new Point(430, 22),
            Width = 100,
            Enabled = false
        };
        _btnWrite = new Button
        {
            Text = Strings.Write,
            Location = new Point(540, 20),
            Size = new Size(70, 28),
            Enabled = false
        };
        _btnWrite.Click += BtnWrite_Click;

        _grpOperation.Controls.Add(_lblSelectedInfo);
        _grpOperation.Controls.Add(_lblWriteValue);
        _grpOperation.Controls.Add(_txtWriteValue);
        _grpOperation.Controls.Add(_btnWrite);

        // ---------- 底部工具栏 ----------
        _bottomToolbar = new ToolStrip { Dock = DockStyle.Bottom, GripStyle = ToolStripGripStyle.Hidden };

        _btnAdd = new ToolStripButton(Strings.AddVariable) { ImageScaling = ToolStripItemImageScaling.None };
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = new ToolStripButton(Strings.EditVariable) { ImageScaling = ToolStripItemImageScaling.None };
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = new ToolStripButton(Strings.DeleteVariable) { ImageScaling = ToolStripItemImageScaling.None };
        _btnDelete.Click += BtnDelete_Click;

        _btnImport = new ToolStripButton(Strings.ImportVariables) { ImageScaling = ToolStripItemImageScaling.None };
        _btnImport.Click += BtnImport_Click;

        _btnExport = new ToolStripButton(Strings.ExportVariables) { ImageScaling = ToolStripItemImageScaling.None };
        _btnExport.Click += BtnExport_Click;

        _btnRefresh = new ToolStripButton(Strings.ColValue + " ↻") { ImageScaling = ToolStripItemImageScaling.None };
        _btnRefresh.Click += (s, e) => _dgvVariables.Refresh();

        _bottomToolbar.Items.Add(_btnAdd);
        _bottomToolbar.Items.Add(_btnEdit);
        _bottomToolbar.Items.Add(_btnDelete);
        _bottomToolbar.Items.Add(new ToolStripSeparator());
        _bottomToolbar.Items.Add(_btnImport);
        _bottomToolbar.Items.Add(_btnExport);
        _bottomToolbar.Items.Add(new ToolStripSeparator());
        _bottomToolbar.Items.Add(_btnRefresh);

        // ---------- 布局: 自上而下 ----------
        Controls.Add(_dgvVariables);        // 填充中间
        Controls.Add(_grpOperation);        // 底部固定
        Controls.Add(_bottomToolbar);       // 最底
        Controls.Add(_topToolbar);          // 最顶
    }

    // ================================================================
    // DataGridView 数据绑定与格式化
    // ================================================================

    /// <summary>
    /// 将变量管理器的 BindingList 绑定到 DataGridView
    /// 当变量列表发生变化时，表格自动更新
    /// </summary>
    private void BindDataGridView()
    {
        // 将 BindingList 设置为 DataGridView 的数据源
        _dgvVariables.DataSource = _variableManager.Variables;

        // 配置列 — 因为 DataSource 是 BindingList<ModbusVariable>
        // WinForms 会自动为每个公共属性生成列
        // 这里对自动生成的列进行定制

        // 先清空可能存在的列，然后让系统根据 DataSource 重新生成
        _dgvVariables.AutoGenerateColumns = true;
        _dgvVariables.DataSource = null;
        _dgvVariables.DataSource = _variableManager.Variables;

        // 禁用不需要显示的列
        if (_dgvVariables.Columns["LastReadTime"] is DataGridViewColumn lastCol)
            lastCol.Visible = false;

        // 重命名列头文本
        if (_dgvVariables.Columns["Name"] is DataGridViewColumn nameCol)
            nameCol.HeaderText = Strings.ColName;
        if (_dgvVariables.Columns["Address"] is DataGridViewColumn addrCol)
            addrCol.HeaderText = Strings.ColAddress;
        if (_dgvVariables.Columns["DataType"] is DataGridViewColumn dtCol)
            dtCol.HeaderText = Strings.ColDataType;
        if (_dgvVariables.Columns["CurrentValue"] is DataGridViewColumn valCol)
            valCol.HeaderText = Strings.ColValue;
        if (_dgvVariables.Columns["IsConnected"] is DataGridViewColumn connCol)
            connCol.HeaderText = Strings.ColStatus;
        if (_dgvVariables.Columns["PollInterval"] is DataGridViewColumn intCol)
            intCol.HeaderText = Strings.ColInterval;
        if (_dgvVariables.Columns["CanWrite"] is DataGridViewColumn wCol)
            wCol.HeaderText = Strings.ColCanWrite;

        // 设置列宽比例
        if (_dgvVariables.Columns["Name"] is DataGridViewColumn nc)
            nc.FillWeight = 25;
        if (_dgvVariables.Columns["Address"] is DataGridViewColumn ac)
            ac.FillWeight = 12;
        if (_dgvVariables.Columns["DataType"] is DataGridViewColumn dc)
            dc.FillWeight = 18;
        if (_dgvVariables.Columns["CurrentValue"] is DataGridViewColumn vc)
            vc.FillWeight = 15;
        if (_dgvVariables.Columns["IsConnected"] is DataGridViewColumn cc)
            cc.FillWeight = 12;
        if (_dgvVariables.Columns["PollInterval"] is DataGridViewColumn ic)
            ic.FillWeight = 10;
        if (_dgvVariables.Columns["CanWrite"] is DataGridViewColumn wc)
            wc.FillWeight = 8;

        // 注册单元格格式化事件 — 让值以更友好的形式显示
        _dgvVariables.CellFormatting += DgvVariables_CellFormatting;
    }

    /// <summary>
    /// 格式化 DataGridView 单元格的显示
    /// </summary>
    private void DgvVariables_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        // 只处理 CurrentValue 列
        if (_dgvVariables.Columns[e.ColumnIndex].Name != "CurrentValue") return;
        if (e.Value == null || e.Value == DBNull.Value)
        {
            e.Value = "---"; // 未读取到值时显示 "---"
        }
        else if (e.Value is bool b)
        {
            // 布尔值显示为 ON/OFF
            e.Value = b ? "ON" : "OFF";
            // 根据值设置颜色：ON=绿色, OFF=灰色
            e.CellStyle!.ForeColor = b ? Color.Green : Color.Gray;
            e.CellStyle.Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold);
        }

        // 通信状态列 — 根据 IsConnected 值染色
        if (_dgvVariables.Columns[e.ColumnIndex].Name == "IsConnected" && e.Value is bool connVal)
        {
            e.Value = connVal ? Strings.StatusGood : Strings.StatusError;
            e.CellStyle!.ForeColor = connVal ? Color.Green : Color.Red;
        }
    }

    // ================================================================
    // 工具栏事件处理
    // ================================================================

    /// <summary>
    /// "连接" 按钮 — 连接到 Modbus 设备并启动后台轮询
    /// </summary>
    private async void BtnConnect_Click(object? sender, EventArgs e)
    {
        var success = await _modbusService.ConnectAsync(_settings);

        if (success)
        {
            // 更新 UI 状态
            _lblStatusValue.Text = $"{Strings.Connected} {_settings.IPAddress}:{_settings.Port}";
            _lblStatusValue.ForeColor = Color.Green;
            _btnConnect.Enabled = false;
            _btnDisconnect.Enabled = true;

            // 启动后台轮询
            _modbusService.StartPoll(_variableManager, _settings);

            // 状态栏提示
            ShowInfo(Strings.MsgConnectSuccess);
        }
        else
        {
            ShowError(Strings.MsgConnectFail);
        }
    }

    /// <summary>
    /// "断开" 按钮 — 断开连接并停止后台轮询
    /// </summary>
    private void BtnDisconnect_Click(object? sender, EventArgs e)
    {
        _modbusService.Disconnect();

        // 更新 UI 状态
        _lblStatusValue.Text = Strings.NotConnected;
        _lblStatusValue.ForeColor = Color.Red;
        _btnConnect.Enabled = true;
        _btnDisconnect.Enabled = false;
    }

    /// <summary>
    /// "连接设置" 按钮 — 打开连接设置弹窗
    /// </summary>
    private void BtnConfigConn_Click(object? sender, EventArgs e)
    {
        using var form = new ConnectionSettingsForm(_settings);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            // 用户点击保存，更新设置并持久化
            _settings = form.Settings;
            ConfigService.SaveConnectionSettings(_settings);

            // 如果连接设置中的语言与当前不同，切换语言
            if (_settings.Language != LanguageHelper.CurrentLanguage)
            {
                LanguageHelper.SetLanguage(_settings.Language);
                RefreshAllUILanguage();
            }

            ShowInfo(Strings.MsgSaveSuccess);
        }
    }

    /// <summary>
    /// 语言下拉框切换
    /// </summary>
    private void CboLanguage_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cboLanguage.SelectedIndex < 0) return;

        // 根据显示名称找到语言代码
        var displayName = _cboLanguage.SelectedItem?.ToString() ?? "中文";
        var langCode = LanguageHelper.SupportedLanguages
            .FirstOrDefault(kv => kv.Value == displayName).Key ?? "zh-CN";

        // 如果语言未改变则退出
        if (langCode == LanguageHelper.CurrentLanguage) return;

        // 切换语言
        LanguageHelper.SetLanguage(langCode);
        // 保存到配置
        _settings.Language = langCode;
        ConfigService.SaveConnectionSettings(_settings);

        // 刷新所有 UI 文本
        RefreshAllUILanguage();
    }

    // ================================================================
    // 变量管理事件处理
    // ================================================================

    /// <summary>
    /// "添加变量" 按钮
    /// </summary>
    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        // 收集当前已有的变量名称
        var names = _variableManager.Variables.Select(v => v.Name).ToList();
        using var form = new VariableConfigForm(names);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            // 用户点击保存，将新变量加入列表并保存到文件
            _variableManager.Add(form.Result);
            _variableManager.SaveToFile();
        }
    }

    /// <summary>
    /// "编辑变量" 按钮
    /// </summary>
    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        // 确保有选中行
        if (_dgvVariables.SelectedRows.Count == 0)
        {
            ShowWarning(Strings.MsgSelectVariable);
            return;
        }

        // 获取选中行的索引
        var index = _dgvVariables.SelectedRows[0].Index;
        var variable = _variableManager.Variables[index];

        // 打开编辑窗口（排除自身名称参与重名检查）
        var names = _variableManager.Variables
            .Select(v => v.Name)
            .Where(n => n != variable.Name)
            .ToList();
        using var form = new VariableConfigForm(variable, names);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            // 将修改后的值赋回原变量
            variable.Name = form.Result.Name;
            variable.Address = form.Result.Address;
            variable.DataType = form.Result.DataType;
            variable.CanWrite = form.Result.CanWrite;
            variable.PollInterval = form.Result.PollInterval;

            // 通知列表该行数据已变更
            _variableManager.Variables.ResetItem(index);
            _variableManager.SaveToFile();
        }
    }

    /// <summary>
    /// "删除变量" 按钮
    /// </summary>
    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_dgvVariables.SelectedRows.Count == 0)
        {
            ShowWarning(Strings.MsgSelectVariable);
            return;
        }

        var result = MessageBox.Show(
            Strings.MsgConfirmDelete,
            Strings.DeleteVariable,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            var index = _dgvVariables.SelectedRows[0].Index;
            _variableManager.RemoveAt(index);
            _variableManager.SaveToFile();
        }
    }

    /// <summary>
    /// "导入" 按钮 — 从文件导入变量配置
    /// </summary>
    private void BtnImport_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = Strings.ImportVariables,
            Filter = "JSON 文件|*.json|所有文件|*.*",
            DefaultExt = "json"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName, System.Text.Encoding.UTF8);
                var variables = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ModbusVariable>>(json);
                if (variables != null)
                {
                    _variableManager.Variables.Clear();
                    foreach (var v in variables) _variableManager.Add(v);
                    _variableManager.SaveToFile();
                    ShowInfo(Strings.MsgSaveSuccess);
                }
            }
            catch (Exception ex)
            {
                ShowError($"导入失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// "导出" 按钮 — 导出变量配置到文件
    /// </summary>
    private void BtnExport_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Title = Strings.ExportVariables,
            Filter = "JSON 文件|*.json|所有文件|*.*",
            DefaultExt = "json",
            FileName = "variables_export.json"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(
                    _variableManager.Variables.ToList(),
                    Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(dialog.FileName, json, System.Text.Encoding.UTF8);
                ShowInfo(Strings.MsgSaveSuccess);
            }
            catch (Exception ex)
            {
                ShowError($"导出失败: {ex.Message}");
            }
        }
    }

    // ================================================================
    // 变量操作（读写）
    // ================================================================

    /// <summary>
    /// 表格选中行变化时，更新底部操作面板
    /// </summary>
    private void DgvVariables_SelectionChanged(object? sender, EventArgs e)
    {
        // 没有选中行
        if (_dgvVariables.SelectedRows.Count == 0)
        {
            _lblSelectedInfo.Text = $"{Strings.SelectedVariable}: ---";
            _txtWriteValue.Enabled = false;
            _btnWrite.Enabled = false;
            return;
        }

        // 获取选中变量
        var index = _dgvVariables.SelectedRows[0].Index;
        if (index >= _variableManager.Variables.Count) return;
        var variable = _variableManager.Variables[index];

        // 更新选中信息显示
        var infoText = _variableManager.GetDisplayText(index);
        _lblSelectedInfo.Text = $"{Strings.SelectedVariable}: {infoText}";

        // 更新当前值显示
        var valStr = variable.CurrentValue switch
        {
            bool b => b ? "ON" : "OFF",
            ushort n => n.ToString(),
            _ => "---"
        };
        _lblSelectedInfo.Text += $"  —  {Strings.CurrentValue}: {valStr}";

        // 只有可写变量才允许写入
        bool canWrite = variable.CanWrite && _modbusService.IsConnected;
        _txtWriteValue.Enabled = canWrite;
        _btnWrite.Enabled = canWrite;
        _txtWriteValue.Text = string.Empty;
    }

    /// <summary>
    /// "写入" 按钮 — 将用户输入的值写入选中变量
    /// </summary>
    private async void BtnWrite_Click(object? sender, EventArgs e)
    {
        // 校验选中
        if (_dgvVariables.SelectedRows.Count == 0) return;
        var index = _dgvVariables.SelectedRows[0].Index;
        if (index >= _variableManager.Variables.Count) return;
        var variable = _variableManager.Variables[index];

        // 校验可写
        if (!variable.CanWrite)
        {
            ShowWarning(Strings.MsgCantWriteReadOnly);
            return;
        }

        bool success = false;

        // 根据数据类型执行写入
        switch (variable.DataType)
        {
            case ModbusDataType.Coil:
                // Coil 类型: 用户输入 true/false, 1/0, ON/OFF
                if (ParseBoolInput(_txtWriteValue.Text, out bool coilVal))
                {
                    success = await _modbusService.WriteCoilAsync(_settings.SlaveId, variable.Address, coilVal);
                    if (success)
                    {
                        variable.CurrentValue = coilVal;
                        _variableManager.Variables.ResetItem(index);
                    }
                }
                else
                {
                    ShowWarning("请输入: ON / OFF / 1 / 0 / true / false");
                    return;
                }
                break;

            case ModbusDataType.HoldingRegister:
                // 保持寄存器: 用户输入 0-65535 的整数
                if (ushort.TryParse(_txtWriteValue.Text, out ushort regVal))
                {
                    success = await _modbusService.WriteHoldingRegisterAsync(_settings.SlaveId, variable.Address, regVal);
                    if (success)
                    {
                        variable.CurrentValue = regVal;
                        _variableManager.Variables.ResetItem(index);
                    }
                }
                else
                {
                    ShowWarning("请输入 0-65535 之间的整数");
                    return;
                }
                break;

            default:
                ShowWarning(Strings.MsgCantWriteReadOnly);
                return;
        }

        // 提示写入结果
        if (success) ShowInfo(Strings.MsgWriteSuccess);
        else ShowError(Strings.MsgWriteFail);
    }

    // ================================================================
    // 辅助方法
    // ================================================================

    /// <summary>
    /// 解析用户输入的布尔值 — 支持 "ON" / "OFF" / "1" / "0" / "true" / "false"
    /// </summary>
    private static bool ParseBoolInput(string input, out bool result)
    {
        input = input.Trim().ToUpperInvariant();
        if (input == "ON" || input == "1" || input == "TRUE")
        {
            result = true;
            return true;
        }
        if (input == "OFF" || input == "0" || input == "FALSE")
        {
            result = false;
            return true;
        }
        result = false;
        return false;
    }

    /// <summary>
    /// 刷新所有 UI 控件上的文本（语言切换后调用）
    /// 每个控件重新从资源文件读取对应语言的文本
    /// </summary>
    private void RefreshAllUILanguage()
    {
        // 窗口标题
        Text = Strings.AppTitle;

        // 工具栏按钮
        _btnConnect.Text = Strings.Connect;
        _btnDisconnect.Text = Strings.Disconnect;
        _btnConfigConn.Text = Strings.ConfigConnection;

        // 状态标签
        _lblStatusText.Text = Strings.ConnectionStatus + ": ";

        // 底部按钮
        _btnAdd.Text = Strings.AddVariable;
        _btnEdit.Text = Strings.EditVariable;
        _btnDelete.Text = Strings.DeleteVariable;
        _btnImport.Text = Strings.ImportVariables;
        _btnExport.Text = Strings.ExportVariables;

        // 操作面板
        _grpOperation.Text = Strings.VariableOperation;
        _lblWriteValue.Text = Strings.WriteNewValue + ": ";
        _btnWrite.Text = Strings.Write;

        // 表格列头
        if (_dgvVariables.Columns["Name"] is DataGridViewColumn nc)
            nc.HeaderText = Strings.ColName;
        if (_dgvVariables.Columns["Address"] is DataGridViewColumn ac)
            ac.HeaderText = Strings.ColAddress;
        if (_dgvVariables.Columns["DataType"] is DataGridViewColumn dc)
            dc.HeaderText = Strings.ColDataType;
        if (_dgvVariables.Columns["CurrentValue"] is DataGridViewColumn vc)
            vc.HeaderText = Strings.ColValue;
        if (_dgvVariables.Columns["IsConnected"] is DataGridViewColumn cc)
            cc.HeaderText = Strings.ColStatus;
        if (_dgvVariables.Columns["PollInterval"] is DataGridViewColumn ic)
            ic.HeaderText = Strings.ColInterval;
        if (_dgvVariables.Columns["CanWrite"] is DataGridViewColumn wc)
            wc.HeaderText = Strings.ColCanWrite;

        // 重新刷新表格内容（让 CellFormatting 事件重新触发）
        _dgvVariables.Refresh();
    }

    /// <summary>
    /// 显示信息提示（状态栏 + 弹窗）
    /// </summary>
    private void ShowInfo(string msg)
    {
        // 状态栏显示提示 — 由于 WinForms 没有内置状态栏，用 MessageBox 提示
        // 实际上这里可以用 ToolStripStatusLabel 或 NotifyIcon
        // 简单起见使用 MessageBox 轻量提示
    }

    /// <summary>
    /// 显示错误提示
    /// </summary>
    private void ShowError(string msg)
    {
        MessageBox.Show(msg, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>
    /// 显示警告提示
    /// </summary>
    private void ShowWarning(string msg)
    {
        MessageBox.Show(msg, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    // ================================================================
    // 窗口关闭时释放资源
    // ================================================================

    /// <summary>
    /// 窗口关闭事件 — 释放 Modbus 连接和后台轮询
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 断开连接并释放通信资源
        _modbusService.Dispose();
        base.OnFormClosing(e);
    }
}
