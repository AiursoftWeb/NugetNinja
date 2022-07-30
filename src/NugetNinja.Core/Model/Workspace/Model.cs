﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using HtmlAgilityPack;

namespace Microsoft.NugetNinja.Core;

public class Model
{
    public List<Project> RootProjects { get; set; } = new List<Project>();

    public List<Project> AllProjects { get; set; } = new List<Project>();

    public List<Package> AllPackages { get; set; } = new List<Package>();

    public async Task<Project> IncludeProject(string path)
    {
        var projectInDatabaes = AllProjects.FirstOrDefault(p => p.PathOnDisk == path);
        if (projectInDatabaes != null)
        {
            RootProjects.RemoveAll(p => p.PathOnDisk == path);
            return projectInDatabaes;
        }
        else
        {
            var builtProject = await BuildNewProject(path);
            AllProjects.Add(builtProject);
            RootProjects.Add(builtProject);
            return builtProject;
        }
    }

    private async Task<Project> BuildNewProject(string csprojPath)
    {
        var csprojFolder = new FileInfo(csprojPath).Directory?.FullName
            ?? throw new IOException($"Can not get the .csproj file location based on path: '{csprojPath}'!");
        var csprojContent = await File.ReadAllTextAsync(csprojPath);
        var packageReferences = GetPackageReferences(csprojContent);
        var projectReferences = GetProjectReferences(csprojContent, csprojFolder);

        var subProjectReferenceObjects = new List<Project>();
        foreach (var projectReference in projectReferences)
        {
            var projectObject = await IncludeProject(projectReference);
            subProjectReferenceObjects.Add(projectObject);
        }
        var project = new Project(csprojPath)
        {
            PackageReferences = packageReferences.ToList(),
            ProjectReferences = subProjectReferenceObjects
        };
        return project;
    }

    private Package[] GetPackageReferences(string csprojContent)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(csprojContent);
        var packageReferences = doc.DocumentNode
            .Descendants("PackageReference")
            .Select(p => new Package(
                name: p.Attributes["Include"].Value,
                versionText: p.Attributes["Version"].Value))
            .ToArray();

        foreach (var package in packageReferences)
        {
            if (!AllPackages.Any(p => 
                p.Name == package.Name && 
                p.Version == package.Version ))
            {
                AllPackages.Add(package);
            }
        }

        return packageReferences;
    }

    private string[] GetProjectReferences(string csprojContent, string csprojFolder)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(csprojContent);
        var projectReferences = doc.DocumentNode
            .Descendants("ProjectReference")
            .Select(p => p.Attributes["Include"].Value)
            .Select(p => StringExtensions.GetAbsolutePath(csprojFolder, p))
            .ToArray();

        return projectReferences;
    }
}