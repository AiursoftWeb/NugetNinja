using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja
{
    public interface IActionGenerator
    {
        public string GetHelp();

        public string GetCommandAlias();

        public IEnumerable<IAction> Analyze(Model context);
    }
}
