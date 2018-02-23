using System.Threading.Tasks;

namespace Sudokumb
{
    /// <summary>
    /// Tells the solver if it should solve sudoku puzzles in a dumb way.
    /// </summary>
    public interface IDumb
    {
        /// <summary>
        /// Should the sorver solve sudoku puzzles in a dumb way?
        /// </summary>
        Task<bool> IsDumbAsync();
    }
}