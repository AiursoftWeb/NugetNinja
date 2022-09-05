# Nuget Ninja

![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)
[![Build status](https://dev.azure.com/aiursoft/Star/_apis/build/status/NugetNinja%20Build)](https://dev.azure.com/aiursoft/Star/_build/latest?definitionId=18)
[![NuGet version (Aiursoft.NugetNinja)](https://img.shields.io/nuget/v/Aiursoft.NugetNinja.svg?style=flat-square)](https://www.nuget.org/packages/Aiursoft.NugetNinja/)
[![NuGet version (Aiursoft.NugetNinja.PrBot)](https://img.shields.io/nuget/v/Aiursoft.NugetNinja.PrBot.svg?style=flat-square)](https://www.nuget.org/packages/Aiursoft.NugetNinja.PrBot/)

Nuget Ninjia is a tool for detecting dependencies of .NET projects. It analyzes the dependency structure of .NET projects in a directory and builds a directed acyclic graph. And will give some modification suggestions for Nuget packages, so that the dependencies of the project are as concise and up-to-date as possible.

## Install

Run the following command to install this tool:

```bash
dotnet tool install --global Aiursoft.NugetNinja
```

## Usage

After getting the binary, run it directly in the terminal.

```cmd
C:\workspace> ninja.exe

Description:
  Nuget Ninja, a tool for detecting dependencies of .NET projects.

Usage:
  ninja [command] [options]

Options:
  -p, --path <path> (REQUIRED)   Path of the projects to be changed.
  -d, --dry-run                  Preview changes without actually making them
  -v, --verbose                  Show detailed log
  --allow-preview                Allow using preview versions of packages from Nuget.
  --nuget-server <nuget-server>  If you want to use a customized nuget server instead of the official nuget.org, you can set it with a value like: https://nuget.myserver/v3/index.json
  --token <token>                The PAT token which has privilege to access the nuget server. See: https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate
  --version                      Show version information
  -?, -h, --help                 Show help and usage information

Commands:
  all, all-officials  The command to run all officially supported features.
  fill-properties     The command to fill all missing properties for .csproj files.
  remove-deprecated   The command to replace all deprecated packages to new packages.
  upgrade-pkg         The command to upgrade all package references to possible latest and avoid conflicts.
  clean-pkg           The command to clean up possible useless package references.
  clean-prj           The command to clean up possible useless project references.
```

### Sample

Generate suggestions for the current workspace without modifying local files:

```cmd
C:\workspace> ninja.exe all --path . --dry-run
```

Fill missing properties for current workspace:

```cmd
C:\workspace> ninja.exe fill-properties --path .
```

Run all plugins under the current folder:

```cmd
C:\workspace> ninja.exe all --path .
```

## How to build and run locally

Requirements about how to develop.

* [.NET SDK 6.0](https://github.com/dotnet/core/tree/master/release-notes)

1. Execute `dotnet restore` to restore all .NET dependencies.
2. Execute the following command to build the app:
   * `dotnet publish -c Release -r win-x64` on Windows.
   * `dotnet publish -c Release -r linux-x64` on Linux.
   * `dotnet publish -c Release -r osx-x64` on Mac OS.
3. Execute `dotnet run` to run the app

## Run in Microsoft Visual Studio

1. Open the `.sln` file in the project path.
2. Press `F5`.

## Contributing

There are many ways to contribute to the project: logging bugs, submitting pull requests, reporting issues, and creating suggestions.

Even if you have push rights on the repository, you should create a personal fork and create feature branches there when you need them. This keeps the main repository clean and your personal workflow cruft out of sight.

We're also interested in your feedback for the future of this project. You can submit a suggestion or feature request through the issue tracker. To make this process more effective, we're asking that these include more information to help define them more clearly.
