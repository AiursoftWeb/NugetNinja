﻿using Aiursoft.CSTools.Services;
using Aiursoft.CSTools.Tools;
using Aiursoft.NugetNinja.Core.Abstracts;
using Aiursoft.NugetNinja.Core.Model.Workspace;
using Aiursoft.NugetNinja.MissingPropertyPlugin.Models;
using Microsoft.Extensions.Logging;

namespace Aiursoft.NugetNinja.MissingPropertyPlugin.Services;

public class MissingPropertyDetector(
    ProjectTypeDetector projectTypeDetector,
    ILogger<MissingPropertyDetector> logger)
    : IActionDetector
{
    private readonly bool _enforceNullable = false;

    private readonly string[] _notSupportedRuntimes =
    [
        "net7.0",
        "net6.0",
        "net5.0",
        "netcoreapp3.1",
        "netcoreapp3.0",
        "netcoreapp2.2",
        "netcoreapp2.1",
        "netcoreapp1.1",
        "netcoreapp1.0"
    ];

    private readonly string _suggestedRuntime = "net6.0";

    public async IAsyncEnumerable<IAction> AnalyzeAsync(Model context)
    {
        await Task.CompletedTask;
        foreach (var project in context.AllProjects)
        {
            if (string.IsNullOrWhiteSpace(project.Nullable) && _enforceNullable)
                yield return new MissingProperty(project, nameof(project.Nullable), "enable");

            // Help upgrade old web projects.
            if (
                project.PackageReferences.Any(p => p.Name == "Microsoft.AspNetCore.App") ||
                project.PackageReferences.Any(p => p.Name == "Microsoft.AspNetCore.All") // Is an old Web Project.
            )
            {
                if (project.PackageReferences.FirstOrDefault(p => p.Name == "Microsoft.AspNetCore.App") is not null)
                    yield return new ObsoletePackageReference(project, "Microsoft.AspNetCore.App");
                if (project.PackageReferences.FirstOrDefault(p => p.Name == "Microsoft.AspNetCore.All") is not null)
                    yield return new ObsoletePackageReference(project, "Microsoft.AspNetCore.All");
                if (project.PackageReferences.FirstOrDefault(p => p.Name == "Microsoft.AspNetCore.Razor.Design") is not
                    null)
                    yield return new ObsoletePackageReference(project, "Microsoft.AspNetCore.Razor.Design");

                if (project.Sdk?.Equals("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) == false)
                    yield return new InsertFrameworkReference(project, "Microsoft.AspNetCore.App");
            }

            logger.LogTrace("Analysing project {Project} with detector...", project);
            var projectInfo = projectTypeDetector.Detect(project);

            // Output type.
            var outputType = 
                projectInfo.IsWindowsExecutable ? "WinExe" :
                projectInfo.IsExecutable ? "Exe" : "Library";
            if (project.OutputType != outputType)
            {
                logger.LogTrace("Project {Project} is missing property OutputType", project);
                yield return new MissingProperty(project, nameof(project.OutputType), outputType);
            }

            // Target framework.
            var versionSuggestion = GetResetRuntimeSuggestion(project);
            if (versionSuggestion != null) yield return versionSuggestion;

            // Assembly name.
            if (string.IsNullOrWhiteSpace(project.AssemblyName) && !projectInfo.IsUnitTest)
            {
                var assemblyName = projectInfo.ShouldPackAsNugetTool ? GenerateExecutableFileName(project.FileName) : project.FileName;
                logger.LogTrace("Project {Project} is missing property AssemblyName", project);
                yield return new MissingProperty(project, nameof(project.AssemblyName), assemblyName);
            }
            
            // Root namespace.
            if (string.IsNullOrWhiteSpace(project.RootNamespace))
            {
                var rootNamespace = project.FileName.Replace("-", string.Empty);
                logger.LogTrace("Project {Project} is missing property RootNamespace", project);
                yield return new MissingProperty(project, nameof(project.RootNamespace), rootNamespace);
            }

            // Is test project
            if (project.IsTestProject != projectInfo.IsUnitTest.ToString().ToLower())
            {
                logger.LogTrace("Project {Project} is missing property IsTestProject", project);
                yield return new MissingProperty(project, nameof(project.IsTestProject), projectInfo.IsUnitTest.ToString().ToLower());
            }
            
            // Is Packable
            if (project.IsPackable != projectInfo.ShouldPackAsNugetLibrary.ToString().ToLower())
            {
                logger.LogTrace("Project {Project} is missing property IsPackable", project);
                yield return new MissingProperty(project, nameof(project.IsPackable), projectInfo.ShouldPackAsNugetLibrary.ToString().ToLower());
            }
            
            // GeneratePackageOnBuild
            if (projectInfo.ShouldPackAsNugetLibrary && project.GeneratePackageOnBuild.IsFalse())
            {
                logger.LogTrace("Project {Project} is missing property GeneratePackageOnBuild", project);
                yield return new MissingProperty(project, nameof(project.GeneratePackageOnBuild), true.ToString().ToLower());
            }

            if (projectInfo.ShouldPackAsNugetTool)
            {
                // Pack as tool
                if (project.PackAsTool.IsFalse())
                {
                    logger.LogTrace("Project {Project} is missing property PackAsTool", project);
                    yield return new MissingProperty(project, nameof(project.PackAsTool), projectInfo.ShouldPackAsNugetTool.ToString().ToLower());
                }
            
                // Tool command name
                if (project.ToolCommandName != project.AssemblyName)
                {
                    var assemblyName = GenerateExecutableFileName(project.FileName);
                    logger.LogTrace("Project {Project} is missing property ToolCommandName", project);
                    yield return new MissingProperty(project, nameof(project.ToolCommandName), assemblyName);
                }
            }
            
            // Implicit using
            if (project.ImplicitUsings != "enable")
            {
                logger.LogTrace("Project {Project} is missing property Implicit using", project);
                yield return new MissingProperty(project, nameof(project.ImplicitUsings), "enable");
            }

            // Package metadata
            if (projectInfo.ShouldPackAsNugetLibrary)
            {
                // Company
                if (string.IsNullOrWhiteSpace(project.Company))
                {
                    var company = project.FileName.Split('.').First();
                    logger.LogTrace("Project {Project} is missing property Company", project);
                    yield return new MissingProperty(project, nameof(project.Company), company);
                }
                
                // Product
                if (string.IsNullOrWhiteSpace(project.Product))
                {
                    var product = project.FileName.Split('.').Last();
                    logger.LogTrace("Project {Project} is missing property Product", project);
                    yield return new MissingProperty(project, nameof(project.Product), product);
                }
                
                // Description
                if (string.IsNullOrWhiteSpace(project.Description))
                {
                    var company = project.FileName.Split('.').First();
                    var product = project.FileName.Split('.').Last();
                    logger.LogTrace("Project {Project} is missing property Description", project);
                    yield return new MissingProperty(project, nameof(project.Description), $"Nuget package of '{product}' provided by {company}");
                }
                
                // PackageId
                if (project.PackageId != project.FileName)
                {
                    logger.LogTrace("Project {Project} is missing property PackageId", project);
                    yield return new MissingProperty(project, nameof(project.PackageId), project.FileName);
                }
                
                // PackageTags
                if (string.IsNullOrWhiteSpace(project.PackageTags))
                {
                    var tags = "nuget package dotnet csproj dependencies";
                    if (projectInfo.ShouldPackAsNugetTool)
                    {
                        tags = "nuget package dotnet cli tool";
                    }
                    logger.LogTrace("Project {Project} is missing property PackageTags", project);
                    yield return new MissingProperty(project, nameof(project.PackageTags), tags);
                }
                
                // PackageLicenseExpression
                var licenseExpression = GetLicenseExpression(project);
                if (!string.IsNullOrWhiteSpace(licenseExpression) && string.IsNullOrWhiteSpace(project.PackageLicenseExpression))
                {
                    logger.LogTrace("Project {Project} is missing property PackageLicenseExpression", project);
                    yield return new MissingProperty(project, nameof(project.PackageLicenseExpression), licenseExpression);
                }
                
                // PackageProjectUrl
                var gitRemoteUrl = await GetGitRemote(Path.GetDirectoryName(project.PathOnDisk)!);
                var projectUrl = gitRemoteUrl.Replace(".git", string.Empty);
                if (string.IsNullOrWhiteSpace(project.PackageProjectUrl))
                {
                    logger.LogTrace("Project {Project} is missing property PackageProjectUrl", project);
                    yield return new MissingProperty(project, nameof(project.PackageProjectUrl), projectUrl);
                }
                
                // RepositoryType
                if (string.IsNullOrWhiteSpace(project.RepositoryType))
                {
                    logger.LogTrace("Project {Project} is missing property RepositoryType", project);
                    yield return new MissingProperty(project, nameof(project.RepositoryType), "git"); // NugetNinja only supports git repositories
                }
                
                // RepositoryUrl
                if (string.IsNullOrWhiteSpace(project.RepositoryUrl))
                {
                    logger.LogTrace("Project {Project} is missing property RepositoryUrl", project);
                    yield return new MissingProperty(project, nameof(project.RepositoryUrl), gitRemoteUrl);
                }

                // Readme file
                var readmePath = GetReadmePath(project);
                if (!string.IsNullOrWhiteSpace(readmePath) && string.IsNullOrWhiteSpace(project.PackageReadmeFile))
                {
                    logger.LogTrace("Project {Project} is missing readme info. Suggested readme file is: {Readme}", project, readmePath);
                    
                    var fileName = Path.GetFileName(readmePath);
                    yield return new MissingProperty(project, nameof(project.PackageReadmeFile), fileName);
                    yield return new PackFile(project, readmePath);
                }
            }
            
            logger.LogTrace("Project {Project} analyse finished", project);
        }
    }

    private ResetRuntime? GetResetRuntimeSuggestion(Project project)
    {
        var runtimes = project.GetTargetFrameworks();
        for (var i = 0; i < runtimes.Length; i++)
            foreach (var notSupportedRuntime in _notSupportedRuntimes)
                if (runtimes[i].Contains(notSupportedRuntime, StringComparison.OrdinalIgnoreCase))
                    runtimes[i] = runtimes[i].ToLower().Replace(notSupportedRuntime, _suggestedRuntime);

        var cleanedRuntimes = runtimes.Select(r => r.ToLower().Trim()).Distinct().ToArray();

        var deprecatedCount = project.GetTargetFrameworks().Except(cleanedRuntimes).Count();
        var insertedCount = cleanedRuntimes.Except(project.GetTargetFrameworks()).Count();
        if (deprecatedCount > 0 || insertedCount > 0)
            return new ResetRuntime(project, cleanedRuntimes, insertedCount, deprecatedCount);
        return null;
    }

    private static string GenerateExecutableFileName(string projectName)
    {
        string[] nameParts = projectName.Split('.');
        string lastName = nameParts[^1];

        string fileName = "";
        for (int i = 0; i < lastName.Length; i++)
        {
            if (char.IsUpper(lastName[i]))
            {
                if (i > 0)
                {
                    fileName += "-";
                }

                fileName += char.ToLower(lastName[i]);
            }
            else
            {
                fileName += lastName[i];
            }
        }

        return fileName;
    }

    /// <summary>
    /// Gets the relative path to the README.md file in the project directory or any of its parent directories.
    /// </summary>
    /// <param name="project">The project to search for the README.md file.</param>
    /// <returns>The relative path to the README.md file, or an empty string if the file is not found.</returns>
    private string GetReadmePath(Project project)
    {
        return SearchInRepo(project, "readme.md");
    }
    
    private string GetLicensePath(Project project)
    {
        var licensePath = SearchInRepo(project, "license");
        if (string.IsNullOrWhiteSpace(licensePath))
        {
            licensePath = SearchInRepo(project, "license.md");
        }
        return licensePath;
    }
    
    private string GetLicenseExpression(Project project)
    {
        var licensePath = GetLicensePath(project);
        if (string.IsNullOrWhiteSpace(licensePath))
        {
            return string.Empty;
        }
        
        var absoluteLicensePath = Path.Combine(Path.GetDirectoryName(project.PathOnDisk)!, licensePath);
        var licenseContent = File.ReadAllText(absoluteLicensePath);
        var licenseExpression = LicenseExpressionParser.Parse(licenseContent);
        return licenseExpression;
    }
    
    private async Task<string> GetGitRemote(string projectPath)
    {
        var commandService = new CommandService();
        var (code, output, err) = await commandService.RunCommandAsync(
            bin: "git", 
            arg: "remote get-url origin",
            path: projectPath);
        if (code != 0)
        {
            throw new Exception($"Failed to get git remote: {err}. Command executed: git remote get-url origin at {projectPath}");
        }
        
        return output.Trim();
    }

    /// <summary>
    /// Searches for a file in the repository by iterating through the project directory and its parent directories.
    /// </summary>
    /// <param name="project">The project to search in.</param>
    /// <param name="fileNameEndsWith">The file name to search for. Only match the file if it ends with this string. Case-insensitive.</param>
    /// <returns>The relative path to the file if found, otherwise an empty string.</returns>
    private string SearchInRepo(Project project, string fileNameEndsWith)
    {
        var csprojDirectoryPath = Path.GetDirectoryName(project.PathOnDisk)!;
        var path = csprojDirectoryPath;
        var readmePath = string.Empty;
        while (true)
        {
            // Case-insensitive search:
            var files = Directory.GetFiles(path)
                .Where(f => f.EndsWith(fileNameEndsWith, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (files.Any())
            {
                readmePath = files.First();
                break;
            }

            var parent = Directory.GetParent(path);
            if (parent == null)
            {
                break;
            }
            
            var isGitRoot = Directory.Exists(Path.Combine(path, ".git"));
            if (isGitRoot)
            {
                break;
            }

            path = parent.FullName;
        }

        if (string.IsNullOrWhiteSpace(readmePath))
        {
            return string.Empty;
        }
        
        // Get relative path from project.PathOnDisk to readmePath:
        var relativePath = Path.GetRelativePath(csprojDirectoryPath, readmePath);
        return relativePath;
    }
}