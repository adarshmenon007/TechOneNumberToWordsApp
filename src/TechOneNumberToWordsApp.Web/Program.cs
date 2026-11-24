// Composition root only (D50). Error handling, headers and limits are added test-first in later steps.
using Microsoft.AspNetCore.Routing;
using TechOneNumberToWordsApp.Core;
using TechOneNumberToWordsApp.Web.Endpoints;
using TechOneNumberToWordsApp.Web.Errors;
using TechOneNumberToWordsApp.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

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

// One generic 500 in every environment except Development, where the developer exception page (added by the host)
// shows the details instead (D24). The exception is logged by the handler; no try/catch here (D42).
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();

app.MapConvertEndpoint();

app.Run();
