# Nuget Ninja (A Hackthon project)

![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)
![Build Status](https://github.com/AiursoftWeb/NugetNinja/actions/workflows/build.yml/badge.svg)

Nuget Ninjia is a tool for detecting dependencies of .NET projects. It analyzes the dependency structure of .NET projects in a directory and builds a directed acyclic graph. And will give some modification suggestions for Nuget packages, so that the dependencies of the project are as concise and up-to-date as possible.

## Usage

After getting the binary, run it directly in the terminal.

```cmd
C:\workspace> ninja.exe

Description:
  Nuget Ninja, a tool for detecting dependencies of .NET projects.

Usage:
  Microsoft.NugetNinja [command] [options]

Options:
  -p, --path <path> (REQUIRED)   Path of the projects to be changed.
  --nuget-server <nuget-server>  If you want to use a customized nuget server instead of the official nuget.org, 
  --token <token>                The PAT token which has privilege to access the nuget server.
  -d, --dry-run                  Preview changes without actually making them
  -v, --verbose                  Show detailed log
  -?, -h, --help                 Show help and usage information

Commands:
  all, all-officials  The command to run all officially supported features.
  remove-deprecated   The command to replace all deprecated packages to new packages.
  upgrade-pkg         The command to upgrade all package references to possible latest and avoid conflicts.
  clean-pkg           The command to clean up possible useless package references.
  clean-prj           The command to clean up possible useless project references.
```

## How to build and run locally

Requirements about how to develop.

* [.NET SDK 6.0](https://github.com/dotnet/core/tree/master/release-notes)

1. Execute `dotnet restore` to restore all .NET dependencies.
2. Execute the following command to build the app:
   * `dotnet publish -c Release -r win-x64   --self-contained` on Windows.
   * `dotnet publish -c Release -r linux-x64 --self-contained` on Linux.
   * `dotnet publish -c Release -r osx-x64   --self-contained` on Mac OS.
3. Execute `dotnet run` to run the app

## Run in Microsoft Visual Studio

1. Open the `.sln` file in the project path.
2. Press `F5`.

## Contributing

There are many ways to contribute to the project: logging bugs, submitting pull requests, reporting issues, and creating suggestions.

Even if you have push rights on the repository, you should create a personal fork and create feature branches there when you need them. This keeps the main repository clean and your personal workflow cruft out of sight.

We're also interested in your feedback for the future of this project. You can submit a suggestion or feature request through the issue tracker. To make this process more effective, we're asking that these include more information to help define them more clearly.