using DevOpsCopilot.Models.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevOpsCopilot.Services;

/// <summary>
/// Best-guess field mapping service. Matches user-provided field names to
/// ADO reference names using ranked strategies:
/// 1. Exact configured mapping (highest confidence)
/// 2. Normalized short-name match
/// 3. Display-name match (case-insensitive)
/// 4. Fuzzy / partial match (lowest confidence — requires user confirmation)
/// </summary>
public sealed class MappingService
{
    private readonly IOptionsMonitor<CustomFieldConfiguration> _fieldConfig;
    private readonly ILogger<MappingService> _logger;

    /// <summary>Confidence threshold below which the match should be confirmed by the user.</summary>
    private const double LowConfidenceThreshold = 0.6;

    public MappingService(
        IOptionsMonitor<CustomFieldConfiguration> fieldConfig,
        ILogger<MappingService> logger)
    {
        _fieldConfig = fieldConfig;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to resolve a user-provided field name to an ADO reference name.
    /// Returns a <see cref="FieldMatchResult"/> with the best match and confidence.
    /// </summary>
    public FieldMatchResult ResolveFieldName(string userInput, string? workItemType = null)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return FieldMatchResult.NoMatch(userInput);

        var mappings = _fieldConfig.CurrentValue.FieldMappings;

        // Filter by work item type if provided
        var candidates = workItemType is not null
            ? mappings.Where(f =>
                f.WorkItemTypes.Count == 0 ||
                f.WorkItemTypes.Contains(workItemType, StringComparer.OrdinalIgnoreCase))
                .ToList()
            : mappings;

        var normalized = Normalize(userInput);

        // Strategy 1: Exact reference name match
        var exact = candidates.FirstOrDefault(f =>
            string.Equals(f.ReferenceName, userInput, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return FieldMatchResult.Match(exact, 1.0, "Exact reference name match");

        // Strategy 2: Exact short name match
        var shortMatch = candidates.FirstOrDefault(f =>
            string.Equals(Normalize(f.ShortName), normalized, StringComparison.OrdinalIgnoreCase));
        if (shortMatch is not null)
            return FieldMatchResult.Match(shortMatch, 0.95, "Short name match");

        // Strategy 3: Display name match (case-insensitive)
        var displayMatch = candidates.FirstOrDefault(f =>
            string.Equals(Normalize(f.DisplayName), normalized, StringComparison.OrdinalIgnoreCase));
        if (displayMatch is not null)
            return FieldMatchResult.Match(displayMatch, 0.9, "Display name match");

        // Strategy 4: Partial / contains match
        var partialMatches = candidates
            .Select(f => new
            {
                Field = f,
                Score = ComputeSimilarity(normalized, f),
            })
            .Where(x => x.Score > 0.3)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (partialMatches.Count > 0)
        {
            var best = partialMatches[0];
            var confidence = Math.Min(best.Score, 0.75); // Cap fuzzy match confidence
            var strategy = confidence >= LowConfidenceThreshold
                ? "Fuzzy match"
                : "Low-confidence fuzzy match";

            _logger.LogInformation(
                "Field '{UserInput}' mapped to '{RefName}' ({Strategy}, confidence: {Confidence:P0})",
                userInput, best.Field.ReferenceName, strategy, confidence);

            return FieldMatchResult.Match(best.Field, confidence, strategy);
        }

        _logger.LogWarning("No field match found for '{UserInput}'", userInput);
        return FieldMatchResult.NoMatch(userInput);
    }

    /// <summary>
    /// Returns true if the match confidence is too low for automatic application.
    /// The caller should ask the user for confirmation.
    /// </summary>
    public static bool RequiresConfirmation(FieldMatchResult result) =>
        result.IsMatch && result.Confidence < LowConfidenceThreshold;

    /// <summary>
    /// Computes a similarity score between the user input and a field mapping.
    /// </summary>
    private static double ComputeSimilarity(string normalizedInput, CustomFieldMapping field)
    {
        var targets = new[]
        {
            Normalize(field.ShortName),
            Normalize(field.DisplayName),
            Normalize(ExtractLastSegment(field.ReferenceName)),
        };

        double bestScore = 0;
        foreach (var target in targets)
        {
            if (string.IsNullOrEmpty(target)) continue;

            // Contains check
            if (target.Contains(normalizedInput, StringComparison.OrdinalIgnoreCase) ||
                normalizedInput.Contains(target, StringComparison.OrdinalIgnoreCase))
            {
                var containsScore = (double)Math.Min(normalizedInput.Length, target.Length) /
                                    Math.Max(normalizedInput.Length, target.Length);
                bestScore = Math.Max(bestScore, containsScore * 0.8);
            }

            // Levenshtein-based similarity
            var distance = LevenshteinDistance(normalizedInput, target);
            var maxLen = Math.Max(normalizedInput.Length, target.Length);
            var similarity = maxLen > 0 ? 1.0 - (double)distance / maxLen : 0;
            bestScore = Math.Max(bestScore, similarity);
        }

        return bestScore;
    }

    private static string Normalize(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Replace(" ", "").Replace("_", "").Replace("-", "").Replace(".", "").ToLowerInvariant();

    private static string ExtractLastSegment(string referenceName) =>
        referenceName.Contains('.') ? referenceName[(referenceName.LastIndexOf('.') + 1)..] : referenceName;

    /// <summary>
    /// Standard Levenshtein edit distance.
    /// </summary>
    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var dp = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[a.Length, b.Length];
    }
}

/// <summary>
/// Result of attempting to map a user-provided field name to an ADO field.
/// </summary>
public sealed class FieldMatchResult
{
    public bool IsMatch { get; init; }
    public string UserInput { get; init; } = string.Empty;
    public CustomFieldMapping? Field { get; init; }
    public double Confidence { get; init; }
    public string Strategy { get; init; } = string.Empty;

    public static FieldMatchResult Match(CustomFieldMapping field, double confidence, string strategy) =>
        new()
        {
            IsMatch = true,
            UserInput = field.ShortName,
            Field = field,
            Confidence = confidence,
            Strategy = strategy,
        };

    public static FieldMatchResult NoMatch(string userInput) =>
        new()
        {
            IsMatch = false,
            UserInput = userInput,
            Confidence = 0,
            Strategy = "No match found",
        };
}
