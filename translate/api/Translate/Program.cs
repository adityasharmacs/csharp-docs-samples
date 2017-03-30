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
using CommandLine;
using Google.Cloud.Translation.V2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Translate
{
    [Verb("translate", HelpText = "Translate text.")]
    class TranslateArgs
    {
        [Value(0, HelpText = "The text to translate.",
            Required = true)]
        public string Text { get; set; }

        [Option('i')]
        public string SourceLanguage { get; set; }

        [Option('o', Default="ru")]
        public string TargetLanguage { get; set; }    
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.Unicode;
            Parser.Default.ParseArguments<TranslateArgs>(args)
                .MapResult((TranslateArgs targs) => Translate(targs),
                errs => 1);
        }

        static object Translate(TranslateArgs args)
        {
            TranslationClient client = TranslationClient.Create();
            var response = client.TranslateText(args.Text, args.TargetLanguage, args.SourceLanguage);
            Console.WriteLine(response.TranslatedText);
            return 0;
        }
    }
}
