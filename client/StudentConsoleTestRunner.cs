using testprog.messenger;

namespace testprog.client;

/// <summary>
/// Convenience entry point for running student solutions from a console application.
/// </summary>
public static class StudentConsoleTestRunner
{
    /// <summary>
    /// Runs a test session and returns a detailed summary.
    /// </summary>
    /// <param name="options">Client connection and identity options.</param>
    /// <param name="solve">Student solution delegate invoked for each testcase.</param>
    /// <param name="cancellationToken">Cancellation token used to stop the session.</param>
    /// <returns>Final run summary returned by the server/client runtime.</returns>
    public static TestRunSummary Run(
        StudentClientOptions options,
        Func<TestInput, object?> solve,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(solve);

        return RunCoreAsync(options, solve, cancellationToken).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs a test session and returns process-friendly exit code semantics.
    /// </summary>
    /// <param name="options">Client connection and identity options.</param>
    /// <param name="solve">Student solution delegate invoked for each testcase.</param>
    /// <param name="cancellationToken">Cancellation token used to stop the session.</param>
    /// <returns>
    /// <c>0</c> when the run completed and all testcases passed; otherwise <c>1</c>.
    /// </returns>
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
        Console.Clear();
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
    private const int ProgressBarWidth = 24;
    private readonly StudentClientOptions _options;
    private int _groupIndex;
    private readonly bool _useCursorAnchor;
    private int _progressBarLeft;
    private int _progressBarTop = -1;
    private bool _groupProgressLineActive;
    private int _lastProgressLineLength;

    public ConsoleProgressRenderer(StudentClientOptions options)
    {
        _options = options;
        _useCursorAnchor = !Console.IsOutputRedirected;
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
                CloseGroupProgressLine();
                WriteHeader("Test run started");
                break;

            case TestRunProgressKind.TestGroupStart:
                CloseGroupProgressLine();
                _groupIndex += 1;
                WriteHeader(
                    $"Group {_groupIndex}: {progress.GroupDisplayName ?? progress.GroupId} " +
                    $"({progress.GroupTestCaseCount} testcases)");
                _progressBarLeft = 0;
                _progressBarTop = Console.CursorTop;
                if (_useCursorAnchor)
                {
                    // Reserve a dedicated writable row below the bar.
                    // Console.WriteLine();
                    _progressBarTop = Math.Max(0, Console.CursorTop - 1);
                }
                RenderGroupProgress(0, progress.GroupTestCaseCount);
                EnsureCursorBelowProgressBar();
                break;

            case TestRunProgressKind.TestCaseResult:
                RenderGroupProgress(
                    progress.GroupPassedCount + progress.GroupFailedCount,
                    progress.GroupTestCaseCount);
                break;

            case TestRunProgressKind.TestCaseStart:
                // Intentionally keep progress line anchored above and let solver write below it.
                break;

            case TestRunProgressKind.TestGroupEnd:
                int totalInGroup = Math.Max(
                    progress.GroupTestCaseCount,
                    progress.GroupPassedCount + progress.GroupFailedCount);
                RenderGroupProgress(totalInGroup, totalInGroup);
                // EnsureCursorBelowProgressBar();
                double passedPercent = totalInGroup == 0
                    ? 0d
                    : (double)progress.GroupPassedCount * 100d / totalInGroup;

                string groupName = progress.GroupDisplayName ?? progress.GroupId ?? "<unknown>";
                string summary =
                    $"\n  Group done: {groupName} - {passedPercent:0.#}% passed ({progress.GroupPassedCount}/{totalInGroup}).\n";

                if (progress.GroupFailedCount == 0)
                {
                    WritePass(summary);
                }
                else
                {
                    WriteWarn(summary);
                }

                break;

            case TestRunProgressKind.Stop:
                CloseGroupProgressLine();
                WriteWarn(
                    $"Run stopped: {progress.ReasonCode ?? StopReasonCodes.ServerStop}" +
                    $"{FormatDetail(progress.ReasonDetail)}");
                break;
        }
    }

    public void PrintSummary(TestRunSummary summary)
    {
        CloseGroupProgressLine();
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

    private static string FormatDetail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : $" - {value}";
    }

    private void RenderGroupProgress(int completedInGroup, int totalInGroup)
    {
        int total = Math.Max(0, totalInGroup);
        int completed = Math.Clamp(completedInGroup, 0, total);
        int filled = total == 0 ? 0 : (int)Math.Round((double)completed * ProgressBarWidth / total);
        filled = Math.Clamp(filled, 0, ProgressBarWidth);
        double percent = total == 0 ? 0d : (double)completed * 100d / total;

        string bar = $"{new string('#', filled)}{new string('-', ProgressBarWidth - filled)}";
        string line = $"\n  Progress: [{bar}] {completed}/{total} ({percent:#}%)";

        int paddedLength = Math.Max(_lastProgressLineLength, line.Length);

        if (!_useCursorAnchor || _progressBarTop < 0)
        {
            Console.Write(line.PadRight(paddedLength));
            _groupProgressLineActive = true;
            _lastProgressLineLength = paddedLength;
            return;
        }

        int userLeft = Console.CursorLeft;
        int userTop = Console.CursorTop;
        ConsoleColor fg = Console.ForegroundColor;
        ConsoleColor bg = Console.BackgroundColor;

        try
        {
            Console.ResetColor();
            Console.SetCursorPosition(_progressBarLeft, _progressBarTop);
            Console.Write(line.PadRight(paddedLength));

            int belowBarTop = Math.Min(_progressBarTop + 1, Console.BufferHeight - 1);
            int restoreTop = Math.Max(userTop, belowBarTop);
            int restoreLeft = restoreTop == userTop ? userLeft : 0;
            Console.SetCursorPosition(restoreLeft, restoreTop);
        }
        finally
        {
            Console.ForegroundColor = fg;
            Console.BackgroundColor = bg;
        }

        _groupProgressLineActive = true;
        _lastProgressLineLength = paddedLength;
    }

    private void CloseGroupProgressLine()
    {
        if (!_groupProgressLineActive)
        {
            return;
        }

        if (!_useCursorAnchor)
        {
            Console.WriteLine();
        }

        _groupProgressLineActive = false;
        _lastProgressLineLength = 0;
        _progressBarTop = -1;
    }

    private void EnsureCursorBelowProgressBar()
    {
        if (!_useCursorAnchor || _progressBarTop < 0)
        {
            return;
        }

        int targetTop = Math.Min(_progressBarTop + 1, Console.BufferHeight - 1);
        Console.SetCursorPosition(0, targetTop);
    }

    private static void WriteHeader(string text)
    {
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
