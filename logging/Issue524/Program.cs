/*
 * Copyright (c) 2016 Google Inc.
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

using Google.Api;
using Google.Logging.Type;
using Google.Logging.V2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Issue524
{
    class Program
    {
        static readonly string s_projectId = 
            Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID");

        static void Main(string[] args)
        {
            Program m = new Program();
            m.Go();
        }

        void Go()
        {
            WriteLogEntry("logForTestListEntries", "Example log entry.");
            WriteLogEntry("logForTestListEntries", "Another example log entry.");
            WriteLogEntry("logForTestListEntries", "Additional example log entry.");
            ListLogEntries("logForTestListEntries");
            DeleteLog("logForTestListEntries");
            WriteLogEntry("logForTestCreateLogEntry", "Example log entry.");
            ListLogEntries("logForTestCreateLogEntry");
            throw new Exception("This line of code never exceutes!");
            DeleteLog("logForTestCreateLogEntry");
        }

        private void WriteLogEntry(string logId, string message)
        {
            var client = LoggingServiceV2Client.Create();
            string logName = $"projects/{s_projectId}/logs/{logId}";
            LogEntry logEntry = new LogEntry();
            logEntry.LogName = logName;
            logEntry.Severity = LogSeverity.Info;
            string entrySeverity = logEntry.Severity.ToString().ToUpper();
            logEntry.TextPayload =
                $"{entrySeverity} {GetType().Namespace}.LoggingSample - {message}";
            // Set the resource type to control which GCP resource the log entry belongs to.
            // See the list of resource types at:
            // https://cloud.google.com/logging/docs/api/v2/resource-list
            // This sample uses 'global' which will cause log entries to appear in the 
            // "Global" resource list of the Developers Console Logs Viewer:
            //  https://console.cloud.google.com/logs/viewer
            MonitoredResource resource = new MonitoredResource();
            resource.Type = "global";
            // Create dictionary object to add custom labels to the log entry.
            IDictionary<string, string> entryLabels = new Dictionary<string, string>();
            entryLabels.Add("size", "large");
            entryLabels.Add("color", "red");
            // Add log entry to collection for writing. Multiple log entries can be added.
            IEnumerable<LogEntry> logEntries = new LogEntry[] { logEntry };
            client.WriteLogEntries(logName, resource, entryLabels, logEntries);
            Console.WriteLine($"Created log entry in log-id: {logId}.");
        }

        private void ListLogEntries(string logId)
        {
            var client = LoggingServiceV2Client.Create();
            string logName = $"projects/{s_projectId}/logs/{logId}";
            IEnumerable<string> projectIds = new string[] { s_projectId };
            var results = client.ListLogEntries(projectIds, logName, "timestamp desc");
            foreach (var row in results)
            {
                if (row != null && !String.IsNullOrEmpty(row.TextPayload))
                {
                    Console.WriteLine($"{row.TextPayload.Trim()}");
                }
            }
        }

        private void DeleteLog(string logId)
        {
            var client = LoggingServiceV2Client.Create();
            string logName = $"projects/{s_projectId}/logs/{logId}";
            client.DeleteLog(logName);
            Console.WriteLine($"Deleted {logId}.");
        }

    }
}
