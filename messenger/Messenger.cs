using System.IO;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace testprog.messenger;

/// <summary>
/// Factory entry point for establishing a connection to a test server.
/// </summary>
public static class TestProgClient
{
    /// <summary>
    /// Connects to a server using configured discovery mode and performs protocol handshake.
    /// </summary>
    /// <param name="options">Connection and identity options.</param>
    /// <param name="cancellationToken">Cancellation token for connection and handshake.</param>
    /// <returns>Connected test client instance.</returns>
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
            Console.WriteLine(endpoint.Host);
            Console.WriteLine(endpoint.Port);
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

/// <summary>
/// Represents an active client session against the test server.
/// </summary>
public interface ITestProgClient : IAsyncDisposable
{
    /// <summary>
    /// Runs a test session using synchronous solver callback.
    /// </summary>
    /// <param name="solve">Student solver callback.</param>
    /// <param name="cancellationToken">Cancellation token for the run.</param>
    /// <returns>Final test run summary.</returns>
    Task<TestRunSummary> RunAsync(
        Func<TestInput, object?> solve,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a test session using synchronous solver callback and progress reporting.
    /// </summary>
    /// <param name="solve">Student solver callback.</param>
    /// <param name="onProgress">Progress callback invoked for protocol milestones.</param>
    /// <param name="cancellationToken">Cancellation token for the run.</param>
    /// <returns>Final test run summary.</returns>
    Task<TestRunSummary> RunAsync(
        Func<TestInput, object?> solve,
        Action<TestRunProgress>? onProgress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a test session using asynchronous solver callback.
    /// </summary>
    /// <param name="solveAsync">Async student solver callback.</param>
    /// <param name="cancellationToken">Cancellation token for the run.</param>
    /// <returns>Final test run summary.</returns>
    Task<TestRunSummary> RunAsync(
        Func<TestInput, Task<object?>> solveAsync,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a test session using asynchronous solver callback and progress reporting.
    /// </summary>
    /// <param name="solveAsync">Async student solver callback.</param>
    /// <param name="onProgress">Progress callback invoked for protocol milestones.</param>
    /// <param name="cancellationToken">Cancellation token for the run.</param>
    /// <returns>Final test run summary.</returns>
    Task<TestRunSummary> RunAsync(
        Func<TestInput, Task<object?>> solveAsync,
        Action<TestRunProgress>? onProgress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a stop request to server and terminates the session.
    /// </summary>
    /// <param name="reasonCode">Machine-readable reason code.</param>
    /// <param name="reasonDetail">Optional human-readable detail.</param>
    /// <param name="cancellationToken">Cancellation token for the stop operation.</param>
    Task StopAsync(
        string reasonCode = StopReasonCodes.ClientStop,
        string? reasonDetail = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Low-level connection options consumed by <see cref="TestProgClient"/>.
/// </summary>
public sealed class TestProgClientOptions
{
    /// <summary>
    /// Unique student identifier.
    /// </summary>
    public string StudentId { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable student display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Discovery and direct-connect options.
    /// </summary>
    public DiscoveryOptions Discovery { get; init; } = DiscoveryOptions.Default;

    /// <summary>
    /// Maximum time allowed for connection establishment and handshake.
    /// </summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Maximum time between incoming messages before timeout is considered.
    /// </summary>
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

/// <summary>
/// Options controlling how server endpoint is discovered.
/// </summary>
public sealed class DiscoveryOptions
{
    /// <summary>
    /// Default discovery options using multicast auto discovery.
    /// </summary>
    public static DiscoveryOptions Default { get; } = new();

    /// <summary>
    /// Discovery mode.
    /// </summary>
    public DiscoveryMode Mode { get; init; } = DiscoveryMode.Auto;

    /// <summary>
    /// Multicast address for UDP discovery.
    /// </summary>
    public string MulticastAddress { get; init; } = "239.0.0.222";

    /// <summary>
    /// Multicast port for UDP discovery.
    /// </summary>
    public int MulticastPort { get; init; } = 11000;

    /// <summary>
    /// Server host used when <see cref="Mode"/> is <see cref="DiscoveryMode.DirectTcp"/>.
    /// </summary>
    public string? DirectServerHost { get; init; }

    /// <summary>
    /// Server TCP port used when <see cref="Mode"/> is <see cref="DiscoveryMode.DirectTcp"/>.
    /// </summary>
    public int DirectServerPort { get; init; } = 5000;

    /// <summary>
    /// Maximum time allowed for UDP discovery.
    /// </summary>
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

/// <summary>
/// Supported strategies for locating a server endpoint.
/// </summary>
public enum DiscoveryMode
{
    /// <summary>
    /// Resolve server endpoint via UDP multicast discovery.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Connect directly to configured host and port.
    /// </summary>
    DirectTcp = 1
}

/// <summary>
/// Normalized testcase result status.
/// </summary>
public enum TestCaseResultStatus
{
    /// <summary>
    /// Testcase passed.
    /// </summary>
    Passed = 0,

    /// <summary>
    /// Testcase failed.
    /// </summary>
    Failed = 1
}

/// <summary>
/// Progress event categories emitted during a run.
/// </summary>
public enum TestRunProgressKind
{
    /// <summary>Full run started.</summary>
    TestBegin = 0,
    /// <summary>New test group started.</summary>
    TestGroupStart = 1,
    /// <summary>New testcase has been received.</summary>
    TestCaseStart = 2,
    /// <summary>Server returned testcase verdict.</summary>
    TestCaseResult = 3,
    /// <summary>Current group finished.</summary>
    TestGroupEnd = 4,
    /// <summary>Full run finished normally.</summary>
    TestEnd = 5,
    /// <summary>Run was stopped before completion.</summary>
    Stop = 6
}

/// <summary>
/// Snapshot describing one run progress event.
/// </summary>
public sealed class TestRunProgress
{
    /// <summary>Progress event kind.</summary>
    public TestRunProgressKind Kind { get; init; }
    /// <summary>Current group identifier.</summary>
    public string? GroupId { get; init; }
    /// <summary>Current group display name.</summary>
    public string? GroupDisplayName { get; init; }
    /// <summary>Total testcase count in current group.</summary>
    public int GroupTestCaseCount { get; init; }
    /// <summary>Passed count in current group so far.</summary>
    public int GroupPassedCount { get; init; }
    /// <summary>Failed count in current group so far.</summary>
    public int GroupFailedCount { get; init; }
    /// <summary>Current testcase identifier.</summary>
    public string? TestCaseId { get; init; }
    /// <summary>Current testcase textual status.</summary>
    public string? TestCaseStatus { get; init; }
    /// <summary>Machine-readable reason code for failures/stops.</summary>
    public string? ReasonCode { get; init; }
    /// <summary>Optional human-readable reason detail.</summary>
    public string? ReasonDetail { get; init; }
    /// <summary>Total passed count across all groups.</summary>
    public int TotalPassedCount { get; init; }
    /// <summary>Total failed count across all groups.</summary>
    public int TotalFailedCount { get; init; }
}

/// <summary>
/// Final run summary returned when run completes or stops.
/// </summary>
public sealed class TestRunSummary
{
    /// <summary>Session token assigned by server.</summary>
    public string SessionToken { get; internal set; } = string.Empty;
    /// <summary>UTC timestamp when run started.</summary>
    public DateTimeOffset StartedAtUtc { get; internal set; }
    /// <summary>UTC timestamp when run finished.</summary>
    public DateTimeOffset FinishedAtUtc { get; internal set; }
    /// <summary>Total number of groups processed.</summary>
    public int TestGroupCount { get; internal set; }
    /// <summary>Total number of testcases processed.</summary>
    public int TestCaseCount { get; internal set; }
    /// <summary>Total passed testcase count.</summary>
    public int PassedCount { get; internal set; }
    /// <summary>Total failed testcase count.</summary>
    public int FailedCount { get; internal set; }
    /// <summary>Indicates whether run reached normal <c>test-end</c>.</summary>
    public bool Completed { get; internal set; }
    /// <summary>Stop reason code when <see cref="Completed"/> is false.</summary>
    public string? StopReasonCode { get; internal set; }
    /// <summary>Stop reason detail when <see cref="Completed"/> is false.</summary>
    public string? StopReasonDetail { get; internal set; }
}

/// <summary>
/// Well-known reason codes used in stop and error payloads.
/// </summary>
public static class StopReasonCodes
{
    /// <summary>Client requested stop.</summary>
    public const string ClientStop = "client-stop";
    /// <summary>Server requested stop.</summary>
    public const string ServerStop = "server-stop";
    /// <summary>Client is not authorized.</summary>
    public const string Unauthorized = "unauthorized";
    /// <summary>Operation timed out.</summary>
    public const string Timeout = "timeout";
    /// <summary>Submitted answer is invalid.</summary>
    public const string InvalidAnswer = "invalid-answer";
    /// <summary>Too many invalid answers were submitted.</summary>
    public const string TooManyWrongAnswers = "too-many-wrong-answers";
    /// <summary>Unexpected internal server error.</summary>
    public const string InternalServerError = "internal-server-error";
}

internal sealed class ConnectedTestProgClient : ITestProgClient
{
    private readonly FramedTcpChannel _channel;
    private readonly string _sessionToken;
    private readonly TimeSpan _heartbeatTimeout;
    private bool _hasRun;
    private bool _disposed;

    private enum TestCaseSendOutcome
    {
        Sent = 0,
        ChannelUnavailable = 1
    }

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
        // If the server timed out while the solver was still running, a late send can fail.
        // In that case, try to drain the terminal server message instead of crashing the client.
        bool recoveringAfterSendFailure = false;

        while (true)
        {
            ProtocolEnvelope? envelope = recoveringAfterSendFailure
                ? await TryReceiveAfterSendFailureAsync(cancellationToken).ConfigureAwait(false)
                : await ReceiveWithHeartbeatTimeoutAsync(cancellationToken).ConfigureAwait(false);

            if (envelope is null)
            {
                FinalizeAbortedRunSummary(summary);
                EmitProgress(onProgress, new TestRunProgress
                {
                    Kind = TestRunProgressKind.Stop,
                    ReasonCode = summary.StopReasonCode,
                    ReasonDetail = summary.StopReasonDetail,
                    TotalPassedCount = summary.PassedCount,
                    TotalFailedCount = summary.FailedCount
                });
                return summary;
            }

            EnsureSessionToken(envelope);

            switch (envelope.Type)
            {
                case MessageTypes.TestBegin:
                    recoveringAfterSendFailure = false;
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
                    recoveringAfterSendFailure = false;
                    break;
                case MessageTypes.Ping:
                    recoveringAfterSendFailure = false;
                    await SendEnvelopeAsync(MessageTypes.Pong, null, cancellationToken).ConfigureAwait(false);
                    break;
                case MessageTypes.TestGroupStart:
                    summary.TestGroupCount += 1;
                    currentGroup = ProtocolSerializer.DeserializePayload<TestGroupStartPayload>(envelope);
                    currentGroupPassed = 0;
                    currentGroupFailed = 0;
                    recoveringAfterSendFailure = false;
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

                    recoveringAfterSendFailure = await SolveAndRespondAsync(envelope, solveAsync, cancellationToken)
                        .ConfigureAwait(false) == TestCaseSendOutcome.ChannelUnavailable;
                    break;
                case MessageTypes.TestCaseResult:
                    recoveringAfterSendFailure = false;
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
                    recoveringAfterSendFailure = false;
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
                    recoveringAfterSendFailure = false;
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
                    if (recoveringAfterSendFailure)
                    {
                        ApplyErrorAsStop(summary, envelope);
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
                    }

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

    private async Task<TestCaseSendOutcome> SolveAndRespondAsync(
        ProtocolEnvelope testcaseEnvelope,
        Func<TestInput, Task<object?>> solveAsync,
        CancellationToken cancellationToken)
    {
        TestCasePayload testcase = ProtocolSerializer.DeserializePayload<TestCasePayload>(testcaseEnvelope);
        if (string.IsNullOrWhiteSpace(testcase.TestCaseId))
        {
            throw new InvalidOperationException("Received testcase without testcaseId.");
        }

        TestCaseSolvedPayload solvedPayload;
        try
        {
            TestInput input = new(testcase.Input ?? new JObject());
            object? solved = await solveAsync(input).ConfigureAwait(false);
            solvedPayload = CreateSolvedPayload(testcase.TestCaseId, solved);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Treat local solver failures as a failed testcase answer so the session can continue.
            solvedPayload = CreateFallbackSolvedPayload(testcase.TestCaseId, ex.Message);
        }

        try
        {
            await SendEnvelopeAsync(MessageTypes.TestCaseSolved, solvedPayload, cancellationToken).ConfigureAwait(false);
            return TestCaseSendOutcome.Sent;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ObjectDisposedException)
        {
            return TestCaseSendOutcome.ChannelUnavailable;
        }
        catch (IOException)
        {
            return TestCaseSendOutcome.ChannelUnavailable;
        }
        catch (SocketException)
        {
            return TestCaseSendOutcome.ChannelUnavailable;
        }
    }

    private static TestCaseSolvedPayload CreateSolvedPayload(string testcaseId, object? solved)
    {
        TestOutput output = TestOutput.FromObject(solved);
        return new TestCaseSolvedPayload
        {
            TestCaseId = testcaseId,
            Output = output.Payload
        };
    }

    private static TestCaseSolvedPayload CreateFallbackSolvedPayload(string testcaseId, string? reason)
    {
        return CreateSolvedPayload(
            testcaseId,
            new
            {
                clientFailure = true,
                reason = string.IsNullOrWhiteSpace(reason) ? "Student solver failed." : reason
            });
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

    private void ApplyErrorAsStop(TestRunSummary summary, ProtocolEnvelope errorEnvelope)
    {
        try
        {
            ErrorPayload payload = ProtocolSerializer.DeserializePayload<ErrorPayload>(errorEnvelope);
            summary.StopReasonCode = string.IsNullOrWhiteSpace(payload.ReasonCode)
                ? StopReasonCodes.InternalServerError
                : payload.ReasonCode;
            summary.StopReasonDetail = payload.ReasonDetail ?? "Server reported an unspecified protocol error.";
        }
        catch (JsonException)
        {
            summary.StopReasonCode = StopReasonCodes.InternalServerError;
            summary.StopReasonDetail = "Server sent invalid error payload.";
        }
    }

    private void FinalizeAbortedRunSummary(TestRunSummary summary)
    {
        summary.FinishedAtUtc = DateTimeOffset.UtcNow;
        summary.StopReasonCode ??= StopReasonCodes.Timeout;
        summary.StopReasonDetail ??=
            "Connection closed while sending testcase result. The server likely stopped the run due to a testcase timeout.";
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

    private async Task<ProtocolEnvelope?> TryReceiveAfterSendFailureAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        heartbeatCts.CancelAfter(_heartbeatTimeout);

        try
        {
            return await _channel.ReceiveAsync(heartbeatCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
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
