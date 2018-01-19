using System;
using System.IO;
using Xunit;

namespace Sudokumb
{
    public class SolverTest
    {
        [Fact]
        public void TestSolve()
        {
            using (Stream m = File.OpenRead("SampleBoard.txt"))
            {
                GameBoard a = GameBoard.ParseHandInput(m);
                GameBoard solution = Solver.Solve(a);
                var expectedSolution = GameBoard.Create(
                    "123|645|789" +
                    "987|321|654" +
                    "645|987|312" +
                    "---+---+---" +
                    "839|472|561" +
                    "276|159|843" +
                    "514|836|927" +
                    "---+---+---" +
                    "791|268|435" +
                    "458|713|296" +
                    "362|594|178");
                Assert.Equal(expectedSolution.ToPrettyString(), 
                    solution.ToPrettyString());
            }
        }
    }
}
