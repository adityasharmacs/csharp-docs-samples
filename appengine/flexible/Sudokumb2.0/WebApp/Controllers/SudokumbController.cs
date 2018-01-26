using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Controllers
{
    public class SudokumbController : Controller
    {
        [Authorize(Roles="admin")]
        public IActionResult Index()
        {
            return View();
        }
    }        
}