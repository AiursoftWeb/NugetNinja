using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja
{
    public class Entry
    {
        private readonly ILogger<Entry> logger;

        public Entry(ILogger<Entry> logger)
        {
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
            var csprojs = Directory
                .EnumerateFiles(workingPath, "*.csproj", SearchOption.AllDirectories)
                .ToArray();

            foreach (var csproj in csprojs)
            {
                logger.LogTrace($"Parsing {csproj}...");
            }

            await Task.Delay(1000);

            logger.LogInformation("Stopping NugetNinja...");
        }
    }
}
