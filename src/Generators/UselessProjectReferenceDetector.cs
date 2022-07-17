using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja
{
    public class UselessProjectReferenceDetector
    {
        public IEnumerable<UselessProjectReference> Analyse(Model context)
        {
            foreach (var rootProject in context.AllProjects)
            {
                var uselessReferences = this.AnalyseProject(rootProject);
                foreach(var reference in uselessReferences)
                {
                    yield return reference;
                }
            }
        }

        public IEnumerable<UselessProjectReference> AnalyseProject(Project context)
        {
            var directReferences = context.ProjectReferences;

            var allRecursiveReferences = new List<Project>();
            foreach (var directReference in directReferences)
            {
                var recursiveReferences = this.EnumerateAllBuiltProjects(directReference, includeSelf: false);
                allRecursiveReferences.AddRange(recursiveReferences);
            }

            foreach (var directReference in directReferences)
            {
                if (allRecursiveReferences.Contains(directReference))
                {
                    yield return new UselessProjectReference(context, directReference);
                }
            }
        }


        private IEnumerable<Project> EnumerateAllBuiltProjects(Project input, bool includeSelf = true)
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
