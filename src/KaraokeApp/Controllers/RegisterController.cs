using Microsoft.AspNetCore.Mvc;

namespace KaraokeApp.Controllers
{
    public class RegisterController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
