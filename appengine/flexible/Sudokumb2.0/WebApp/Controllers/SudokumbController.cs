using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.Models.SudokumbViewModels;

namespace WebApp.Controllers
{
    public class SudokumbController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(IndexViewModel model)
        {
            if (ModelState.IsValid)  
            {
                // Solve the puzzle.
            }
            return View();
        }
         

        [Authorize(Roles="admin")]
        public IActionResult Admin()
        {
            return View();
        }
    }        
}