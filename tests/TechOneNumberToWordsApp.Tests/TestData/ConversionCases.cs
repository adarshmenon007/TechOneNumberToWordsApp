using System.Text.Json;
using System.Text.Json.Serialization;
using TechOneNumberToWordsApp.Core;

namespace TechOneNumberToWordsApp.Tests.TestData;

/// <summary>
/// Loads <c>conversion-cases.json</c> once and checks it (D43). A badly formed data file fails the
/// whole test run with a message naming every problem, rather than silently skipping or misreading a row.
/// Both the unit tests and the API tests read their cases from here (D44).
/// </summary>
internal static class ConversionCases
{
    private const string FileName = "conversion-cases.json";

    private static readonly Lazy<IReadOnlyList<ConversionCase>> AllCases = new(Load);

    // Exact enum member names, so a misspelt or renamed code in the data file fails the run (D43).
    private static readonly HashSet<string> ErrorCodeNames = [.. Enum.GetNames<ConversionErrorCode>()];

    /// <summary>Every case, in file order.</summary>
    public static IReadOnlyList<ConversionCase> All => AllCases.Value;

    /// <summary>Finds a case by its ID. Tests take only the ID so the data stays in the file.</summary>
    public static ConversionCase Get(string id) =>
        All.FirstOrDefault(c => c.Id == id)
        ?? throw new KeyNotFoundException($"No case with ID '{id}' in {FileName}.");

    private static IReadOnlyList<ConversionCase> Load()
    {
        // The csproj copies TestData/*.json next to the test assembly.
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", FileName);

        List<ConversionCase>? cases;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                // An explicit null in a field that cannot be null (e.g. "decisions": null) fails with the row's position.
                RespectNullableAnnotations = true,
                // A misspelt field (e.g. "messageContain") fails the run instead of being ignored.
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            };
            cases = JsonSerializer.Deserialize<List<ConversionCase>>(File.ReadAllText(path), options);
        }
        catch (JsonException ex)
        {
            // ex.Path says where in the file the problem is (e.g. "$[3].input").
            throw new InvalidDataException($"{FileName} is not valid: {ex.Message} (at {ex.Path}).", ex);
        }

        var problems = Validate(cases);
        if (problems.Count > 0)
        {
            throw new InvalidDataException(
                $"{FileName} has {problems.Count} problem(s):{Environment.NewLine}- "
                + string.Join($"{Environment.NewLine}- ", problems));
        }

        return cases!;
    }

    private static List<string> Validate(List<ConversionCase>? cases)
    {
        var problems = new List<string>();

        if (cases is null || cases.Count == 0)
        {
            problems.Add("The file contains no cases.");
            return problems;
        }

        var seen = new HashSet<string>();
        foreach (var c in cases)
        {
            if (string.IsNullOrWhiteSpace(c.Id))
            {
                problems.Add("A row has an empty ID.");
                continue;
            }

            if (!seen.Add(c.Id))
            {
                problems.Add($"{c.Id}: the ID is used more than once.");
            }

            if (c.Decisions.Length == 0)
            {
                problems.Add($"{c.Id}: decisions is empty; link the row to at least one decision (D47).");
            }

            var hasWords = c.ExpectedWords is not null;
            var hasError = c.ExpectedErrorCode is not null;

            if (hasError && !ErrorCodeNames.Contains(c.ExpectedErrorCode!))
            {
                problems.Add($"{c.Id}: expectedErrorCode '{c.ExpectedErrorCode}' is not a member of ConversionErrorCode.");
            }

            if (hasWords == hasError)
            {
                problems.Add(hasWords
                    ? $"{c.Id}: has both expectedWords and expectedErrorCode; it needs exactly one."
                    : $"{c.Id}: has neither expectedWords nor expectedErrorCode; it needs exactly one.");
            }
        }

        return problems;
    }
}
