using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Xml.Linq;
using ModbusSCADA.Forms;
using ModbusSCADA.Helpers;
using ModbusSCADA.Models;
using ModbusSCADA.Services;
using NModbus;

namespace ModbusSCADA.Tests;

internal static class Program
{
    [STAThread]
    public static int Main()
    {
        return RunAsync().GetAwaiter().GetResult();
    }

    private static async Task<int> RunAsync()
    {
        var tests = new (string Name, Func<Task> Body)[]
        {
            ("ConfigService_UsesInjectedDirectoryForAllConfigFiles", ConfigService_UsesInjectedDirectoryForAllConfigFiles),
            ("ConfigService_InvalidJsonFallsBackAndBacksUpBadFiles", ConfigService_InvalidJsonFallsBackAndBacksUpBadFiles),
            ("ConfigService_SaveVariablesDoesNotPersistRuntimeFields", ConfigService_SaveVariablesDoesNotPersistRuntimeFields),
            ("VariableConfigForm_AppliesValidationAndReadOnlyWriteRules", VariableConfigForm_AppliesValidationAndReadOnlyWriteRules),
            ("ResourceFiles_HaveMatchingKeysAndSupportedLanguages", ResourceFiles_HaveMatchingKeysAndSupportedLanguages),
            ("MainForm_VariableEditCommandsCanBeDisabledWhileConnected", MainForm_VariableEditCommandsCanBeDisabledWhileConnected),
            ("MainForm_RefreshesSelectedInfoWhenSelectedVariableValueUpdates", MainForm_RefreshesSelectedInfoWhenSelectedVariableValueUpdates),
            ("ModbusService_AsyncStopAndDisconnectAreAvailableAndIdempotent", ModbusService_AsyncStopAndDisconnectAreAvailableAndIdempotent),
            ("ModbusService_ConnectAsyncCompletesWithinConfiguredTimeout", ModbusService_ConnectAsyncCompletesWithinConfiguredTimeout),
            ("ModbusService_SerializesConcurrentModbusReadWriteCalls", ModbusService_SerializesConcurrentModbusReadWriteCalls),
            ("ModbusService_RaisesConnectionLostAndMarksAllVariablesDisconnectedWhenServerCloses", ModbusService_RaisesConnectionLostAndMarksAllVariablesDisconnectedWhenServerCloses),
            ("ModbusService_RaisesConnectionLostOnlyOnceForMultipleFailedReads", ModbusService_RaisesConnectionLostOnlyOnceForMultipleFailedReads),
            ("ModbusService_DoesNotRaiseConnectionLostWhenUserInitiatedDisconnect", ModbusService_DoesNotRaiseConnectionLostWhenUserInitiatedDisconnect)
        };

        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                await test.Body().ConfigureAwait(false);
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"FAIL {test.Name}");
                Console.WriteLine(ex);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private static Task ConfigService_UsesInjectedDirectoryForAllConfigFiles()
    {
        using var scope = new ConfigDirectoryScope();

        var settings = new ConnectionSettings { IPAddress = "192.168.0.10", Port = 1502, SlaveId = 2 };
        ConfigService.SaveConnectionSettings(settings);
        ConfigService.SaveVariables(new List<ModbusVariable>
        {
            new() { Name = "Motor", Address = 10, DataType = ModbusDataType.Coil, CanWrite = true, PollInterval = 100 }
        });

        TestAssert.Equal(scope.DirectoryPath, ConfigService.ConfigDirectory, "ConfigDirectory 应返回注入目录。");
        TestAssert.True(File.Exists(Path.Combine(scope.DirectoryPath, "conn-settings.json")), "连接配置应写入注入目录。");
        TestAssert.True(File.Exists(Path.Combine(scope.DirectoryPath, "variables.json")), "变量配置应写入注入目录。");
        return Task.CompletedTask;
    }

