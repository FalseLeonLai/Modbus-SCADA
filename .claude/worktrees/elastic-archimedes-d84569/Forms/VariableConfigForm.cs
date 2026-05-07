// ============================================================
// 文件: VariableConfigForm.cs
// 作用: 变量配置弹窗 — 添加或编辑一个 Modbus 监控变量
// ============================================================

using ModbusSCADA.Models;
using ModbusSCADA.Resources;

namespace ModbusSCADA.Forms;

/// <summary>
/// 变量配置弹窗 — 用于新增或修改变量
/// </summary>
public partial class VariableConfigForm : Form
{
    // ---------- 控件声明 ----------
    private Label _lblName, _lblAddress, _lblDataType, _lblPollInterval;
    private TextBox _txtName, _txtAddress, _txtPollInterval;
    private ComboBox _cboDataType;
    private CheckBox _chkCanWrite;
    private Button _btnSave, _btnCancel;
    private TableLayoutPanel _table;

    /// <summary>配置后的变量对象（调用者读取使用）</summary>
    public ModbusVariable Result { get; private set; }

    /// <summary>是否为编辑模式（true=编辑，false=新增）</summary>
    private readonly bool _isEditMode;

    /// <summary>已有的变量名称列表（用于重名检查）</summary>
    private readonly List<string> _existingNames;

    /// <summary>
    /// 构造函数 — 新增模式
    /// </summary>
    /// <param name="existingNames">已存在的变量名称列表，用于检查重名</param>
    public VariableConfigForm(List<string> existingNames)
    {
        _isEditMode = false;
        _existingNames = existingNames;
        Result = new ModbusVariable(); // 空变量，用户填写
        InitializeForm();
    }

    /// <summary>
    /// 构造函数 — 编辑模式
    /// </summary>
    /// <param name="variable">要编辑的变量对象</param>
    /// <param name="existingNames">已存在的变量名称列表（排除自身）</param>
    public VariableConfigForm(ModbusVariable variable, List<string> existingNames)
    {
        _isEditMode = true;
        _existingNames = existingNames.Where(n => n != variable.Name).ToList();
        // 复制变量，不直接修改原对象
        Result = new ModbusVariable
        {
            Name = variable.Name,
            Address = variable.Address,
            DataType = variable.DataType,
            CanWrite = variable.CanWrite,
            PollInterval = variable.PollInterval
        };
        InitializeForm();
        LoadVariableToUI();
    }

