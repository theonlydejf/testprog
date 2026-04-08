using Newtonsoft.Json.Linq;

namespace testprog.server;

/// <summary>
/// Root definition of a full server test suite.
/// </summary>
public sealed class TestSuiteDefinition
{
    /// <summary>
    /// Ordered list of test groups.
    /// </summary>
    public IReadOnlyList<TestGroupDefinition> Groups { get; init; } = Array.Empty<TestGroupDefinition>();

    internal void Validate()
    {
        if (Groups is null)
        {
            throw new ArgumentException("Groups collection is required.", nameof(Groups));
        }

        if (Groups.Count == 0)
        {
            throw new ArgumentException("At least one test group is required.", nameof(Groups));
        }

        HashSet<string> groupIds = new(StringComparer.Ordinal);
        foreach (TestGroupDefinition group in Groups)
        {
            group.Validate();
            if (!groupIds.Add(group.GroupId))
            {
                throw new ArgumentException($"Duplicate group id '{group.GroupId}'.", nameof(Groups));
            }
        }
    }
}

/// <summary>
/// One logical group of testcases.
/// </summary>
public sealed class TestGroupDefinition
{
    /// <summary>
    /// Unique group identifier.
    /// </summary>
    public string GroupId { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable group name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Static testcase list. Must be used exclusively with <see cref="Randomized"/>.
    /// </summary>
    public IReadOnlyList<TestCaseDefinition> TestCases { get; init; } = Array.Empty<TestCaseDefinition>();

    /// <summary>
    /// Randomized testcase source. Must be used exclusively with <see cref="TestCases"/>.
    /// </summary>
    public RandomTestGroupDefinition? Randomized { get; init; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(GroupId))
        {
            throw new ArgumentException("GroupId is required.", nameof(GroupId));
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new ArgumentException("DisplayName is required.", nameof(DisplayName));
        }

        bool hasStaticCases = TestCases is { Count: > 0 };
        bool hasRandomizedCases = Randomized is not null;

        if (hasStaticCases == hasRandomizedCases)
        {
            throw new ArgumentException(
                $"Group '{GroupId}' must define exactly one testcase source: 'TestCases' or 'Randomized'.",
                nameof(TestCases));
        }

        if (hasStaticCases)
        {
            HashSet<string> testcaseIds = new(StringComparer.Ordinal);
            foreach (TestCaseDefinition testcase in TestCases)
            {
                testcase.Validate();
                if (!testcaseIds.Add(testcase.TestCaseId))
                {
                    throw new ArgumentException(
                        $"Group '{GroupId}' contains duplicate testcase id '{testcase.TestCaseId}'.",
                        nameof(TestCases));
                }
            }
        }

        Randomized?.Validate(GroupId);
    }
}

/// <summary>
/// Definition of one testcase in static group mode.
/// </summary>
public sealed class TestCaseDefinition
{
    /// <summary>
    /// Unique testcase identifier.
    /// </summary>
    public string TestCaseId { get; init; } = string.Empty;

    /// <summary>
    /// Input JSON object sent to the student solution.
    /// </summary>
    public JObject Input { get; init; } = new();

    /// <summary>
    /// Expected output JSON token for direct comparison mode.
    /// </summary>
    public JToken? ExpectedOutput { get; init; }

    /// <summary>
    /// Optional golden standard evaluator reference used instead of <see cref="ExpectedOutput"/>.
    /// </summary>
    public GoldenStandardDefinition? GoldenStandard { get; init; }

    /// <summary>
    /// Output comparison behavior.
    /// </summary>
    public TestCaseComparisonMode ComparisonMode { get; init; } = TestCaseComparisonMode.StrictJson;

    /// <summary>
    /// Optional timeout override for this testcase.
    /// </summary>
    public TimeSpan? ResponseTimeout { get; init; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(TestCaseId))
        {
            throw new ArgumentException("TestCaseId is required.", nameof(TestCaseId));
        }

        if (Input is null)
        {
            throw new ArgumentException($"Testcase '{TestCaseId}' has null input.", nameof(Input));
        }

        bool hasExpectedOutput = ExpectedOutput is not null;
        bool hasGoldenStandard = GoldenStandard is not null;

        if (hasExpectedOutput == hasGoldenStandard)
        {
            throw new ArgumentException(
                $"Testcase '{TestCaseId}' must define exactly one expected-output source: 'ExpectedOutput' or 'GoldenStandard'.",
                nameof(ExpectedOutput));
        }

