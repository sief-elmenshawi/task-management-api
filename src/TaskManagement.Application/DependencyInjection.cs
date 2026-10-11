using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Authentication;

namespace TaskManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<IAuthService>(ServiceLifetime.Scoped);
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
