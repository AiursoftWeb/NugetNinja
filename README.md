# Nuget Ninja

[![MIT licensed](https://img.shields.io/badge/license-MIT-blue.svg)](https://gitlab.aiursoft.com/aiursoft/nugetninja/-/blob/master/LICENSE)
[![Pipeline stat](https://gitlab.aiursoft.com/aiursoft/nugetninja/badges/master/pipeline.svg)](https://gitlab.aiursoft.com/aiursoft/nugetninja/-/pipelines)
[![Test Coverage](https://gitlab.aiursoft.com/aiursoft/nugetninja/badges/master/coverage.svg)](https://gitlab.aiursoft.com/aiursoft/nugetninja/-/pipelines)
[![NuGet version (Aiursoft.NugetNinja)](https://img.shields.io/nuget/v/Aiursoft.NugetNinja.svg)](https://www.nuget.org/packages/Aiursoft.NugetNinja/)
[![NuGet version (Aiursoft.NugetNinja.PrBot)](https://img.shields.io/nuget/v/Aiursoft.NugetNinja.PrBot.svg)](https://www.nuget.org/packages/Aiursoft.NugetNinja.PrBot/)
[![Man hours](https://manhours.aiursoft.com/r/gitlab.aiursoft.com/aiursoft/NugetNinja.svg)](https://manhours.aiursoft.com/r/gitlab.aiursoft.com/aiursoft/NugetNinja.html)

Nuget Ninja is a tool for detecting dependencies of .NET projects. It analyzes the dependency structure of .NET projects in a directory and builds a directed acyclic graph. And will give some modification suggestions for Nuget packages, so that the dependencies of the project are as concise and up-to-date as possible.

## Installation

Requirements:

1. [.NET 10 SDK](http://dot.net/)

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

### Config file

Beyond managing NuGet packages, NugetNinja can also enforce repository structure by ensuring common files (like `.gitignore`, `LICENSE`, or `.editorconfig`) are present and up-to-date.

This feature is configured by placing a `ninja.yaml` file in the root of your repository.

This YAML file defines a list of files you expect to be in your repository. For each file, you must specify its `name` and can optionally provide a `contentUri` pointing to the raw content that file should have.

Here is a sample `ninja.yaml`:

```yaml
configVersion: 1
files:
  - name: .editorconfig
    contentUri: https://gitlab.aiursoft.com/aiursoft/tracer/-/raw/master/.editorconfig
  - name: .gitignore
    contentUri: https://gitlab.aiursoft.com/aiursoft/tracer/-/raw/master/.gitignore
  - name: .gitlab-ci.yml
    contentUri: https://gitlab.aiursoft.com/aiursoft/tracer/-/raw/master/.gitlab-ci.yml
  - name: LICENSE
    contentUri: https://gitlab.aiursoft.com/aiursoft/tracer/-/raw/master/LICENSE
  - name: CODE_OF_CONDUCT.md
    contentUri: https://gitlab.aiursoft.com/aiursoft/tracer/-/raw/master/CODE_OF_CONDUCT.md
  - name: ninja.yaml
    contentUri: https://gitlab.aiursoft.com/aiursoft/tracer/-/raw/master/ninja.yaml
  - name: nuget.config
    contentUri: https://gitlab.aiursoft.com/aiursoft/tracer/-/raw/master/nuget.config
  - name: README.md
```

When you run the `expect-files` command, NugetNinja will read this configuration and perform the following actions:

1.  **Create Missing Files:** If a file specified in `files` (e.g., `LICENSE`) does not exist in your project's root directory, NugetNinja will create it.

      * If a `contentUri` is provided, the tool will download the content from that URL and write it to the new file.
      * If no `contentUri` is provided, an empty file will be created.

2.  **Patch Existing Files:** If a file already exists, NugetNinja will compare its content with the content at the specified `contentUri`.

      * If the contents do not match, the local file will be **overwritten** (patched) with the content from the URL.
      * If the contents match, no action is taken.

3.  **Correct File Casing:** The tool performs a case-insensitive search for the file. If it finds a file with a matching name but different casing (e.g., your repo has `license` but the YAML specifies `LICENSE`), it will rename the file to match the exact casing in the `name` property.

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

1. [.NET 10 SDK](http://dot.net/)
2. Execute `dotnet run` to run the app

## Run in Microsoft Visual Studio

1. Open the `.sln` file in the project path.
2. Press `F5`.

## How to contribute

There are many ways to contribute to the project: logging bugs, submitting pull requests, reporting issues, and creating suggestions.

Even if you with push rights on the repository, you should create a personal fork and create feature branches there when you need them. This keeps the main repository clean and your workflow cruft out of sight.

We're also interested in your feedback on the future of this project. You can submit a suggestion or feature request through the issue tracker. To make this process more effective, we're asking that these include more information to help define them more clearly.

## Docker & Bots

Nuget Ninja provides two bots for automating dependency management:
1. **PrBot**: Automatically scans your starred repositories and creates pull requests for dependency upgrades.
2. **MergeBot**: Automatically merges open pull requests if the CI pipeline succeeds.

### Deployment with Docker

You can run the Nuget Ninja bots using Docker. The image includes both bots and runs them periodically via cron.

```bash
docker run -d \
    --name nuget-ninja \
    -e "Servers__0__Provider=GitHub" \
    -e "Servers__0__Token=YOUR_GITHUB_TOKEN" \
    -e "Servers__0__UserName=YOUR_GITHUB_USERNAME" \
    -e "Servers__0__UserEmail=your-email@example.com" \
    -e "Servers__0__DisplayName=Nuget Ninja Bot" \
    -e "Servers__0__EndPoint=https://api.github.com" \
    -e "Servers__0__PushEndPoint=https://{0}@github.com" \
    -e "Servers__0__ContributionBranch=users/nugetninja/evergreen" \
    hub.aiursoft.com/aiursoft/nugetninja
```

### Configuration via Environment Variables

The bots support full configuration via environment variables. This is the recommended way to provide secrets and server information. Both **PrBot** and **MergeBot** share the same `Servers` configuration section.

#### Server Configuration
To configure multiple servers, use the `Servers__N__Property` pattern (where `N` is the index starting from 0):

- `Servers__0__Provider`: The git provider (e.g., `GitHub`, `GitLab`, `Gitea`, `AzureDevOps`).
- `Servers__0__EndPoint`: The API endpoint of the server.
- `Servers__0__Token`: Your Personal Access Token (PAT).
- `Servers__0__UserName`: Your username on the server.
- `Servers__0__UserEmail`: Your email for git commits.
- `Servers__0__DisplayName`: The name used for git commits.
- `Servers__0__ContributionBranch`: The branch name used for creating PRs.
- `Servers__0__OnlyUpdate`: (Optional) Set to `true` to only run the update plugin.

#### PR Bot Options
You can also configure PR Bot specific options:

- `PrBot__LocalizationEnabled`: Set to `true` to enable automatic localization.
- `PrBot__OllamaApiEndpoint`: The Ollama API endpoint for localization.
- `PrBot__OllamaModel`: The Ollama model to use.
- `PrBot__OllamaApiKey`: The API key for the localization service.
