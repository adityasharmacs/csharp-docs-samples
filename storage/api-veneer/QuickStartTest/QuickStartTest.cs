// Copyright 2016 Google Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;


namespace GoogleCloudSamples
{
    public class QuickStartTest
    {
        struct RunResult
        {
            public int ExitCode;
            public string Stdout;
        };

        /// <summary>Runs StorageSample.exe with the provided arguments</summary>
        /// <returns>The console output of this program</returns>
        RunResult Run(params string[] arguments)
        {
            var standardOut = Console.Out;
            using (var output = new StringWriter())
            {
                Console.SetOut(output);
                try
                {
                    return new RunResult()
                    {
                        ExitCode = QuickStart.Main(arguments),
                        Stdout = output.ToString()
                    };
                }
                finally
                {
                    Console.SetOut(standardOut);
                }
            }
        }

        [Fact]
        public void TestCreateAndDelete()
        {
            // Create a randomly named bucket.
            var created = Run("create");
            Assert.Equal(0, created.ExitCode);
            var created_regex = new Regex(@"Created\s+(.+)\.\s*", RegexOptions.IgnoreCase);
            var match = created_regex.Match(created.Stdout);
            Assert.True(match.Success);
            string bucketName = match.Groups[1].Value;
            RunResult deleted;
            try
            {
                // Try creating another bucket with the same name.  Should fail.
                var created_again = Run("create", bucketName);
                Assert.Equal(409, created_again.ExitCode);

                // Try listing the buckets.  We should find the new one.
                var listed = Run("list");
                Assert.Equal(0, listed.ExitCode);
                Assert.Contains(bucketName, listed.Stdout);
            }
            finally
            {
                deleted = Run("delete", bucketName);
            }
            Assert.Equal(0, deleted.ExitCode);
            // Make sure a second attempt to delete fails.
            Assert.Equal(404, Run("delete", bucketName).ExitCode);
        }
    }
}