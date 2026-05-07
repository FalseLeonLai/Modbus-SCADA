// ============================================================
// 文件: MainForm.cs
// 作用: 主窗口 — 实时变量监视、数据读写、变量管理
// 设计: 这是用户与 Modbus 设备交互的核心界面
// ============================================================

using System.ComponentModel;
using ModbusSCADA.Models;
using ModbusSCADA.Helpers;
using ModbusSCADA.Resources;
using ModbusSCADA.Services;

namespace ModbusSCADA.Forms;

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
    private ToolStrip _topToolbar = null!;
    private ToolStripButton _btnConnect = null!;
    private ToolStripButton _btnDisconnect = null!;
    private ToolStripButton _btnConfigConn = null!;
    private ToolStripLabel _lblStatusText = null!;
    private ToolStripLabel _lblStatusValue = null!;
    private ToolStripLabel _lblLanguage = null!;
    private ToolStripComboBox _cboLanguage = null!;

    // 变量监视表格
    private DataGridView _dgvVariables = null!;

    // 变量操作面板（底部）
    private GroupBox _grpOperation = null!;
    private Label _lblSelectedInfo = null!;
    private Label _lblWriteValue = null!;
    private TextBox _txtWriteValue = null!;
    private Button _btnWrite = null!;

    // 底部工具栏
    private ToolStrip _bottomToolbar = null!;
    private ToolStripButton _btnAdd = null!;
    private ToolStripButton _btnEdit = null!;
    private ToolStripButton _btnDelete = null!;
    private ToolStripButton _btnImport = null!;
    private ToolStripButton _btnExport = null!;
    private ToolStripButton _btnRefresh = null!;

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
        SuspendLayout();

        // ---------- 窗口属性 ----------
        Text = Strings.AppTitle;
        Size = new Size(1100, 650);
        MinimumSize = new Size(900, 560);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9F);

        // ---------- 顶部工具栏 ----------
        _topToolbar = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = false,
            Height = 42,
            Padding = new Padding(8, 4, 8, 4),
            RenderMode = ToolStripRenderMode.System
        };

        _btnConnect = CreateToolbarButton(Strings.Connect);
        _btnConnect.Click += BtnConnect_Click;

        _btnDisconnect = CreateToolbarButton(Strings.Disconnect);
        _btnDisconnect.Enabled = false;
        _btnDisconnect.Click += BtnDisconnect_Click;

        _btnConfigConn = CreateToolbarButton(Strings.ConfigConnection);
        _btnConfigConn.Click += BtnConfigConn_Click;

        _lblStatusText = CreateToolbarLabel($"{Strings.ConnectionStatus}: ");
        _lblStatusValue = new ToolStripLabel(Strings.NotConnected)
        {
            ForeColor = Color.Red,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            Margin = new Padding(4, 0, 8, 0)
        };

        _cboLanguage = new ToolStripComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 110,
            AutoSize = false,
            Margin = new Padding(4, 0, 0, 0)
        };
        foreach (var lang in LanguageHelper.SupportedLanguages)
        {
            _cboLanguage.Items.Add(lang.Value); // "中文" / "English" / "Русский"
        }
        // 选中当前语言
        var currentDisplay = LanguageHelper.GetCurrentLanguageDisplay();
        for (int i = 0; i < _cboLanguage.Items.Count; i++)
        {
            if (_cboLanguage.Items[i]?.ToString() == currentDisplay)
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
        _lblLanguage = CreateToolbarLabel(Strings.SwitchLanguage + ": ");
        _topToolbar.Items.Add(_lblLanguage);
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
            ColumnHeadersHeight = 34,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
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
            Height = 116,
            Padding = new Padding(12, 28, 12, 12)
        };

        var operationLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = false,
            Height = 46,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _lblSelectedInfo = new Label
        {
            Text = $"{Strings.SelectedVariable}: ---",
            AutoSize = false,
            AutoEllipsis = true,
            Width = 360,
            Height = 40,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 28, 0)
        };

        _lblWriteValue = new Label
        {
            Text = Strings.WriteNewValue + ": ",
            AutoSize = false,
            Width = 120,
            Height = 40,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 0, 8, 0)
        };
        _txtWriteValue = new TextBox
        {
            Width = 130,
            Enabled = false,
            Margin = new Padding(0, 6, 14, 0)
        };
        _btnWrite = new Button
        {
            Text = Strings.Write,
            Size = new Size(90, 34),
            MinimumSize = new Size(90, 34),
            Enabled = false,
            Margin = new Padding(0, 3, 0, 0)
        };
        _btnWrite.Click += BtnWrite_Click;

        operationLayout.Controls.Add(_lblSelectedInfo);
        operationLayout.Controls.Add(_lblWriteValue);
        operationLayout.Controls.Add(_txtWriteValue);
        operationLayout.Controls.Add(_btnWrite);
        _grpOperation.Controls.Add(operationLayout);

        // ---------- 底部工具栏 ----------
        _bottomToolbar = new ToolStrip
        {
            Dock = DockStyle.Bottom,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = false,
            Height = 42,
            Padding = new Padding(8, 4, 8, 4),
            RenderMode = ToolStripRenderMode.System
        };

        _btnAdd = CreateToolbarButton(Strings.AddVariable);
        _btnAdd.Click += BtnAdd_Click;

        _btnEdit = CreateToolbarButton(Strings.EditVariable);
        _btnEdit.Click += BtnEdit_Click;

        _btnDelete = CreateToolbarButton(Strings.DeleteVariable);
        _btnDelete.Click += BtnDelete_Click;

        _btnImport = CreateToolbarButton(Strings.ImportVariables);
        _btnImport.Click += BtnImport_Click;

        _btnExport = CreateToolbarButton(Strings.ExportVariables);
        _btnExport.Click += BtnExport_Click;

        _btnRefresh = CreateToolbarButton(Strings.Refresh);
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

        ResumeLayout(false);
        PerformLayout();
    }

    private static ToolStripButton CreateToolbarButton(string text)
    {
        return new ToolStripButton(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ImageScaling = ToolStripItemImageScaling.None,
            AutoSize = true,
            Padding = new Padding(6, 0, 6, 0),
            Margin = new Padding(2, 0, 2, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
    }

    private static ToolStripLabel CreateToolbarLabel(string text)
    {
        return new ToolStripLabel(text)
        {
            AutoSize = true,
            Margin = new Padding(6, 0, 2, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };
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

        ConfigureVariableGridColumns();

        // 注册单元格格式化事件 — 让值以更友好的形式显示
        _dgvVariables.CellFormatting += DgvVariables_CellFormatting;
    }

    private void ConfigureVariableGridColumns()
    {
        if (_dgvVariables.Columns["LastReadTime"] is DataGridViewColumn lastCol)
            lastCol.Visible = false;

        ConfigureColumn("Name", Strings.ColName, 24, 160);
        ConfigureColumn("Address", Strings.ColAddress, 12, 90);
        ConfigureColumn("DataType", Strings.ColDataType, 18, 130);
        ConfigureColumn("CanWrite", Strings.ColCanWrite, 8, 80);
        ConfigureColumn("PollInterval", Strings.ColInterval, 15, 180);
        ConfigureColumn("CurrentValue", Strings.ColValue, 11, 120);
        ConfigureColumn("IsConnected", Strings.ColStatus, 12, 120);
    }

    private void ConfigureColumn(string name, string headerText, float fillWeight, int minimumWidth)
    {
        if (_dgvVariables.Columns[name] is not DataGridViewColumn column) return;

        column.HeaderText = headerText;
        column.FillWeight = fillWeight;
        column.MinimumWidth = minimumWidth;
    }

    /// <summary>
    /// 格式化 DataGridView 单元格的显示
    /// </summary>
    private void DgvVariables_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        var columnName = _dgvVariables.Columns[e.ColumnIndex].Name;

        if (columnName == "CurrentValue")
        {
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
        }
        else if (columnName == "IsConnected" && e.Value is bool connVal)
        {
            // 通信状态列：根据 IsConnected 值显示当前语言文本并染色
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
        var variable = UpdateSelectedVariableInfo();

        // 只有可写变量才允许写入
        bool canWrite = variable?.CanWrite == true && _modbusService.IsConnected;
        _txtWriteValue.Enabled = canWrite;
        _btnWrite.Enabled = canWrite;
        _txtWriteValue.Text = string.Empty;
    }

    private ModbusVariable? UpdateSelectedVariableInfo()
    {
        if (_dgvVariables.SelectedRows.Count == 0)
        {
            _lblSelectedInfo.Text = $"{Strings.SelectedVariable}: ---";
            return null;
        }

        var index = _dgvVariables.SelectedRows[0].Index;
        if (index < 0 || index >= _variableManager.Variables.Count)
        {
            _lblSelectedInfo.Text = $"{Strings.SelectedVariable}: ---";
            return null;
        }

        var variable = _variableManager.Variables[index];
        var infoText = _variableManager.GetDisplayText(index);
        var valStr = variable.CurrentValue switch
        {
            bool b => b ? "ON" : "OFF",
            ushort n => n.ToString(),
            _ => "---"
        };

        _lblSelectedInfo.Text = $"{Strings.SelectedVariable}: {infoText}    {Strings.CurrentValue}: {valStr}";
        return variable;
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
        _lblLanguage.Text = Strings.SwitchLanguage + ": ";
        _lblStatusValue.Text = _modbusService.IsConnected
            ? $"{Strings.Connected} {_settings.IPAddress}:{_settings.Port}"
            : Strings.NotConnected;
        _lblStatusValue.ForeColor = _modbusService.IsConnected ? Color.Green : Color.Red;

        // 底部按钮
        _btnAdd.Text = Strings.AddVariable;
        _btnEdit.Text = Strings.EditVariable;
        _btnDelete.Text = Strings.DeleteVariable;
        _btnImport.Text = Strings.ImportVariables;
        _btnExport.Text = Strings.ExportVariables;
        _btnRefresh.Text = Strings.Refresh;

        // 操作面板
        _grpOperation.Text = Strings.VariableOperation;
        _lblWriteValue.Text = Strings.WriteNewValue + ": ";
        _btnWrite.Text = Strings.Write;
        UpdateSelectedVariableInfo();

        // 表格列头
        ConfigureVariableGridColumns();

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