    /// <summary>
    /// 初始化表单控件
    /// </summary>
    private void InitializeForm()
    {
        SuspendLayout();

        // DPI 缩放 — 必须在添加任何控件之前设置,且 AutoScaleDimensions 在 AutoScaleMode 之前
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        Text = Strings.VarConfigTitle;
        // 使用 ClientSize(不含标题栏/边框),DPI 下行为更稳定
        ClientSize = new Size(420, 260);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Microsoft YaHei", 9F);

        _table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(15),
        };
        // 标签列 AutoSize — 适应多语言长文字(如俄文 "Интервал опроса")
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // 6 行均使用 AutoSize,根据控件实际高度自适应,避免被压缩
        for (int i = 0; i < 6; i++)
            _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 变量名称
        _lblName = new Label
        {
            Text = Strings.VarName,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 8, 3, 8)
        };
        _txtName = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };

        // 地址
        _lblAddress = new Label
        {
            Text = Strings.VarAddress,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 8, 3, 8)
        };
        _txtAddress = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };

        // 数据类型
        _lblDataType = new Label
        {
            Text = Strings.VarDataType,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 8, 3, 8)
        };
        _cboDataType = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        // 添加四种数据类型（显示名称 + 枚举值）
        _cboDataType.Items.Add(new KeyValuePair<string, ModbusDataType>(Strings.DT_Coil, ModbusDataType.Coil));
        _cboDataType.Items.Add(new KeyValuePair<string, ModbusDataType>(Strings.DT_DiscreteInput, ModbusDataType.DiscreteInput));
        _cboDataType.Items.Add(new KeyValuePair<string, ModbusDataType>(Strings.DT_InputRegister, ModbusDataType.InputRegister));
        _cboDataType.Items.Add(new KeyValuePair<string, ModbusDataType>(Strings.DT_HoldingRegister, ModbusDataType.HoldingRegister));
        _cboDataType.DisplayMember = "Key";   // 显示 "线圈 (Coil)" 等
        _cboDataType.ValueMember = "Value";   // 值是枚举 ModbusDataType
        _cboDataType.SelectedIndexChanged += CboDataType_SelectedIndexChanged;

        // 轮询间隔
        _lblPollInterval = new Label
        {
            Text = Strings.VarPollInterval,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 8, 3, 8)
        };
        _txtPollInterval = new TextBox { Text = "1000", Anchor = AnchorStyles.Left | AnchorStyles.Right };

        // 可写复选框
        _chkCanWrite = new CheckBox
        {
            Text = Strings.VarCanWrite,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 3, 8)
        };

        // 按钮行
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 10, 0, 0)
        };
        // 使用 MinimumSize + AutoSize,让按钮在高 DPI 下随字体缩放
        _btnCancel = new Button
        {
            Text = Strings.Cancel,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(80, 30)
        };
        _btnSave = new Button
        {
            Text = Strings.Save,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(80, 30)
        };
        _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        _btnSave.Click += BtnSave_Click;
        buttonPanel.Controls.Add(_btnCancel);
        buttonPanel.Controls.Add(_btnSave);

        // 填充表格布局
        _table.Controls.Add(_lblName, 0, 0);
        _table.Controls.Add(_txtName, 1, 0);
        _table.Controls.Add(_lblAddress, 0, 1);
        _table.Controls.Add(_txtAddress, 1, 1);
        _table.Controls.Add(_lblDataType, 0, 2);
        _table.Controls.Add(_cboDataType, 1, 2);
        _table.Controls.Add(_lblPollInterval, 0, 3);
        _table.Controls.Add(_txtPollInterval, 1, 3);
        _table.Controls.Add(_chkCanWrite, 0, 4);
        _table.SetColumnSpan(_chkCanWrite, 2);
        _table.Controls.Add(buttonPanel, 0, 5);
        _table.SetColumnSpan(buttonPanel, 2);

        Controls.Add(_table);

        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>
    /// 将变量数据加载到 UI（编辑模式使用）
    /// </summary>
    private void LoadVariableToUI()
    {
        _txtName.Text = Result.Name;
        _txtAddress.Text = Result.Address.ToString();
        _txtPollInterval.Text = Result.PollInterval.ToString();

        // 选中对应的数据类型
        for (int i = 0; i < _cboDataType.Items.Count; i++)
        {
            var kv = (KeyValuePair<string, ModbusDataType>)_cboDataType.Items[i];
            if (kv.Value == Result.DataType)
            {
                _cboDataType.SelectedIndex = i;
                break;
            }
        }

        _chkCanWrite.Checked = Result.CanWrite;
    }

    /// <summary>
    /// 数据类型改变时，自动调整 CanWrite 的可用性和默认值
    /// </summary>
    private void CboDataType_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cboDataType.SelectedItem is KeyValuePair<string, ModbusDataType> kv)
        {
            switch (kv.Value)
            {
                // 离散输入和输入寄存器是只读的 → 禁用 CanWrite 复选框，自动设为 false
                case ModbusDataType.DiscreteInput:
                case ModbusDataType.InputRegister:
                    _chkCanWrite.Checked = false;
                    _chkCanWrite.Enabled = false;
                    break;
                // 线圈和保持寄存器可读可写 → 允许用户勾选
                default:
                    _chkCanWrite.Enabled = true;
                    break;
            }
        }
    }

    /// <summary>
    /// 保存按钮点击 — 校验输入
    /// </summary>
    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // 变量名称不能为空
        var name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(Strings.MsgVarNameEmpty);
            _txtName.Focus();
            return;
        }

        // 检查是否与已有变量重名
        if (_existingNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(Strings.MsgVarNameDuplicate);
            _txtName.Focus();
            return;
        }
        Result.Name = name;

        // 验证地址（0-65535）
        if (!ushort.TryParse(_txtAddress.Text, out ushort address))
        {
            MessageBox.Show(Strings.MsgVarAddrRange);
            _txtAddress.Focus();
            return;
        }
        Result.Address = address;

        // 获取数据类型
        if (_cboDataType.SelectedItem is KeyValuePair<string, ModbusDataType> kv)
        {
            Result.DataType = kv.Value;
        }

        // 验证轮询间隔
        if (!int.TryParse(_txtPollInterval.Text, out int interval) || interval < 50)
        {
            MessageBox.Show(Strings.MsgVarIntervalRange);
            _txtPollInterval.Focus();
            return;
        }
        Result.PollInterval = interval;

        // CanWrite（只读类型已自动设为 false）
        Result.CanWrite = _chkCanWrite.Checked && _chkCanWrite.Enabled;

        // 通过验证
        DialogResult = DialogResult.OK;
        Close();
    }
}
