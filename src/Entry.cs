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
        private readonly ILogger<Entry> logger;

        public Entry(
            Extractor extractor,
            ILogger<Entry> logger)
        {
            this.extractor = extractor;
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
            await extractor.Extract(workingPath);

            logger.LogInformation("Stopping NugetNinja...");
        }
    }
}
