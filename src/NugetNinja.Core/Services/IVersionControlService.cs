using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja.Core;

public interface IVersionControlService
{
    public string GetName();

    public Task<Repository> GetRepo(string endPoint, string orgName, string repoName);

    public Task<bool> RepoExists(string endPoint, string orgName, string repoName);

    public IAsyncEnumerable<Repository> GetStars(string endPoint, string userName);

    public Task ForkRepo(string endPoint, string org, string repo, string patToken);

    Task<List<PullRequest>> GetPullRequest(string endPoint, string org, string repo, string head);

    Task CreatePullRequest(string endPoint, string org, string repo, string head, string baseBranch, string patToken);
}
