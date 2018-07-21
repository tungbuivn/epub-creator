using System;
using Xunit;
using epub_creator;

namespace Tests
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            Program.RegisterDependencies(new[] {"" }, (container) =>
            {
                
            });
        }
    }
}