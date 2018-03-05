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
        public const string SamplePuzzle = @". . .  . 3 2  . . 7
9 2 7  . . .  . . .
. . 5  . . .  2 6 .

. . .  . 2 6  . . .
. 8 9  . 5 .  3 4 .
. . .  8 9 .  . . .

. 9 3  . . .  8 . .
. . .  . . .  1 5 6
5 . .  7 8 .  . . .
";

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
