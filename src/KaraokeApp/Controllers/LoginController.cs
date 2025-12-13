using Microsoft.AspNetCore.Mvc;

namespace KaraokeApp.Controllers
{
    public class LoginController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
