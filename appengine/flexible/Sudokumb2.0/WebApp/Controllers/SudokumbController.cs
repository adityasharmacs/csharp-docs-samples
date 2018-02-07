using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Controllers
{
    public class SudokumbController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles="admin")]
        public IActionResult Admin()
        {
            return View();
        }
    }        
}