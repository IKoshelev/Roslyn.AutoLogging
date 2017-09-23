using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Roslyn.AutoLogging.Test
{
    [TestClass]
    public class UnitTest
    {
        [TestMethod]
        public void OnEmptyCode_NothingHappens()
        {
            var testClassFileContents = @" ";

            TestUtil.TestAsertingRefactorings(
                testClassFileContents,
                new TextSpan(0, 1),
                (workspace, document, proposedCodeRefactorings) =>
                {
                    var len = proposedCodeRefactorings.Count();
                    Assert.AreEqual(len, 0);
                });
        }

        [TestMethod]
        public void OnTypeNameClick_WillReverseName()
        {
            var testClassFileContents = @"
using System;

public class FooBar
{
}";

            var testClassExpectedNewContents = @"
using System;

public class raBooF
{
}";

            TestUtil.TestAssertingEndText(
                            testClassFileContents,
                            "FooBar",
                            testClassExpectedNewContents);
        }
    }
}
