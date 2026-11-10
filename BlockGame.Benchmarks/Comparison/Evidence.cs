using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BlockGame.Benchmarks.Comparison;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ScenarioFamilyAttribute(string family, string kind = "engine") : Attribute
{
    public string Family { get; } = family;
    public string Kind { get; } = kind;
}

public static class EvidenceJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };
    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string Canonical(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",",value.EnumerateObject().OrderBy(p => p.Name,StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name)+":"+Canonical(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(",",value.EnumerateArray().Select(Canonical)) + "]",
        _ => value.GetRawText()
    };
    public static string Fingerprint(object value) => Hash(Encoding.UTF8.GetBytes(Canonical(JsonSerializer.SerializeToElement(value,Options))));
    public static void Write(string path, object value) => File.WriteAllText(path,JsonSerializer.Serialize(value,Options));
}

public sealed record ScenarioContract(string Id, int Version, string Unit, string InputFingerprint, JsonElement Inputs, string TimingClass = "normal")
{
    public static ScenarioContract Create(string id, int version, string unit, object inputs, string timingClass = "normal") =>
        new(id,version,unit,EvidenceJson.Fingerprint(inputs),JsonSerializer.SerializeToElement(inputs,EvidenceJson.Options),timingClass);
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || Version < 1 || string.IsNullOrWhiteSpace(Unit) ||
            !new[] {"normal","early-return","diagnostic"}.Contains(TimingClass) || EvidenceJson.Fingerprint(Inputs) != InputFingerprint)
            throw new InvalidDataException("Invalid scenario contract/fingerprint");
    }
}

public sealed record Observation(string CaseId, ScenarioContract Scenario, string Implementation, double[] SamplesNs,
    double? BytesPerOperation, string[] Diagnostics, bool Success = true, string? OutputFingerprint = null);

public sealed record BenchmarkRun(int SchemaVersion, string Kind, string Mode, string EngineRevision, bool EngineClean,
    string HarnessTree, string EnvironmentFingerprint, Observation[] Cases, string Provenance)
{
    public const int CurrentSchemaVersion = 4;
    public JsonElement? EnvironmentInfo { get; init; }
}

public sealed record ComparisonPolicy(double PracticalPercent = 5, double AbsoluteDeltaNs = 10,
    double NearZeroNs = 100, double MaxCvPercent = 3, int MinimumSamples = 10, double UncertaintyMultiplier = 3);

public sealed record ComparisonRow(string ScenarioId, string Status, string Reason, double? BaselineNs = null,
    double? CandidateNs = null, double? AbsoluteDeltaNs = null, double? Ratio = null, double? PercentChange = null,
    double? AllocationDeltaBytes = null, string AllocationStatus = "unavailable", ScenarioContract? Contract = null);

public sealed record ComparisonReport(int SchemaVersion, string Purpose, ComparisonPolicy Policy, ComparisonRow[] Rows,
    string BaselineRevision, string CandidateRevision, string BaselineHarnessTree, string CandidateHarnessTree,
    string EnvironmentFingerprint, string ComparatorFingerprint)
{
    public string[] Warnings { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ControlValidationResult? Controls { get; init; }
    public JsonElement? BaselineEnvironment { get; init; }
    public JsonElement? CandidateEnvironment { get; init; }
}
