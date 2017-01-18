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
        [Value(0, HelpText = "A path to a sound file.  Encoding must be "
            + "Linear16 with a sample rate of 16000.", Required = true)]
        public string FilePath { get; set; }
    }

    [Verb("sync", HelpText = "Detects speech in an audio file.")]
    class SyncOptions : Options { }

    [Verb("async", HelpText = "Creates a job to detect speech in an audio "
        + "file, and waits for the job to complete.")]
    class AsyncOptions : Options { }


    class Recognize
    {
        // [START speech_sync_recognize]
        static object SyncRecognize(string filePath)
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
            return 0;
        }
        // [END speech_sync_recognize]

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<SyncOptions>(args).MapResult(
                (SyncOptions opts) => SyncRecognize(opts.FilePath),
                errs => 1);
        }
    }
}
