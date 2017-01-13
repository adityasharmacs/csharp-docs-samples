using CommandLine;
using Google.Cloud.Speech.V1Beta1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleCloudSamples
{
    class Options
    {
        [Option('a', "async", Default = false, HelpText =
            "Make an asynchronous request", Required = false)]
        public bool Async { get; set; }

        [Option('m', "stream", Default = false, HelpText =
            "Stream the request a few kilobytes at a time. Simulates audio"
            + "arriving from a microphone.", Required = false)]
        public bool Stream { get; set; }

        [Value(0, HelpText = "A path to a sound file.", Required = true)]
        public string FilePath { get; set; }

    }
    class Recognize
    {
        static void SyncRecognize(string filePath)
        {
            var speech = SpeechClient.Create();
            var response = speech.SyncRecognize(new RecognitionConfig()
            {
                Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                SampleRate = 16000,
            }, RecognitionAudio.FromFile(filePath));
            foreach (var result in response.Results)
            {
                foreach (var alternative in result.Alternatives)
                {
                    Console.WriteLine(alternative.Transcript);
                }
            }
        }
        
        static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);
            var options = result.WithParsed<Options>()
            SyncRecognize(args[0]);
        }
    }
}
