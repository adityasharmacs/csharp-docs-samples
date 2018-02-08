using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebApp.Models.SudokumbViewModels
{
    public class PuzzleAttribute : ValidationAttribute
    {
        public override bool IsValid(object value)
        {
            try 
            {                    
                var board = Sudokumb.GameBoard.ParseHandInput(new MemoryStream(
                    Encoding.UTF8.GetBytes((string)value)));
                return board != null;
            } 
            catch (Exception e)
            {
                this.   
            }
        }

    }

    public class IndexViewModel
    {
        const string samplePuzzle = "1 2 3   . . .   7 8 9\n. . .   . . .   . . .\n. . .   . . .   . . .\n\n. . .   4 . .   . . .                \n. 7 .   . 5 .   . . .\n. . .   . . 6   . 2 .\n\n. . 1   . . .   . . .\n. 5 .   . . 3   . . .\n3 . .   . . .   1 . .\n";

        [Required]
        [Puzzle]
        [DataType(DataType.MultilineText)]
        [Display(Prompt = samplePuzzle)]

        public string Puzzle { get; set; }
    }
}
