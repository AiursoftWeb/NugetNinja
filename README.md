# Nuget Ninja

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://gitlab.aiursoft.cn/aiursoft/nugetninja/-/blob/master/LICENSE)
[![Pipeline stat](https://gitlab.aiursoft.cn/aiursoft/nugetninja/badges/master/pipeline.svg)](https://gitlab.aiursoft.cn/aiursoft/nugetninja/-/pipelines)
[![Test Coverage](https://gitlab.aiursoft.cn/aiursoft/nugetninja/badges/master/coverage.svg)](https://gitlab.aiursoft.cn/aiursoft/nugetninja/-/pipelines)
[![NuGet version (Aiursoft.NugetNinja)](https://img.shields.io/nuget/v/Aiursoft.NugetNinja.svg)](https://www.nuget.org/packages/Aiursoft.NugetNinja/)
[![NuGet version (Aiursoft.NugetNinja.PrBot)](https://img.shields.io/nuget/v/Aiursoft.NugetNinja.PrBot.svg)](https://www.nuget.org/packages/Aiursoft.NugetNinja.PrBot/)
[![ManHours](https://manhours.aiursoft.cn/r/gitlab.aiursoft.cn/aiursoft/NugetNinja.svg)](https://gitlab.aiursoft.cn/aiursoft/NugetNinja/-/commits/master?ref_type=heads)

Nuget Ninja is a tool for detecting dependencies of .NET projects. It analyzes the dependency structure of .NET projects in a directory and builds a directed acyclic graph. And will give some modification suggestions for Nuget packages, so that the dependencies of the project are as concise and up-to-date as possible.

## Installation

Requirements:

1. [.NET 9 SDK](http://dot.net/)

Run the following command to install this tool:

```bash
dotnet tool install --global Aiursoft.NugetNinja
```

## Usage

After getting the binary, run it directly in the terminal.

```cmd
C:\workspace> ninja.exe

Description:
  A tool for detecting dependencies of .NET projects.

Usage:
  ninja [command] [options]

Options:
  -p, --path <path> (REQUIRED)                     Path of the projects to be changed.
  -d, --dry-run                                    Preview changes without actually making them
  -v, --verbose                                    Show detailed log
  --allow-preview                                  Allow using preview versions of packages from Nuget.
  --nuget-server <nuget-server>                    If you want to use a customized nuget server instead of the official nuget.org, you can set it with a value like: https://nuget.myserver/v3/index.json
  --token <token>                                  The PAT token which has privilege to access the nuget server. See: 
                                                   https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate
  --allow-package-version-cross-microsoft-runtime  Allow using NuGet package versions for different Microsoft runtime versions. For example, when using runtime 6.0, it will avoid upgrading packages to 7.0.
  --version                                        Show version information
  -?, -h, --help                                   Show help and usage information

Commands:
  all, all-officials  The command to run all officially supported features.
  fill-properties     The command to fill all missing properties for .csproj files.
  remove-deprecated   The command to replace all deprecated packages to new packages.
  upgrade-pkg         The command to upgrade all package references to possible latest and avoid conflicts.
  clean-pkg           The command to clean up possible useless package references.
  clean-prj           The command to clean up possible useless project references.
  visualize           The command to visualize the dependency relationship, with mermaid markdown.
  expect-files        The command to search for all expected files and add patch the content.

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

## Run locally

Requirements about how to run

1. [.NET 9 SDK](http://dot.net/)
2. Execute `dotnet run` to run the app

## Run in Microsoft Visual Studio

1. Open the `.sln` file in the project path.
2. Press `F5`.

## How to contribute

There are many ways to contribute to the project: logging bugs, submitting pull requests, reporting issues, and creating suggestions.

Even if you with push rights on the repository, you should create a personal fork and create feature branches there when you need them. This keeps the main repository clean and your workflow cruft out of sight.

We're also interested in your feedback on the future of this project. You can submit a suggestion or feature request through the issue tracker. To make this process more effective, we're asking that these include more information to help define them more clearly.
