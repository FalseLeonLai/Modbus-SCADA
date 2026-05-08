// ============================================================
// 文件: ModbusService.cs
// 作用: Modbus TCP 通信核心 — 连接、断开、读写寄存器、后台轮询
// 依赖: NModbus NuGet 包
// ============================================================

using System.Net.Sockets;
using NModbus;
using ModbusSCADA.Models;

namespace ModbusSCADA.Services;

/// <summary>
/// Modbus TCP 服务 — 封装与 PLC/设备的通信操作
/// </summary>
public class ModbusService : IDisposable
{
    // ---------- 私有字段 ----------

    /// <summary>TCP 客户端对象</summary>
    private TcpClient? _tcpClient;

    /// <summary>NModbus 的 Modbus 主站对象（用于读写操作）</summary>
    private IModbusMaster? _master;

    /// <summary>后台轮询的取消令牌源（用于停止轮询）</summary>
    private CancellationTokenSource? _cts;

    /// <summary>轮询任务引用</summary>
    private Task? _pollTask;

    /// <summary>串行化所有 Modbus 请求，避免单 TCP 连接上请求/响应交错</summary>
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    /// <summary>当前连接状态</summary>
    public bool IsConnected => _tcpClient?.Connected ?? false;

    /// <summary>变量管理器引用</summary>
    private VariableManager? _variableManager;

    /// <summary>UI 线程的同步上下文 — 用于把后台轮询数据回写到 UI 线程</summary>
    /// <remarks>
    /// BindingList 绑定到 DataGridView 时,从后台线程调用 ResetItem 会触发跨线程访问
    /// Handle 抛 InvalidOperationException。通过捕获 UI 线程的 SynchronizationContext,
    /// 用 Post 把数据更新切回 UI 线程,可彻底避免该异常。
    /// 在 StartPoll 时捕获(此时由用户点击触发,必然在 UI 线程),
    /// 比字段初始化阶段捕获更稳健(后者时机过早,Control 基类可能尚未安装上下文)。
    /// </remarks>
    private SynchronizationContext? _uiContext;

    // ================================================================
    // 连接 / 断开
    // ================================================================

