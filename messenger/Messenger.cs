using System.IO;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace testprog.messenger;

public static class TestProgClient
{
    public static Task<ITestProgClient> ConnectAsync(
        TestProgClientOptions options,
        CancellationToken cancellationToken = default)
    {
        return ConnectCoreAsync(options, cancellationToken);
    }

    private static async Task<ITestProgClient> ConnectCoreAsync(
        TestProgClientOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.ConnectTimeout);

        ServerEndpoint endpoint;
        FramedTcpChannel channel;
        try
        {
            endpoint = await ServerDiscovery.ResolveAsync(options, timeoutCts.Token).ConfigureAwait(false);
            channel = await FramedTcpChannel.ConnectAsync(endpoint.Host, endpoint.Port, timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Unable to connect within configured timeout ({options.ConnectTimeout}).");
        }

        try
        {
            ProtocolEnvelope clientHello = new()
            {
                Type = MessageTypes.ClientHello,
                Payload = ProtocolSerializer.ToPayloadObject(new ClientHelloPayload
                {
                    StudentId = options.StudentId,
                    DisplayName = options.DisplayName,
                    ClientVersion = typeof(TestProgClient).Assembly.GetName().Version?.ToString() ?? "0.0.0"
                })
            };

            await channel.SendAsync(clientHello, timeoutCts.Token).ConfigureAwait(false);

            ProtocolEnvelope serverHelloEnvelope = await channel.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
            if (!string.Equals(serverHelloEnvelope.Type, MessageTypes.ServerHello, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Expected '{MessageTypes.ServerHello}' but received '{serverHelloEnvelope.Type}'.");
            }

            ServerHelloPayload serverHello = ProtocolSerializer.DeserializePayload<ServerHelloPayload>(serverHelloEnvelope);
            if (string.IsNullOrWhiteSpace(serverHello.SessionToken))
            {
                throw new InvalidOperationException("Server did not return a session token.");
            }

            return new ConnectedTestProgClient(channel, serverHello.SessionToken, options.HeartbeatTimeout);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await channel.DisposeAsync().ConfigureAwait(false);
            throw new TimeoutException(
                $"Server handshake did not complete within configured timeout ({options.ConnectTimeout}).");
        }
        catch
        {
            await channel.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}

public interface ITestProgClient : IAsyncDisposable
{
    Task<TestRunSummary> RunAsync(
        Func<TestInput, object?> solve,
        CancellationToken cancellationToken = default);

    Task<TestRunSummary> RunAsync(
        Func<TestInput, object?> solve,
        Action<TestRunProgress>? onProgress,
        CancellationToken cancellationToken = default);

    Task<TestRunSummary> RunAsync(
        Func<TestInput, Task<object?>> solveAsync,
        CancellationToken cancellationToken = default);

    Task<TestRunSummary> RunAsync(
        Func<TestInput, Task<object?>> solveAsync,
        Action<TestRunProgress>? onProgress,
        CancellationToken cancellationToken = default);

    Task StopAsync(
        string reasonCode = StopReasonCodes.ClientStop,
        string? reasonDetail = null,
        CancellationToken cancellationToken = default);
}

public sealed class TestProgClientOptions
{
    public string StudentId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public DiscoveryOptions Discovery { get; init; } = DiscoveryOptions.Default;
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(8);
    public TimeSpan HeartbeatTimeout { get; init; } = TimeSpan.FromSeconds(10);

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(StudentId))
        {
            throw new ArgumentException("StudentId is required.", nameof(StudentId));
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("DisplayName is required.", nameof(DisplayName));
        }

        if (ConnectTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ConnectTimeout), "ConnectTimeout must be greater than zero.");
        }

        if (HeartbeatTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(HeartbeatTimeout), "HeartbeatTimeout must be greater than zero.");
        }

        if (Discovery is null)
        {
            throw new ArgumentException("Discovery is required.", nameof(Discovery));
        }

        Discovery.Validate();
    }
}

public sealed class DiscoveryOptions
{
    public static DiscoveryOptions Default { get; } = new();

    public DiscoveryMode Mode { get; init; } = DiscoveryMode.Auto;
    public string MulticastAddress { get; init; } = "239.0.0.222";
    public int MulticastPort { get; init; } = 11000;
    public string? DirectServerHost { get; init; }
    public int DirectServerPort { get; init; } = 5000;
    public TimeSpan DiscoveryTimeout { get; init; } = TimeSpan.FromSeconds(3);

    internal void Validate()
    {
        if (MulticastPort is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(MulticastPort), "MulticastPort must be between 1 and 65535.");
        }

