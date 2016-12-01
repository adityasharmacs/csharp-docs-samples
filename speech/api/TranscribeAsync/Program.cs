/*
 * Copyright (c) 2015 Google Inc.
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
using System.Linq;
using System.Threading;
using Google.Cloud.Speech.V1Beta1;

namespace GoogleCloudSamples
{
    public class TranscribeAsync
    {
        static public void Main(string[] args)
        {
            if (args.Count() < 1)
            {
                Console.WriteLine("Usage:\nTranscribe audio_file");
                return;
            }
            string audio_file_path = args[0];
            var client = SpeechClient.Create();
            var asyncResponse = client.AsyncRecognize(new RecognitionConfig()
            {
                Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                SampleRate = 16000,
                LanguageCode = "en-US"
            },
                RecognitionAudio.FromFile(audio_file_path));
            var opsClient = Google.Longrunning.OperationsClient.Create();
            Google.Longrunning.Operation op;
            do
            {
                Console.WriteLine("Waiting for server processing...");
                Thread.Sleep(1000);
                op = opsClient.GetOperation(asyncResponse.Name);
            } while (!op.Done);
            var response = op.Response.Unpack<AsyncRecognizeResponse>();
            foreach (var result in response.Results)
            {
                foreach (var alternative in result.Alternatives)
                    Console.WriteLine(alternative.Transcript);
            }
        }
    }
}
