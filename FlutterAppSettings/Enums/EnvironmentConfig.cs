namespace FlutterAppSettings.Enums;

public enum EnvironmentConfig
{
    Local,
    Dev,
}

internal static class EnvironmentConfigExtensions
{
    public static string GetFileExtension(this EnvironmentConfig config)
    {
        return config switch
        {
            EnvironmentConfig.Local => "",
            EnvironmentConfig.Dev => ".dev",
            _ => throw new ArgumentOutOfRangeException(nameof(config), config, null)
        };
    }

    public static EnvironmentConfig GetEnvironmentConfig(this string filePath)
    {
        var fileNameWithEnvironmentConfig = Path.GetFileNameWithoutExtension(filePath);
        return Path.GetExtension(fileNameWithEnvironmentConfig) switch
        {
            "" => EnvironmentConfig.Local,
            ".dev" => EnvironmentConfig.Dev,
            _ => throw new ArgumentOutOfRangeException(nameof(filePath), filePath, null)
        };
    }
}