using System.Text.Json;
using System.Text.Json.Serialization;
using CaseExtensions;

namespace FlutterAppSettings.Models;

public class FlutterAppSettings
{
    public WebAppSettings? Web { get; set; }

    public AndroidAppSettings? Android { get; set; }

    public IosAppSettings? Ios { get; set; }

    public RunAppSettings? Run { get; set; }

    public Dictionary<string, JsonElement>? DartDefines { get; set; }

    public class WebAppSettings : DeviceSpecificSettings
    {
        public const int DefaultWebPort = 30001;
        public const string DefaultWebRenderer = "canvaskit";

        public int? WebPort { get; set; }

        public string? WebRenderer { get; set; }
    }

    public class RunAppSettings : DeviceSpecificSettings
    {
    }

    public class AndroidAppSettings : DeviceSpecificSettings
    {
    }

    public class IosAppSettings : DeviceSpecificSettings
    {
    }

    public class DeviceSpecificSettings
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Options { get; set; }

        public List<string>? Flags { get; set; }
    }
}

public class ScreamingSnakeCaseNamingPolicy : JsonNamingPolicy
{
    public static readonly JsonNamingPolicy ScreamingSnakeCase = new ScreamingSnakeCaseNamingPolicy();

    private ScreamingSnakeCaseNamingPolicy()
    {
    }

    public override string ConvertName(string name)
    {
        return name.ToSnakeCase().ToUpper();
    }
}