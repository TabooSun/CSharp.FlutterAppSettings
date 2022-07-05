using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml;
using CaseExtensions;
using CommandLine;
using FlutterAppSettings.Enums;

namespace FlutterAppSettings.Command;

[Verb("reflect", HelpText = "Reflect the current settings to Run/Debug Configurations as well as Xcode Scheme.")]
public class ReflectCommand : BaseCommand
{
    public override Task ExecuteAsync()
    {
        var flutterProjectRoot = GetFlutterProjectPath();
        var flutterAppSettings = GetFlutterAppSettings(flutterProjectRoot);
        var additionalArgs = ComputeAdditionalArgs(flutterAppSettings);
        
        ReflectFlutterRunDebugConfigurations(flutterProjectRoot, additionalArgs);

        return Task.CompletedTask;
    }

    private void ReflectFlutterRunDebugConfigurations(string flutterProjectRoot, string additionalArgs)
    {
        XmlDocument doc = new XmlDocument();
        var filename = Path.Combine(flutterProjectRoot, ".run", "main.dart.run.xml");
        doc.Load(filename);

        var componentNode = doc.FirstChild;
        var configurationNode = componentNode!.FirstChild;
        foreach (XmlNode childNode in configurationNode!.ChildNodes)
        {
            if (childNode.Attributes?.GetNamedItem("name")?.Value != "additionalArgs")
            {
                continue;
            }

            childNode.Attributes!.GetNamedItem("value")!.Value = additionalArgs;
        }
        
        doc.Save(filename);
    }

    private string ComputeAdditionalArgs(Models.FlutterAppSettings flutterAppSettings)
    {
        var sb = new StringBuilder();
        if (flutterAppSettings.Web != null)
        {
            sb.Append($" --{nameof(flutterAppSettings.Web.WebRenderer).ToKebabCase()} {flutterAppSettings.Web.WebRenderer}");
            sb.Append($" --{nameof(flutterAppSettings.Web.WebPort).ToKebabCase()} {flutterAppSettings.Web.WebPort}");
        }

        if (flutterAppSettings.DartDefines != null)
        {
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
        }

        return sb.ToString().TrimStart();
    }

    private Models.FlutterAppSettings GetFlutterAppSettings(string flutterProjectRoot)
    {
        var allAppSettingFiles = Directory.EnumerateFiles(flutterProjectRoot, "appsettings.*")
            .Where(x => x.EndsWith(".json"));
        var allAppSettingsFileMap =
            allAppSettingFiles.ToDictionary(x => x.GetEnvironmentConfig(), LoadFlutterAppSettingsFromFile);
        var devAppSettings = allAppSettingsFileMap[EnvironmentConfig.Dev];
        var localAppSettings = allAppSettingsFileMap[EnvironmentConfig.Local];
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

        if (devAppSettings.DartDefines != null)
        {
            foreach (var (key, value) in devAppSettings.DartDefines)
            {
                appSettings.DartDefines[key] = value;
            }
        }

        if (localAppSettings.DartDefines != null)
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