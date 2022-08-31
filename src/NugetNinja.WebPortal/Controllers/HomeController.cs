using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.NugetNinja.WebPortal.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        _logger.LogInformation("A user accessed the index page.");
        return View();
    }

    [HttpPost]
    public IActionResult Subscribe([FromForm][Required][Url]string githubUrl)
    {
        _logger.LogInformation($"A user subscribed the repo: {githubUrl}");

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
    public IActionResult ConsiderUnsubscribe([FromQuery]string? repo)
    {
        throw new NotImplementedException();
        // return view();
    }

    [HttpPost]
    public IActionResult Unsubscribe([FromForm][Required][Url]string githubUrl)
    {
        _logger.LogInformation($"A user unsubscribed the repo: {githubUrl}");

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
