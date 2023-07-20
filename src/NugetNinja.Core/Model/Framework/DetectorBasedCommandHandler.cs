using Aiursoft.CommandFramework.Abstracts;

namespace Aiursoft.NugetNinja.Core;

public abstract class DetectorBasedCommandHandler<T, TS> : ServiceCommandHandler<DetectorStarter<T>, TS>
    where T : IActionDetector
    where TS : class, IStartUp, new()
{
}