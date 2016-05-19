using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CommandLine;

namespace GoogleCloudSamples
{
    [TestClass]
    public class UnitTest
    {
        [TestMethod]
        public void TestExport()
        {
            string projectId = Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID");
            Options options = new Options();
            var args = new[] { "-p", projectId, "-o", $"gs://{projectId}/test_table.csv"};
            Assert.IsTrue(Parser.Default.ParseArguments(args, options));
            var job = Program.ExportTable(options);
            Assert.AreEqual("DONE", job.Status.State);            
        }
    }
}
