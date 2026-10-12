using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.Api.Extensions;
using TaskManagement.Api.Middleware;
using TaskManagement.Application;
using TaskManagement.Infrastructure;
using TaskManagement.Infrastructure.Authentication;
using TaskManagement.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var configurationSources = ((IConfigurationBuilder)builder.Configuration).Sources;
var environmentSettingsFile = $"appsettings.{builder.Environment.EnvironmentName}.json";
var environmentSettingsIndex = configurationSources
    .Select((source, index) => (source, index))
    .Where(item => item.source is FileConfigurationSource file
        && string.Equals(file.Path, environmentSettingsFile, StringComparison.OrdinalIgnoreCase))
    .Select(item => item.index)
    .DefaultIfEmpty(configurationSources.Count - 1)
    .Max();

configurationSources.Insert(environmentSettingsIndex + 1, new JsonConfigurationSource
{
    FileProvider = builder.Environment.ContentRootFileProvider,
    Path = "appsettings.Local.json",
    Optional = true,
    ReloadOnChange = true
});

builder.Services.AddControllers()
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

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>() ?? new JwtSettings();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();

                await context.Response.WriteProblemDetailsAsync(
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized.",
                    "Authentication is required to access this resource.",
                    "Unauthorized");
            },
            OnForbidden = context => context.Response.WriteProblemDetailsAsync(
                StatusCodes.Status403Forbidden,
                "Forbidden.",
                "You do not have permission to access this resource.",
                "Forbidden")
        };
    });

builder.Services.AddAuthorization();

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

app.UseHttpsRedirection();

app.UseCors("Default");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await DatabaseInitializer.InitializeAsync(app.Services);

await app.RunAsync();
