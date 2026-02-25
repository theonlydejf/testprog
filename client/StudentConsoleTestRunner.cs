using testprog.messenger;

namespace testprog.client;

public static class StudentConsoleTestRunner
{
    public static TestRunSummary Run(
        StudentClientOptions options,
        Func<TestInput, object?> solve,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(solve);

        return RunCoreAsync(options, solve, cancellationToken).GetAwaiter().GetResult();
    }

    public static int RunWithExitCode(
        StudentClientOptions options,
        Func<TestInput, object?> solve,
        CancellationToken cancellationToken = default)
    {
        TestRunSummary summary = Run(options, solve, cancellationToken);

        if (!summary.Completed)
        {
            return 1;
        }

        return summary.FailedCount == 0 ? 0 : 1;
    }

    private static async Task<TestRunSummary> RunCoreAsync(
        StudentClientOptions options,
        Func<TestInput, object?> solve,
        CancellationToken cancellationToken)
    {
        TestProgClientOptions coreOptions = options.ToCoreOptions();
        ConsoleProgressRenderer renderer = new(options);

        renderer.PrintConnecting();

        await using ITestProgClient client = await TestProgClient.ConnectAsync(coreOptions, cancellationToken)
            .ConfigureAwait(false);

        renderer.PrintConnected();

        TestRunSummary summary = await client.RunAsync(
                solve,
                renderer.HandleProgress,
                cancellationToken)
            .ConfigureAwait(false);

        renderer.PrintSummary(summary);
        return summary;
    }
}

internal sealed class ConsoleProgressRenderer
{
    private readonly StudentClientOptions _options;
    private int _groupIndex;

    public ConsoleProgressRenderer(StudentClientOptions options)
    {
        _options = options;
    }

    public void PrintConnecting()
    {
        WriteInfo($"Student: {_options.DisplayName} ({_options.StudentId})");

        if (string.IsNullOrWhiteSpace(_options.ServerHost))
        {
            WriteInfo(
                $"Looking for server on {_options.DiscoveryMulticastAddress}:{_options.DiscoveryPort}...");
        }
        else
        {
            WriteInfo($"Connecting to server {_options.ServerHost}:{_options.ServerPort}...");
        }
    }

    public void PrintConnected()
    {
        WriteInfo("Connected. Waiting for test stream...");
    }

    public void HandleProgress(TestRunProgress progress)
    {
        switch (progress.Kind)
        {
            case TestRunProgressKind.TestBegin:
                WriteHeader("Test run started");
                break;

            case TestRunProgressKind.TestGroupStart:
                _groupIndex += 1;
                WriteHeader(
                    $"Group {_groupIndex}: {progress.GroupDisplayName ?? progress.GroupId} " +
                    $"({progress.GroupTestCaseCount} testcases)");
                break;

            case TestRunProgressKind.TestCaseResult:
                PrintTestCaseResult(progress);
                break;

            case TestRunProgressKind.TestGroupEnd:
                WriteInfo(
                    $"Group done: passed {progress.GroupPassedCount}, failed {progress.GroupFailedCount}.");
                break;

            case TestRunProgressKind.Stop:
                WriteWarn(
                    $"Run stopped: {progress.ReasonCode ?? StopReasonCodes.ServerStop}" +
                    $"{FormatDetail(progress.ReasonDetail)}");
                break;
        }
    }

    public void PrintSummary(TestRunSummary summary)
    {
        WriteHeader("Final summary");
        WriteInfo($"Groups: {summary.TestGroupCount}");
        WriteInfo($"Testcases: {summary.TestCaseCount}");
        WritePass($"Passed: {summary.PassedCount}");
        if (summary.FailedCount == 0)
        {
            WritePass("Failed: 0");
        }
        else
        {
            WriteFail($"Failed: {summary.FailedCount}");
        }

        if (!summary.Completed)
        {
            WriteWarn(
                $"Run was stopped ({summary.StopReasonCode ?? StopReasonCodes.ServerStop})" +
                $"{FormatDetail(summary.StopReasonDetail)}");
        }
    }

    private void PrintTestCaseResult(TestRunProgress progress)
    {
        string testcaseId = progress.TestCaseId ?? "<unknown>";
        bool passed = string.Equals(progress.TestCaseStatus, TestCaseResultStatuses.Passed, StringComparison.OrdinalIgnoreCase);

        if (passed)
        {
            WritePass(
                $"  PASS {testcaseId}  " +
                $"[group {progress.GroupPassedCount}/{progress.GroupTestCaseCount}, total {progress.TotalPassedCount} passed]");
            return;
        }

        string detail = $"{FormatDetail(progress.ReasonCode)}{FormatDetail(progress.ReasonDetail)}";
        WriteFail(
            $"  FAIL {testcaseId}  " +
            $"[group failed {progress.GroupFailedCount}, total failed {progress.TotalFailedCount}]{detail}");
    }

    private static string FormatDetail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $" - {value}";
    }

    private static void WriteHeader(string text)
    {
        Console.WriteLine();
        WriteWithColor(text, ConsoleColor.Cyan);
    }

    private static void WriteInfo(string text)
    {
        WriteWithColor(text, ConsoleColor.Gray);
    }

    private static void WritePass(string text)
    {
        WriteWithColor(text, ConsoleColor.Green);
    }

    private static void WriteFail(string text)
    {
        WriteWithColor(text, ConsoleColor.Red);
    }

    private static void WriteWarn(string text)
    {
        WriteWithColor(text, ConsoleColor.Yellow);
    }

    private static void WriteWithColor(string text, ConsoleColor color)
    {
        ConsoleColor previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }
}
