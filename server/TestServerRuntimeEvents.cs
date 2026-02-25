namespace testprog.server;

/// <summary>
/// Categories of runtime events emitted by server host.
/// </summary>
public enum TestServerRuntimeEventKind
{
    /// <summary>Client transport connection was accepted.</summary>
    ClientConnected = 0,
    /// <summary>Session handshake succeeded and test run started.</summary>
    SessionStarted = 1,
    /// <summary>Session was rejected, typically due to authorization.</summary>
    SessionRejected = 2,
    /// <summary>A test group started.</summary>
    TestGroupStarted = 3,
    /// <summary>A testcase was evaluated and result produced.</summary>
    TestCaseEvaluated = 4,
    /// <summary>A test group ended.</summary>
    TestGroupEnded = 5,
    /// <summary>Session completed normally.</summary>
    SessionCompleted = 6,
    /// <summary>Session ended due to stop/timeout/cancellation.</summary>
    SessionStopped = 7,
    /// <summary>Session terminated due to unexpected fault.</summary>
    SessionFaulted = 8
}

/// <summary>
/// Event payload emitted by server runtime callback.
/// </summary>
public sealed class TestServerRuntimeEvent
{
    /// <summary>UTC timestamp when event was emitted.</summary>
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>Event category.</summary>
    public TestServerRuntimeEventKind Kind { get; init; }
    /// <summary>Remote client endpoint when available.</summary>
    public string? RemoteEndpoint { get; init; }
    /// <summary>Session token when session has been established.</summary>
    public string? SessionToken { get; init; }
    /// <summary>Student identifier related to this event.</summary>
    public string? StudentId { get; init; }
    /// <summary>Student display name related to this event.</summary>
    public string? DisplayName { get; init; }
    /// <summary>Group identifier related to this event.</summary>
    public string? GroupId { get; init; }
    /// <summary>Group display name related to this event.</summary>
    public string? GroupDisplayName { get; init; }
    /// <summary>Testcase identifier related to this event.</summary>
    public string? TestCaseId { get; init; }
    /// <summary>Testcase textual status when available.</summary>
    public string? TestCaseStatus { get; init; }
    /// <summary>Total passed testcase count at event time.</summary>
    public int PassedCount { get; init; }
    /// <summary>Total failed testcase count at event time.</summary>
    public int FailedCount { get; init; }
    /// <summary>Machine-readable reason code for stop/failure events.</summary>
    public string? ReasonCode { get; init; }
    /// <summary>Optional human-readable reason detail.</summary>
    public string? ReasonDetail { get; init; }
}
