namespace TechOneNumberToWordsApp.Web.Middleware;

/// <summary>
/// Adds the security headers to every response (D34). They are set in <c>Response.OnStarting</c>, which runs just before
/// the response is sent, so they are still there when the exception handler clears the response and writes its own.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    // The script and style rules need separate files, not inline code, which is how the page is built (D34).
    private static readonly KeyValuePair<string, string>[] Headers =
    [
        new("Content-Security-Policy", "default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'"),
        new("X-Content-Type-Options", "nosniff"),
        new("Referrer-Policy", "no-referrer"),
        new("X-Frame-Options", "DENY"),
    ];

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(
            static state =>
            {
                var response = (HttpResponse)state;
                foreach (var (name, value) in Headers)
                {
                    response.Headers[name] = value;
                }

                return Task.CompletedTask;
            },
            context.Response);

        return next(context);
    }
}
