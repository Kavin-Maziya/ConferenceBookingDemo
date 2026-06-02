using Scalar.AspNetCore;
using Serilog;
using API.Middleware;
using API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

//════════════════════════════════════════════════════
// Bootstrap Serilog before the host is built.
// This ensures even startup exceptions are logged.
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

    //════════════════════════════════════════════════════
    // BUILDER — Register services
    //════════════════════════════════════════════════════

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Day 4 — CORS: tells the browser that requests from the Next.js dev server are permitted.
    // CHANGED: Fixed typo in origin (was "localhost:300") and renamed policy to "NextJsPolicy"
    // to match the frontend it is actually serving.
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("NextJsPolicy", policy =>
        {
            policy.WithOrigins("http://localhost:3000") // Next.js dev server
                  .AllowAnyHeader()                     // Allows Authorization, Content-Type, etc.
                  .AllowAnyMethod();                    // Allows GET, POST, PUT, DELETE, OPTIONS
        });
    });

    // Day 4 — CHANGED: Secret is now read from appsettings.Development.json instead of being
    // hardcoded here. In production this would come from Azure Key Vault or AWS Secrets Manager.
    var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]!;

    // Day 4 — Registers the JWT Bearer scheme.
    // Teaches the pipeline to read "Authorization: Bearer <token>" on every request
    // and validate it before any controller action runs.
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,           // Not required — we are the only issuer
                ValidateAudience = false,          // Not required — single API consumer
                ValidateLifetime = true,           // Reject tokens past their expiry
                ValidateIssuerSigningKey = true,   // Verify the signature using our secret
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSecretKey))
            };
        });

    // Day 4 — Required for [Authorize(Roles = "...")] attributes to evaluate correctly.
    builder.Services.AddAuthorization();

    // Day 4 — CHANGED: Register AuthService so AuthController can receive it via DI.
    // Previously AuthController built tokens itself with a hardcoded key.
    // Now the controller delegates to this service; token logic lives in one place.
    builder.Services.AddScoped<IAuthService, AuthService>();

    //════════════════════════════════════════════════════
    // TRANSITION — Build() seals the DI container.
    // Nothing can be registered after this line.
    //════════════════════════════════════════════════════
    var app = builder.Build();

    //════════════════════════════════════════════════════
    // PIPELINE — Configure the middleware chain.
    // Order matters. Top to bottom.
    //════════════════════════════════════════════════════

    app.UseSerilogRequestLogging();

    // Day 4 — CORS must sit before auth so that browser preflight OPTIONS requests are
    // handled before the pipeline attempts to validate a Bearer token. Preflight requests
    // carry no token and would otherwise be rejected before CORS headers are written.
    app.UseCors("NextJsPolicy");

    // CHANGED: Moved UseExceptionHandler above UseAuthentication so that any exception
    // thrown during authentication or further down the pipeline is caught and formatted
    // as Problem Details. Previously it sat after auth, leaving auth errors unhandled.
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    // Day 4 — UseAuthentication must come before UseAuthorization.
    // "Who are you?" must be answered before "Are you allowed in?"
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start correctly.");
}
finally
{
    Log.CloseAndFlush();
}
