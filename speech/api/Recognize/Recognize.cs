using CommandLine;
using Google.Cloud.Speech.V1Beta1;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
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

    [Verb("stream", HelpText = "Detects speech in an audio file by streaming "
        + "it to the Speech API.")]
    class StreamOptions : Options { }

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

        // [START speech_async_recognize]
        static object AsyncRecognize(string filePath)
        {
            var speech = SpeechClient.Create();
            var longOperation = speech.AsyncRecognize(new RecognitionConfig()
            {
                Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                SampleRate = 16000,
            }, RecognitionAudio.FromFile(filePath));
            longOperation = longOperation.PollUntilCompleted();
            var response = longOperation.Result;
            foreach (var result in response.Results)
            {
                foreach (var alternative in result.Alternatives)
                {
                    Console.WriteLine(alternative.Transcript);
                }
            }
            return 0;
        }
        // [END speech_async_recognize]

        // [START speech_streaming_recognize]
        static object StreamingRecognize(string filePath)
        {
            var speech = SpeechClient.Create();
            var writeStream = new AnonymousPipeServerStream();
            var readStream = new AnonymousPipeClientStream(
                writeStream.GetClientHandleAsString());
            var audio = RecognitionAudio.FromStream(readStream);
            var longOperation = speech.AsyncRecognize(new RecognitionConfig()
            {
                Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                SampleRate = 16000,
            }, audio);
            longOperation = longOperation.PollOnce();
            Debug.Assert(!longOperation.IsCompleted);
            using (var f = new FileStream(filePath, FileMode.Open))
            {
                f.CopyTo(writeStream);
                writeStream.Close();
            }
            var response = longOperation.Result;
            foreach (var result in response.Results)
            {
                foreach (var alternative in result.Alternatives)
                {
                    Console.WriteLine(alternative.Transcript);
                }
            }
            return 0;
        }
        // [END speech_streaming_recognize]

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<
                SyncOptions, AsyncOptions, StreamOptions>(args).MapResult(
                (SyncOptions opts) => SyncRecognize(opts.FilePath),
                (AsyncOptions opts) => AsyncRecognize(opts.FilePath),
                (StreamOptions opts) => StreamingRecognize(opts.FilePath),
                errs => 1);
        }
    }
}
