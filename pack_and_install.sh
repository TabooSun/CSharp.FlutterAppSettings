#!/bin/sh

cd FlutterAppSettings || exit
dotnet pack
dotnet tool install -g --add-source ./nupkg FlutterAppSettings
cd ..
