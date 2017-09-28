using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Roslyn.AutoLogging.Test
{
    [TestClass]
    public class EntryAndReturnLoggingTests
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
        public void OnMethodNameClick_CanInsertEntryLogging()
        {
            var testClassFileContents = @"
using System;

public class FooBar
{
    void TestMethod(int a, FooBar b)
    {
    }
}";

            var testClassExpectedNewContents = @"
using System;

public class FooBar
{
    void TestMethod(int a, FooBar b)
    {
        _log.LogMethodEntry(nameof(TestMethod), new Dictionary<string,object>()
{
{nameof(a),a},
{nameof(b),b}
});
    }
}";

            TestUtil.TestAssertingEndText(
                            testClassFileContents,
                            "TestMethod",
                            testClassExpectedNewContents);
        }

        [TestMethod]
        public void OnMethodNameClick_DoesNotPassStateWhenNoState()
        {
            var testClassFileContents = @"
using System;

public class FooBar
{
    void TestMethod()
    {
    }
}";

            var testClassExpectedNewContents = @"
using System;

public class FooBar
{
    void TestMethod()
    {
        _log.LogMethodEntry(nameof(TestMethod));
    }
}";

            TestUtil.TestAssertingEndText(
                            testClassFileContents,
                            "TestMethod",
                            testClassExpectedNewContents);
        }

        [TestMethod]
        public void OnMethodNameClick_CanInsertReturnLogging()
        {
            var testClassFileContents = @"
using System;

public class FooBar
{
    void TestMethod(int a, FooBar b)
    {
        return;
    }
}";

            var testClassExpectedNewContents = @"
using System;

public class FooBar
{
    void TestMethod(int a, FooBar b)
    {
_log.LogMethodEntry(nameof(TestMethod), new Dictionary<string,object>()
{
{nameof(a),a},
{nameof(b),b}
});

_log.LogMethodReturn(nameof(TestMethod));
        return;
    }
}";

            TestUtil.TestAssertingEndText(
                            testClassFileContents,
                            "TestMethod",
                            testClassExpectedNewContents);
        }

        [TestMethod]
        public void OnInsertingReturnLogging_DoesNotInserForReturnsInInnerFunctions()
        {
            var testClassFileContents = @"
using System;

public class FooBar
{
    void TestMethod(int a, FooBar b)
    {
        void inner(){
            return;
        }
        var a = new int[0];
        var b = a.Select(x => 
        {
            return x;
        });
    }
}";

            var testClassExpectedNewContents = @"
using System;

public class FooBar
{
    void TestMethod(int a, FooBar b)
    {
_log.LogMethodEntry(nameof(TestMethod), new Dictionary<string,object>()
{
{nameof(a),a},
{nameof(b),b}
});

        void inner(){
            return;
        }
        var a = new int[0];
        var b = a.Select(x => 
        {
            return x;
        });
    }
}";

            TestUtil.TestAssertingEndText(
                            testClassFileContents,
                            "TestMethod",
                            testClassExpectedNewContents);
        }

        [TestMethod]
        public void OnInsertingReturnLogging_WillReturnStateOfVarReturned()
        {
            var testClassFileContents = @"
using System;

public class FooBar
{
    object TestMethod()
    {
        var a = null;
        return a;
    }
}";

            var testClassExpectedNewContents = @"
using System;

public class FooBar
{
    object TestMethod()
    {
_log.LogMethodEntry(nameof(TestMethod));

        var a = null;

_log.LogMethodReturn(nameof(TestMethod), a);
        return a;
    }
}";

            TestUtil.TestAssertingEndText(
                            testClassFileContents,
                            "TestMethod",
                            testClassExpectedNewContents);
        }

        [TestMethod]
        public void OnInsertingReturnLogging_WillCreateReturnStatementBufferAndLogIt()
        {
            var testClassFileContents = @"
using System;

public class FooBar
{
    object TestMethod()
    {
        return null;
    }
}";

            var testClassExpectedNewContents = @"
using System;

public class FooBar
{
    object TestMethod()
    {
_log.LogMethodEntry(nameof(TestMethod));

    object result = null;
        _log.LogMethodReturn(nameof(TestMethod), result);
        return  result;
    }
}";

            TestUtil.TestAssertingEndText(
                            testClassFileContents,
                            "TestMethod",
                            testClassExpectedNewContents);
        }

        [TestMethod]
        public void OnInsertingReturnLogging_CanHandleMultipleReturns()
        {
            var testClassFileContents = @"
using System;

public class FooBar
{
    object TestMethod()
    {
        var c = 5;

        return 1;

        return c;

        return SomeMethod();
    }
}";

            var testClassExpectedNewContents = @"
using System;

public class FooBar
{
    object TestMethod()
    {
_log.LogMethodEntry(nameof(TestMethod));

        var c = 5;
    object result = 1;
        _log.LogMethodReturn(nameof(TestMethod), result);
        return  result;
        _log.LogMethodReturn(nameof(TestMethod), c);

        return c;
        result = SomeMethod();
        _log.LogMethodReturn(nameof(TestMethod), result);
        return  result;
    }
}";

            TestUtil.TestAssertingEndText(
                            testClassFileContents,
                            "TestMethod",
                            testClassExpectedNewContents);
        }
    }
}
