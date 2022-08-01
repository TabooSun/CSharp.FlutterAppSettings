using System.Text.Json;
using System.Text.Json.Serialization;
using CommandLine;
using FlutterAppSettings.Enums;
using FlutterAppSettings.Models;

namespace FlutterAppSettings.Command;

[Verb("bootstrap", HelpText = "Bootstrap the project with a new configuration.")]
public class BootstrapCommand : BaseCommand
{
    private const string AppSettingsFileName = "appsettings";
    private const string AppSettingsJsonFileExtension = ".json";
    private const string AppSettingsJsonFileName = $"{AppSettingsFileName}{AppSettingsJsonFileExtension}";

    [Option('c', "config", HelpText = $"The configuration for the generated {AppSettingsJsonFileName}.")]
    public EnvironmentConfig Config { get; set; }

    public override async Task ExecuteAsync()
    {
        await CreateConfigurationFileAsync();
    }

    private async Task CreateConfigurationFileAsync()
    {
        var filePath = Path.Combine(GetFlutterProjectPath(),
            $"{AppSettingsFileName}{Config.GetFileExtension()}{AppSettingsJsonFileExtension}");
        if (File.Exists(filePath))
        {
            Console.WriteLine($"File {filePath} already exists. Terminating operation...");
        }

        var flutterAppSettings = new Models.FlutterAppSettings();
        await using var fileStream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(fileStream, flutterAppSettings, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = ScreamingSnakeCaseNamingPolicy.ScreamingSnakeCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }
}