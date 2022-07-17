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
        private readonly UselessProjectReferenceDetector detector;
        private readonly ILogger<Entry> logger;

        public Entry(
            Extractor extractor,
            UselessProjectReferenceDetector detector,
            ILogger<Entry> logger)
        {
            this.extractor = extractor;
            this.detector = detector;
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

            var uselessReferences = this.detector.Analyse(model).ToArray();
            foreach (var uselessReference in uselessReferences)
            {
                logger.LogWarning(uselessReference.BuildMessage());
            }

            logger.LogInformation("Stopping NugetNinja...");
        }
    }
}
