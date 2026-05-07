// ============================================================
// 文件: ConnectionSettingsForm.cs
// 作用: 连接设置弹窗 — 编辑 Modbus TCP 连接参数
// 使用: 工具栏 "连接设置" 按钮打开此窗口
// ============================================================

using ModbusSCADA.Models;
using ModbusSCADA.Resources;

namespace ModbusSCADA.Forms;

/// <summary>
/// 连接设置弹窗
/// </summary>
public partial class ConnectionSettingsForm : Form
{
    // ---------- 控件声明 ----------
    private Label _lblIP, _lblPort, _lblSlaveId, _lblTimeout, _lblReconnectInterval, _lblLanguage;
    private TextBox _txtIP, _txtPort, _txtSlaveId, _txtTimeout, _txtReconnectInterval;
    private ComboBox _cboLanguage;
    private Button _btnSave, _btnCancel;
    private TableLayoutPanel _table;

    /// <summary>编辑后的连接设置（由调用者读取）</summary>
    public ConnectionSettings Settings { get; private set; }

    /// <summary>
    /// 构造函数 — 接收现有设置并构造界面
    /// </summary>
    /// <param name="settings">当前连接设置</param>
    public ConnectionSettingsForm(ConnectionSettings settings)
    {
        // 复制一份，避免直接修改外部对象（"防御性拷贝"）
        Settings = new ConnectionSettings
        {
            IPAddress = settings.IPAddress,
            Port = settings.Port,
            SlaveId = settings.SlaveId,
            Timeout = settings.Timeout,
            ReconnectInterval = settings.ReconnectInterval,
            Language = settings.Language
        };

        InitializeForm();
        LoadSettingsToUI();
    }

    /// <summary>
    /// 初始化表单控件和布局
    /// </summary>
    private void InitializeForm()
    {
        SuspendLayout();

        // DPI 缩放 — 必须在添加任何控件之前设置,且 AutoScaleDimensions 在 AutoScaleMode 之前
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        // 表单基本属性
        Text = Strings.ConnSettingsTitle;
        // 使用 ClientSize(不含标题栏/边框),DPI 下行为更稳定
        ClientSize = new Size(400, 300);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Microsoft YaHei", 9F); // 微软雅黑支持中/英/俄

        // 使用 TableLayoutPanel 做规整的网格布局
        _table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(15),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        // 标签列 AutoSize — 适应多语言长文字,第二列填充剩余空间
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // 7 行均使用 AutoSize,根据控件实际高度自适应,避免被压缩
        for (int i = 0; i < 7; i++)
            _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 创建所有控件
        _lblIP = new Label
        {
            Text = Strings.IPAddress,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 8, 3, 8)
        };
        _txtIP = new TextBox { Text = "127.0.0.1", Anchor = AnchorStyles.Left | AnchorStyles.Right };

