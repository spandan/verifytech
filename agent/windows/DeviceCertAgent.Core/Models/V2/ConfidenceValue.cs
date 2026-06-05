namespace DeviceCertAgent.Core.Models.V2;

public enum ConfidenceLevel
{
    High,
    Medium,
    Low,
}

public sealed class ConfidenceValue<T>
{
    public T? Value { get; init; }
    public string Source { get; init; } = "unavailable";
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Low;
    public string CollectionMethod { get; init; } = "not_collected";
    public string CollectedAt { get; init; } = "";

    public static ConfidenceValue<T> Collected(
        T value,
        string source,
        string method,
        ConfidenceLevel confidence = ConfidenceLevel.High) =>
        new()
        {
            Value = value,
            Source = source,
            Confidence = confidence,
            CollectionMethod = method,
            CollectedAt = DateTime.UtcNow.ToString("o"),
        };

    public static ConfidenceValue<T> Estimated(
        T value,
        string source,
        string method,
        string reason) =>
        new()
        {
            Value = value,
            Source = $"{source} (estimated: {reason})",
            Confidence = ConfidenceLevel.Medium,
            CollectionMethod = method,
            CollectedAt = DateTime.UtcNow.ToString("o"),
        };

    public static ConfidenceValue<T> Unavailable(string source, string method, string? reason = null) =>
        new()
        {
            Value = default,
            Source = reason is null ? source : $"{source}: {reason}",
            Confidence = ConfidenceLevel.Low,
            CollectionMethod = method,
            CollectedAt = DateTime.UtcNow.ToString("o"),
        };

    public static ConfidenceValue<T> Unknown(string method = "not_verified") =>
        Unavailable("unknown", method, "not positively verified");
}

public sealed class TriStateValue
{
    public bool? Value { get; init; }
    public string CollectionStatus { get; init; } = "unknown";
    public string Source { get; init; } = "";
    public ConfidenceLevel Confidence { get; init; } = ConfidenceLevel.Low;
    public string CollectedAt { get; init; } = "";

    public static TriStateValue Verified(bool value, string source, string method) =>
        new()
        {
            Value = value,
            CollectionStatus = "verified",
            Source = source,
            Confidence = ConfidenceLevel.High,
            CollectedAt = DateTime.UtcNow.ToString("o"),
        };

    public static TriStateValue Unknown(string method) =>
        new()
        {
            Value = null,
            CollectionStatus = "unknown",
            Source = "not_verified",
            Confidence = ConfidenceLevel.Low,
            CollectedAt = DateTime.UtcNow.ToString("o"),
        };
}
