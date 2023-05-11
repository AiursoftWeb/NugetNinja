namespace Aiursoft.NugetNinja.Core;

public interface IAction
{
    public string BuildMessage();

    public Task TakeActionAsync();
}