        _lblPort = new Label
        {
            Text = Strings.Port,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 8, 3, 8)
        };
        _txtPort = new TextBox { Text = "502", Anchor = AnchorStyles.Left | AnchorStyles.Right };

        _lblSlaveId = new Label
        {
            Text = Strings.SlaveId,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 8, 3, 8)
        };
        _txtSlaveId = new TextBox { Text = "1", Anchor = AnchorStyles.Left | AnchorStyles.Right };

        _lblTimeout = new Label
        {
            Text = Strings.Timeout,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 8, 3, 8)
        };
        _txtTimeout = new TextBox { Text = "3000", Anchor = AnchorStyles.Left | AnchorStyles.Right };

        _lblReconnectInterval = new Label
        {
            Text = Strings.ReconnectInterval,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 8, 3, 8)
        };
        _txtReconnectInterval = new TextBox { Text = "5000", Anchor = AnchorStyles.Left | AnchorStyles.Right };

        _lblLanguage = new Label
        {
            Text = Strings.SwitchLanguage,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(3, 8, 3, 8)
        };
        _cboLanguage = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        foreach (var lang in Helpers.LanguageHelper.SupportedLanguages)
        {
            _cboLanguage.Items.Add(new KeyValuePair<string, string>(lang.Key, lang.Value));
        }
        _cboLanguage.DisplayMember = "Value"; // 显示 "中文" / "English" / "Русский"
        _cboLanguage.ValueMember = "Key";      // 值是 "zh-CN" / "en" / "ru"

        // 底部按钮行
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

        // 填充表格
        _table.Controls.Add(_lblIP, 0, 0);
        _table.Controls.Add(_txtIP, 1, 0);
        _table.Controls.Add(_lblPort, 0, 1);
        _table.Controls.Add(_txtPort, 1, 1);
        _table.Controls.Add(_lblSlaveId, 0, 2);
        _table.Controls.Add(_txtSlaveId, 1, 2);
        _table.Controls.Add(_lblTimeout, 0, 3);
        _table.Controls.Add(_txtTimeout, 1, 3);
        _table.Controls.Add(_lblReconnectInterval, 0, 4);
        _table.Controls.Add(_txtReconnectInterval, 1, 4);
        _table.Controls.Add(_lblLanguage, 0, 5);
        _table.Controls.Add(_cboLanguage, 1, 5);
        _table.Controls.Add(buttonPanel, 0, 6);
        _table.SetColumnSpan(buttonPanel, 2); // 按钮行跨两列

        Controls.Add(_table);

        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>
    /// 将 Settings 中的值加载到 UI 控件中
    /// </summary>
    private void LoadSettingsToUI()
    {
        _txtIP.Text = Settings.IPAddress;
        _txtPort.Text = Settings.Port.ToString();
        _txtSlaveId.Text = Settings.SlaveId.ToString();
        _txtTimeout.Text = Settings.Timeout.ToString();
        _txtReconnectInterval.Text = Settings.ReconnectInterval.ToString();

        // 设置语言下拉框选中项
        for (int i = 0; i < _cboLanguage.Items.Count; i++)
        {
            var kv = (KeyValuePair<string, string>)_cboLanguage.Items[i];
            if (kv.Key == Settings.Language)
            {
                _cboLanguage.SelectedIndex = i;
                break;
            }
        }
    }

    /// <summary>
    /// 保存按钮点击 — 校验输入并保存设置
    /// </summary>
    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // 验证 IP 地址格式
        if (!System.Net.IPAddress.TryParse(_txtIP.Text, out var ip))
        {
            MessageBox.Show("IP 地址格式不正确", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtIP.Focus();
            return;
        }
        Settings.IPAddress = ip.ToString();

        // 验证端口号（1-65535）
        if (!int.TryParse(_txtPort.Text, out int port) || port < 1 || port > 65535)
        {
            MessageBox.Show("端口范围应为 1-65535", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtPort.Focus();
            return;
        }
        Settings.Port = port;

        // 验证从站地址（1-247）
        if (!byte.TryParse(_txtSlaveId.Text, out byte slaveId) || slaveId < 1 || slaveId > 247)
        {
            MessageBox.Show("从站地址范围应为 1-247", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtSlaveId.Focus();
            return;
        }
        Settings.SlaveId = slaveId;

        // 验证超时
        if (!int.TryParse(_txtTimeout.Text, out int timeout) || timeout < 100)
        {
            MessageBox.Show("超时不能小于 100ms", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtTimeout.Focus();
            return;
        }
        Settings.Timeout = timeout;

        // 验证重连间隔
        if (!int.TryParse(_txtReconnectInterval.Text, out int reconnect) || reconnect < 100)
        {
            MessageBox.Show("重连间隔不能小于 100ms", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtReconnectInterval.Focus();
            return;
        }
        Settings.ReconnectInterval = reconnect;

        // 保存语言选择
        if (_cboLanguage.SelectedItem is KeyValuePair<string, string> kv)
        {
            Settings.Language = kv.Key;
        }

        // 验证通过，关闭弹窗并返回 OK
        DialogResult = DialogResult.OK;
        Close();
    }
}
