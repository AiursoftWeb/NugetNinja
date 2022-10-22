using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja.Core;

public interface IVersionControlService
{
    public string GetName();

    public Task<Repository> GetRepo(string endPoint, string orgName, string repoName, string patToken);

    public Task<bool> RepoExists(string endPoint, string orgName, string repoName, string patToken);

    public IAsyncEnumerable<Repository> GetStars(string endPoint, string userName, string patToken);

    public Task ForkRepo(string endPoint, string org, string repo, string patToken);

    Task<List<PullRequest>> GetPullRequest(string endPoint, string org, string repo, string head, string patToken);

    Task CreatePullRequest(string endPoint, string org, string repo, string head, string baseBranch, string patToken);
}
