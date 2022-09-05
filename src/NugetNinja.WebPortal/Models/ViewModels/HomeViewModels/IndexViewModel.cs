using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Aiursoft.NugetNinja.WebPortal.Models.ViewModels.HomeViewModels;

public class IndexViewModel
{
    private const string GithubRepoRegexPattern = @"^https:\/\/github\.com(?'org'\/[^\s\/]+)(?'repo'\/[^\s\/]+)$";
    private static readonly Regex GithubRepoRegex = new(GithubRepoRegexPattern, RegexOptions.Compiled);

    [Required(ErrorMessage = "The {0} is required.")]
    [Display(Name = "GitHub repository url")]
    [Url(ErrorMessage = "The {0} is not a valid url!")]
    [RegularExpression(pattern: GithubRepoRegexPattern, ErrorMessage = "The URL is not a GitHub repository address!")]
    public string GitHubRepositoryUrl { get; set; } = string.Empty;

    public (string org, string repo) GetGitHubValues()
    {
        var match = GithubRepoRegex.Match(GitHubRepositoryUrl);
        var org = match.Groups[1].Value.Trim('/');
        var repo = match.Groups[2].Value.Trim('/');
        return (org, repo);
    }
}