        if (DirectServerPort is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(DirectServerPort), "DirectServerPort must be between 1 and 65535.");
        }

        if (DiscoveryTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(DiscoveryTimeout), "DiscoveryTimeout must be greater than zero.");
        }

        if (Mode == DiscoveryMode.DirectTcp && string.IsNullOrWhiteSpace(DirectServerHost))
        {
            throw new ArgumentException("DirectServerHost is required when discovery mode is DirectTcp.", nameof(DirectServerHost));
        }
    }
}

public enum DiscoveryMode
{
    Auto = 0,
    DirectTcp = 1
}

public enum TestCaseResultStatus
{
    Passed = 0,
    Failed = 1
}

public enum TestRunProgressKind
{
    TestBegin = 0,
    TestGroupStart = 1,
    TestCaseStart = 2,
    TestCaseResult = 3,
    TestGroupEnd = 4,
    TestEnd = 5,
    Stop = 6
}

public sealed class TestRunProgress
{
    public TestRunProgressKind Kind { get; init; }
    public string? GroupId { get; init; }
    public string? GroupDisplayName { get; init; }
    public int GroupTestCaseCount { get; init; }
    public int GroupPassedCount { get; init; }
    public int GroupFailedCount { get; init; }
    public string? TestCaseId { get; init; }
    public string? TestCaseStatus { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
    public int TotalPassedCount { get; init; }
    public int TotalFailedCount { get; init; }
}

public sealed class TestRunSummary
{
    public string SessionToken { get; internal set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; internal set; }
    public DateTimeOffset FinishedAtUtc { get; internal set; }
    public int TestGroupCount { get; internal set; }
    public int TestCaseCount { get; internal set; }
    public int PassedCount { get; internal set; }
    public int FailedCount { get; internal set; }
    public bool Completed { get; internal set; }
    public string? StopReasonCode { get; internal set; }
    public string? StopReasonDetail { get; internal set; }
}

public static class StopReasonCodes
{
    public const string ClientStop = "client-stop";
    public const string ServerStop = "server-stop";
    public const string Unauthorized = "unauthorized";
    public const string Timeout = "timeout";
    public const string InvalidAnswer = "invalid-answer";
    public const string TooManyWrongAnswers = "too-many-wrong-answers";
    public const string InternalServerError = "internal-server-error";
}

internal sealed class ConnectedTestProgClient : ITestProgClient
{
    private readonly FramedTcpChannel _channel;
    private readonly string _sessionToken;
    private readonly TimeSpan _heartbeatTimeout;
    private bool _hasRun;
    private bool _disposed;

    public ConnectedTestProgClient(FramedTcpChannel channel, string sessionToken, TimeSpan heartbeatTimeout)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new ArgumentException("Session token is required.", nameof(sessionToken));
        }

