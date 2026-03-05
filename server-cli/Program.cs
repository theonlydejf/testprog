using System.Text;
using testprog.server;

namespace testprog.server.cli;

internal static class Program
{
    private static readonly object UiLock = new();

    public static int Main(string[] args)
    {
        try
        {
            return RunAsync(args).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        ServerCliArguments cliArguments = ServerCliArguments.Parse(args);
        if (!cliArguments.IsValid)
        {
            Console.Error.WriteLine(cliArguments.ErrorMessage);
            PrintUsage();
            return 1;
        }

        LoadedServerConfiguration loaded = TestServerConfigurationLoader.LoadFromFile(cliArguments.ConfigPath!);

        string logFilePath = ResolveLogPath(cliArguments.LogFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

        using ServerFileLogger logger = new(logFilePath);
        StudentStateTracker tracker = new(loaded.Suite.Groups.Count);
        DashboardRenderer renderer = new(
            loaded.ServerOptions,
            loaded.Suite,
            logFilePath,
            cliArguments.ConfigPath!);

        using CancellationTokenSource shutdown = new();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        void OnRuntimeEvent(TestServerRuntimeEvent runtimeEvent)
        {
            tracker.Apply(runtimeEvent);
            logger.LogEvent(runtimeEvent);

            lock (UiLock)
            {
                renderer.Render(tracker.Snapshot(), shutdown.IsCancellationRequested);
            }
        }

        lock (UiLock)
        {
            renderer.Render(tracker.Snapshot(), shutdown.IsCancellationRequested);
        }

        logger.LogInfo($"Server CLI started with config '{cliArguments.ConfigPath}'.");
        logger.LogInfo($"TCP:{loaded.ServerOptions.TcpPort} Discovery:{loaded.ServerOptions.DiscoveryPort}");

        Task uiRefreshTask = StartUiRefreshLoopAsync(renderer, tracker, shutdown.Token);

        await using TestServerHost host = new(loaded.ServerOptions, loaded.Suite, OnRuntimeEvent);

        try
        {
            await host.RunAsync(shutdown.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
        }
        finally
        {
            shutdown.Cancel();
            try
            {
                await uiRefreshTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            lock (UiLock)
            {
                renderer.Render(tracker.Snapshot(), isStopping: true);
                Console.WriteLine();
                Console.WriteLine("Server stopped.");
                Console.WriteLine($"Log file: {logFilePath}");
            }

            logger.LogInfo("Server CLI stopped.");
        }

        return 0;
    }

    private static async Task StartUiRefreshLoopAsync(
        DashboardRenderer renderer,
        StudentStateTracker tracker,
        CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            lock (UiLock)
            {
                renderer.Render(tracker.Snapshot(), cancellationToken.IsCancellationRequested);
            }
        }
    }

    private static string ResolveLogPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string fileName = $"server-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log";
        return Path.Combine(Environment.CurrentDirectory, "logs", fileName);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  server-cli --config <path-to-server-config.json> [--log-file <path>]");
    }
}

internal sealed class ServerCliArguments
{
    public bool IsValid { get; private init; }
    public string? ConfigPath { get; private init; }
    public string? LogFilePath { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static ServerCliArguments Parse(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return Invalid("Missing required argument '--config'.");
        }

        string? configPath = null;
        string? logFilePath = null;

        for (int index = 0; index < args.Length; index++)
        {
            string current = args[index];
            switch (current)
            {
                case "--config":
                    if (index + 1 >= args.Length)
                    {
                        return Invalid("Argument '--config' requires a value.");
                    }

                    configPath = args[++index];
                    break;

                case "--log-file":
                    if (index + 1 >= args.Length)
                    {
                        return Invalid("Argument '--log-file' requires a value.");
                    }

                    logFilePath = args[++index];
                    break;

                case "--help":
                case "-h":
                    return Invalid("Help requested.");

                default:
                    return Invalid($"Unknown argument '{current}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(configPath))
        {
            return Invalid("Missing required argument '--config'.");
        }

        return new ServerCliArguments
        {
            IsValid = true,
            ConfigPath = Path.GetFullPath(configPath),
            LogFilePath = logFilePath
        };
    }

    private static ServerCliArguments Invalid(string message)
    {
        return new ServerCliArguments
        {
            IsValid = false,
            ErrorMessage = message
        };
    }
}

internal sealed class ServerFileLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public ServerFileLogger(string filePath)
    {
        _writer = new StreamWriter(
            File.Open(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public void LogInfo(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        WriteLine($"[{DateTimeOffset.UtcNow:O}] [info] {message}");
    }

    public void LogEvent(TestServerRuntimeEvent runtimeEvent)
    {
        string line =
            $"[{runtimeEvent.OccurredAtUtc:O}] " +
            $"[{runtimeEvent.Kind}] " +
            $"studentId={runtimeEvent.StudentId ?? "-"} " +
            $"displayName={runtimeEvent.DisplayName ?? "-"} " +
            $"session={runtimeEvent.SessionToken ?? "-"} " +
            $"remote={runtimeEvent.RemoteEndpoint ?? "-"} " +
            $"group={runtimeEvent.GroupId ?? "-"} " +
            $"testcase={runtimeEvent.TestCaseId ?? "-"} " +
            $"status={runtimeEvent.TestCaseStatus ?? "-"} " +
            $"passed={runtimeEvent.PassedCount} failed={runtimeEvent.FailedCount} " +
            $"reasonCode={runtimeEvent.ReasonCode ?? "-"} " +
            $"reasonDetail={runtimeEvent.ReasonDetail ?? "-"} " +
            $"inputJson=\"{EscapeQuotedValue(runtimeEvent.TestCaseInputJson)}\" " +
            $"answerJson=\"{EscapeQuotedValue(runtimeEvent.TestCaseAnswerJson)}\"";

        WriteLine(line);
    }

    private static string EscapeQuotedValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "-";
        }

        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private void WriteLine(string line)
    {
        lock (_lock)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer.Dispose();
        }
    }
}

internal sealed class DashboardRenderer
{
    private readonly TestServerOptions _options;
    private readonly TestSuiteDefinition _suite;
    private readonly string _logFilePath;
    private readonly string _configPath;
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;

    public DashboardRenderer(
        TestServerOptions options,
        TestSuiteDefinition suite,
        string logFilePath,
        string configPath)
    {
        _options = options;
        _suite = suite;
        _logFilePath = logFilePath;
        _configPath = configPath;
    }

    public void Render(StudentDashboardSnapshot snapshot, bool isStopping)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Clear();

        WriteTitle("TestProg Server CLI");
        Console.WriteLine($"Started: {_startedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Now:     {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Config:  {_configPath}");
        Console.WriteLine($"Log:     {_logFilePath}");
        Console.WriteLine($"Server:  {_options.ServerId}  TCP:{_options.TcpPort}  Discovery:{_options.DiscoveryMulticastAddress}:{_options.DiscoveryPort}");
        Console.WriteLine($"Suite:   {_suite.Groups.Count} groups");
        Console.WriteLine($"State:   {(isStopping ? "Stopping" : "Running")}  Sessions:{snapshot.Students.Count}");
        Console.WriteLine();

        if (_options.StudentIdWhitelist.Count == 0)
        {
            WriteInfo("Whitelist: disabled (all students allowed)");
        }
        else
        {
            WriteInfo($"Whitelist: {string.Join(", ", _options.StudentIdWhitelist)}");
        }

        Console.WriteLine();
        WriteHeaderRow();
        Console.WriteLine(new string('-', 120));

        foreach (StudentState student in snapshot.Students)
        {
            WriteStudentRow(student);
        }

        if (snapshot.Students.Count == 0)
        {
            WriteInfo("No active student sessions yet.");
        }

        Console.WriteLine();
        WriteInfo("Press Ctrl+C to stop server.");
    }

    private static void WriteTitle(string text)
    {
        ConsoleColor previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }

    private static void WriteInfo(string text)
    {
        ConsoleColor previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }

    private static void WriteHeaderRow()
    {
        Console.WriteLine(
            $"{Pad("StudentId", 16)} " +
            $"{Pad("Name", 22)} " +
            $"{Pad("Status", 20)} " +
            $"{Pad("Group", 18)} " +
            $"{Pad("Groups C/T", 11)} " +
            $"{Pad("Last Reason", 26)} " +
            $"{Pad("Updated", 14)}");
    }

    private static void WriteStudentRow(StudentState student)
    {
        ConsoleColor previous = Console.ForegroundColor;
        Console.ForegroundColor = RowColor(student);

        Console.WriteLine(
            $"{Pad(student.StudentId, 16)} " +
            $"{Pad(student.DisplayName, 22)} " +
            $"{Pad(student.Status, 20)} " +
            $"{Pad(student.CurrentGroup, 18)} " +
            $"{Pad($"{student.CompletedGroupCount}/{student.TotalGroupCount}", 11)} " +
            $"{Pad(student.LastReason, 26)} " +
            $"{Pad(student.LastUpdatedUtc.ToLocalTime().ToString("HH:mm:ss"), 14)}");

        Console.ForegroundColor = previous;
    }

    private static ConsoleColor RowColor(StudentState student)
    {
        if (student.ProcessedGroupCount > 0 &&
            (student.CompletedGroupCount * 2) < student.ProcessedGroupCount)
        {
            return ConsoleColor.Red;
        }

        return StatusColor(student.Status);
    }

    private static ConsoleColor StatusColor(string status)
    {
        return status switch
        {
            "Running" => ConsoleColor.Green,
            "Completed" => ConsoleColor.Green,
            "Completed (partial)" => ConsoleColor.Yellow,
            "Rejected" => ConsoleColor.Yellow,
            "Stopped" => ConsoleColor.Yellow,
            "Error" => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };
    }

    private static string Pad(string? value, int width)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? "-" : value;
        if (normalized.Length <= width)
        {
            return normalized.PadRight(width);
        }

        return $"{normalized[..Math.Max(0, width - 1)]}~";
    }
}

internal sealed class StudentStateTracker
{
    private readonly int _totalGroupCount;
    private readonly Dictionary<string, StudentState> _students = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _sessionToStudentKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _groupFailedBaseline = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public StudentStateTracker(int totalGroupCount)
    {
        _totalGroupCount = Math.Max(0, totalGroupCount);
    }

    public void Apply(TestServerRuntimeEvent runtimeEvent)
    {
        lock (_lock)
        {
            if (!TryResolveStudentKey(runtimeEvent, out string key))
            {
                return;
            }

            if (!_students.TryGetValue(key, out StudentState? state))
            {
                state = new StudentState
                {
                    Key = key
                };
                _students[key] = state;
            }

            state.StudentId = runtimeEvent.StudentId ?? state.StudentId;
            state.DisplayName = runtimeEvent.DisplayName ?? state.DisplayName;
            state.CurrentGroup = runtimeEvent.GroupDisplayName ?? runtimeEvent.GroupId ?? state.CurrentGroup;
            state.PassedCount = runtimeEvent.PassedCount;
            state.FailedCount = runtimeEvent.FailedCount;
            state.LastReason = runtimeEvent.ReasonCode ?? runtimeEvent.ReasonDetail ?? state.LastReason;
            state.LastUpdatedUtc = runtimeEvent.OccurredAtUtc;
            state.TotalGroupCount = _totalGroupCount;

            if (!string.IsNullOrWhiteSpace(runtimeEvent.SessionToken))
            {
                _sessionToStudentKey[runtimeEvent.SessionToken] = key;
            }

            switch (runtimeEvent.Kind)
            {
                case TestServerRuntimeEventKind.SessionStarted:
                    state.CurrentGroup = "-";
                    state.CompletedGroupCount = 0;
                    state.ProcessedGroupCount = 0;
                    state.LastReason = "-";
                    _groupFailedBaseline.Remove(key);
                    break;
                case TestServerRuntimeEventKind.TestGroupStarted:
                    _groupFailedBaseline[key] = runtimeEvent.FailedCount;
                    break;
                case TestServerRuntimeEventKind.TestGroupEnded:
                    state.ProcessedGroupCount += 1;
                    if (_groupFailedBaseline.TryGetValue(key, out int failedBeforeGroup))
                    {
                        if (runtimeEvent.FailedCount == failedBeforeGroup)
                        {
                            state.CompletedGroupCount += 1;
                        }

                        _groupFailedBaseline.Remove(key);
                    }
                    break;
                case TestServerRuntimeEventKind.SessionCompleted:
                case TestServerRuntimeEventKind.SessionStopped:
                case TestServerRuntimeEventKind.SessionRejected:
                case TestServerRuntimeEventKind.SessionFaulted:
                    _groupFailedBaseline.Remove(key);
                    break;
            }

            state.Status = runtimeEvent.Kind switch
            {
                TestServerRuntimeEventKind.SessionStarted => "Running",
                TestServerRuntimeEventKind.TestGroupStarted => "Running",
                TestServerRuntimeEventKind.TestCaseEvaluated => "Running",
                TestServerRuntimeEventKind.TestGroupEnded => "Running",
                TestServerRuntimeEventKind.SessionCompleted =>
                    state.CompletedGroupCount >= state.TotalGroupCount ? "Completed" : "Completed (partial)",
                TestServerRuntimeEventKind.SessionRejected => "Rejected",
                TestServerRuntimeEventKind.SessionStopped => "Stopped",
                TestServerRuntimeEventKind.SessionFaulted => "Error",
                _ => state.Status
            };
        }
    }

    public StudentDashboardSnapshot Snapshot()
    {
        lock (_lock)
        {
            List<StudentState> students = _students.Values
                .Select(static state => state.Clone())
                .OrderBy(static state => state.StudentId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static state => state.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new StudentDashboardSnapshot
            {
                Students = students
            };
        }
    }

    private bool TryResolveStudentKey(TestServerRuntimeEvent runtimeEvent, out string key)
    {
        if (!string.IsNullOrWhiteSpace(runtimeEvent.StudentId))
        {
            key = runtimeEvent.StudentId;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(runtimeEvent.SessionToken) &&
            _sessionToStudentKey.TryGetValue(runtimeEvent.SessionToken, out string? mappedKey))
        {
            key = mappedKey;
            return true;
        }

        key = string.Empty;
        return false;
    }
}

internal sealed class StudentDashboardSnapshot
{
    public IReadOnlyList<StudentState> Students { get; init; } = Array.Empty<StudentState>();
}

internal sealed class StudentState
{
    public string Key { get; set; } = string.Empty;
    public string StudentId { get; set; } = "<unknown>";
    public string DisplayName { get; set; } = "<unknown>";
    public string Status { get; set; } = "Connecting";
    public string CurrentGroup { get; set; } = "-";
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int TotalGroupCount { get; set; }
    public int CompletedGroupCount { get; set; }
    public int ProcessedGroupCount { get; set; }
    public string LastReason { get; set; } = "-";
    public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public StudentState Clone()
    {
        return new StudentState
        {
            Key = Key,
            StudentId = StudentId,
            DisplayName = DisplayName,
            Status = Status,
            CurrentGroup = CurrentGroup,
            PassedCount = PassedCount,
            FailedCount = FailedCount,
            TotalGroupCount = TotalGroupCount,
            CompletedGroupCount = CompletedGroupCount,
            ProcessedGroupCount = ProcessedGroupCount,
            LastReason = LastReason,
            LastUpdatedUtc = LastUpdatedUtc
        };
    }
}
