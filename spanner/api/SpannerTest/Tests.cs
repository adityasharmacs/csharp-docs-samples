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

using Google.Cloud.Spanner.Data;
using System;
using Xunit;
using Xunit.Sdk;

namespace GoogleCloudSamples.Spanner
{
    public class QuickStartTests
    {
        [Fact]
        public void TestQuickStart()
        {
            CommandLineRunner runner = new CommandLineRunner()
            {
                VoidMain = QuickStart.Main,
                Command = "QuickStart"
            };
            var result = runner.Run();
            Assert.Equal(0, result.ExitCode);
        }
    }

    public class SpannerTests
    {
        private static readonly string s_projectId =
            Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID");
        private static readonly string _instanceName = 
            Environment.GetEnvironmentVariable("TEST_SPANNER_INSTANCE") ?? "my-instance";
        private static readonly string _databaseId =
            Environment.GetEnvironmentVariable("TEST_SPANNER_DATABASE") ?? "my-database";

        readonly CommandLineRunner _spanner = new CommandLineRunner()
        {
            VoidMain = Program.Main,
            Command = "Spanner"
        };

        void QuerySampleData()
        {
            ConsoleOutput output = _spanner.Run("querySampleData",
                s_projectId, _instanceName, _databaseId);
            Assert.Equal(0, output.ExitCode);
            Assert.Contains("SingerId : 1 AlbumId : 1", output.Stdout);
            Assert.Contains("SingerId : 2 AlbumId : 1", output.Stdout);
        }

        static bool ContainsError(AggregateException e, ErrorCode errorCode)
        {
            foreach (var innerException in e.InnerExceptions)
            {
                SpannerException spannerException = innerException as SpannerException;
                if (spannerException != null && spannerException.ErrorCode == errorCode)
                    return true;
            }
            return false;
        }
        

        [Fact]
        void TestQuery()
        {
            try
            {
                QuerySampleData();
                return;
            }
            catch (AggregateException e) when (ContainsError(e, ErrorCode.NotFound))
            {
                // Create database and retry.
                _spanner.Run("createSampleDatabase",
                    s_projectId, _instanceName, _databaseId);
                _spanner.Run("insertSampleData",
                    s_projectId, _instanceName, _databaseId);
                QuerySampleData();
            }
            catch (XunitException)
            {
                // Insert sample data and retry.
                _spanner.Run("insertSampleData",
                    s_projectId, _instanceName, _databaseId);
                QuerySampleData();
            }
        }

        //[Fact]
        //void TestQueryTransaction()
        //{
        //    ConsoleOutput output = _spanner.Run("queryDataWithTransaction",
        //        s_projectId, _instanceId, _databseId);
        //    Assert.Equal(0, output.ExitCode);
        //    Assert.Contains("SingerId : 1 AlbumId : 1", output.Stdout);
        //    Assert.Contains("SingerId : 2 AlbumId : 1", output.Stdout);
        //}
        // TODO: Uncomment the above test when the client library is updated
        // for transactions which will enable this test to run consistently
        // without transient issues. 
        // Link to issue: https://github.com/grpc/grpc/issues/11824


        [Fact]
        void TestSpannerNoArgsSucceeds()
        {
            ConsoleOutput output = _spanner.Run();
            Assert.Equal(0, output.ExitCode);
        }
    }
}