    /// <summary>
    /// 连接到 Modbus TCP 设备
    /// </summary>
    /// <param name="settings">连接参数（IP、端口、超时）</param>
    /// <returns>连接是否成功</returns>
    public async Task<bool> ConnectAsync(ConnectionSettings settings)
    {
        // 先断开已有连接
        await DisconnectAsync().ConfigureAwait(false);

        TcpClient? client = null;

        try
        {
            // 创建 TCP 客户端并连接到设备
            client = new TcpClient
            {
                ReceiveTimeout = settings.Timeout,
                SendTimeout = settings.Timeout
            };
            var timeout = TimeSpan.FromMilliseconds(Math.Max(settings.Timeout, 100));
            await client.ConnectAsync(settings.IPAddress, settings.Port)
                .WaitAsync(timeout)
                .ConfigureAwait(false);

            // 在 TCP 连接上创建 Modbus 主站
            // NModbus 的 ModbusFactory 会通过 TCP 发送/接收 Modbus 报文
            var factory = new ModbusFactory();
            var master = factory.CreateMaster(client);

            await _ioLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _tcpClient = client;
                _master = master;
                client = null;
            }
            finally
            {
                _ioLock.Release();
            }

            return true; // 连接成功
        }
        catch (Exception)
        {
            // 连接失败，清理资源
            client?.Dispose();
            await ClearConnectionAsync().ConfigureAwait(false);
            return false;
        }
    }

    /// <summary>
    /// 断开与 Modbus 设备的连接
    /// </summary>
    public void Disconnect()
    {
        DisconnectAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 异步断开连接。会先等待轮询任务退出，再释放 TCP/Modbus 资源。
    /// </summary>
    public async Task DisconnectAsync()
    {
        var (cts, pollTask) = CancelPoll();
        CloseConnectionToInterruptIo();
        await WaitForPollTaskAsync(pollTask, cts).ConfigureAwait(false);
        await ClearConnectionAsync().ConfigureAwait(false);
    }

    // ================================================================
    // 单次读取操作
    // ================================================================

    /// <summary>
    /// 读取线圈值（功能码 01）
    /// </summary>
    /// <param name="slaveId">从站地址</param>
    /// <param name="address">线圈地址</param>
    /// <returns>true/false 或 null（读取失败）</returns>
    public async Task<bool?> ReadCoilAsync(byte slaveId, ushort address)
    {
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_master == null || !IsConnected) return null;
            // NModbus 的 ReadCoilsAsync: (slaveAddress, startAddress, numberOfPoints)
            var coils = await _master.ReadCoilsAsync(slaveId, address, 1).ConfigureAwait(false);
            // 返回第一个（也是唯一一个）线圈的值
            return coils.Length > 0 ? coils[0] : null;
        }
        catch { return null; }
        finally { _ioLock.Release(); }
    }

    /// <summary>
    /// 读取离散输入值（功能码 02）
    /// </summary>
    public async Task<bool?> ReadDiscreteInputAsync(byte slaveId, ushort address)
    {
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_master == null || !IsConnected) return null;
            var inputs = await _master.ReadInputsAsync(slaveId, address, 1).ConfigureAwait(false);
            return inputs.Length > 0 ? inputs[0] : null;
        }
        catch { return null; }
        finally { _ioLock.Release(); }
    }

    /// <summary>
    /// 读取输入寄存器值（功能码 04）
    /// </summary>
    public async Task<ushort?> ReadInputRegisterAsync(byte slaveId, ushort address)
    {
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_master == null || !IsConnected) return null;
            // ReadInputRegistersAsync: (slaveAddress, startAddress, numberOfPoints)
            var registers = await _master.ReadInputRegistersAsync(slaveId, address, 1).ConfigureAwait(false);
            return registers.Length > 0 ? registers[0] : null; // 返回第一个寄存器的值（0-65535）
        }
        catch { return null; }
        finally { _ioLock.Release(); }
    }

    /// <summary>
    /// 读取保持寄存器值（功能码 03）
    /// </summary>
    public async Task<ushort?> ReadHoldingRegisterAsync(byte slaveId, ushort address)
    {
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_master == null || !IsConnected) return null;
            // ReadHoldingRegistersAsync 读取保持寄存器
            var registers = await _master.ReadHoldingRegistersAsync(slaveId, address, 1).ConfigureAwait(false);
            return registers.Length > 0 ? registers[0] : null;
        }
        catch { return null; }
        finally { _ioLock.Release(); }
    }

    // ================================================================
    // 单次写入操作
    // ================================================================

    /// <summary>
    /// 写入单个线圈（功能码 05）
    /// </summary>
    /// <param name="slaveId">从站地址</param>
    /// <param name="address">线圈地址</param>
    /// <param name="value">要写入的布尔值</param>
    /// <returns>写入是否成功</returns>
    public async Task<bool> WriteCoilAsync(byte slaveId, ushort address, bool value)
    {
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_master == null || !IsConnected) return false;
            // WriteSingleCoilAsync: (slaveAddress, coilAddress, value)
            await _master.WriteSingleCoilAsync(slaveId, address, value).ConfigureAwait(false);
            return true;
        }
        catch { return false; }
        finally { _ioLock.Release(); }
    }

    /// <summary>
    /// 写入单个保持寄存器（功能码 06）
    /// </summary>
    /// <param name="slaveId">从站地址</param>
    /// <param name="address">寄存器地址</param>
    /// <param name="value">要写入的值（0-65535）</param>
    /// <returns>写入是否成功</returns>
    public async Task<bool> WriteHoldingRegisterAsync(byte slaveId, ushort address, ushort value)
    {
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_master == null || !IsConnected) return false;
            // WriteSingleRegisterAsync: (slaveAddress, registerAddress, value)
            await _master.WriteSingleRegisterAsync(slaveId, address, value).ConfigureAwait(false);
            return true;
        }
        catch { return false; }
        finally { _ioLock.Release(); }
    }

    // ================================================================
    // 后台轮询 — 定时读取所有变量
    // ================================================================

    /// <summary>
    /// 启动后台轮询 — 用一个独立线程按变量的 PollInterval 定时刷新数据
    /// </summary>
    /// <param name="variableManager">变量管理器引用</param>
    /// <param name="settings">连接设置（需要从站地址）</param>
    public void StartPoll(VariableManager variableManager, ConnectionSettings settings)
    {
        // 先停止已有的轮询
        StopPoll();
        // 保存变量管理器引用，供轮询内部使用
        _variableManager = variableManager;
        // 捕获 UI 线程的同步上下文 — StartPoll 由 UI 事件(连接按钮点击)调用,
        // 此时 SynchronizationContext.Current 一定是 WindowsFormsSynchronizationContext
        _uiContext = SynchronizationContext.Current;
        // 创建新的取消令牌
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        // 启动异步轮询任务
        _pollTask = Task.Run(() => PollLoopAsync(settings, token), token);
    }

    /// <summary>
    /// 停止后台轮询
    /// </summary>
    public void StopPoll()
    {
        StopPollAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// 停止后台轮询，并等待轮询任务观察到取消后退出。
    /// </summary>
    public async Task StopPollAsync()
    {
        var (cts, pollTask) = CancelPoll();
        await WaitForPollTaskAsync(pollTask, cts).ConfigureAwait(false);
    }

    /// <summary>
    /// 轮询主循环 — 在外层循环中按每个变量的配置间隔读取数据
    /// </summary>
    /// <param name="settings">连接设置</param>
    /// <param name="token">取消令牌，外部可通过 Cancel 停止循环</param>
    private async Task PollLoopAsync(ConnectionSettings settings, CancellationToken token)
    {
        int currentIndex = 0; // 当前轮询的变量索引

        try
        {
            while (!token.IsCancellationRequested)
            {
                // 如果未连接或变量列表为空，等待后继续
                if (!IsConnected || _variableManager == null ||
                    _variableManager.Variables.Count == 0)
                {
                    await Task.Delay(100, token).ConfigureAwait(false);
                    continue;
                }

                var variables = _variableManager.Variables;
                if (currentIndex >= variables.Count)
                {
                    currentIndex = 0;
                }

                // 获取当前要读取的变量
                var variable = variables[currentIndex];

                // 根据数据类型调用对应的读取方法
                object? value = variable.DataType switch
                {
                    ModbusDataType.Coil => await ReadCoilAsync(settings.SlaveId, variable.Address).ConfigureAwait(false),
                    ModbusDataType.DiscreteInput => await ReadDiscreteInputAsync(settings.SlaveId, variable.Address).ConfigureAwait(false),
                    ModbusDataType.InputRegister => await ReadInputRegisterAsync(settings.SlaveId, variable.Address).ConfigureAwait(false),
                    ModbusDataType.HoldingRegister => await ReadHoldingRegisterAsync(settings.SlaveId, variable.Address).ConfigureAwait(false),
                    _ => null
                };

                // 更新变量管理器中的值 — 必须切到 UI 线程,否则 BindingList 绑定的
                // DataGridView 会在后台线程访问 Handle 抛 InvalidOperationException
                var connected = value != null; // 如果读取成功，value 不为 null
                // 捕获到局部变量,避免闭包陷阱(currentIndex/value 在循环中会改变)
                int idxSnapshot = currentIndex;
                object? valSnapshot = value;
                bool connSnapshot = connected;
                var manager = _variableManager;
                // 闭包捕获 token — Post 的 lambda 在 UI 线程执行时,如果用户已点"断开"
                // (token 已 Cancel),就不要再写入,否则会覆盖 MarkAllDisconnected 的结果
                var ctxToken = token;
                if (_uiContext != null)
                {
                    _uiContext.Post(_ =>
                    {
                        if (ctxToken.IsCancellationRequested) return;
                        manager?.UpdateValue(idxSnapshot, valSnapshot, connSnapshot);
                    }, null);
                }
                else
                {
                    // 兜底: 没有 UI 上下文时同步调用(例如单元测试场景)
                    manager.UpdateValue(idxSnapshot, valSnapshot, connSnapshot);
                }

                // 移动到下一个变量（循环遍历）
                // 数学取模运算: 确保索引在 0 到 Count-1 之间循环
                var countAfterUpdate = _variableManager?.Variables.Count ?? 0;
                currentIndex = countAfterUpdate == 0 ? 0 : (currentIndex + 1) % countAfterUpdate;

                // 按当前变量的轮询间隔等待（最小值 50ms 防止 CPU 空转）
                var delay = Math.Max(variable.PollInterval, 50);
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 正常停止轮询。
        }
    }

    private async Task ClearConnectionAsync()
    {
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;
            _master = null;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private (CancellationTokenSource? Cts, Task? PollTask) CancelPoll()
    {
        var cts = _cts;
        var pollTask = _pollTask;
        _cts = null;
        _pollTask = null;
        cts?.Cancel();
        return (cts, pollTask);
    }

    private static async Task WaitForPollTaskAsync(Task? pollTask, CancellationTokenSource? cts)
    {
        try
        {
            if (pollTask != null)
            {
                await pollTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 轮询任务按预期响应取消。
        }
        catch (ObjectDisposedException) when (cts?.IsCancellationRequested == true)
        {
            // 断开连接期间底层资源已释放，视为取消收尾。
        }
        finally
        {
            cts?.Dispose();
        }
    }

    private void CloseConnectionToInterruptIo()
    {
        try
        {
            _tcpClient?.Close();
        }
        catch
        {
            // 断开流程里关闭 socket 只是为了打断阻塞 I/O，失败后仍会进入资源清理。
        }
    }

    // ================================================================
    // 资源释放
    // ================================================================

    /// <summary>
    /// 释放所有资源（实现 IDisposable 接口）
    /// </summary>
    public void Dispose()
    {
        Disconnect();
        _cts?.Dispose();
        _ioLock.Dispose();
    }
}
