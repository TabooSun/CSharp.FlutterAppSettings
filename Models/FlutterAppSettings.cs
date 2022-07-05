using System.Text.Json;
using System.Text.Json.Serialization;
using CaseExtensions;

namespace FlutterAppSettings.Models;

public class FlutterAppSettings
{
    [JsonPropertyName("WEB")]
    public WebAppSettings? Web { get; set; }
    
    [JsonPropertyName("DART_DEFINES")]
    public Dictionary<string, JsonElement>? DartDefines { get; set; }
    
    public class WebAppSettings
    {
        public const int DefaultWebPort = 30001;
        public const string DefaultWebRenderer = "canvaskit";
        
        public int WebPort { get; set; } = DefaultWebPort;

        public string WebRenderer { get; set; } = DefaultWebRenderer;
    }
}


class ScreamingSnakeCaseNamingPolicy : JsonNamingPolicy
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