namespace Aiursoft.NugetNinja.Core;

public interface IEntryService
{
    public Task OnServiceStartedAsync(string path, bool shouldTakeAction);
}