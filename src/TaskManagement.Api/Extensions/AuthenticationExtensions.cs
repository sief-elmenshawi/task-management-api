using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.Api.Extensions;
using TaskManagement.Infrastructure.Authentication;

namespace TaskManagement.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration
            .GetSection(JwtSettings.SectionName)
            .Get<JwtSettings>() ?? new JwtSettings();

        services
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

        services.AddAuthorization();

        return services;
    }
}
