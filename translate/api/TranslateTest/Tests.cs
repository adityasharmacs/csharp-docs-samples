using System;
using Xunit;

namespace GoogleCloudSamples
{
    public class Tests
    {
        [Fact]
        public void Test1() 
        {
            GoogleCloudSamples.Translator.Main(new[] { "Hello World" });
            Assert.True(true);
        }
    }
}
