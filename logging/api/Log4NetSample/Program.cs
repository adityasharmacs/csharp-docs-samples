/*
 * Copyright (c) 2018 Google Inc.
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
using log4net;
using log4net.Config;
using System;
using System.IO;

namespace GoogleCloudSamples
{
    class Program
    {
        static void Main(string[] args)
        {
            log4net.Util.LogLog.InternalDebugging = true;
            // Configure log4net to use Google Stackdriver logging from the XML
            // configuration file.
            XmlConfigurator.Configure(LogManager.GetRepository(
                typeof(Program).Assembly), new FileInfo("log4net.xml"));

            // Retrieve a logger for this context.
            ILog log = LogManager.GetLogger(typeof(Program));
            // Log some information. This log entry will be sent to Google
            // Stackdriver Logging.
            log.Info("An exciting log entry!");
            LogManager.Flush(10000);
            Console.WriteLine("Hello World!");
        }
    }
}
