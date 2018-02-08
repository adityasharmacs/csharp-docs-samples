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
            // Show the user a puzzle by default.    
            var model = new IndexViewModel
            { 
                Form = new IndexViewForm() 
                {
                    Puzzle = IndexViewForm.SamplePuzzle
                }
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(IndexViewForm form)
        {
            var model = new IndexViewModel { Form = form };
            if (ModelState.IsValid)  
            {
                // Solve the puzzle.
                GameBoard board = GameBoard.ParseHandInput(form.Puzzle);
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