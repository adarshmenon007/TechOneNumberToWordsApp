using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TechOneNumberToWordsApp.Tests.Api;

/// <summary>
/// Starts the web application in memory for the API tests (D45). It runs in the <c>Testing</c> environment unless a
/// test asks for another: <c>WebApplicationFactory</c> would otherwise pick Development, which turns on the developer
/// exception page and would hide the 500 behaviour the 500 test checks.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly Action<IServiceCollection>? _configureServices;

    public ApiFactory()
        : this("Testing")
    {
    }

    // Internal so xUnit sees one public constructor, as it requires of a fixture. Tests that need another
    // environment create the factory themselves.
    // The optional services action lets a test replace a registration, for example the converter in the 500 test (D23).
    internal ApiFactory(string environment, Action<IServiceCollection>? configureServices = null)
    {
        _environment = environment;
        _configureServices = configureServices;
    }

    /// <summary>Everything the application logs while this factory is running.</summary>
    public InMemoryLogProvider Logs { get; } = new();

    /// <summary>
    /// A client for the in-memory server. Its address is https (D35), so the HTTPS redirect can never interfere,
    /// and it does not follow redirects, so a stray one would show up as a failure.
    /// </summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        if (_configureServices is not null)
        {
            builder.ConfigureTestServices(_configureServices);
        }

        builder.ConfigureLogging(logging =>
        {
            // Only our provider, so a test sees every entry and the test output stays quiet.
            logging.ClearProviders();
            logging.AddProvider(Logs);
        });
    }
}
