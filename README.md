# FlutterAppSettings

A tool for updating certain files in the project for reflecting all the defined settings in appsettings.json.

## Installation

### Windows

```powershell
.\pack_and_install.ps1
```

### macOS or Linux

```shell
./pack_and_install.sh
```
You might need to uninstall the previously installed version by running:

```shell
dotnet tool uninstall -g FlutterAppSettings
```

## Usage

Within your project, run:

```shell
flappsettings reflect
```
