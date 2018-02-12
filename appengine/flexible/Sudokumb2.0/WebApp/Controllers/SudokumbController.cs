using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sudokumb;
using WebApp.Models;
using WebApp.Models.SudokumbViewModels;

namespace WebApp.Controllers
{
    public class SudokumbController : Controller
    {
        readonly SolveStateStore solveStateStore_;

        public SudokumbController(SolveStateStore solveStateStore)
        {
            solveStateStore_ = solveStateStore;
        }

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
                model.SolveRequestId = Guid.NewGuid().ToString();
                // GameBoard solution = Solver.Solve(board);
                // model.Solution = solution.ToHandInputString();
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Solve(string id) =>
            new JsonResult(await solveStateStore_.GetAsync(id));

        [Authorize(Roles="admin")]
        public IActionResult Admin()
        {
            return View();
        }
    }
}