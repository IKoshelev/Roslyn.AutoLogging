using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Roslyn.AutoLogging.Test
{
    [TestClass]
    public class InvocationLoggingTests
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
        public void OnMethodNameClick_CanInsertAssignmentLogging()
        {
            var testClassFileContents = @"
using System;

public class FooBar
{
    void TestMethod(int a, FooBar b)
    {
        a = null;
    }
}";

            var testClassExpectedNewContents = @"
using System;

public class FooBar
{
    void TestMethod(int a, FooBar b)
    {
        a = null;
        _log.LogAssginment(nameof(TestMethod), nameof(a), a);
    }
}";

            TestUtil.TestAssertingEndText(
                            testClassFileContents,
                            "TestMethod",
                            testClassExpectedNewContents,
                            1);
        }

        [TestMethod]
        public void OnMethodNameClick_CanInsertRelevantDeclarationLogging()
        {
            var testClassFileContents = @"
using System;

public class FooBar
{
    void TestMethod(int a, FooBar b)
    {
        object c = null;
        var d = new int[0];
        var e = new Object();
        var f = new FooBar()
        {
            g = SomeMethod()           
        };
    }
}";

            var testClassExpectedNewContents = @"
using System;

public class FooBar
{
    void TestMethod(int a, FooBar b)
    {
        object c = null;
        var d = new int[0];
        var e = new Object();
        _log.LogAssginment(nameof(TestMethod), nameof(e), e);
        var f = new FooBar()
        {
            g = SomeMethod()
        };
        _log.LogAssginment(nameof(TestMethod), nameof(f), f);
    }
}";

            TestUtil.TestAssertingEndText(
                            testClassFileContents,
                            "TestMethod",
                            testClassExpectedNewContents,
                            1);
        }
    }
}
