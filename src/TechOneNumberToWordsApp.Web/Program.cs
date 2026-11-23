// Composition root only (D50). Endpoints, error handling and middleware are added test-first in later slices.
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();
