using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.Application.Authentication;
using TaskManagement.Application.Users;

namespace TaskManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<IAuthService>(ServiceLifetime.Scoped);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
