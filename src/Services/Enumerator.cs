using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja
{
    public class Enumerator
    {
        public IEnumerable<Project> EnumerateAllBuiltProjects(Project input, bool includeSelf = true)
        {
            if (includeSelf)
            {
                yield return input;
            }
            foreach (var subProject in input.ProjectReferences)
            {
                foreach (var result in EnumerateAllBuiltProjects(subProject))
                {
                    yield return result;
                }
            }
        }
    }
}
