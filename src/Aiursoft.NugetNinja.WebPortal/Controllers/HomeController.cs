﻿using System.ComponentModel.DataAnnotations;
using Aiursoft.NugetNinja.WebPortal.Models.ViewModels.HomeViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.NugetNinja.WebPortal.Controllers;

public class HomeController(ILogger<HomeController> logger) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        logger.LogInformation("A user accessed the index page");

        var model = new IndexViewModel();
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Subscribe([FromForm] IndexViewModel model)
    {
        if (!ModelState.IsValid) return View(nameof(Index), model);

        //var (org, repo) = model.GetGitHubValues();

        await Task.Delay(1);

        //if (!await _gitHubService.RepoExists("https://api.github.com", org, repo, string.Empty))
        //{
        //    ModelState.AddModelError(nameof(model.GitHubRepositoryUrl), $"The repository '{org}/{repo}' doesn't exist on GitHub!");
        //    return View(viewName: nameof(Index), model: model);
        //}

        //_logger.LogInformation($"A user subscribed the repo: {model.GitHubRepositoryUrl}");

        // Really subscribe.
        return RedirectToAction(nameof(SuccessfullySubscribed));
    }

    [HttpGet]
    public IActionResult SuccessfullySubscribed()
    {
        throw new NotImplementedException();
        // return view();
    }

    [HttpGet]
    public IActionResult ConsiderUnsubscribe([FromQuery] string? repo)
    {
        throw new NotImplementedException();
        // return view();
    }

    [HttpPost]
    public IActionResult Unsubscribe([FromForm] [Required] [Url] string githubUrl)
    {
        logger.LogInformation("A user unsubscribed the repo: {GithubUrl}", githubUrl);

        // Really unsubscribe.
        return RedirectToAction(nameof(SuccessfullyUnSubscribed));
    }

    [HttpGet]
    public IActionResult SuccessfullyUnSubscribed()
    {
        throw new NotImplementedException();
        // return view();
    }
}