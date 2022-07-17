using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja
{
    public class UselessPackageReference
    {
        public UselessPackageReference(Project source, Package target)
        {
            SourceProjectName = source;
            TargetPackageName = target;
        }

        public Project SourceProjectName { get; set; }
        public Package TargetPackageName { get; set; }

        public string BuildMessage()
        {
            return $"The project: '{SourceProjectName}' don't have to reference package '{TargetPackageName}' because it already has its access via another path!";
        }
    }
}
