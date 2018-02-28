using System;
using System.Threading;
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
        readonly ISolveRequester solver_;
        readonly AdminSettings adminSettings_;

        public SudokumbController(ISolveRequester solver,
            AdminSettings adminSettings)
        {
            solver_ = solver;
            adminSettings_ = adminSettings;
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
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Solve(string id,
            CancellationToken cancellationToken)
        {
            SolveState state = await solver_.GetProgress(id, cancellationToken);
            return new JsonResult(new
            {
                BoardsExaminedCount = state.BoardsExaminedCount,
                Solution = state.Solution?.ToHandInputString()
            });
        }


        [HttpGet]
        [Authorize(Roles="admin")]
        public async Task<IActionResult> Admin()
        {
            AdminViewModel model = new AdminViewModel()
            {
                Dumb = await adminSettings_.IsDumbAsync()
            };
            return View(model);
        }

        [HttpPost]
        [Authorize(Roles="admin")]
        public async Task<IActionResult> Admin(AdminViewModel model)
        {
            await adminSettings_.SetDumbAsync(model.Dumb);
            return View(model);
        }
    }
}