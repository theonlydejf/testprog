using Newtonsoft.Json.Linq;

namespace testprog.server;

public sealed class TestSuiteDefinition
{
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

public sealed class TestGroupDefinition
{
    public string GroupId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<TestCaseDefinition> TestCases { get; init; } = Array.Empty<TestCaseDefinition>();
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

public sealed class TestCaseDefinition
{
    public string TestCaseId { get; init; } = string.Empty;
    public JObject Input { get; init; } = new();
    public JToken? ExpectedOutput { get; init; }
    public GoldenStandardDefinition? GoldenStandard { get; init; }
    public TestCaseComparisonMode ComparisonMode { get; init; } = TestCaseComparisonMode.StrictJson;

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
    }
}

public sealed class GoldenStandardDefinition
{
    public string SourceFilePath { get; init; } = string.Empty;
    public string TypeName { get; init; } = "GoldenStandard";
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

public enum TestCaseComparisonMode
{
    StrictJson = 0,
    NormalizedText = 1
}

public sealed class RandomTestGroupDefinition
{
    public int Count { get; init; }
    public string TestCaseIdPrefix { get; init; } = "random-";
    public int? Seed { get; init; }
    public TestCaseComparisonMode ComparisonMode { get; init; } = TestCaseComparisonMode.StrictJson;
    public GoldenStandardDefinition? GoldenStandard { get; init; }
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

public sealed class RandomInputGeneratorDefinition
{
    public RandomInputGeneratorMode Mode { get; init; } = RandomInputGeneratorMode.Default;
    public DefaultRandomInputGeneratorDefinition? Default { get; init; }
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

public enum RandomInputGeneratorMode
{
    Default = 0,
    SourceFile = 1
}

public sealed class DefaultRandomInputGeneratorDefinition
{
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

public sealed class RandomIntFieldDefinition
{
    public string Name { get; init; } = string.Empty;
    public int MinValue { get; init; } = 0;
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

public sealed class SourceFileRandomInputGeneratorDefinition
{
    public string SourceFilePath { get; init; } = string.Empty;
    public string TypeName { get; init; } = "RandomInputGenerator";
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
