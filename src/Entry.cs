using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja
{
    public class Entry
    {
        private readonly Extractor extractor;
        private readonly UselessProjectReferenceDetector projectDetector;
        private readonly UselessPackageReferenceDetector packageDetector;
        private readonly ILogger<Entry> logger;

        public Entry(
            Extractor extractor,
            UselessProjectReferenceDetector projectDetector,
            UselessPackageReferenceDetector packageDetector,
            ILogger<Entry> logger)
        {
            this.extractor = extractor;
            this.projectDetector = projectDetector;
            this.packageDetector = packageDetector;
            this.logger = logger;
        }

        public async Task StartEntry(string[] args)
        {
            logger.LogInformation("Starting NugetNinja...");

            if (args.Length < 1)
            {
                logger.LogWarning("Usage: [Working path]");
                return;
            }

            var workingPath = args[0];
            var model = await extractor.Parse(workingPath);

            var uselessProjectReferences = this.projectDetector.Analyse(model);
            foreach (var uselessReference in uselessProjectReferences)
            {
                logger.LogWarning(uselessReference.BuildMessage());
            }

            var uselessPackageReferences = this.packageDetector.Analyse(model);
            foreach (var uselessReference in uselessPackageReferences)
            {
                logger.LogWarning(uselessReference.BuildMessage());
            }

            logger.LogInformation("Stopping NugetNinja...");
        }
    }
}
