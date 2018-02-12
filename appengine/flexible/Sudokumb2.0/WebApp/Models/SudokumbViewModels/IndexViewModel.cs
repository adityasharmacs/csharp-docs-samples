using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sudokumb;

namespace WebApp.Models.SudokumbViewModels
{
    public class PuzzleAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            try
            {
                var board = GameBoard.ParseHandInput((string)value);
                return board != null;
            }
            catch (BadGameBoardException e)
            {
                this.ErrorMessage = e.Message;
                return false;
            }
            catch (ArgumentException)
            {
                this.ErrorMessage = "The puzzle must have 81 numbers or dots.";
                return false;
            }
        }

    }

    public class IndexViewForm
    {
        public const string SamplePuzzle = "1 2 3   . . .   7 8 9\n. . .   . . .   . . .\n. . .   . . .   . . .\n\n. . .   4 . .   . . .                \n. 7 .   . 5 .   . . .\n. . .   . . 6   . 2 .\n\n. . 1   . . .   . . .\n. 5 .   . . 3   . . .\n3 . .   . . .   1 . .\n";

        [Required]
        [Puzzle]
        [DataType(DataType.MultilineText)]
        [Display(Prompt = SamplePuzzle)]
        public string Puzzle { get; set; }
    }

    public class IndexViewModel
    {
        public IndexViewForm Form { get; set; }
        public string Solution { get; set; }

        public string SolveRequestId { get; set; }
    }

}
