using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja
{
    public class Extractor
    {
        private readonly ILogger<Extractor> logger;

        public Extractor(ILogger<Extractor> logger)
        {
            this.logger = logger;
        }

        public async Task<Model> Extract(string rootPath)
        {
            var csprojs = Directory
                .EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories)
                .ToArray();

            var model = new Model();

            foreach (var csprojPath in csprojs)
            {
                logger.LogTrace($"Parsing {csprojPath}...");
                await model.GetProject(csprojPath);
            }

            return model;
        }
    }
}