        if (GoldenStandard is not null)
        {
            GoldenStandard.Validate(TestCaseId);
        }

        if (ResponseTimeout is not null && ResponseTimeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"Testcase '{TestCaseId}' timeout must be greater than zero.",
                nameof(ResponseTimeout));
        }
    }
}

/// <summary>
/// Source-file based golden standard evaluator configuration.
/// </summary>
public sealed class GoldenStandardDefinition
{
    /// <summary>
    /// Source file path containing golden standard implementation.
    /// </summary>
    public string SourceFilePath { get; init; } = string.Empty;

    /// <summary>
    /// CLR type name containing evaluation method.
    /// </summary>
    public string TypeName { get; init; } = "GoldenStandard";

    /// <summary>
    /// Static method name used for evaluation.
    /// </summary>
    public string MethodName { get; init; } = "Solve";

    internal void Validate(string testcaseId)
    {
        if (string.IsNullOrWhiteSpace(SourceFilePath))
        {
            throw new ArgumentException(
                $"Testcase '{testcaseId}' has empty golden standard source path.",
                nameof(SourceFilePath));
        }

        if (string.IsNullOrWhiteSpace(TypeName))
        {
            throw new ArgumentException(
                $"Testcase '{testcaseId}' has empty golden standard type name.",
                nameof(TypeName));
        }

        if (string.IsNullOrWhiteSpace(MethodName))
        {
            throw new ArgumentException(
                $"Testcase '{testcaseId}' has empty golden standard method name.",
                nameof(MethodName));
        }
    }
}

/// <summary>
/// Supported testcase output comparison modes.
/// </summary>
public enum TestCaseComparisonMode
{
    /// <summary>
    /// Deep JSON token comparison.
    /// </summary>
    StrictJson = 0,

    /// <summary>
    /// Text comparison after normalization and string trimming.
    /// </summary>
    NormalizedText = 1
}

/// <summary>
/// Randomized testcase group configuration.
/// </summary>
public sealed class RandomTestGroupDefinition
{
    /// <summary>
    /// Number of generated testcases in the group.
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Prefix used to generate testcase identifiers.
    /// </summary>
    public string TestCaseIdPrefix { get; init; } = "random-";

    /// <summary>
    /// Optional deterministic random seed.
    /// </summary>
    public int? Seed { get; init; }

    /// <summary>
    /// Comparison mode used for generated outputs.
    /// </summary>
    public TestCaseComparisonMode ComparisonMode { get; init; } = TestCaseComparisonMode.StrictJson;

    /// <summary>
    /// Optional timeout override applied to each generated testcase in this group.
    /// </summary>
    public TimeSpan? ResponseTimeout { get; init; }

    /// <summary>
    /// Golden standard evaluator used to derive expected outputs.
    /// </summary>
    public GoldenStandardDefinition? GoldenStandard { get; init; }

    /// <summary>
    /// Random input generator configuration.
    /// </summary>
    public RandomInputGeneratorDefinition? InputGenerator { get; init; }

    internal void Validate(string groupId)
    {
        if (Count <= 0)
        {
            throw new ArgumentException(
                $"Group '{groupId}' random testcase count must be greater than zero.",
                nameof(Count));
        }

        if (string.IsNullOrWhiteSpace(TestCaseIdPrefix))
        {
            throw new ArgumentException(
                $"Group '{groupId}' random testcase id prefix cannot be empty.",
                nameof(TestCaseIdPrefix));
        }

        if (ResponseTimeout is not null && ResponseTimeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"Group '{groupId}' random testcase timeout must be greater than zero.",
                nameof(ResponseTimeout));
        }

        if (GoldenStandard is null)
        {
            throw new ArgumentException(
                $"Group '{groupId}' random testcase source requires a golden standard definition.",
                nameof(GoldenStandard));
        }

        if (InputGenerator is null)
        {
            throw new ArgumentException(
                $"Group '{groupId}' random testcase source requires an input generator definition.",
                nameof(InputGenerator));
        }

        GoldenStandard.Validate($"{groupId}/*");
        InputGenerator.Validate(groupId);
    }
}

/// <summary>
/// Root random input generator configuration.
/// </summary>
public sealed class RandomInputGeneratorDefinition
{
    /// <summary>
    /// Generator mode.
    /// </summary>
    public RandomInputGeneratorMode Mode { get; init; } = RandomInputGeneratorMode.Default;

    /// <summary>
    /// Configuration for default built-in generator mode.
    /// </summary>
    public DefaultRandomInputGeneratorDefinition? Default { get; init; }

