using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml;
using CaseExtensions;
using CommandLine;
using FlutterAppSettings.Enums;

namespace FlutterAppSettings.Command;

[Verb("reflect", HelpText = "Reflect the current settings to Run/Debug Configurations.")]
public class ReflectCommand : BaseCommand
{
    public override async Task ExecuteAsync()
    {
        var flutterProjectRoot = GetFlutterProjectPath();
        var flutterAppSettings = GetFlutterAppSettings(flutterProjectRoot);
        var additionalArgs = ComputeDartDefinesAdditionalArgs(flutterAppSettings);

        ReflectFlutterRunDebugConfigurations(flutterProjectRoot, flutterAppSettings, additionalArgs);
        await ReflectNativeAndroidProjectAsync(flutterProjectRoot, additionalArgs);
        await ReflectNativeIosProjectAsync(additionalArgs);

        Console.WriteLine("Done reflecting settings.");
    }

    private async Task ReflectNativeIosProjectAsync(string additionalArgs)
    {
        Console.WriteLine("Reflecting Native iOS Project...");
        using var process = Process.Start(
            new ProcessStartInfo("flutter", $"build ios --config-only {additionalArgs.TrimStart()}")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
            })!;
        Console.WriteLine($"Running: {process.StartInfo.FileName} {process.StartInfo.Arguments}");
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            Console.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            Console.Error.WriteLine(e.Data);
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
    }

    private async Task ReflectNativeAndroidProjectAsync(string flutterProjectRoot, string additionalArgs)
    {
        Console.WriteLine("Reflecting Native Android Project...");
        const string buildApkScriptFileName = "buildapk.sh";
        var buildApkShellScriptFilePath = Path.Combine(flutterProjectRoot, "android", buildApkScriptFileName);
        await using var fileStream = File.Create(buildApkShellScriptFilePath);
        await using var manifestResourceStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"{nameof(FlutterAppSettings)}.Assets.{buildApkScriptFileName}")!;
        await manifestResourceStream.CopyToAsync(fileStream);

        var bytes = Encoding.UTF8.GetBytes(GetFlutterBuildCommand());
        await fileStream.WriteAsync(bytes);

        string GetFlutterBuildCommand()
        {
            return $"\nflutter build apk --debug {additionalArgs.TrimStart()}";
        }
    }

    private void ReflectFlutterRunDebugConfigurations(
        string flutterProjectRoot,
        Models.FlutterAppSettings flutterAppSettings,
        string additionalArgs)
    {
        Console.WriteLine("Reflecting Flutter Run/Debug Configurations...");
        var configurationFilePath = Path.Combine(flutterProjectRoot, ".run", "main.dart.run.xml");
        ReflectRunDebugConfigurations(configurationFilePath,
            ComputeWebAdditionalArgs(flutterAppSettings) + additionalArgs);
    }

    private void ReflectRunDebugConfigurations(string configurationFilePath, string additionalArgs)
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(configurationFilePath);

        var componentNode = doc.FirstChild;
        var configurationNode = componentNode!.FirstChild;
        foreach (XmlNode childNode in configurationNode!.ChildNodes)
        {
            if (childNode.Attributes?.GetNamedItem("name")?.Value != "additionalArgs")
            {
                continue;
            }

            childNode.Attributes!.GetNamedItem("value")!.Value = additionalArgs.TrimStart();
        }

        doc.Save(configurationFilePath);
    }

    private string ComputeDartDefinesAdditionalArgs(Models.FlutterAppSettings flutterAppSettings)
    {
        if (flutterAppSettings.DartDefines is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var (key, jsonElement) in flutterAppSettings.DartDefines)
        {
            object? value;
            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.String:
                    value = jsonElement.GetString();
                    break;
                case JsonValueKind.Number:
                    value = jsonElement.GetInt64();
                    break;
                case JsonValueKind.False:
                case JsonValueKind.True:
                    value = jsonElement.GetBoolean().ToString().ToLower();
                    break;
                case JsonValueKind.Null:
                    value = null;
                    break;
                default:
                    throw new NotImplementedException($"Unsupported value kind: {jsonElement.ValueKind}");
            }

            sb.Append($" --dart-define={key}={value}");
        }

        return sb.ToString();
    }

    private static string ComputeWebAdditionalArgs(Models.FlutterAppSettings flutterAppSettings)
    {
        if (flutterAppSettings.Web is null) return string.Empty;

        var sb = new StringBuilder();
        sb.Append(
            $" --{nameof(flutterAppSettings.Web.WebRenderer).ToKebabCase()} {flutterAppSettings.Web.WebRenderer}");
        sb.Append($" --{nameof(flutterAppSettings.Web.WebPort).ToKebabCase()} {flutterAppSettings.Web.WebPort}");
        return sb.ToString();
    }

    private Models.FlutterAppSettings GetFlutterAppSettings(string flutterProjectRoot)
    {
        var allAppSettingFiles = Directory.EnumerateFiles(flutterProjectRoot, "appsettings.*")
            .Where(x => x.EndsWith(".json"));
        var allAppSettingsFileMap =
            allAppSettingFiles.ToDictionary(x => x.GetEnvironmentConfig(), LoadFlutterAppSettingsFromFile);
        var devAppSettings = allAppSettingsFileMap.ContainsKey(EnvironmentConfig.Dev)
            ? allAppSettingsFileMap[EnvironmentConfig.Dev]
            : new Models.FlutterAppSettings();
        var localAppSettings = allAppSettingsFileMap.ContainsKey(EnvironmentConfig.Local)
            ? allAppSettingsFileMap[EnvironmentConfig.Local]
            : new Models.FlutterAppSettings();
        var appSettings = new Models.FlutterAppSettings
        {
            Web = new Models.FlutterAppSettings.WebAppSettings
            {
                WebPort = devAppSettings.Web?.WebPort ??
                          localAppSettings.Web?.WebPort ?? Models.FlutterAppSettings.WebAppSettings.DefaultWebPort,
                WebRenderer = devAppSettings.Web?.WebRenderer ??
                              localAppSettings.Web?.WebRenderer ??
                              Models.FlutterAppSettings.WebAppSettings.DefaultWebRenderer,
            },
            DartDefines = new Dictionary<string, JsonElement>()
        };

        if (devAppSettings.DartDefines is not null)
        {
            foreach (var (key, value) in devAppSettings.DartDefines)
            {
                appSettings.DartDefines[key] = value;
            }
        }

        if (localAppSettings.DartDefines is not null)
        {
            foreach (var (key, value) in localAppSettings.DartDefines)
            {
                // Ignore this key pair if it already exists.

                if (appSettings.DartDefines.ContainsKey(key))
                {
                    continue;
                }

                appSettings.DartDefines[key] = value;
            }
        }

        return appSettings;

        Models.FlutterAppSettings LoadFlutterAppSettingsFromFile(string filePath)
        {
            return JsonSerializer.Deserialize<Models.FlutterAppSettings>(File.ReadAllText(filePath))!;
        }
    }
}