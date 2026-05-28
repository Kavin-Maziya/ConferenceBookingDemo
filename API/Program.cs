using Scalar.AspNetCore;
using Serilog;
using API.Middleware;
// 
//════════════════════════════════════════════════════ 
// Bootstrap Serilog before the host is built. 
// This ensures even startup exceptions are logged. 
// 
//════════════════════════════════════════════════════ 
Log.Logger = new LoggerConfiguration()
.WriteTo.Console()
.CreateLogger();
try
{
    Log.Information("Starting up the Conference Booking API...");
    var builder = WebApplication.CreateBuilder(args);
    // Replace the default .NET logger with Serilog 
    builder.Host.UseSerilog();
    // 
    //════════════════════════════════════════════════════ 
    // BUILDER — Register services 
    // 
    //════════════════════════════════════════════════════ 
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>(); // Day 3 — typed handler
    builder.Services.AddProblemDetails();
    // 
    // Day 2 — standardised errors 
    //════════════════════════════════════════════════════ 
    // TRANSITION — Build() seals the DI container. 
    // Nothing can be registered after this line. 
    // 
    //════════════════════════════════════════════════════ 
    var app = builder.Build();
    // 
    //════════════════════════════════════════════════════ 
    // PIPELINE — Configure the middleware chain. 
    // Order matters. Top to bottom. 
    // 
    //════════════════════════════════════════════════════ 
    app.UseSerilogRequestLogging(); // Logs every HTTP request + final response automatically 
    app.UseExceptionHandler();  // Activates GlobalExceptionHandler — catches all thrown exceptions 
    app.UseStatusCodePages();   // Fills empty 4xx/5xx responses with Problem Details body 
    if (app.Environment.IsDevelopment())
    {
    }
    app.MapOpenApi();
    // Serves /openapi/v1.json 
    app.MapScalarApiReference();  // Serves the Scalar UI at /scalar/v1 
    app.MapControllers();  // Activates attribute routing for all [ApiController] classes 
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start correctly.");
}

finally
{
    Log.CloseAndFlush(); //Ensure all buffered log entries are flushed before application exit. 
}