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
        private readonly IEnumerable<IActionGenerator> generators;
        private readonly ILogger<Entry> logger;

        public Entry(
            Extractor extractor,
            IEnumerable<IActionGenerator> generators,
            ILogger<Entry> logger)
        {
            this.extractor = extractor;
            this.generators = generators;
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

            foreach(var generator in this.generators)
            {
                var actions = generator.Analyze(model);
                foreach (var action in actions)
                {
                    logger.LogWarning(action.BuildMessage());
                }
            }

            logger.LogInformation("Stopping NugetNinja...");
        }
    }
}
