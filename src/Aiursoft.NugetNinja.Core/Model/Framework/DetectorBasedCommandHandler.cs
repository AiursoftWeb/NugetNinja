using Aiursoft.CommandFramework.Abstracts;
using Aiursoft.NugetNinja.Core.Abstracts;

namespace Aiursoft.NugetNinja.Core.Model.Framework;

public abstract class DetectorBasedCommandHandler<T, TS> : ServiceCommandHandler<DetectorStarter<T>, TS>
    where T : IActionDetector
    where TS : class, IStartUp, new()
{
}