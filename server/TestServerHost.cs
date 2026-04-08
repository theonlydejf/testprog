using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using testprog.messenger;

namespace testprog.server;

/// <summary>
/// Host runtime that serves test sessions over UDP discovery and TCP protocol.
/// </summary>
public sealed class TestServerHost : IAsyncDisposable
{
    private readonly TestServerOptions _options;
    private readonly TestSuiteDefinition _suite;
    private readonly Action<TestServerRuntimeEvent>? _onRuntimeEvent;
    private readonly Dictionary<TestGroupDefinition, Func<Random, int, JObject>> _randomInputGenerators = new();
    private readonly SemaphoreSlim _sessionSlots;
    private readonly List<Task> _sessionTasks = new();
    private readonly object _sessionTasksLock = new();

    private UdpClient? _discoveryClient;
    private TcpListener? _tcpListener;
    private bool _disposed;

    /// <summary>
    /// Creates a new server host instance.
    /// </summary>
    /// <param name="options">Server runtime options.</param>
    /// <param name="suite">Test suite definition served to clients.</param>
    /// <param name="onRuntimeEvent">
    /// Optional callback invoked for session lifecycle and testcase evaluation events.
    /// </param>
    public TestServerHost(
        TestServerOptions options,
        TestSuiteDefinition suite,
        Action<TestServerRuntimeEvent>? onRuntimeEvent = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _suite = suite ?? throw new ArgumentNullException(nameof(suite));
        _onRuntimeEvent = onRuntimeEvent;

        _options.Validate();
        _suite.Validate();
        InitializeEvaluators();
        _sessionSlots = new SemaphoreSlim(_options.MaxConcurrentSessions, _options.MaxConcurrentSessions);
    }

    private void EmitRuntimeEvent(TestServerRuntimeEvent runtimeEvent)
    {
        if (_onRuntimeEvent is null)
        {
            return;
        }

        try
        {
            _onRuntimeEvent(runtimeEvent);
        }
        catch
        {
        }
    }

    private void InitializeEvaluators()
    {
        foreach (TestGroupDefinition group in _suite.Groups)
        {
            if (group.Randomized is not null)
            {
                _ = GoldenStandardCompiler.GetOrCreateEvaluator(group.Randomized.GoldenStandard!);
                _randomInputGenerators[group] = ResolveRandomInputGenerator(group.Randomized);
                continue;
            }

            foreach (TestCaseDefinition testcase in group.TestCases)
            {
                if (testcase.GoldenStandard is null)
                {
                    continue;
                }

                _ = GoldenStandardCompiler.GetOrCreateEvaluator(testcase.GoldenStandard);
            }
        }
    }

    /// <summary>
    /// Starts discovery and TCP loops and runs until cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token used to stop the host.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        IPAddress multicastAddress = TestServerOptions.ParseMulticastAddress(_options.DiscoveryMulticastAddress);
        _discoveryClient = CreateDiscoveryClient(multicastAddress);

        _tcpListener = new TcpListener(IPAddress.Any, _options.TcpPort);
        _tcpListener.Start();

        Task discoveryTask = RunDiscoveryLoopAsync(_discoveryClient, cancellationToken);
        Task acceptTask = RunTcpAcceptLoopAsync(_tcpListener, cancellationToken);

