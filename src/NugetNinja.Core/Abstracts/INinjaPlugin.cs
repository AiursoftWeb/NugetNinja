namespace Aiursoft.NugetNinja.Core;

public interface INinjaPlugin
{
    public CommandHandler[] Install();
}