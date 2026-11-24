// Composition root only (D50). Error handling, headers and limits are added test-first in later steps.
using TechOneNumberToWordsApp.Core;
using TechOneNumberToWordsApp.Web.Endpoints;
using TechOneNumberToWordsApp.Web.Errors;

var builder = WebApplication.CreateBuilder(args);

// The converter holds no state, so one instance serves every request (D23).
builder.Services.AddSingleton<AmountToWordsConverter>();

// Errors with no body (404, 405, 415, a bad request the framework rejects) become ProblemDetails (D24, D41).
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsCustomizer.Customize);

var app = builder.Build();

app.UseStatusCodePages();

app.MapConvertEndpoint();

app.Run();
