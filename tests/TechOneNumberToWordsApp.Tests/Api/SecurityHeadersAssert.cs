namespace TechOneNumberToWordsApp.Tests.Api;

/// <summary>
/// The four security headers every response must carry, with their exact values (D34). Shared by the tests of the API's
/// own responses and of the static files, so the expected values exist once.
/// </summary>
internal static class SecurityHeadersAssert
{
    private static readonly (string Name, string Value)[] Expected =
    [
        ("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'"),
        ("X-Content-Type-Options", "nosniff"),
        ("Referrer-Policy", "no-referrer"),
        ("X-Frame-Options", "DENY"),
    ];

    /// <summary>Each header is present once, with exactly the agreed value.</summary>
    public static void Present(HttpResponseMessage response)
    {
        foreach (var (name, value) in Expected)
        {
            Assert.True(response.Headers.TryGetValues(name, out var values), $"Missing header {name}.");
            Assert.Equal(value, Assert.Single(values));
        }
    }
}
