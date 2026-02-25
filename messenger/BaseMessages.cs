using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace testprog.messenger;

public static class MessageTypes
{
    public const string ServerWanted = "server-wanted";
    public const string ServerAvailable = "server-available";
    public const string ClientHello = "client-hello";
    public const string ServerHello = "server-hello";
    public const string TestBegin = "test-begin";
    public const string Ping = "ping";
    public const string Pong = "pong";
    public const string TestGroupStart = "testgroup-start";
    public const string TestCase = "testcase";
    public const string TestCaseSolved = "testcase-solved";
    public const string TestCaseResult = "testcase-result";
    public const string TestGroupEnd = "testgroup-end";
    public const string TestEnd = "test-end";
    public const string Stop = "stop";
    public const string Error = "error";
}

public sealed class ProtocolEnvelope
{
    [JsonProperty("v")]
    public int Version { get; init; } = 2;

    [JsonProperty("type")]
    public string Type { get; init; } = string.Empty;

    [JsonProperty("sessionToken", NullValueHandling = NullValueHandling.Ignore)]
    public string? SessionToken { get; init; }

    [JsonProperty("requestId")]
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

    [JsonProperty("sentAtUtc")]
    public DateTimeOffset SentAtUtc { get; init; } = DateTimeOffset.UtcNow;

    [JsonProperty("payload")]
    public JObject Payload { get; init; } = new();
}

public sealed class ServerWantedPayload
{
    [JsonProperty("studentId")]
    public string StudentId { get; init; } = string.Empty;

    [JsonProperty("displayName")]
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class ServerAvailablePayload
{
    [JsonProperty("serverId")]
    public string ServerId { get; init; } = string.Empty;

    [JsonProperty("serverHost")]
    public string ServerHost { get; init; } = string.Empty;

    [JsonProperty("serverPort")]
    public int ServerPort { get; init; }
}

public sealed class ClientHelloPayload
{
    [JsonProperty("studentId")]
    public string StudentId { get; init; } = string.Empty;

    [JsonProperty("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonProperty("clientVersion")]
    public string ClientVersion { get; init; } = string.Empty;
}

public sealed class ServerHelloPayload
{
    [JsonProperty("sessionToken")]
    public string SessionToken { get; init; } = string.Empty;

    [JsonProperty("heartbeatSeconds")]
    public int HeartbeatSeconds { get; init; }
}

public sealed class TestCasePayload
{
    [JsonProperty("testcaseId")]
    public string TestCaseId { get; init; } = string.Empty;

    [JsonProperty("input")]
    public JObject Input { get; init; } = new();
}

public sealed class TestBeginPayload
{
    [JsonProperty("studentId")]
    public string StudentId { get; init; } = string.Empty;

    [JsonProperty("displayName")]
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class TestGroupStartPayload
{
    [JsonProperty("groupId")]
    public string GroupId { get; init; } = string.Empty;

    [JsonProperty("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonProperty("testcaseCount")]
    public int TestCaseCount { get; init; }
}

public sealed class TestGroupEndPayload
{
    [JsonProperty("groupId")]
    public string GroupId { get; init; } = string.Empty;
}

public sealed class TestEndPayload
{
    [JsonProperty("testGroupCount")]
    public int TestGroupCount { get; init; }

    [JsonProperty("testcaseCount")]
    public int TestCaseCount { get; init; }

    [JsonProperty("passedCount")]
    public int PassedCount { get; init; }

    [JsonProperty("failedCount")]
    public int FailedCount { get; init; }
}

public sealed class TestCaseSolvedPayload
{
    [JsonProperty("testcaseId")]
    public string TestCaseId { get; init; } = string.Empty;

    [JsonProperty("output")]
    public JObject Output { get; init; } = new();
}

public sealed class TestCaseResultPayload
{
    [JsonProperty("testcaseId")]
    public string TestCaseId { get; init; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; init; } = string.Empty;

    [JsonProperty("reasonCode", NullValueHandling = NullValueHandling.Ignore)]
    public string? ReasonCode { get; init; }

    [JsonProperty("reasonDetail", NullValueHandling = NullValueHandling.Ignore)]
    public string? ReasonDetail { get; init; }
}

public sealed class StopPayload
{
    [JsonProperty("reasonCode")]
    public string ReasonCode { get; init; } = StopReasonCodes.ClientStop;

    [JsonProperty("reasonDetail", NullValueHandling = NullValueHandling.Ignore)]
    public string? ReasonDetail { get; init; }
}

public sealed class ErrorPayload
{
    [JsonProperty("reasonCode")]
    public string ReasonCode { get; init; } = StopReasonCodes.InternalServerError;

    [JsonProperty("reasonDetail", NullValueHandling = NullValueHandling.Ignore)]
    public string? ReasonDetail { get; init; }
}

public static class TestCaseResultStatuses
{
    public const string Passed = "passed";
    public const string Failed = "failed";
}
