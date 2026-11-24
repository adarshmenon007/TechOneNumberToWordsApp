// Composition root only (D50). Error handling, headers and limits are added test-first in later steps.
using TechOneNumberToWordsApp.Core;
using TechOneNumberToWordsApp.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// The converter holds no state, so one instance serves every request (D23).
builder.Services.AddSingleton<AmountToWordsConverter>();

var app = builder.Build();

app.MapConvertEndpoint();

app.Run();
