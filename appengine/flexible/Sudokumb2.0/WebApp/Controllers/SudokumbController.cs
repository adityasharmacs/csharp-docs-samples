using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sudokumb;
using WebApp.Models;
using WebApp.Models.SudokumbViewModels;
using WebApp.Services;

namespace WebApp.Controllers
{
    public class SudokumbController : Controller
    {
        readonly Solver solver_;

        public SudokumbController(Solver solver)
        {
            solver_ = solver;
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
        public async Task<IActionResult> Index(IndexViewForm form)
        {
            var model = new IndexViewModel { Form = form };
            if (ModelState.IsValid)
            {
                // Solve the puzzle.
                GameBoard board = GameBoard.ParseHandInput(form.Puzzle);
                model.SolveRequestId = await solver_.StartSolving(board);
                // GameBoard solution = Solver.Solve(board);
                // model.Solution = solution.ToHandInputString();
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Solve(string id)
        {
            SolveState state = await solver_.GetProgress(id);
            return new JsonResult(new
            {
                BoardsExaminedCount = state.BoardsExaminedCount,
                Solution = state.Solution?.ToHandInputString()
            });
        }


        [Authorize(Roles="admin")]
        public IActionResult Admin()
        {
            return View();
        }
    }
}