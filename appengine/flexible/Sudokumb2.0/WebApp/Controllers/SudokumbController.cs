using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sudokumb;
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
                GameBoard board = GameBoard.ParseHandInput(model.Puzzle);
                GameBoard solution = Solver.Solve(board);
                model.Solution = solution.ToHandInputString();
            }
            return View(model);
        }

        [Authorize(Roles="admin")]
        public IActionResult Admin()
        {
            return View();
        }
    }        
}