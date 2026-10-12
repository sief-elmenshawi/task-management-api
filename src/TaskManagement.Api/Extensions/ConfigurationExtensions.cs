using Microsoft.Extensions.Configuration.Json;

namespace TaskManagement.Api.Extensions;

public static class ConfigurationExtensions
{
    public static IConfigurationBuilder AddLocalConfiguration(
        this IConfigurationBuilder configuration,
        IWebHostEnvironment environment)
    {
        var configurationSources = configuration.Sources;
        var environmentSettingsFile = $"appsettings.{environment.EnvironmentName}.json";
        var environmentSettingsIndex = configurationSources
            .Select((source, index) => (source, index))
            .Where(item => item.source is FileConfigurationSource file
                && string.Equals(file.Path, environmentSettingsFile, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index)
            .DefaultIfEmpty(configurationSources.Count - 1)
            .Max();

        configurationSources.Insert(environmentSettingsIndex + 1, new JsonConfigurationSource
        {
            FileProvider = environment.ContentRootFileProvider,
            Path = "appsettings.Local.json",
            Optional = true,
            ReloadOnChange = true
        });

        return configuration;
    }
}
