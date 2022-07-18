using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja.Abstracts
{
    public interface IAction
    {
        public string BuildMessage();

        public void TakeAction();
    }
}
