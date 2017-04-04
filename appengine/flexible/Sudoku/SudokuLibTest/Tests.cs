/*
 * Copyright (c) 2017 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy of
 * the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations under
 * the License.
 */
using System.Linq;
using Xunit;

namespace SudokuLib
{
    public class Tests
    {
        static string _boardA =
            "123|   |789" +
            "   |   |   " +
            "   |   |   " +
            "---+---+---" +
            "   |4  |   " +
            " 7 | 5 |   " +
            "   |  6| 2 " +
            "---+---+---" +
            "  1|   |   " +
            " 2 |  3|   " +
            "3  |   |1  ";

        [Fact]
        public void Test1() 
        {
            GameBoard board = new GameBoard()
            {
                Board = new string(_boardA.Where((c) => GameBoard.LegalCharacters.Contains(c)).ToArray())
            };

            Assert.Equal("123   789", board.Row(0));
            Assert.Equal(" 7  5    ", board.Row(4));
            Assert.Equal("3     1  ", board.Row(8));

            Assert.Equal("1       3", board.Column(0));
            Assert.Equal("    5    ", board.Column(4));
            Assert.Equal("9        ", board.Column(8));

            Assert.Equal("123      ", board.Group(0, 0));
            Assert.Equal("    7    ", board.Group(4, 1));
            Assert.Equal("      1  ", board.Group(8, 8));
        }
    }
}