    /// <summary>
    /// Configuration for source-file generator mode.
    /// </summary>
    public SourceFileRandomInputGeneratorDefinition? SourceFile { get; init; }

    internal void Validate(string groupId)
    {
        switch (Mode)
        {
            case RandomInputGeneratorMode.Default:
                if (Default is null)
                {
                    throw new ArgumentException(
                        $"Group '{groupId}' input generator mode 'default' requires 'Default' definition.",
                        nameof(Default));
                }

                if (SourceFile is not null)
                {
                    throw new ArgumentException(
                        $"Group '{groupId}' input generator mode 'default' cannot define 'SourceFile'.",
                        nameof(SourceFile));
                }

                Default.Validate(groupId);
                break;

            case RandomInputGeneratorMode.SourceFile:
                if (SourceFile is null)
                {
                    throw new ArgumentException(
                        $"Group '{groupId}' input generator mode 'source-file' requires 'SourceFile' definition.",
                        nameof(SourceFile));
                }

                if (Default is not null)
                {
                    throw new ArgumentException(
                        $"Group '{groupId}' input generator mode 'source-file' cannot define 'Default'.",
                        nameof(Default));
                }

                SourceFile.Validate(groupId);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(Mode), $"Unsupported input generator mode '{Mode}'.");
        }
    }
}

/// <summary>
/// Supported random input generator modes.
/// </summary>
public enum RandomInputGeneratorMode
{
    /// <summary>
    /// Built-in integer field generator.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Source-file custom generator compiled at runtime.
    /// </summary>
    SourceFile = 1
}

/// <summary>
/// Built-in random input generator configuration.
/// </summary>
public sealed class DefaultRandomInputGeneratorDefinition
{
    /// <summary>
    /// Integer field definitions generated for each testcase input.
    /// </summary>
    public IReadOnlyList<RandomIntFieldDefinition> IntFields { get; init; } = Array.Empty<RandomIntFieldDefinition>();

    internal void Validate(string groupId)
    {
        if (IntFields is null || IntFields.Count == 0)
        {
            throw new ArgumentException(
                $"Group '{groupId}' default random input generator must define at least one integer field.",
                nameof(IntFields));
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (RandomIntFieldDefinition field in IntFields)
        {
            field.Validate(groupId);
            if (!names.Add(field.Name))
            {
                throw new ArgumentException(
                    $"Group '{groupId}' default random input generator contains duplicate field '{field.Name}'.",
                    nameof(IntFields));
            }
        }
    }
}

/// <summary>
/// One generated integer field configuration.
/// </summary>
public sealed class RandomIntFieldDefinition
{
    /// <summary>
    /// Target property name in generated input object.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Inclusive minimum value.
    /// </summary>
    public int MinValue { get; init; } = 0;

    /// <summary>
    /// Inclusive maximum value.
    /// </summary>
    public int MaxValue { get; init; } = 100;

    internal void Validate(string groupId)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException(
                $"Group '{groupId}' random int field name cannot be empty.",
                nameof(Name));
        }

        if (MinValue > MaxValue)
        {
            throw new ArgumentException(
                $"Group '{groupId}' random int field '{Name}' has invalid range [{MinValue}, {MaxValue}].",
                nameof(MinValue));
        }
    }
}

/// <summary>
/// Source-file random input generator configuration.
/// </summary>
public sealed class SourceFileRandomInputGeneratorDefinition
{
    /// <summary>
    /// Source file path containing generator implementation.
    /// </summary>
    public string SourceFilePath { get; init; } = string.Empty;

    /// <summary>
    /// CLR type name containing generator method.
    /// </summary>
    public string TypeName { get; init; } = "RandomInputGenerator";

    /// <summary>
    /// Static method name used to generate input values.
    /// </summary>
    public string MethodName { get; init; } = "Generate";

    internal void Validate(string groupId)
    {
        if (string.IsNullOrWhiteSpace(SourceFilePath))
        {
            throw new ArgumentException(
                $"Group '{groupId}' has empty source-file random input generator path.",
                nameof(SourceFilePath));
        }

        if (string.IsNullOrWhiteSpace(TypeName))
        {
            throw new ArgumentException(
                $"Group '{groupId}' has empty source-file random input generator type name.",
                nameof(TypeName));
        }

        if (string.IsNullOrWhiteSpace(MethodName))
        {
            throw new ArgumentException(
                $"Group '{groupId}' has empty source-file random input generator method name.",
                nameof(MethodName));
        }
    }
}
