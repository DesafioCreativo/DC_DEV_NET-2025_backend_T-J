using Microsoft.AspNetCore.Mvc;

namespace ThinkAndJobSolution.Controllers
{
    public class Web : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
