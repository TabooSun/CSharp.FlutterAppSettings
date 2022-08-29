using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml;
using CaseExtensions;
using CommandLine;
using FlutterAppSettings.Enums;
using FlutterAppSettings.Models;

namespace FlutterAppSettings.Command;

[Verb("reflect", HelpText = "Reflect the current settings to Run/Debug Configurations.")]
public class ReflectCommand : BaseCommand
{
    public override async Task ExecuteAsync()
    {
        var flutterProjectRoot = GetFlutterProjectPath();
        var flutterAppSettings = GetFlutterAppSettings(flutterProjectRoot);
        var additionalArgs = ComputeAdditionalArgs(flutterAppSettings);

        ReflectFlutterRunDebugConfigurations(flutterProjectRoot, flutterAppSettings, additionalArgs);
        ReflectNativeAndroidProjectAsync(flutterProjectRoot, flutterAppSettings);
        await ReflectNativeIosProjectAsync(additionalArgs, flutterAppSettings);

        Console.WriteLine("Done reflecting settings.");
    }

    private async Task ReflectNativeIosProjectAsync(string additionalArgs, Models.FlutterAppSettings flutterAppSettings)
    {
        Console.WriteLine("Reflecting Native iOS Project...");
        using var process = Process.Start(
            new ProcessStartInfo(
                "flutter",
                $"build ios --config-only --no-codesign {additionalArgs.TrimStart()} {GetDeviceSpecificAdditionalArgs(flutterAppSettings.Ios!)}"
            )
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

    private void ReflectNativeAndroidProjectAsync(
        string flutterProjectRoot,
        Models.FlutterAppSettings flutterAppSettings)
    {
        Console.WriteLine("Reflecting Native Android Project...");
        var jetBrainsWorkspaceFilePath = Path.Combine(flutterProjectRoot, "android", ".idea", "workspace.xml");
        if (!File.Exists(jetBrainsWorkspaceFilePath))
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Could not find workspace.xml file in: {jetBrainsWorkspaceFilePath}. Please open the android project with Android Studio/Intellij Idea by running the following command (make sure you are in the project root):");
            sb.AppendLine("\t- Android Studio: studio android");
            sb.AppendLine("\t- Intellij Idea: idea android");
            Console.WriteLine(sb.ToString());
            return;
        }
        ReflectNativeAndroidWorkspaceConfiguration(jetBrainsWorkspaceFilePath, flutterAppSettings);
    }

    private void ReflectNativeAndroidWorkspaceConfiguration(
        string jetBrainsWorkspaceFilePath,
        Models.FlutterAppSettings flutterAppSettings)
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(jetBrainsWorkspaceFilePath);

        XmlElement? component = null;
        var projectNode = doc["project"]!;
        foreach (XmlElement c in projectNode.GetElementsByTagName("component"))
        {
            if (!c.HasAttributes
                || !c.HasAttribute("name")
                || c.GetAttribute("name") != "AndroidGradleBuildConfiguration")
            {
                continue;
            }

            component = c;
        }

        CreateOrUpdateDartDefinesConfiguration();

        doc.Save(jetBrainsWorkspaceFilePath);

        void CreateOrUpdateDartDefinesConfiguration()
        {
            XmlElement? commandLineOptionsElement = null;
            const string commandLineOptionsKey = "COMMAND_LINE_OPTIONS";
            if (component is null)
            {
                var androidGradleBuildConfigurationElement = doc.CreateElement("component");
                projectNode.PrependChild(androidGradleBuildConfigurationElement);
                androidGradleBuildConfigurationElement.SetAttribute("name", "AndroidGradleBuildConfiguration");
                component = androidGradleBuildConfigurationElement;

                commandLineOptionsElement = CreateCommandLineOptionsElement(component);
            }

            if (commandLineOptionsElement is null)
            {
                foreach (XmlElement c in component.GetElementsByTagName("option"))
                {
                    if (!c.HasAttribute("name") || c.GetAttribute("name") != commandLineOptionsKey) continue;

                    commandLineOptionsElement = c;
                    goto AssignDartDefine;
                }

                commandLineOptionsElement ??= CreateCommandLineOptionsElement(component);
            }

            AssignDartDefine:
            commandLineOptionsElement.SetAttribute("value", ComputeAndroidGradleCommandLineOptions(flutterAppSettings));

            XmlElement CreateCommandLineOptionsElement(XmlElement androidGradleBuildConfigurationElement)
            {
                commandLineOptionsElement = doc.CreateElement("option");
                commandLineOptionsElement.SetAttribute("name", commandLineOptionsKey);
                androidGradleBuildConfigurationElement.AppendChild(commandLineOptionsElement);
                return commandLineOptionsElement;
            }
        }
    }