        try
        {
            await Task.WhenAll(discoveryTask, acceptTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await DisposeNetworkResourcesAsync().ConfigureAwait(false);
            await AwaitActiveSessionsAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes host resources and waits for active sessions to complete.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DisposeNetworkResourcesAsync().ConfigureAwait(false);
        await AwaitActiveSessionsAsync().ConfigureAwait(false);
        _sessionSlots.Dispose();
    }

    private UdpClient CreateDiscoveryClient(IPAddress multicastAddress)
    {
        UdpClient client = new();
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        client.Client.Bind(new IPEndPoint(IPAddress.Any, _options.DiscoveryPort));
        client.JoinMulticastGroup(multicastAddress);
        return client;
    }

    private async Task RunDiscoveryLoopAsync(UdpClient client, CancellationToken cancellationToken)
    {
        string advertiseHost = _options.ResolveAdvertiseHost();

        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult incoming;
            try
            {
                incoming = await ReceiveUdpAsync(client, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            string text = Encoding.UTF8.GetString(incoming.Buffer);
            ProtocolEnvelope envelope;
            try
            {
                envelope = ProtocolSerializer.Deserialize(text);
            }
            catch (JsonException)
            {
                continue;
            }

            if (!string.Equals(envelope.Type, MessageTypes.ServerWanted, StringComparison.Ordinal))
            {
                continue;
            }

            ProtocolEnvelope response = new()
            {
                Type = MessageTypes.ServerAvailable,
                Payload = ProtocolSerializer.ToPayloadObject(new ServerAvailablePayload
                {
                    ServerId = _options.ServerId,
                    ServerHost = advertiseHost,
                    ServerPort = _options.TcpPort
                })
            };

            string responseText = ProtocolSerializer.Serialize(response);
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
            try
            {
                await client.SendAsync(responseBytes, responseBytes.Length, incoming.RemoteEndPoint).ConfigureAwait(false);
            }
            catch (SocketException)
            {
            }
        }
    }

    private async Task RunTcpAcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient tcpClient;
            try
            {
                tcpClient = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            try
            {
                await _sessionSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                tcpClient.Dispose();
                break;
            }

            string remoteEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "<unknown>";
            Task handler = RunClientSessionSafeAsync(tcpClient, remoteEndpoint, cancellationToken);

            lock (_sessionTasksLock)
            {
                _sessionTasks.Add(handler);
            }

            _ = handler.ContinueWith(
                task =>
                {
                    _ = task.Exception;
                    lock (_sessionTasksLock)
                    {
                        _sessionTasks.Remove(task);
                    }

                    _sessionSlots.Release();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task RunClientSessionSafeAsync(
        TcpClient client,
        string remoteEndpoint,
        CancellationToken cancellationToken)
    {
        EmitRuntimeEvent(new TestServerRuntimeEvent
        {
            Kind = TestServerRuntimeEventKind.ClientConnected,
            RemoteEndpoint = remoteEndpoint
        });

        using (client)
        await using (FramedTcpChannel channel = FramedTcpChannel.FromAcceptedClient(client))
        {
            try
            {
                await ExecuteClientSessionAsync(channel, remoteEndpoint, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch
            {
            }
        }
    }

    private async Task ExecuteClientSessionAsync(
        FramedTcpChannel channel,
        string remoteEndpoint,
        CancellationToken cancellationToken)
    {
        string? sessionToken = null;
        string? studentId = null;
        string? displayName = null;
        try
        {
            ProtocolEnvelope helloEnvelope = await ReceiveWithTimeoutAsync(channel, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(helloEnvelope.Type, MessageTypes.ClientHello, StringComparison.Ordinal))
            {
                EmitRuntimeEvent(new TestServerRuntimeEvent
                {
                    Kind = TestServerRuntimeEventKind.SessionRejected,
                    RemoteEndpoint = remoteEndpoint,
                    ReasonCode = StopReasonCodes.InvalidAnswer,
                    ReasonDetail = $"Expected '{MessageTypes.ClientHello}' as first message."
                });

                await TrySendErrorAsync(
                    channel,
                    null,
                    StopReasonCodes.InvalidAnswer,
                    $"Expected '{MessageTypes.ClientHello}' as first message.").ConfigureAwait(false);
                return;
            }

            ClientHelloPayload hello = ProtocolSerializer.DeserializePayload<ClientHelloPayload>(helloEnvelope);
            if (string.IsNullOrWhiteSpace(hello.StudentId) || string.IsNullOrWhiteSpace(hello.DisplayName))
            {
                EmitRuntimeEvent(new TestServerRuntimeEvent
                {
                    Kind = TestServerRuntimeEventKind.SessionRejected,
                    RemoteEndpoint = remoteEndpoint,
                    ReasonCode = StopReasonCodes.InvalidAnswer,
                    ReasonDetail = "Client hello is missing student identity."
                });

                await TrySendErrorAsync(
                    channel,
                    null,
                    StopReasonCodes.InvalidAnswer,
                    "Client hello is missing student identity.").ConfigureAwait(false);
                return;
            }

            studentId = hello.StudentId;
            displayName = hello.DisplayName;

            if (!_options.IsStudentAllowed(studentId))
            {
                EmitRuntimeEvent(new TestServerRuntimeEvent
                {
                    Kind = TestServerRuntimeEventKind.SessionRejected,
                    RemoteEndpoint = remoteEndpoint,
                    StudentId = studentId,
                    DisplayName = displayName,
                    ReasonCode = StopReasonCodes.Unauthorized,
                    ReasonDetail = $"Student '{studentId}' is not in whitelist."
                });

                await TrySendErrorAsync(
                    channel,
                    null,
                    StopReasonCodes.Unauthorized,
                    $"Student '{studentId}' is not allowed on this server.").ConfigureAwait(false);
                return;
            }

            sessionToken = Guid.NewGuid().ToString("N");
            int heartbeatSeconds = (int)Math.Ceiling(_options.ClientResponseTimeout.TotalSeconds);

            EmitRuntimeEvent(new TestServerRuntimeEvent
            {
                Kind = TestServerRuntimeEventKind.SessionStarted,
                RemoteEndpoint = remoteEndpoint,
                SessionToken = sessionToken,
                StudentId = studentId,
                DisplayName = displayName
            });

            await SendEnvelopeAsync(
                channel,
                MessageTypes.ServerHello,
                new ServerHelloPayload
                {
                    SessionToken = sessionToken,
                    HeartbeatSeconds = heartbeatSeconds
                },
                null,
                cancellationToken).ConfigureAwait(false);

            await SendEnvelopeAsync(
                channel,
                MessageTypes.TestBegin,
                new
                {
                    studentId = hello.StudentId,
                    displayName = hello.DisplayName
                },
                sessionToken,
                cancellationToken).ConfigureAwait(false);

            int total = 0;
            int passed = 0;
            int failed = 0;
            int groupCount = 0;

            foreach (TestGroupDefinition group in _suite.Groups)
            {
                IReadOnlyList<TestCaseDefinition> runtimeTestCases = MaterializeGroupTestCases(group);
                groupCount++;

                EmitRuntimeEvent(new TestServerRuntimeEvent
                {
                    Kind = TestServerRuntimeEventKind.TestGroupStarted,
                    RemoteEndpoint = remoteEndpoint,
                    SessionToken = sessionToken,
                    StudentId = studentId,
                    DisplayName = displayName,
                    GroupId = group.GroupId,
                    GroupDisplayName = group.DisplayName,
                    PassedCount = passed,
                    FailedCount = failed
                });

                await SendEnvelopeAsync(
                    channel,
                    MessageTypes.TestGroupStart,
                    new
                    {
                        groupId = group.GroupId,
                        displayName = group.DisplayName,
                        testcaseCount = runtimeTestCases.Count
                    },
                    sessionToken,
                    cancellationToken).ConfigureAwait(false);

                foreach (TestCaseDefinition testcase in runtimeTestCases)
                {
                    total += 1;

                    await SendEnvelopeAsync(
                        channel,
                        MessageTypes.TestCase,
                        new TestCasePayload
                        {
                            TestCaseId = testcase.TestCaseId,
                            Input = (JObject)testcase.Input.DeepClone()
                        },
                        sessionToken,
                        cancellationToken).ConfigureAwait(false);

                    ProtocolEnvelope? solvedEnvelope = await ReceiveClientResponseAsync(
                        channel,
                        sessionToken,
                        testcase,
                        cancellationToken).ConfigureAwait(false);

                    if (solvedEnvelope is null)
                    {
                        EmitRuntimeEvent(new TestServerRuntimeEvent
                        {
                            Kind = TestServerRuntimeEventKind.SessionStopped,
                            RemoteEndpoint = remoteEndpoint,
                            SessionToken = sessionToken,
                            StudentId = studentId,
                            DisplayName = displayName,
                            GroupId = group.GroupId,
                            GroupDisplayName = group.DisplayName,
                            PassedCount = passed,
                            FailedCount = failed,
                            ReasonCode = StopReasonCodes.ClientStop,
                            ReasonDetail = "Client requested stop."
                        });

                        return;
                    }

                    TestCaseSolvedPayload solved = ProtocolSerializer.DeserializePayload<TestCaseSolvedPayload>(solvedEnvelope);
                    TestCaseResultPayload result = EvaluateTestCase(testcase, solved);

                    if (string.Equals(result.Status, TestCaseResultStatuses.Passed, StringComparison.Ordinal))
                    {
                        passed += 1;
                    }
                    else
                    {
                        failed += 1;
                    }

                    await SendEnvelopeAsync(
                        channel,
                        MessageTypes.TestCaseResult,
                        result,
                        sessionToken,
                        cancellationToken).ConfigureAwait(false);

                    EmitRuntimeEvent(new TestServerRuntimeEvent
                    {
                        Kind = TestServerRuntimeEventKind.TestCaseEvaluated,
                        RemoteEndpoint = remoteEndpoint,
                        SessionToken = sessionToken,
                        StudentId = studentId,
                        DisplayName = displayName,
                        GroupId = group.GroupId,
                        GroupDisplayName = group.DisplayName,
                        TestCaseId = testcase.TestCaseId,
                        TestCaseInputJson = testcase.Input.ToString(Formatting.None),
                        TestCaseAnswerJson = solved.Output?.ToString(Formatting.None),
                        TestCaseStatus = result.Status,
                        PassedCount = passed,
                        FailedCount = failed,
                        ReasonCode = result.ReasonCode,
                        ReasonDetail = result.ReasonDetail
                    });
                }

                await SendEnvelopeAsync(
                    channel,
                    MessageTypes.TestGroupEnd,
                    new
                    {
                        groupId = group.GroupId
                    },
                    sessionToken,
                    cancellationToken).ConfigureAwait(false);

                EmitRuntimeEvent(new TestServerRuntimeEvent
                {
                    Kind = TestServerRuntimeEventKind.TestGroupEnded,
                    RemoteEndpoint = remoteEndpoint,
                    SessionToken = sessionToken,
                    StudentId = studentId,
                    DisplayName = displayName,
                    GroupId = group.GroupId,
                    GroupDisplayName = group.DisplayName,
                    PassedCount = passed,
                    FailedCount = failed
                });
            }

            await SendEnvelopeAsync(
                channel,
                MessageTypes.TestEnd,
                new
                {
                    testGroupCount = groupCount,
                    testcaseCount = total,
                    passedCount = passed,
                    failedCount = failed
                },
                sessionToken,
                cancellationToken).ConfigureAwait(false);

            EmitRuntimeEvent(new TestServerRuntimeEvent
            {
                Kind = TestServerRuntimeEventKind.SessionCompleted,
                RemoteEndpoint = remoteEndpoint,
                SessionToken = sessionToken,
                StudentId = studentId,
                DisplayName = displayName,
                PassedCount = passed,
                FailedCount = failed
            });
        }
        catch (TimeoutException ex)
        {
            await TrySendStopAsync(channel, sessionToken, StopReasonCodes.Timeout, ex.Message).ConfigureAwait(false);
            EmitRuntimeEvent(new TestServerRuntimeEvent
            {
                Kind = TestServerRuntimeEventKind.SessionStopped,
                RemoteEndpoint = remoteEndpoint,
                SessionToken = sessionToken,
                StudentId = studentId,
                DisplayName = displayName,
                ReasonCode = StopReasonCodes.Timeout,
                ReasonDetail = ex.Message
            });
        }
        catch (JsonException ex)
        {
            await TrySendErrorAsync(channel, sessionToken, StopReasonCodes.InvalidAnswer, ex.Message).ConfigureAwait(false);
            EmitRuntimeEvent(new TestServerRuntimeEvent
            {
                Kind = TestServerRuntimeEventKind.SessionFaulted,
                RemoteEndpoint = remoteEndpoint,
                SessionToken = sessionToken,
                StudentId = studentId,
                DisplayName = displayName,
                ReasonCode = StopReasonCodes.InvalidAnswer,
                ReasonDetail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            await TrySendErrorAsync(channel, sessionToken, StopReasonCodes.InvalidAnswer, ex.Message).ConfigureAwait(false);
            EmitRuntimeEvent(new TestServerRuntimeEvent
            {
                Kind = TestServerRuntimeEventKind.SessionFaulted,
                RemoteEndpoint = remoteEndpoint,
                SessionToken = sessionToken,
                StudentId = studentId,
                DisplayName = displayName,
                ReasonCode = StopReasonCodes.InvalidAnswer,
                ReasonDetail = ex.Message
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await TrySendErrorAsync(channel, sessionToken, StopReasonCodes.InternalServerError, ex.Message).ConfigureAwait(false);
            EmitRuntimeEvent(new TestServerRuntimeEvent
            {
                Kind = TestServerRuntimeEventKind.SessionFaulted,
                RemoteEndpoint = remoteEndpoint,
                SessionToken = sessionToken,
                StudentId = studentId,
                DisplayName = displayName,
                ReasonCode = StopReasonCodes.InternalServerError,
                ReasonDetail = ex.Message
            });
        }
    }

    private async Task<ProtocolEnvelope?> ReceiveClientResponseAsync(
        FramedTcpChannel channel,
        string sessionToken,
        TestCaseDefinition testcase,
        CancellationToken cancellationToken)
    {
        TimeSpan responseTimeout = GetResponseTimeout(testcase);
        using CancellationTokenSource testcaseTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        testcaseTimeoutCts.CancelAfter(responseTimeout);

        while (true)
        {
            ProtocolEnvelope envelope = await ReceiveUntilDeadlineAsync(
                channel,
                cancellationToken,
                testcaseTimeoutCts.Token,
                responseTimeout,
                testcase.TestCaseId).ConfigureAwait(false);
            EnsureSessionToken(sessionToken, envelope);

            switch (envelope.Type)
            {
                case MessageTypes.TestCaseSolved:
                    return envelope;
                case MessageTypes.Pong:
                    continue;
                case MessageTypes.Stop:
                    return null;
                case MessageTypes.Ping:
                    await SendEnvelopeAsync(channel, MessageTypes.Pong, null, sessionToken, cancellationToken).ConfigureAwait(false);
                    continue;
                default:
                    throw new InvalidOperationException($"Unexpected client message '{envelope.Type}'.");
            }
        }
    }

    private TimeSpan GetResponseTimeout(TestCaseDefinition testcase)
    {
        return testcase.ResponseTimeout ?? _options.ClientResponseTimeout;
    }

    private async Task<ProtocolEnvelope> ReceiveWithTimeoutAsync(
        FramedTcpChannel channel,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.ClientResponseTimeout);

        try
        {
            return await channel.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No client message arrived in {_options.ClientResponseTimeout.TotalSeconds:0.##}s.");
        }
    }

    private static async Task<ProtocolEnvelope> ReceiveUntilDeadlineAsync(
        FramedTcpChannel channel,
        CancellationToken cancellationToken,
        CancellationToken deadlineToken,
        TimeSpan timeout,
        string testcaseId)
    {
        try
        {
            return await channel.ReceiveAsync(deadlineToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            deadlineToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Client did not finish testcase '{testcaseId}' within {timeout.TotalSeconds:0.##}s.");
        }
    }

    private async Task SendEnvelopeAsync(
        FramedTcpChannel channel,
        string type,
        object? payload,
        string? sessionToken,
        CancellationToken cancellationToken)
    {
        ProtocolEnvelope envelope = new()
        {
            Type = type,
            SessionToken = sessionToken,
            Payload = ProtocolSerializer.ToPayloadObject(payload)
        };

        await channel.SendAsync(envelope, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureSessionToken(string expectedToken, ProtocolEnvelope envelope)
    {
        if (!string.Equals(envelope.SessionToken, expectedToken, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Client message has invalid sessionToken.");
        }
    }

    private TestCaseResultPayload EvaluateTestCase(TestCaseDefinition testcase, TestCaseSolvedPayload solved)
    {
        JToken expectedOutput = ResolveExpectedOutput(testcase);
        JToken actualOutput = solved.Output is null
            ? JValue.CreateNull()
            : AlignActualOutputForComparison(expectedOutput, solved.Output);

        if (!string.Equals(solved.TestCaseId, testcase.TestCaseId, StringComparison.Ordinal))
        {
            return new TestCaseResultPayload
            {
                TestCaseId = testcase.TestCaseId,
                Status = TestCaseResultStatuses.Failed,
                ReasonCode = StopReasonCodes.InvalidAnswer,
                ReasonDetail = $"Expected testcase id '{testcase.TestCaseId}', received '{solved.TestCaseId}'."
            };
        }

        bool passed = testcase.ComparisonMode switch
        {
            TestCaseComparisonMode.StrictJson => JToken.DeepEquals(expectedOutput, actualOutput),
            TestCaseComparisonMode.NormalizedText => string.Equals(
                NormalizeToken(expectedOutput),
                NormalizeToken(actualOutput),
                StringComparison.Ordinal),
            _ => throw new InvalidOperationException($"Unsupported comparison mode '{testcase.ComparisonMode}'.")
        };

        if (passed)
        {
            return new TestCaseResultPayload
            {
                TestCaseId = testcase.TestCaseId,
                Status = TestCaseResultStatuses.Passed
            };
        }

        return new TestCaseResultPayload
        {
            TestCaseId = testcase.TestCaseId,
            Status = TestCaseResultStatuses.Failed,
            ReasonCode = StopReasonCodes.InvalidAnswer,
            ReasonDetail = "Output does not match expected value."
        };
    }

    private static JToken AlignActualOutputForComparison(JToken expectedOutput, JToken actualOutput)
    {
        if (expectedOutput is JObject)
        {
            return actualOutput.DeepClone();
        }

        return TryUnwrapScalarEnvelope(actualOutput);
    }

    private static JToken TryUnwrapScalarEnvelope(JToken token)
    {
        // Client protocol payloads are always JSON objects. When solve returns a scalar
        // value, it is serialized as: { "value": <scalar> }.
        if (token is JObject obj &&
            obj.Count == 1 &&
            obj.TryGetValue("value", StringComparison.Ordinal, out JToken? wrappedValue))
        {
            return wrappedValue.DeepClone();
        }

        return token.DeepClone();
    }

    private JToken ResolveExpectedOutput(TestCaseDefinition testcase)
    {
        if (testcase.GoldenStandard is not null)
        {
            Func<JObject, JToken> evaluator = GoldenStandardCompiler.GetOrCreateEvaluator(testcase.GoldenStandard);
            return evaluator((JObject)testcase.Input.DeepClone());
        }

        return testcase.ExpectedOutput?.DeepClone() ?? JValue.CreateNull();
    }

    private IReadOnlyList<TestCaseDefinition> MaterializeGroupTestCases(TestGroupDefinition group)
    {
        if (group.Randomized is null)
        {
            return group.TestCases;
        }

        if (!_randomInputGenerators.TryGetValue(group, out Func<Random, int, JObject>? generator))
        {
            throw new InvalidOperationException(
                $"Random input generator is not initialized for group '{group.GroupId}'.");
        }

        RandomTestGroupDefinition randomized = group.Randomized;
        int seed = randomized.Seed ?? Random.Shared.Next(int.MinValue, int.MaxValue);
        Random random = new(seed);

        List<TestCaseDefinition> materialized = new(randomized.Count);
        for (int index = 0; index < randomized.Count; index++)
        {
            JObject input = generator(random, index);

            materialized.Add(new TestCaseDefinition
            {
                TestCaseId = $"{randomized.TestCaseIdPrefix}{index + 1}",
                Input = (JObject)input.DeepClone(),
                ExpectedOutput = null,
                GoldenStandard = randomized.GoldenStandard,
                ComparisonMode = randomized.ComparisonMode,
                ResponseTimeout = randomized.ResponseTimeout
            });
        }

        return materialized;
    }

    private static Func<Random, int, JObject> ResolveRandomInputGenerator(RandomTestGroupDefinition randomized)
    {
        switch (randomized.InputGenerator!.Mode)
        {
            case RandomInputGeneratorMode.Default:
                DefaultRandomInputGeneratorDefinition defaultGenerator = randomized.InputGenerator.Default!;
                return (random, _) => GenerateDefaultRandomInput(defaultGenerator, random);

            case RandomInputGeneratorMode.SourceFile:
                SourceFileRandomInputGeneratorDefinition sourceGenerator = randomized.InputGenerator.SourceFile!;
                return RandomInputGeneratorCompiler.GetOrCreateGenerator(sourceGenerator);

            default:
                throw new InvalidOperationException(
                    $"Unsupported random input generator mode '{randomized.InputGenerator.Mode}'.");
        }
    }

    private static JObject GenerateDefaultRandomInput(
        DefaultRandomInputGeneratorDefinition definition,
        Random random)
    {
        JObject input = new();
        foreach (RandomIntFieldDefinition field in definition.IntFields)
        {
            int value = NextIntInclusive(random, field.MinValue, field.MaxValue);
            input[field.Name] = value;
        }

        return input;
    }

    private static int NextIntInclusive(Random random, int minValue, int maxValue)
    {
        if (minValue == maxValue)
        {
            return minValue;
        }

        long range = (long)maxValue - minValue + 1L;
        long offset = random.NextInt64(range);
        return (int)(minValue + offset);
    }

    private static string NormalizeToken(JToken token)
    {
        JToken normalized = NormalizeTokenNode(token);
        return normalized.ToString(Formatting.None).Trim();
    }

    private static JToken NormalizeTokenNode(JToken token)
    {
        if (token.Type == JTokenType.String)
        {
            return new JValue((token.Value<string>() ?? string.Empty).Trim());
        }

        if (token is JObject obj)
        {
            JObject normalizedObject = new();
            foreach (JProperty property in obj.Properties())
            {
                normalizedObject[property.Name] = NormalizeTokenNode(property.Value);
            }

            return normalizedObject;
        }

        if (token is JArray array)
        {
            JArray normalizedArray = new();
            foreach (JToken item in array)
            {
                normalizedArray.Add(NormalizeTokenNode(item));
            }

            return normalizedArray;
        }

        return token.DeepClone();
    }

    private async Task TrySendStopAsync(
        FramedTcpChannel channel,
        string? sessionToken,
        string reasonCode,
        string reasonDetail)
    {
        try
        {
            await SendEnvelopeAsync(
                channel,
                MessageTypes.Stop,
                new StopPayload
                {
                    ReasonCode = reasonCode,
                    ReasonDetail = reasonDetail
                },
                sessionToken,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task TrySendErrorAsync(
        FramedTcpChannel channel,
        string? sessionToken,
        string reasonCode,
        string reasonDetail)
    {
        try
        {
            await SendEnvelopeAsync(
                channel,
                MessageTypes.Error,
                new ErrorPayload
                {
                    ReasonCode = reasonCode,
                    ReasonDetail = reasonDetail
                },
                sessionToken,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static async Task<UdpReceiveResult> ReceiveUdpAsync(UdpClient client, CancellationToken cancellationToken)
    {
        Task<UdpReceiveResult> receiveTask = client.ReceiveAsync();
        Task waitTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        Task completed = await Task.WhenAny(receiveTask, waitTask).ConfigureAwait(false);

        if (completed == receiveTask)
        {
            return await receiveTask.ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new OperationCanceledException(cancellationToken);
    }

    private async Task DisposeNetworkResourcesAsync()
    {
        UdpClient? discovery = Interlocked.Exchange(ref _discoveryClient, null);
        if (discovery is not null)
        {
            try
            {
                discovery.DropMulticastGroup(TestServerOptions.ParseMulticastAddress(_options.DiscoveryMulticastAddress));
            }
            catch
            {
            }
            finally
            {
                discovery.Dispose();
            }
        }

        TcpListener? listener = Interlocked.Exchange(ref _tcpListener, null);
        listener?.Stop();

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task AwaitActiveSessionsAsync()
    {
        Task[] snapshot;
        lock (_sessionTasksLock)
        {
            snapshot = _sessionTasks.ToArray();
        }

        if (snapshot.Length == 0)
        {
            return;
        }

        await Task.WhenAll(snapshot).ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TestServerHost));
        }
    }
}
