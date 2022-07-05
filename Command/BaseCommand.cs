using CommandLine;

namespace FlutterAppSettings.Command;

public abstract class BaseCommand
{
    private const string FlutterRootProjectSentinel = "pubspec.yaml";

    [Option('p', "path", Required = false, HelpText = "The project path.")]
    public string? ProjectPath { get; set; }

    public abstract Task ExecuteAsync();

    public string GetFlutterProjectPath()
    {
        if (ProjectPath == null)
        {
            return FindFlutterProjectPath();
        }

        if (CheckForFlutterRootProjectSentinel(ProjectPath)) return ProjectPath;

        throw new ArgumentException($"Invalid flutter project. No {FlutterRootProjectSentinel} found.");
    }

    private bool CheckForFlutterRootProjectSentinel(string projectPath)
    {
        return File.Exists(Path.Combine(projectPath, FlutterRootProjectSentinel));
    }

    private string FindFlutterProjectPath()
    {
        var projectPath = Directory.GetCurrentDirectory();
        while (projectPath != null)
        {
            if (CheckForFlutterRootProjectSentinel(projectPath))
            {
                return projectPath;
            }

            projectPath = Directory.GetParent(projectPath)?.ToString();
        }

        throw new InvalidOperationException("Unable to find flutter project.");
    }
}