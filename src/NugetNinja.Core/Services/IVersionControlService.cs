using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aiursoft.NugetNinja.Core;

public interface IVersionControlService
{
    public string GetName();

    public Task<Repository> GetRepo(string orgName, string repoName);

    public Task<bool> RepoExists(string orgName, string repoName);

    public IAsyncEnumerable<Repository> GetStars(string userName);

    public Task ForkRepo(string org, string repo, string patToken);

    Task<List<PullRequest>> GetPullRequest(string org, string repo, string head);

    Task CreatePullRequest(string org, string repo, string head, string baseBranch, string patToken);
}
