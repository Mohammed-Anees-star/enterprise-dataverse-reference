using EnterpriseTicketing.Application;
using EnterpriseTicketing.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// .NET isolated worker host. Uses the same Application + Infrastructure DI registration
// as the API so handlers, repositories, and the Service Bus client are configured identically.
var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

await builder.Build().RunAsync().ConfigureAwait(false);