    public string ComputeAndroidGradleCommandLineOptions(Models.FlutterAppSettings flutterAppSettings)
    {
        return
            $"-Pdart-defines={flutterAppSettings.DartDefines!.Select(x => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{x.Key}={ConvertJsonElementToObject(x.Value)}"))).Aggregate((prev, curr) => $"{prev},{curr}")}";
    }

    string GetDeviceSpecificAdditionalArgs(Models.FlutterAppSettings.DeviceSpecificSettings deviceSpecificSettings)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in deviceSpecificSettings.Options!)
        {
            sb.Append($"--{key.ToKebabCase()} {ConvertJsonElementToObject(value)} ");
        }

        foreach (var flag in deviceSpecificSettings.Flags!)
        {
            // DO NOT add '--' prefix here because we are unsure that it's a single character flag.
            sb.Append($"{flag.ToKebabCase()} ");
        }

        return sb.ToString();
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

    private string ComputeAdditionalArgs(Models.FlutterAppSettings flutterAppSettings)
    {
        if (flutterAppSettings.DartDefines is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var (key, jsonElement) in flutterAppSettings.DartDefines)
        {
            var value = ConvertJsonElementToObject(jsonElement);

            sb.Append($" --dart-define={key}={value}");
        }

        return sb.ToString();
    }

    private static object? ConvertJsonElementToObject(JsonElement jsonElement)
    {
        object? value = null;
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
            case JsonValueKind.Undefined:
                value = null;
                break;
            case JsonValueKind.Array:
                value = jsonElement.EnumerateArray().ToList();
                break;
            case JsonValueKind.Object:
                throw new NotImplementedException("Figure out how to support this");
        }

        return value;
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
            Android = new Models.FlutterAppSettings.AndroidAppSettings
            {
                Options = new Dictionary<string, JsonElement>(),
                Flags = new List<string>(),
            },
            Ios = new Models.FlutterAppSettings.IosAppSettings
            {
                Options = new Dictionary<string, JsonElement>(),
                Flags = new List<string>(),
            },
            Run = new Models.FlutterAppSettings.RunAppSettings
            {
                Options = new Dictionary<string, JsonElement>(),
                Flags = new List<string>(),
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

        void ConfigureDevDeviceSpecificSettings(
            Models.FlutterAppSettings.DeviceSpecificSettings appDeviceSpecificSettings,
            Models.FlutterAppSettings.DeviceSpecificSettings? devDeviceSpecificSettings
        )
        {
            if (devDeviceSpecificSettings?.Options is not null)
            {
                foreach (var (key, value) in devDeviceSpecificSettings.Options)
                {
                    appDeviceSpecificSettings.Options![key] = value;
                }
            }

            if (devDeviceSpecificSettings?.Flags is not null)
            {
                foreach (var flag in devDeviceSpecificSettings.Flags)
                {
                    appDeviceSpecificSettings.Flags!.Add(flag);
                }
            }
        }

        ConfigureDevDeviceSpecificSettings(appSettings.Android, devAppSettings.Android);
        ConfigureDevDeviceSpecificSettings(appSettings.Ios, devAppSettings.Ios);
        ConfigureDevDeviceSpecificSettings(appSettings.Web, devAppSettings.Web);
        ConfigureDevDeviceSpecificSettings(appSettings.Run, devAppSettings.Run);

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

        void ConfigureLocalDeviceSpecificSettings(
            Models.FlutterAppSettings.DeviceSpecificSettings appDeviceSpecificSettings,
            Models.FlutterAppSettings.DeviceSpecificSettings? devDeviceSpecificSettings
        )
        {
            if (devDeviceSpecificSettings?.Options is not null)
            {
                foreach (var (key, value) in devDeviceSpecificSettings.Options)
                {
                    // Ignore this key pair if it already exists.
                    if (appDeviceSpecificSettings.Options!.ContainsKey(key))
                    {
                        continue;
                    }

                    appDeviceSpecificSettings.Options[key] = value;
                }
            }

            if (devDeviceSpecificSettings?.Flags is not null)
            {
                foreach (var flag in devDeviceSpecificSettings.Flags)
                {
                    // Ignore this flag if it already exists.
                    if (appDeviceSpecificSettings.Flags!.Contains(flag))
                    {
                        continue;
                    }

                    appDeviceSpecificSettings.Flags.Add(flag);
                }
            }
        }

        ConfigureLocalDeviceSpecificSettings(appSettings.Android, localAppSettings.Android);
        ConfigureLocalDeviceSpecificSettings(appSettings.Ios, localAppSettings.Ios);
        ConfigureLocalDeviceSpecificSettings(appSettings.Web, localAppSettings.Web);
        ConfigureLocalDeviceSpecificSettings(appSettings.Run, localAppSettings.Run);

        return appSettings;

        Models.FlutterAppSettings LoadFlutterAppSettingsFromFile(string filePath)
        {
            return JsonSerializer.Deserialize<Models.FlutterAppSettings>(File.ReadAllText(filePath),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = ScreamingSnakeCaseNamingPolicy.ScreamingSnakeCase
                })!;
        }
    }
}