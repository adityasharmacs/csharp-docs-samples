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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GoogleCloudSamples
{
    public class BaseTest
    {
        protected struct ConsoleOutput
        {
            public int ExitCode;
            public string Stdout;
        };

        /// <summary>Runs StorageSample.exe with the provided arguments</summary>
        /// <returns>The console output of this program</returns>
        protected static ConsoleOutput Run(params string[] arguments)
        {
            Console.Write("QuickStart.exe ");
            Console.WriteLine(string.Join(" ", arguments));

            using (var output = new StringWriter())
            {
                QuickStart quickStart = new QuickStart(output);
                return new ConsoleOutput()
                {
                    ExitCode = quickStart.Run(arguments),
                    Stdout = output.ToString()
                };
            }
        }
    }

    public class BadCommandTests : BaseTest
    {
        [Fact]
        public void TestNoArgs()
        {
            var ran = Run();
            Assert.Equal(-1, ran.ExitCode);
            Assert.Contains("QuickStart", ran.Stdout);
        }

        [Fact]
        public void TestBadCommand()
        {
            var ran = Run("throb");
            Assert.Equal(-1, ran.ExitCode);
            Assert.Contains("QuickStart", ran.Stdout);
        }

        [Fact]
        public void TestMissingDeleteArg()
        {
            var ran = Run("delete");
            Assert.Equal(-1, ran.ExitCode);
            Assert.Contains("QuickStart", ran.Stdout);
        }
    }

    public class QuickStartTest : BaseTest, IDisposable
    {
        private readonly string _bucketName;
        public QuickStartTest()
        {
            _bucketName = CreateRandomBucket();
        }

        public void Dispose()
        {
            Run("nuke", _bucketName);
        }

        public static string CreateRandomBucket()
        {
            var created = Run("create");
            Assert.Equal(0, created.ExitCode);
            var created_regex = new Regex(@"Created\s+(.+)\.\s*", RegexOptions.IgnoreCase);
            var match = created_regex.Match(created.Stdout);
            Assert.True(match.Success);
            string bucketName = match.Groups[1].Value;
            return bucketName;
        }

        [Fact]
        public void TestCreateAndDeleteBucket()
        {
            ConsoleOutput deleted;
            try
            {
                // Try creating another bucket with the same name.  Should fail.
                var created_again = Run("create", _bucketName);
                Assert.Equal(409, created_again.ExitCode);

                // Try listing the buckets.  We should find the new one.
                var listed = Run("list");
                Assert.Equal(0, listed.ExitCode);
                Assert.Contains(_bucketName, listed.Stdout);
            }
            finally
            {
                deleted = Run("delete", _bucketName);
            }
            Assert.Equal(0, deleted.ExitCode);
            // Make sure a second attempt to delete fails.
            Assert.Equal(404, Run("delete", _bucketName).ExitCode);
        }

        [Fact]
        public void TestListObjectsInBucket()
        {
            // Try listing the files.  There should be none.
            var listed = Run("list", _bucketName);
            Assert.Equal(0, listed.ExitCode);
            Assert.Equal("", listed.Stdout);

            var uploaded = Run("upload", _bucketName, "Hello.txt");
            Assert.Equal(0, uploaded.ExitCode);

            listed = Run("list", _bucketName);
            Assert.Equal(0, listed.ExitCode);
            Assert.Contains("Hello.txt", listed.Stdout);

            var deleted = Run("delete", _bucketName, "Hello.txt");
            Assert.Equal(0, deleted.ExitCode);
        }

        public string[] SplitOutput(string stdout) =>
            stdout.Split('\n')
                .Select((s) => s.Trim()).Where((s) => !string.IsNullOrEmpty(s))
                .OrderBy((s) => s).ToArray();

        [Fact]
        public void TestListObjectsInBucketWithPrefix()
        {
            // Try listing the files.  There should be none.
            var listed = Run("list", _bucketName, "a", null);
            Assert.Equal(0, listed.ExitCode);
            Assert.Equal("", listed.Stdout);

            // Upload 3 files.
            var uploaded = Run("upload", _bucketName, "Hello.txt", "a/1.txt");
            Assert.Equal(0, uploaded.ExitCode);
            uploaded = Run("upload", _bucketName, "Hello.txt", "a/2.txt");
            Assert.Equal(0, uploaded.ExitCode);
            uploaded = Run("upload", _bucketName, "Hello.txt", "a/b/3.txt");
            Assert.Equal(0, uploaded.ExitCode);

            // With no delimiter, we should get all 3 files.
            listed = Run("list", _bucketName, "a/", null);
            Assert.Equal(0, listed.ExitCode);
            Assert.Equal(new string[] {
                "a/1.txt",
                "a/2.txt",
                "a/b/3.txt"
            }, SplitOutput(listed.Stdout));

            // With a delimeter, we should see only direct contents.
            listed = Run("list", _bucketName, "a/", "/");
            Assert.Equal(0, listed.ExitCode);
            Assert.Equal(new string[] {
                "a/1.txt",
                "a/2.txt",
            }, SplitOutput(listed.Stdout));
        }

        [Fact]
        public void TestDownloadObject()
        {
            var uploaded = Run("upload", _bucketName, "Hello.txt");
            Assert.Equal(0, uploaded.ExitCode);
            uploaded = Run("upload", _bucketName, "Hello.txt", "Hello2.txt");
            Assert.Equal(0, uploaded.ExitCode);

            var downloaded = Run("download", _bucketName, "Hello2.txt");
            Assert.Equal(0, downloaded.ExitCode);
            try
            {
                Assert.Equal(File.ReadAllText("Hello.txt"),
                    File.ReadAllText("Hello2.txt"));
                downloaded = Run("download", _bucketName, "Hello.txt",
                    "Hello2.txt");
                Assert.Equal(0, downloaded.ExitCode);
                Assert.Equal(File.ReadAllText("Hello.txt"),
                    File.ReadAllText("Hello2.txt"));
            }
            finally
            {
                File.Delete("Hello2.txt");
            }
        }

        [Fact]
        public void TestGetMetadata()
        {
            var uploaded = Run("upload", _bucketName, "Hello.txt");
            var got = Run("get-metadata", _bucketName, "Hello.txt");
            Assert.Equal(0, got.ExitCode);
            Assert.Contains("Generation", got.Stdout);
            Assert.Contains("Size", got.Stdout);
        }
    }
}