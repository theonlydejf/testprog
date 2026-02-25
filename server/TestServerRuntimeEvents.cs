namespace testprog.server;

public enum TestServerRuntimeEventKind
{
    ClientConnected = 0,
    SessionStarted = 1,
    SessionRejected = 2,
    TestGroupStarted = 3,
    TestCaseEvaluated = 4,
    TestGroupEnded = 5,
    SessionCompleted = 6,
    SessionStopped = 7,
    SessionFaulted = 8
}

public sealed class TestServerRuntimeEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public TestServerRuntimeEventKind Kind { get; init; }
    public string? RemoteEndpoint { get; init; }
    public string? SessionToken { get; init; }
    public string? StudentId { get; init; }
    public string? DisplayName { get; init; }
    public string? GroupId { get; init; }
    public string? GroupDisplayName { get; init; }
    public string? TestCaseId { get; init; }
    public string? TestCaseStatus { get; init; }
    public int PassedCount { get; init; }
    public int FailedCount { get; init; }
    public string? ReasonCode { get; init; }
    public string? ReasonDetail { get; init; }
}
