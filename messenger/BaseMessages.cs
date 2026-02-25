using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace testprog.messenger;

/// <summary>
/// Canonical protocol message type names used in envelope <c>type</c> field.
/// </summary>
public static class MessageTypes
{
    /// <summary>Client UDP discovery request.</summary>
    public const string ServerWanted = "server-wanted";

    /// <summary>Server UDP discovery response with endpoint information.</summary>
    public const string ServerAvailable = "server-available";

    /// <summary>Client TCP handshake request containing identity.</summary>
    public const string ClientHello = "client-hello";

    /// <summary>Server TCP handshake response containing session token.</summary>
    public const string ServerHello = "server-hello";

    /// <summary>Start of a full test run.</summary>
    public const string TestBegin = "test-begin";

    /// <summary>Heartbeat ping sent by server.</summary>
    public const string Ping = "ping";

    /// <summary>Heartbeat pong sent by client.</summary>
    public const string Pong = "pong";

    /// <summary>Start of one test group.</summary>
    public const string TestGroupStart = "testgroup-start";

    /// <summary>One testcase input payload sent to client.</summary>
    public const string TestCase = "testcase";

    /// <summary>Client answer for one testcase.</summary>
    public const string TestCaseSolved = "testcase-solved";

    /// <summary>Server verdict for one testcase answer.</summary>
    public const string TestCaseResult = "testcase-result";

    /// <summary>End of one test group.</summary>
    public const string TestGroupEnd = "testgroup-end";

    /// <summary>End of full test run.</summary>
    public const string TestEnd = "test-end";

    /// <summary>Explicit session stop message.</summary>
    public const string Stop = "stop";

    /// <summary>Protocol or runtime error message.</summary>
    public const string Error = "error";
}

/// <summary>
/// Common envelope used for all protocol messages.
/// </summary>
public sealed class ProtocolEnvelope
{
    /// <summary>
    /// Protocol version.
    /// </summary>
    [JsonProperty("v")]
    public int Version { get; init; } = 2;

    /// <summary>
    /// Message type identifier from <see cref="MessageTypes"/>.
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Session token issued by server during handshake.
    /// </summary>
    [JsonProperty("sessionToken", NullValueHandling = NullValueHandling.Ignore)]
    public string? SessionToken { get; init; }

    /// <summary>
    /// Correlation identifier for troubleshooting and tracing.
    /// </summary>
    [JsonProperty("requestId")]
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// UTC timestamp at message creation time.
    /// </summary>
    [JsonProperty("sentAtUtc")]
    public DateTimeOffset SentAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Message-specific payload object.
    /// </summary>
    [JsonProperty("payload")]
    public JObject Payload { get; init; } = new();
}

/// <summary>
/// Payload for <see cref="MessageTypes.ServerWanted"/>.
/// </summary>
public sealed class ServerWantedPayload
{
    /// <summary>Student identifier asking for available server.</summary>
    [JsonProperty("studentId")]
    public string StudentId { get; init; } = string.Empty;

    /// <summary>Student display name asking for available server.</summary>
    [JsonProperty("displayName")]
    public string DisplayName { get; init; } = string.Empty;
}

/// <summary>
/// Payload for <see cref="MessageTypes.ServerAvailable"/>.
/// </summary>
public sealed class ServerAvailablePayload
{
    /// <summary>Logical server identifier.</summary>
    [JsonProperty("serverId")]
    public string ServerId { get; init; } = string.Empty;

    /// <summary>TCP host clients should connect to.</summary>
    [JsonProperty("serverHost")]
    public string ServerHost { get; init; } = string.Empty;

    /// <summary>TCP port clients should connect to.</summary>
    [JsonProperty("serverPort")]
    public int ServerPort { get; init; }
}

/// <summary>
/// Payload for <see cref="MessageTypes.ClientHello"/>.
/// </summary>
public sealed class ClientHelloPayload
{
    /// <summary>Student identifier.</summary>
    [JsonProperty("studentId")]
    public string StudentId { get; init; } = string.Empty;

