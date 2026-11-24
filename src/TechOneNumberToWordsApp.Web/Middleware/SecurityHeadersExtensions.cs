namespace TechOneNumberToWordsApp.Web.Middleware;

public static class SecurityHeadersExtensions
{
    /// <summary>Adds the security headers to every response. Call it first, before the exception handler (D34).</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
