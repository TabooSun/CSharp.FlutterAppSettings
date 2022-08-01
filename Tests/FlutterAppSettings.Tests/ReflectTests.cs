using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FlutterAppSettings.Command;
using FlutterAppSettings.Models;
using NUnit.Framework;

namespace FlutterAppSettings.Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void ComputeAndroidGradleCommandLineOptions()
    {
        // Arrange
        var reflectCommand = new ReflectCommand
        {
            ProjectPath = FindProjectPath()
        };
        const string flutterAppSettingsJson = @"{
          ""DART_DEFINES"": {
                ""APP_USE_MOCK_SERVICE"": false,
                ""APP_INITIAL_ENDPOINT"": ""PLAYGROUND""
            }
        }";
        var flutterAppSettings = JsonSerializer.Deserialize<Models.FlutterAppSettings>(flutterAppSettingsJson,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = ScreamingSnakeCaseNamingPolicy.ScreamingSnakeCase
            })!;

        // Act
        var androidCommandLineOption = reflectCommand.ComputeAndroidGradleCommandLineOptions(flutterAppSettings);

        // Assert
        androidCommandLineOption = androidCommandLineOption.Replace("-Pdart-defines=", "");
        var dartDefines = androidCommandLineOption.Split(',');
        Assert.AreEqual("APP_USE_MOCK_SERVICE=false",
            Encoding.UTF8.GetString(Convert.FromBase64String(dartDefines[0])));
        Assert.AreEqual("APP_INITIAL_ENDPOINT=PLAYGROUND",
            Encoding.UTF8.GetString(Convert.FromBase64String(dartDefines[1])));
    }

    private static string FindProjectPath()
    {
        var currentTestProjectDir = FindCurrentTestProjectPath();
        if (currentTestProjectDir is null)
        {
            throw new Exception("Could not find current test project path");
        }

        return Path.Combine(currentTestProjectDir.Parent!.FullName, "TestTarget", "test_target1");
    }

    private static DirectoryInfo? FindCurrentTestProjectPath()
    {
        var currentTestProjectName = Assembly.GetExecutingAssembly().GetName().Name;
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir.Name != currentTestProjectName)
        {
            dir = dir.Parent;
            if (dir is null)
            {
                return null;
            }
        }

        return dir;
    }
}