    private static Task ConfigService_InvalidJsonFallsBackAndBacksUpBadFiles()
    {
        using var scope = new ConfigDirectoryScope();
        File.WriteAllText(Path.Combine(scope.DirectoryPath, "conn-settings.json"), "{ bad json");
        File.WriteAllText(Path.Combine(scope.DirectoryPath, "variables.json"), "{ bad json");

        var settings = ConfigService.LoadConnectionSettings();
        var variables = ConfigService.LoadVariables();

        TestAssert.Equal("127.0.0.1", settings.IPAddress, "坏连接配置应回退默认 IP。");
        TestAssert.Equal(502, settings.Port, "坏连接配置应回退默认端口。");
        TestAssert.Empty(variables, "坏变量配置应回退为空列表。");
        TestAssert.True(Directory.GetFiles(scope.DirectoryPath, "conn-settings.json.bad-*").Length == 1, "坏连接配置应被备份。");
        TestAssert.True(Directory.GetFiles(scope.DirectoryPath, "variables.json.bad-*").Length == 1, "坏变量配置应被备份。");
        return Task.CompletedTask;
    }

    private static Task ConfigService_SaveVariablesDoesNotPersistRuntimeFields()
    {
        using var scope = new ConfigDirectoryScope();
        var variable = new ModbusVariable
        {
            Name = "Pump",
            Address = 42,
            DataType = ModbusDataType.HoldingRegister,
            CanWrite = true,
            PollInterval = 250,
            CurrentValue = (ushort)123,
            LastReadTime = new DateTime(2026, 5, 7, 1, 2, 3),
            IsConnected = true
        };

        ConfigService.SaveVariables(new List<ModbusVariable> { variable });

        var json = File.ReadAllText(Path.Combine(scope.DirectoryPath, "variables.json"));
        TestAssert.DoesNotContain("CurrentValue", json, "变量配置不应持久化 CurrentValue。");
        TestAssert.DoesNotContain("LastReadTime", json, "变量配置不应持久化 LastReadTime。");
        TestAssert.DoesNotContain("IsConnected", json, "变量配置不应持久化 IsConnected。");

        var loaded = ConfigService.LoadVariables().Single();
        TestAssert.Equal("Pump", loaded.Name, "配置字段应正常恢复。");
        TestAssert.Null(loaded.CurrentValue, "运行时值应保持默认。");
        TestAssert.Equal(DateTime.MinValue, loaded.LastReadTime, "运行时时间应保持默认。");
        TestAssert.False(loaded.IsConnected, "运行时连接状态应保持默认。");
        return Task.CompletedTask;
    }

    private static Task VariableConfigForm_AppliesValidationAndReadOnlyWriteRules()
    {
        using var form = new VariableConfigForm(new List<string>());

        GetTextBox(form, "_txtName").Text = "Input01";
        GetTextBox(form, "_txtAddress").Text = "65535";
        GetTextBox(form, "_txtPollInterval").Text = "50";
        SelectDataType(GetComboBox(form, "_cboDataType"), ModbusDataType.DiscreteInput);

        var canWrite = GetCheckBox(form, "_chkCanWrite");
        canWrite.Checked = true;

        InvokePrivate(form, "BtnSave_Click", null, EventArgs.Empty);

        TestAssert.Equal(DialogResult.OK, form.DialogResult, "有效变量配置应通过校验。");
        TestAssert.Equal("Input01", form.Result.Name, "变量名称应去除空白后保存。");
        TestAssert.Equal((ushort)65535, form.Result.Address, "地址上界 65535 应有效。");
        TestAssert.Equal(50, form.Result.PollInterval, "轮询间隔下界 50ms 应有效。");
        TestAssert.Equal(ModbusDataType.DiscreteInput, form.Result.DataType, "数据类型应保存为选择值。");
        TestAssert.False(form.Result.CanWrite, "只读数据类型即使勾选也不能保存为可写。");
        return Task.CompletedTask;
    }

