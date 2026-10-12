using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Api.Extensions;
using TaskManagement.Api.Middleware;
using TaskManagement.Application;
using TaskManagement.Infrastructure;
using TaskManagement.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddLocalConfiguration(builder.Environment);

builder.Services.AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = ModelStateProblemDetails.Create(context.ModelState);

            return new ObjectResult(problemDetails)
            {
                StatusCode = StatusCodes.Status400BadRequest,
                ContentTypes = { "application/problem+json" }
            };
        };
    });
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddApiHealthChecks();
builder.Services.AddApiAuthentication(builder.Configuration);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddSwaggerDocs();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

var swaggerEnabled = builder.Configuration.GetValue("Swagger:Enabled", true);

if (swaggerEnabled)
{
    app.UseSwaggerDocs();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

var httpsConfigured = builder.Configuration["ASPNETCORE_HTTPS_PORT"] is not null
    || (builder.Configuration["ASPNETCORE_URLS"]?.Contains("https://", StringComparison.OrdinalIgnoreCase) ?? false);

if (httpsConfigured)
{
    app.UseHttpsRedirection();
}

app.UseCors("Default");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

app.MapApiHealthChecks();

await DatabaseInitializer.InitializeAsync(app.Services);

await app.RunAsync();
