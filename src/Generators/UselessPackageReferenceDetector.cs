using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja
{
    public class UselessPackageReferenceDetector : IActionGenerator
    {
        private readonly Enumerator enumerator;

        public UselessPackageReferenceDetector(Enumerator enumerator)
        {
            this.enumerator = enumerator;
        }

        public IEnumerable<IAction> Analyze(Model context)
        {
            foreach (var rootProject in context.AllProjects)
            {
                var uselessReferences = this.AnalyzeProject(rootProject);
                foreach (var reference in uselessReferences)
                {
                    yield return reference;
                }
            }
        }

        public string GetCommandAlias()
        {
            // To do:
            throw new NotImplementedException();
        }

        public string GetHelp()
        {
            // To do
            throw new NotImplementedException();
        }

        private IEnumerable<UselessPackageReference> AnalyzeProject(Project context)
        {
            var allRelatedProjects = enumerator.EnumerateAllBuiltProjects(context, false);
            var allPackagesBroughtUp = allRelatedProjects.SelectMany(p => p.PackageReferences).ToArray();

            foreach (var directReference in context.PackageReferences)
            {
                if (allPackagesBroughtUp.Any(pa => pa.Name == directReference.Name))
                {
                    yield return new UselessPackageReference(context, directReference);
                }
            }
        }
    }
}
