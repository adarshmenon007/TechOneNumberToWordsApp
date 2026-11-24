// Composition root only (D50): it registers the services and sets up the request pipeline, and holds no logic of its own.
// The order of the pipeline matters; the comments below say what each step is for.
using Microsoft.AspNetCore.Routing;
using TechOneNumberToWordsApp.Core;
using TechOneNumberToWordsApp.Web.Endpoints;
using TechOneNumberToWordsApp.Web.Errors;
using TechOneNumberToWordsApp.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Every request body is limited to 1 KB, for all current and future endpoints (D37). The Server header is removed (D34).
// Kestrel answers an oversized body with an empty 413, which the status code pages turn into ProblemDetails (D41).
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 1024;
});

// The converter holds no state, so one instance serves every request (D23).
builder.Services.AddSingleton<AmountToWordsConverter>();

// Errors with no body (404, 405, 415, a bad request the framework rejects) become ProblemDetails (D24, D41).
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsCustomizer.Customize);

// By default this is true in Development only: a body the framework cannot read (malformed JSON, an amount sent as a
// number) then throws, and the developer exception page answers with a stack trace instead of a 400. False makes
// every environment answer 400 InvalidRequest, as in production (D24, D37).
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

var app = builder.Build();

// First, so every response gets the headers, including errors and, later, static files (D34).
app.UseSecurityHeaders();

// HTTPS: HSTS everywhere except Development (it would stick in a developer's browser), and always redirect plain http
// to https (D35). They come after the headers so the redirect response carries them too.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// One generic 500 in every environment except Development, where the developer exception page (added by the host)
// shows the details instead (D24). The exception is logged by the handler; no try/catch here (D42).
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

// The page and its files from wwwroot, with the default document first (D38). They come after the headers, the redirect
// and the exception handler, so they get the same treatment as the API, and before the status code pages, so a path with
// no file still falls through to the 404 ProblemDetails (D34).
app.UseDefaultFiles();
app.UseStaticFiles();

// Turns every empty error response (400, 404, 405, 413, 415) into ProblemDetails with an errorCode (D41). The 500 is
// written by the exception handler above, through the same customizer.
app.UseStatusCodePages();

app.MapConvertEndpoint();

app.Run();