    private static Task ResourceFiles_HaveMatchingKeysAndSupportedLanguages()
    {
        var root = FindRepositoryRoot();
        var neutral = ReadResourceKeys(Path.Combine(root, "Resources", "Strings.resx"));
        var english = ReadResourceKeys(Path.Combine(root, "Resources", "Strings.en.resx"));
        var russian = ReadResourceKeys(Path.Combine(root, "Resources", "Strings.ru.resx"));

        TestAssert.SetEquals(neutral, english, "英文资源键必须与默认资源一致。");
        TestAssert.SetEquals(neutral, russian, "俄文资源键必须与默认资源一致。");

        foreach (var language in LanguageHelper.SupportedLanguages.Keys)
        {
            if (language == "zh-CN") continue;
            TestAssert.True(
                File.Exists(Path.Combine(root, "Resources", $"Strings.{language}.resx")),
                $"语言 {language} 必须有对应资源文件。");
        }

        return Task.CompletedTask;
    }

    private static Task MainForm_VariableEditCommandsCanBeDisabledWhileConnected()
    {
        using var scope = new ConfigDirectoryScope();
        using var form = new MainForm();
        var method = typeof(MainForm).GetMethod(
            "SetVariableEditCommandsEnabled",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        TestAssert.NotNull(method, "MainForm 应提供 SetVariableEditCommandsEnabled 作为变量修改按钮的统一控制点。");

        method!.Invoke(form, new object[] { false });
        TestAssert.False(GetToolStripButton(form, "_btnAdd").Enabled, "连接期间应禁用新增。");
        TestAssert.False(GetToolStripButton(form, "_btnEdit").Enabled, "连接期间应禁用编辑。");
        TestAssert.False(GetToolStripButton(form, "_btnDelete").Enabled, "连接期间应禁用删除。");
        TestAssert.False(GetToolStripButton(form, "_btnImport").Enabled, "连接期间应禁用导入。");

        method.Invoke(form, new object[] { true });
        TestAssert.True(GetToolStripButton(form, "_btnAdd").Enabled, "断开后应恢复新增。");
        TestAssert.True(GetToolStripButton(form, "_btnEdit").Enabled, "断开后应恢复编辑。");
        TestAssert.True(GetToolStripButton(form, "_btnDelete").Enabled, "断开后应恢复删除。");
        TestAssert.True(GetToolStripButton(form, "_btnImport").Enabled, "断开后应恢复导入。");
        return Task.CompletedTask;
    }

    private static Task MainForm_RefreshesSelectedInfoWhenSelectedVariableValueUpdates()
    {
        using var scope = new ConfigDirectoryScope();
        using var form = new MainForm();

        // 强制创建底层 Win32 Handle, 否则 BindingList → DataGridView 的行同步不会触发
        var createControl = typeof(Control).GetMethod(
            "CreateControl",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null, new Type[] { typeof(bool) }, null);
        TestAssert.NotNull(createControl, "Control.CreateControl(bool) 应可反射访问。");
        createControl!.Invoke(form, new object[] { false });

        var manager = (VariableManager)GetPrivateField(form, "_variableManager");
        var dgv = (DataGridView)GetPrivateField(form, "_dgvVariables");
        var lblInfo = (Label)GetPrivateField(form, "_lblSelectedInfo");

        manager.Add(new ModbusVariable
        {
            Name = "B1",
            Address = 1,
            DataType = ModbusDataType.Coil,
            CanWrite = true,
            PollInterval = 1000
        });

        TestAssert.True(dgv.Rows.Count > 0, "添加变量后 DataGridView 应同步生成对应行。");

        // 让第一行成为当前选中行
        dgv.CurrentCell = dgv.Rows[0].Cells[0];
        dgv.Rows[0].Selected = true;
        InvokePrivate(form, "DgvVariables_SelectionChanged", null, EventArgs.Empty);

        var beforeText = lblInfo.Text;
        TestAssert.True(beforeText.Contains("---"),
            $"初次选中变量时 CurrentValue 仍为 null,底部面板应显示 ---,实际: {beforeText}");

        // 模拟后台轮询写入新值 — 必须触发底部面板刷新, 否则用户看到的是陈旧"---"
        manager.UpdateValue(0, true, true);

        var afterText = lblInfo.Text;
        TestAssert.True(afterText.Contains("ON"),
            $"轮询更新数值后,底部面板必须反映新值 ON,实际: {afterText}");
        return Task.CompletedTask;
    }

    private static async Task ModbusService_AsyncStopAndDisconnectAreAvailableAndIdempotent()
    {
        using var service = new ModbusService();

        await service.StopPollAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        await service.StopPollAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        await service.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        await service.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    private static async Task ModbusService_ConnectAsyncCompletesWithinConfiguredTimeout()
    {
        using var service = new ModbusService();
        var settings = new ConnectionSettings
        {
            IPAddress = "203.0.113.1",
            Port = 65000,
            Timeout = 100
        };

        var started = DateTime.UtcNow;
        var connected = await service.ConnectAsync(settings)
            .WaitAsync(TimeSpan.FromSeconds(2))
            .ConfigureAwait(false);
        var elapsed = DateTime.UtcNow - started;

        TestAssert.False(connected, "不可达地址不应报告连接成功。");
        TestAssert.True(elapsed < TimeSpan.FromSeconds(2), "ConnectAsync 应在配置超时边界内返回。");
    }

    private static async Task ModbusService_SerializesConcurrentModbusReadWriteCalls()
    {
        using var service = new ModbusService();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var client = new TcpClient();
        var acceptTask = listener.AcceptTcpClientAsync();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
        using var serverSide = await acceptTask.ConfigureAwait(false);

        var (master, proxy) = CountingModbusMasterProxy.Create(TimeSpan.FromMilliseconds(100));
        SetPrivateField(service, "_tcpClient", client);
        SetPrivateField(service, "_master", master);

        var read1 = service.ReadHoldingRegisterAsync(1, 10);
        var read2 = service.ReadHoldingRegisterAsync(1, 11);
        var write = service.WriteHoldingRegisterAsync(1, 12, 123);
        await Task.WhenAll(read1, read2, write).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        TestAssert.Equal((ushort)321, read1.Result!.Value, "第一个并发读应成功返回。");
        TestAssert.Equal((ushort)321, read2.Result!.Value, "第二个并发读应成功返回。");
        TestAssert.True(write.Result, "并发写应成功返回。");
        TestAssert.Equal(3, proxy.CallCount, "伪主站应收到两个读和一个写。");
        TestAssert.Equal(1, proxy.MaxConcurrent, "所有 Modbus 读写必须被串行化，不能并发进入主站对象。");

        await service.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        listener.Stop();
    }

    private static async Task ModbusService_RaisesConnectionLostAndMarksAllVariablesDisconnectedWhenServerCloses()
    {
        using var service = new ModbusService();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var client = new TcpClient();
        var acceptTask = listener.AcceptTcpClientAsync();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
        var serverSide = await acceptTask.ConfigureAwait(false);

        var master = ThrowingModbusMasterProxy.Create(new IOException("simulated remote close"));
        SetPrivateField(service, "_tcpClient", client);
        SetPrivateField(service, "_master", master);

        var manager = new VariableManager();
        manager.Add(new ModbusVariable
        {
            Name = "v1", Address = 0,
            DataType = ModbusDataType.HoldingRegister, PollInterval = 50,
            CurrentValue = (ushort)10, IsConnected = true
        });
        manager.Add(new ModbusVariable
        {
            Name = "v2", Address = 1,
            DataType = ModbusDataType.HoldingRegister, PollInterval = 50,
            CurrentValue = (ushort)20, IsConnected = true
        });
        manager.Add(new ModbusVariable
        {
            Name = "v3", Address = 2,
            DataType = ModbusDataType.HoldingRegister, PollInterval = 50,
            CurrentValue = (ushort)30, IsConnected = true
        });
        SetPrivateField(service, "_variableManager", manager);

        var fired = 0;
        service.ConnectionLost += (s, e) => Interlocked.Increment(ref fired);

        // 服务器主动关闭连接 — 模拟 PLC/服务端拔线、宕机或重启场景。
        serverSide.Close();
        listener.Stop();
        await Task.Delay(150).ConfigureAwait(false);

        var result = await service.ReadHoldingRegisterAsync(1, 10).ConfigureAwait(false);
        TestAssert.Null(result, "服务器关闭后读取应失败返回 null。");

        TestAssert.Equal(1, fired, "ConnectionLost 事件应在检测到 socket 失活后触发。");
        foreach (var v in manager.Variables)
        {
            TestAssert.False(v.IsConnected, $"{v.Name} 在连接丢失后应被标记为通信异常。");
            TestAssert.Null(v.CurrentValue, $"{v.Name} 在连接丢失后应清空数值,避免显示陈旧数据。");
        }
    }

    private static async Task ModbusService_RaisesConnectionLostOnlyOnceForMultipleFailedReads()
    {
        using var service = new ModbusService();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var client = new TcpClient();
        var acceptTask = listener.AcceptTcpClientAsync();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
        var serverSide = await acceptTask.ConfigureAwait(false);

        var master = ThrowingModbusMasterProxy.Create(new IOException("simulated remote close"));
        SetPrivateField(service, "_tcpClient", client);
        SetPrivateField(service, "_master", master);

        var fired = 0;
        service.ConnectionLost += (s, e) => Interlocked.Increment(ref fired);

        serverSide.Close();
        listener.Stop();
        await Task.Delay(150).ConfigureAwait(false);

        for (int i = 0; i < 5; i++)
        {
            await service.ReadHoldingRegisterAsync(1, (ushort)i).ConfigureAwait(false);
        }

        TestAssert.Equal(1, fired, "多次失败读取也只应触发一次 ConnectionLost,避免事件风暴。");
    }

    private static async Task ModbusService_DoesNotRaiseConnectionLostWhenUserInitiatedDisconnect()
    {
        using var service = new ModbusService();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var client = new TcpClient();
        var acceptTask = listener.AcceptTcpClientAsync();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
        var serverSide = await acceptTask.ConfigureAwait(false);

        SetPrivateField(service, "_tcpClient", client);
        // 这里不需要装载 master, 用户主动断开不会触发 IO

        var fired = 0;
        service.ConnectionLost += (s, e) => Interlocked.Increment(ref fired);

        await service.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        await Task.Delay(50).ConfigureAwait(false);

        TestAssert.Equal(0, fired, "用户主动断开不应误触发 ConnectionLost,该事件只表示服务器异常断线。");

        serverSide.Close();
        listener.Stop();
    }

    private static ToolStripButton GetToolStripButton(MainForm form, string fieldName)
    {
        var field = typeof(MainForm).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        TestAssert.NotNull(field, $"MainForm 缺少字段 {fieldName}。");
        return (ToolStripButton)field!.GetValue(form)!;
    }

    private static TextBox GetTextBox(object target, string fieldName)
    {
        return (TextBox)GetPrivateField(target, fieldName);
    }

    private static ComboBox GetComboBox(object target, string fieldName)
    {
        return (ComboBox)GetPrivateField(target, fieldName);
    }

    private static CheckBox GetCheckBox(object target, string fieldName)
    {
        return (CheckBox)GetPrivateField(target, fieldName);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        TestAssert.NotNull(field, $"{target.GetType().Name} 缺少字段 {fieldName}。");
        return field!.GetValue(target)!;
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        TestAssert.NotNull(field, $"{target.GetType().Name} 缺少字段 {fieldName}。");
        field!.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        TestAssert.NotNull(method, $"{target.GetType().Name} 缺少方法 {methodName}。");
        method!.Invoke(target, args);
    }

    private static void SelectDataType(ComboBox comboBox, ModbusDataType dataType)
    {
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is KeyValuePair<string, ModbusDataType> kv && kv.Value == dataType)
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        throw new InvalidOperationException($"未找到数据类型 {dataType}。");
    }

    private static HashSet<string> ReadResourceKeys(string path)
    {
        return XDocument.Load(path)
            .Root!
            .Elements("data")
            .Select(e => e.Attribute("name")!.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Modbus-SCADA.csproj")))
        {
            dir = dir.Parent;
        }

        TestAssert.NotNull(dir, "无法定位仓库根目录。");
        return dir!.FullName;
    }
}

internal class CountingModbusMasterProxy : DispatchProxy
{
    private int _active;
    private int _callCount;
    private int _maxConcurrent;

    public TimeSpan Delay { get; private set; }
    public int CallCount => _callCount;
    public int MaxConcurrent => _maxConcurrent;

    public static (IModbusMaster Master, CountingModbusMasterProxy Proxy) Create(TimeSpan delay)
    {
        var master = DispatchProxy.Create<IModbusMaster, CountingModbusMasterProxy>();
        var proxy = (CountingModbusMasterProxy)(object)master;
        proxy.Delay = delay;
        return (master, proxy);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        return targetMethod?.Name switch
        {
            "ReadHoldingRegistersAsync" => ReadRegistersAsync(),
            "WriteSingleRegisterAsync" => WriteAsync(),
            "get_Transport" => null,
            _ => throw new NotSupportedException($"测试代理未实现 {targetMethod?.Name}。")
        };
    }

    private async Task<ushort[]> ReadRegistersAsync()
    {
        await TrackCallAsync();
        return new ushort[] { 321 };
    }

    private async Task WriteAsync()
    {
        await TrackCallAsync();
    }

    private async Task TrackCallAsync()
    {
        var active = Interlocked.Increment(ref _active);
        Interlocked.Increment(ref _callCount);
        TrackMax(active);
        try
        {
            await Task.Delay(Delay);
        }
        finally
        {
            Interlocked.Decrement(ref _active);
        }
    }

    private void TrackMax(int active)
    {
        while (true)
        {
            var current = _maxConcurrent;
            if (active <= current) return;
            if (Interlocked.CompareExchange(ref _maxConcurrent, active, current) == current) return;
        }
    }
}

internal class ThrowingModbusMasterProxy : DispatchProxy
{
    public Exception ToThrow { get; private set; } = new IOException("default test exception");

    public static IModbusMaster Create(Exception exception)
    {
        var master = DispatchProxy.Create<IModbusMaster, ThrowingModbusMasterProxy>();
        ((ThrowingModbusMasterProxy)(object)master).ToThrow = exception;
        return master;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        return targetMethod?.Name switch
        {
            "ReadHoldingRegistersAsync" => Task.FromException<ushort[]>(ToThrow),
            "ReadInputRegistersAsync" => Task.FromException<ushort[]>(ToThrow),
            "ReadCoilsAsync" => Task.FromException<bool[]>(ToThrow),
            "ReadInputsAsync" => Task.FromException<bool[]>(ToThrow),
            "WriteSingleCoilAsync" => Task.FromException(ToThrow),
            "WriteSingleRegisterAsync" => Task.FromException(ToThrow),
            "get_Transport" => null,
            _ => throw new NotSupportedException($"测试代理未实现 {targetMethod?.Name}。")
        };
    }
}

internal sealed class ConfigDirectoryScope : IDisposable
{
    public ConfigDirectoryScope()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), "ModbusSCADA.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        ConfigService.SetConfigDirectoryForTesting(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public void Dispose()
    {
        ConfigService.SetConfigDirectoryForTesting(null);
        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch
        {
            // 测试清理失败不应掩盖真实断言失败。
        }
    }
}

internal static class TestAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void False(bool condition, string message)
    {
        True(!condition, message);
    }

    public static void NotNull(object? value, string message)
    {
        if (value == null) throw new InvalidOperationException(message);
    }

    public static void Null(object? value, string message)
    {
        if (value != null) throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected}; Actual: {actual}");
        }
    }

    public static void Empty<T>(ICollection<T> values, string message)
    {
        if (values.Count != 0) throw new InvalidOperationException($"{message} Count: {values.Count}");
    }

    public static void DoesNotContain(string expectedMissing, string actual, string message)
    {
        if (actual.Contains(expectedMissing, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void SetEquals(HashSet<string> expected, HashSet<string> actual, string message)
    {
        if (!expected.SetEquals(actual))
        {
            var missing = string.Join(", ", expected.Except(actual).OrderBy(x => x));
            var extra = string.Join(", ", actual.Except(expected).OrderBy(x => x));
            throw new InvalidOperationException($"{message} Missing: [{missing}], Extra: [{extra}]");
        }
    }
}
