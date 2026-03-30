using System.Diagnostics;
using HomeWorkJudge.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HomeWorkJudge.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpGet("/Home/StatusCode")]
        public IActionResult StatusCodePage(int code)
        {
            Response.StatusCode = code;

            var model = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                StatusCode = code,
                FriendlyMessage = code switch
                {
                    403 => "You do not have permission to access this resource.",
                    404 => "The requested resource was not found.",
                    429 => "Too many requests. Please try again shortly.",
                    _ => "An unexpected error occurred while processing your request."
                }
            };

            return View("StatusCode", model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