        if (heartbeatTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(heartbeatTimeout), "Heartbeat timeout must be greater than zero.");
        }

        _sessionToken = sessionToken;
        _heartbeatTimeout = heartbeatTimeout;
    }

    public Task<TestRunSummary> RunAsync(
        Func<TestInput, object?> solve,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solve);
        return RunAsync(input => Task.FromResult(solve(input)), onProgress: null, cancellationToken);
    }

    public Task<TestRunSummary> RunAsync(
        Func<TestInput, object?> solve,
        Action<TestRunProgress>? onProgress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solve);
        return RunAsync(input => Task.FromResult(solve(input)), onProgress, cancellationToken);
    }

    public async Task<TestRunSummary> RunAsync(
        Func<TestInput, Task<object?>> solveAsync,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync(solveAsync, onProgress: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TestRunSummary> RunAsync(
        Func<TestInput, Task<object?>> solveAsync,
        Action<TestRunProgress>? onProgress,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(solveAsync);

        if (_hasRun)
        {
            throw new InvalidOperationException("RunAsync can be called only once per connection.");
        }

        _hasRun = true;
        TestRunSummary summary = new()
        {
            SessionToken = _sessionToken,
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        TestGroupStartPayload? currentGroup = null;
        int currentGroupPassed = 0;
        int currentGroupFailed = 0;

        while (true)
        {
            ProtocolEnvelope envelope = await ReceiveWithHeartbeatTimeoutAsync(cancellationToken).ConfigureAwait(false);
            EnsureSessionToken(envelope);

            switch (envelope.Type)
            {
                case MessageTypes.TestBegin:
                    EmitProgress(onProgress, new TestRunProgress
                    {
                        Kind = TestRunProgressKind.TestBegin
                    });
                    break;
                case MessageTypes.TestGroupEnd:
                    TestGroupEndPayload groupEnd = ProtocolSerializer.DeserializePayload<TestGroupEndPayload>(envelope);
                    EmitProgress(onProgress, new TestRunProgress
                    {
                        Kind = TestRunProgressKind.TestGroupEnd,
                        GroupId = groupEnd.GroupId,
                        GroupDisplayName = currentGroup?.DisplayName,
                        GroupTestCaseCount = currentGroup?.TestCaseCount ?? 0,
                        GroupPassedCount = currentGroupPassed,
                        GroupFailedCount = currentGroupFailed,
                        TotalPassedCount = summary.PassedCount,
                        TotalFailedCount = summary.FailedCount
                    });

                    currentGroup = null;
                    currentGroupPassed = 0;
                    currentGroupFailed = 0;
                    break;
                case MessageTypes.Ping:
                    await SendEnvelopeAsync(MessageTypes.Pong, null, cancellationToken).ConfigureAwait(false);
                    break;
                case MessageTypes.TestGroupStart:
                    summary.TestGroupCount += 1;
                    currentGroup = ProtocolSerializer.DeserializePayload<TestGroupStartPayload>(envelope);
                    currentGroupPassed = 0;
                    currentGroupFailed = 0;
                    EmitProgress(onProgress, new TestRunProgress
                    {
                        Kind = TestRunProgressKind.TestGroupStart,
                        GroupId = currentGroup.GroupId,
                        GroupDisplayName = currentGroup.DisplayName,
                        GroupTestCaseCount = currentGroup.TestCaseCount,
                        TotalPassedCount = summary.PassedCount,
                        TotalFailedCount = summary.FailedCount
                    });
                    break;
                case MessageTypes.TestCase:
                    summary.TestCaseCount += 1;
                    TestCasePayload testcase = ProtocolSerializer.DeserializePayload<TestCasePayload>(envelope);
                    EmitProgress(onProgress, new TestRunProgress
                    {
                        Kind = TestRunProgressKind.TestCaseStart,
                        GroupId = currentGroup?.GroupId,
                        GroupDisplayName = currentGroup?.DisplayName,
                        GroupTestCaseCount = currentGroup?.TestCaseCount ?? 0,
                        GroupPassedCount = currentGroupPassed,
                        GroupFailedCount = currentGroupFailed,
                        TestCaseId = testcase.TestCaseId,
                        TotalPassedCount = summary.PassedCount,
                        TotalFailedCount = summary.FailedCount
                    });

                    await SolveAndRespondAsync(envelope, solveAsync, cancellationToken).ConfigureAwait(false);
                    break;
                case MessageTypes.TestCaseResult:
                    TestCaseResultPayload result = ApplyResult(summary, envelope);
                    if (string.Equals(result.Status, TestCaseResultStatuses.Passed, StringComparison.OrdinalIgnoreCase))
                    {
                        currentGroupPassed += 1;
                    }
                    else
                    {
                        currentGroupFailed += 1;
                    }

                    EmitProgress(onProgress, new TestRunProgress
                    {
                        Kind = TestRunProgressKind.TestCaseResult,
                        GroupId = currentGroup?.GroupId,
                        GroupDisplayName = currentGroup?.DisplayName,
                        GroupTestCaseCount = currentGroup?.TestCaseCount ?? 0,
                        GroupPassedCount = currentGroupPassed,
                        GroupFailedCount = currentGroupFailed,
                        TestCaseId = result.TestCaseId,
                        TestCaseStatus = result.Status,
                        ReasonCode = result.ReasonCode,
                        ReasonDetail = result.ReasonDetail,
                        TotalPassedCount = summary.PassedCount,
                        TotalFailedCount = summary.FailedCount
                    });
                    break;
                case MessageTypes.TestEnd:
                    summary.Completed = true;
                    summary.FinishedAtUtc = DateTimeOffset.UtcNow;
                    EmitProgress(onProgress, new TestRunProgress
                    {
                        Kind = TestRunProgressKind.TestEnd,
                        TotalPassedCount = summary.PassedCount,
                        TotalFailedCount = summary.FailedCount
                    });
                    return summary;
                case MessageTypes.Stop:
                    ApplyStop(summary, envelope);
                    summary.FinishedAtUtc = DateTimeOffset.UtcNow;
                    EmitProgress(onProgress, new TestRunProgress
                    {
                        Kind = TestRunProgressKind.Stop,
                        ReasonCode = summary.StopReasonCode,
                        ReasonDetail = summary.StopReasonDetail,
                        TotalPassedCount = summary.PassedCount,
                        TotalFailedCount = summary.FailedCount
                    });
                    return summary;
                case MessageTypes.Error:
                    throw CreateProtocolException(envelope);
                default:
                    throw new InvalidOperationException($"Unknown message type '{envelope.Type}'.");
            }
        }
    }

    public async Task StopAsync(
        string reasonCode = StopReasonCodes.ClientStop,
        string? reasonDetail = null,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        string finalReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? StopReasonCodes.ClientStop : reasonCode;

        try
        {
            await SendEnvelopeAsync(
                MessageTypes.Stop,
                new StopPayload
                {
                    ReasonCode = finalReasonCode,
                    ReasonDetail = reasonDetail
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        finally
        {
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _channel.DisposeAsync().ConfigureAwait(false);
    }

    private async Task SolveAndRespondAsync(
        ProtocolEnvelope testcaseEnvelope,
        Func<TestInput, Task<object?>> solveAsync,
        CancellationToken cancellationToken)
    {
        TestCasePayload testcase = ProtocolSerializer.DeserializePayload<TestCasePayload>(testcaseEnvelope);
        if (string.IsNullOrWhiteSpace(testcase.TestCaseId))
        {
            throw new InvalidOperationException("Received testcase without testcaseId.");
        }

        object? solved;
        try
        {
            TestInput input = new(testcase.Input ?? new JObject());
            solved = await solveAsync(input).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await StopAsync(
                StopReasonCodes.InvalidAnswer,
                $"Student solver threw exception: {ex.Message}",
                cancellationToken).ConfigureAwait(false);
            throw;
        }

        TestOutput output = TestOutput.FromObject(solved);
        TestCaseSolvedPayload solvedPayload = new()
        {
            TestCaseId = testcase.TestCaseId,
            Output = output.Payload
        };

        await SendEnvelopeAsync(MessageTypes.TestCaseSolved, solvedPayload, cancellationToken).ConfigureAwait(false);
    }

    private TestCaseResultPayload ApplyResult(TestRunSummary summary, ProtocolEnvelope resultEnvelope)
    {
        TestCaseResultPayload result = ProtocolSerializer.DeserializePayload<TestCaseResultPayload>(resultEnvelope);
        if (string.Equals(result.Status, TestCaseResultStatuses.Passed, StringComparison.OrdinalIgnoreCase))
        {
            summary.PassedCount += 1;
            return result;
        }

        if (string.Equals(result.Status, TestCaseResultStatuses.Failed, StringComparison.OrdinalIgnoreCase))
        {
            summary.FailedCount += 1;
            return result;
        }

        throw new InvalidOperationException($"Unknown testcase result status '{result.Status}'.");
    }

    private static void EmitProgress(Action<TestRunProgress>? onProgress, TestRunProgress progress)
    {
        if (onProgress is null)
        {
            return;
        }

        try
        {
            onProgress(progress);
        }
        catch
        {
        }
    }

    private void ApplyStop(TestRunSummary summary, ProtocolEnvelope stopEnvelope)
    {
        try
        {
            StopPayload stopPayload = ProtocolSerializer.DeserializePayload<StopPayload>(stopEnvelope);
            summary.StopReasonCode = string.IsNullOrWhiteSpace(stopPayload.ReasonCode)
                ? StopReasonCodes.ServerStop
                : stopPayload.ReasonCode;
            summary.StopReasonDetail = stopPayload.ReasonDetail;
        }
        catch (JsonException)
        {
            summary.StopReasonCode = StopReasonCodes.ServerStop;
            summary.StopReasonDetail = "Stop payload is invalid.";
        }
    }

    private Exception CreateProtocolException(ProtocolEnvelope errorEnvelope)
    {
        try
        {
            ErrorPayload payload = ProtocolSerializer.DeserializePayload<ErrorPayload>(errorEnvelope);
            string reasonCode = string.IsNullOrWhiteSpace(payload.ReasonCode)
                ? StopReasonCodes.InternalServerError
                : payload.ReasonCode;
            string detail = payload.ReasonDetail ?? "Server reported an unspecified protocol error.";
            return new InvalidOperationException($"Server error '{reasonCode}': {detail}");
        }
        catch (JsonException)
        {
            return new InvalidOperationException("Server sent invalid error payload.");
        }
    }

    private void EnsureSessionToken(ProtocolEnvelope envelope)
    {
        if (!string.Equals(envelope.SessionToken, _sessionToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Incoming message has invalid or missing sessionToken.");
        }
    }

    private async Task SendEnvelopeAsync(string type, object? payload, CancellationToken cancellationToken)
    {
        ProtocolEnvelope envelope = new()
        {
            Type = type,
            SessionToken = _sessionToken,
            Payload = ProtocolSerializer.ToPayloadObject(payload)
        };

        await _channel.SendAsync(envelope, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ProtocolEnvelope> ReceiveWithHeartbeatTimeoutAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        heartbeatCts.CancelAfter(_heartbeatTimeout);

        try
        {
            return await _channel.ReceiveAsync(heartbeatCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await StopAsync(
                StopReasonCodes.Timeout,
                $"No message received within heartbeat timeout ({_heartbeatTimeout}).",
                CancellationToken.None).ConfigureAwait(false);

            throw new TimeoutException("Connection timed out while waiting for server message.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ConnectedTestProgClient));
        }
    }
}
