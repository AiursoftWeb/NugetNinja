﻿using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja
{
    public class Model
    {
        public List<Project> RootProjects { get; set; } = new List<Project>();

        public List<Project> AllProjects { get; set; } = new List<Project>();

        public List<Package> AllPackages { get; set; } = new List<Package>();

        public async Task<Project> BuildProject(string path)
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
            var packageReferences = this.GetPackageReferences(csprojContent);
            var projectReferences = this.GetProjectReferences(csprojContent, csprojFolder);

            var subProjectReferenceObjects = new List<Project>();
            foreach (var projectReference in projectReferences)
            {
                var projectObject = await this.BuildProject(projectReference);
                subProjectReferenceObjects.Add(projectObject);
            }
            var project = new Project(csprojPath)
            {
                PackageReferences = packageReferences.Select(p => new Package(p)).ToList(),
                ProjectReferences = subProjectReferenceObjects
            };
            return project;
        }

        public string[] GetPackageReferences(string csprojContent)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(csprojContent);
            var packageReferences = doc.DocumentNode
                .Descendants("PackageReference")
                .Select(p => p.Attributes["Include"].Value)
                .ToArray();

            foreach (var package in packageReferences)
            {
                if (!this.AllPackages.Any(p => p.Name == package))
                {
                    this.AllPackages.Add(new Package(package));
                }
            }

            return packageReferences;
        }

        public string[] GetProjectReferences(string csprojContent, string csprojFolder)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(csprojContent);
            var projectReferences = doc.DocumentNode
                .Descendants("ProjectReference")
                .Select(p => p.Attributes["Include"].Value)
                .Select(p => Path.GetFullPath(Path.Combine(csprojFolder, p)))
                .ToArray();

            return projectReferences;
        }
    }
}
