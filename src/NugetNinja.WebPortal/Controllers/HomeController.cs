using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.NugetNinja.WebPortal.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        _logger.LogInformation("A user accessed the index page.");
        return View();
    }
}
