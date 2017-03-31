// Copyright(c) 2017 Google Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License. You may obtain a copy of
// the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations under
// the License.
//
using System;
using Xunit;

namespace GoogleCloudSamples
{
    public class Tests
    {
        CommandLineRunner _runner = new CommandLineRunner()
        {
            Command = "dotnet run -- ",
            VoidMain = Translator.Main
        };

        [Fact]
        public void TestTranslate() 
        {
            ConsoleOutput output = _runner.Run("translate", "Hello World");
            Assert.Equal(0, output.ExitCode);
            Assert.Contains("Привет", output.Stdout);
        }

        [Fact]
        public void TestListCodes()
        {
            ConsoleOutput output = _runner.Run("list");
            Assert.Equal(0, output.ExitCode);
            // Confirm that Russian is a listed language code.
            Assert.Contains("\nru\r", output.Stdout);
        }

        [Fact]
        public void TestListLanguages()
        {
            ConsoleOutput output = _runner.Run("list", "-t", "en");
            Assert.Equal(0, output.ExitCode);
            // Confirm that Russian is a listed language code.
            Assert.Contains("ru\tRussian", output.Stdout);
        }

        [Fact]
        public void TestDetectText()
        {
            ConsoleOutput output = _runner.Run("detect", "こんにちは");
            Assert.Equal(0, output.ExitCode);
            // Confirm that Japanese is detected.
            Assert.Contains("ja\tConfidence", output.Stdout);
        }
    }
}
