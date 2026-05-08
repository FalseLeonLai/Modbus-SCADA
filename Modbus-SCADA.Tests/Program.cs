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
            ("ModbusService_AsyncStopAndDisconnectAreAvailableAndIdempotent", ModbusService_AsyncStopAndDisconnectAreAvailableAndIdempotent),
            ("ModbusService_ConnectAsyncCompletesWithinConfiguredTimeout", ModbusService_ConnectAsyncCompletesWithinConfiguredTimeout),
            ("ModbusService_SerializesConcurrentModbusReadWriteCalls", ModbusService_SerializesConcurrentModbusReadWriteCalls)
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