    /// <summary>Student display name.</summary>
    [JsonProperty("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Client runtime version string.</summary>
    [JsonProperty("clientVersion")]
    public string ClientVersion { get; init; } = string.Empty;
}

/// <summary>
/// Payload for <see cref="MessageTypes.ServerHello"/>.
/// </summary>
public sealed class ServerHelloPayload
{
    /// <summary>Session token required for subsequent TCP messages.</summary>
    [JsonProperty("sessionToken")]
    public string SessionToken { get; init; } = string.Empty;

    /// <summary>Heartbeat interval in seconds recommended by server.</summary>
    [JsonProperty("heartbeatSeconds")]
    public int HeartbeatSeconds { get; init; }
}

/// <summary>
/// Payload for <see cref="MessageTypes.TestCase"/>.
/// </summary>
public sealed class TestCasePayload
{
    /// <summary>Unique testcase identifier.</summary>
    [JsonProperty("testcaseId")]
    public string TestCaseId { get; init; } = string.Empty;

    /// <summary>JSON input object for student solution.</summary>
    [JsonProperty("input")]
    public JObject Input { get; init; } = new();
}

/// <summary>
/// Payload for <see cref="MessageTypes.TestBegin"/>.
/// </summary>
public sealed class TestBeginPayload
{
    /// <summary>Student identifier for the active session.</summary>
    [JsonProperty("studentId")]
    public string StudentId { get; init; } = string.Empty;

    /// <summary>Student display name for the active session.</summary>
    [JsonProperty("displayName")]
    public string DisplayName { get; init; } = string.Empty;
}

/// <summary>
/// Payload for <see cref="MessageTypes.TestGroupStart"/>.
/// </summary>
public sealed class TestGroupStartPayload
{
    /// <summary>Unique test group identifier.</summary>
    [JsonProperty("groupId")]
    public string GroupId { get; init; } = string.Empty;

    /// <summary>Human-readable group name.</summary>
    [JsonProperty("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Total number of testcases in this group.</summary>
    [JsonProperty("testcaseCount")]
    public int TestCaseCount { get; init; }
}

/// <summary>
/// Payload for <see cref="MessageTypes.TestGroupEnd"/>.
/// </summary>
public sealed class TestGroupEndPayload
{
    /// <summary>Group identifier that has finished.</summary>
    [JsonProperty("groupId")]
    public string GroupId { get; init; } = string.Empty;
}

/// <summary>
/// Payload for <see cref="MessageTypes.TestEnd"/>.
/// </summary>
public sealed class TestEndPayload
{
    /// <summary>Total group count in the run.</summary>
    [JsonProperty("testGroupCount")]
    public int TestGroupCount { get; init; }

    /// <summary>Total testcase count in the run.</summary>
    [JsonProperty("testcaseCount")]
    public int TestCaseCount { get; init; }

    /// <summary>Total passed testcase count.</summary>
    [JsonProperty("passedCount")]
    public int PassedCount { get; init; }

    /// <summary>Total failed testcase count.</summary>
    [JsonProperty("failedCount")]
    public int FailedCount { get; init; }
}

/// <summary>
/// Payload for <see cref="MessageTypes.TestCaseSolved"/>.
/// </summary>
public sealed class TestCaseSolvedPayload
{
    /// <summary>Identifier of solved testcase.</summary>
    [JsonProperty("testcaseId")]
    public string TestCaseId { get; init; } = string.Empty;

    /// <summary>Computed output object provided by client.</summary>
    [JsonProperty("output")]
    public JObject Output { get; init; } = new();
}

/// <summary>
/// Payload for <see cref="MessageTypes.TestCaseResult"/>.
/// </summary>
public sealed class TestCaseResultPayload
{
    /// <summary>Identifier of evaluated testcase.</summary>
    [JsonProperty("testcaseId")]
    public string TestCaseId { get; init; } = string.Empty;

    /// <summary>Result status string from <see cref="TestCaseResultStatuses"/>.</summary>
    [JsonProperty("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Machine-readable reason code for failures.</summary>
    [JsonProperty("reasonCode", NullValueHandling = NullValueHandling.Ignore)]
    public string? ReasonCode { get; init; }

    /// <summary>Optional human-readable detail for <see cref="ReasonCode"/>.</summary>
    [JsonProperty("reasonDetail", NullValueHandling = NullValueHandling.Ignore)]
    public string? ReasonDetail { get; init; }
}

/// <summary>
/// Payload for <see cref="MessageTypes.Stop"/>.
/// </summary>
public sealed class StopPayload
{
    /// <summary>Machine-readable stop reason from <see cref="StopReasonCodes"/>.</summary>
    [JsonProperty("reasonCode")]
    public string ReasonCode { get; init; } = StopReasonCodes.ClientStop;

    /// <summary>Optional human-readable stop detail.</summary>
    [JsonProperty("reasonDetail", NullValueHandling = NullValueHandling.Ignore)]
    public string? ReasonDetail { get; init; }
}

/// <summary>
/// Payload for <see cref="MessageTypes.Error"/>.
/// </summary>
public sealed class ErrorPayload
{
    /// <summary>Machine-readable error reason from <see cref="StopReasonCodes"/>.</summary>
    [JsonProperty("reasonCode")]
    public string ReasonCode { get; init; } = StopReasonCodes.InternalServerError;

    /// <summary>Optional human-readable error detail.</summary>
    [JsonProperty("reasonDetail", NullValueHandling = NullValueHandling.Ignore)]
    public string? ReasonDetail { get; init; }
}

/// <summary>
/// Well-known textual statuses used in testcase result payloads.
/// </summary>
public static class TestCaseResultStatuses
{
    /// <summary>Testcase passed.</summary>
    public const string Passed = "passed";

    /// <summary>Testcase failed.</summary>
    public const string Failed = "failed";
}
