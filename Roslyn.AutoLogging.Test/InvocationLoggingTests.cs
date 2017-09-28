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
    }
}
