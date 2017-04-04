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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SudokuLib
{
    public class GameBoard
    {
        private static string _legalCharacters = "123456789 ";
        private static string _blankBoard = new string(' ', 81);
        private string _board = _blankBoard;

        /// <summary>
        /// The Sudoku game board is represented as an 81-character long string.
        /// The first 9 characters are row 1.  The next 9 are row 2, etc.
        /// The only acceptable characters are 1-9 and space.
        /// </summary>
        public string Board
        {
            get { return _board; }
            set
            {
                if (value.Length != 81)
                {
                    throw new ArgumentException("value", "String must be 81 characters.");
                }
                foreach (char c in value)
                {
                    if (_legalCharacters.IndexOf(c) < 0)
                        throw new ArgumentOutOfRangeException("value", $"Illegal character: {c}");
                }
                _board = value;
            }
        }

        public static string LegalCharacters { get { return _legalCharacters; } }

        /// <summary>
        /// Returns the elements in the row specified by zero-indexed rowNumber.
        /// </summary>
        /// <param name="rowNumber"></param>
        /// <returns></returns>
        public string Row(int rowNumber)
        {
            Debug.Assert(rowNumber >= 0 && rowNumber < 9);
            return _board.Substring(9 * rowNumber, 9);
        }

        public string Column(int colNumber)
        {
            Debug.Assert(colNumber >= 0 && colNumber < 9);
            char[] column = new char[9];
            for (int i = 0; i < 9; ++i)
            {
                column[i] = _board[colNumber + (i * 9)];
            }
            return new string(column);
        }

        /// <summary>
        /// Returns the elements in the row specified by zero-indexed rowNumber.
        /// </summary>
        /// <param name="rowNumber"></param>
        /// <returns></returns>
        public string Group(int rowNumber, int colNumber)
        {
            Debug.Assert(colNumber >= 0 && colNumber < 9);
            Debug.Assert(rowNumber >= 0 && rowNumber < 9);
            int start = (rowNumber - (rowNumber % 3)) * 9 +
                colNumber - (colNumber % 3);
            return _board.Substring(start, 3)
                + _board.Substring(start + 9, 3)
                + _board.Substring(start + 18, 3);
        }

        public IEnumerable<GameBoard> FillNextEmpty()
        {
            var nextGameBoards = new List<GameBoard>();
            int i = _board.IndexOf(' ');
            if (i > 0)
            {
                int rowNumber = i / 9;
                int colNumber = i % 9;
                char[] board = _board.ToCharArray();
                foreach (char move in GetLegalMoves(rowNumber, colNumber))
                {
                    board[i] = move;
                    nextGameBoards.Add(new GameBoard()
                    {
                        Board = new string(board)
                    });
                }
            }
            return nextGameBoards;
        }

        public char ElementAt(int rowNumber, int colNumber)
        {
            Debug.Assert(colNumber >= 0 && colNumber < 9);
            Debug.Assert(rowNumber >= 0 && rowNumber < 9);
            return _board.ElementAt(rowNumber * 9 + colNumber);
        }

        private IEnumerable<char> GetLegalMoves(int rowNumber, int colNumber) =>
            "123456789".Except(Row(rowNumber).Union(Column(colNumber)).Union(Group(rowNumber, colNumber)));

        public override string ToString()
        {
            return _board;
        }
    }
}
