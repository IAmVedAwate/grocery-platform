var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
// Application/Infrastructure/Shared module registrations land here as each
// vertical slice is implemented (see docs/architecture/backend-architecture.md).

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Liveness: is the process up. Readiness: is the process able to serve traffic
// (extended with a database connectivity check once Infrastructure/EF Core
// are wired in Phase 1). See docs/PRD.md §19 Observability.
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();

// Exposed so WebApplicationFactory<Program> in the Integration test project
// can host this API in-process (see backend/tests/Integration).
public partial class Program;
