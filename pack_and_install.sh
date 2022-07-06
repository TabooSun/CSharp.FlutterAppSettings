#!/bin/sh

dotnet pack
dotnet tool install -g --add-source ./nupkg FlutterAppSettings
