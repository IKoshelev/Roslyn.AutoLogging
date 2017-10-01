using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Roslyn.Syntax.Util;
using System;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Roslyn.Autologging
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RoslynAutologCodeRefactoringProvider)), Shared]
    public class RoslynAutologCodeRefactoringProvider : CodeRefactoringProvider
    {
        public static string LoggerClassName = "_log";
        public static string LogMethodEntryName = "LogMethodEntry";
        public static string LogMethodReturnName = "LogMethodReturn";
        public static string LogAssignmentName = "LogAssginment";

        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var node = root.FindNode(context.Span);

            var methodDecl = node as MethodDeclarationSyntax;
            if (methodDecl == null)
            {
                return;
            }

            var action = CodeAction.Create("Insert entry and return logging", 
                c => InsertEntryAndReturnLogging(context.Document, methodDecl, c));

            context.RegisterRefactoring(action);

            action = CodeAction.Create("Insert assignment logging",
                c => InsertAssignmentLogging(context.Document, methodDecl, c));

            context.RegisterRefactoring(action);
        }

        private async Task<Document> InsertEntryAndReturnLogging(
                                                            Document document, 
                                                            MethodDeclarationSyntax methodDecl,
                                                            CancellationToken cancellationToken)
        {
            var workspace = document.Project.Solution.Workspace;
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newMethod = methodDecl;

            var parameterIdentifiers = methodDecl.GetParameterIdentifiers();

            newMethod = AddEntryLogging(newMethod, parameterIdentifiers);

            newMethod = AddReturnLogging(newMethod);

            newMethod = newMethod.WithAdditionalAnnotations(Formatter.Annotation);

            var formattedMethod = Formatter.Format( newMethod, 
                                                    Formatter.Annotation, 
                                                    workspace, 
                                                    workspace.Options, 
                                                    cancellationToken);

            var newDocumentRoot = root.ReplaceNode(methodDecl, formattedMethod);
            document = document.WithSyntaxRoot(newDocumentRoot);
            return document;
        }

        private async Task<Document> InsertAssignmentLogging(
                                                    Document document,
                                                    MethodDeclarationSyntax methodDecl,
                                                    CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            

            //var parameterIdentifiers = methodDecl.GetParameterIdentifiers();

            //var variableDeclarations = methodDecl.GetLocalVariableIdentifiers();

            //var typeDecl = methodDecl.GetDeclaringType();

            //var typeMembers = typeDecl.GetFieldAndPropertyDeclarations();

            var assignments = methodDecl
                                    .Body
                                    .DescendantNodes()
                                    .OfType<AssignmentExpressionSyntax>()
                                    .Where(methodDecl.DirectlyContains)
                                    .ToArray();

            var newMethod = methodDecl.TrackNodes(assignments);

            //foreach (var assignment in assignments)
            //{
            //    var a = assignment;
            //    var logExpression = GetLoggingStatementForAssignment(methodDecl, LogAssignmentName, assignment.Left);
            //    newMethod = newMethod.WithTracking(node => node.ReplaceNode, a, new[] { a, logExpression });
            //}

            var newDocumentRoot = root.ReplaceNode(methodDecl, newMethod);
            document = document.WithSyntaxRoot(newDocumentRoot);
            return document;
        }

        private MethodDeclarationSyntax AddEntryLogging(
            MethodDeclarationSyntax methodDecl,
            SyntaxToken[] identifiersToLog)
        {
            var logInvocationSyntax = GetLoggingStatementWithDictionaryState(
                                                        methodDecl,
                                                        LogMethodEntryName, 
                                                        identifiersToLog);

            logInvocationSyntax = logInvocationSyntax.WithTrailingTrivia(SF.LineFeed, SF.LineFeed);

            MethodDeclarationSyntax newMethod = methodDecl.WithStatementAtTheBegining(logInvocationSyntax);

            return newMethod;
        }

        private static StatementSyntax GetLoggingStatementWithDictionaryState(
            MethodDeclarationSyntax methodDecl,
            string logMethodName, 
            params SyntaxToken[] stateToLog)
        {
            var methodName = methodDecl.Identifier.TrimmedText();

            string logInvocationStr =
                    $@"{LoggerClassName}.{logMethodName}(nameof({methodName})";

            if (stateToLog.Any())
            {
                var dictionaryLiteralParameters = stateToLog
                                                        .Select(x => x.TrimmedText())
                                                        .Select(x => $"{{nameof({x}),{x}}}");

                var dictionaryLiteralParametersStr = String.Join(",\r\n", dictionaryLiteralParameters);

                logInvocationStr +=
                    $@", new Dictionary<string,object>()
{{
{dictionaryLiteralParametersStr}
}});
";
            }
            else
            {
                logInvocationStr += ");";
            }

            var statement = SF.ParseStatement(logInvocationStr);

            return statement;
        }

        private static StatementSyntax GetLoggingStatementWithSingleState(
                                                         MethodDeclarationSyntax methodDecl,
                                                         string logMethodName,
                                                         SyntaxToken stateToLog)
        {
            var methodName = methodDecl.Identifier.TrimmedText();

            string logInvocationStr =
                    $@"{LoggerClassName}.{logMethodName}(nameof({methodName}), {stateToLog.TrimmedText()});";

            var statement = SF.ParseStatement(logInvocationStr);

            return statement;
        }

        private static StatementSyntax GetLoggingStatementForAssignment(
                                                 MethodDeclarationSyntax methodDecl,
                                                 string logMethodName,
                                                 ExpressionSyntax stateToLog)
        {
            var methodName = methodDecl.Identifier.TrimmedText();
            var expressionText = stateToLog.ToString();

            string logInvocationStr =
                    $@"{LoggerClassName}.{logMethodName}(nameof({methodName}), nameof({expressionText}), {expressionText});";

            var statement = SF.ParseStatement(logInvocationStr);

            return statement;
        }

        private MethodDeclarationSyntax AddReturnLogging(MethodDeclarationSyntax methodDecl)
        {
            var returnType = methodDecl.ReturnType;
            MethodDeclarationSyntax newMethod = methodDecl;

            var isVoid = returnType.ChildTokens().Any(x => x.Kind() == SyntaxKind.VoidKeyword);

            var returnStatements = methodDecl
                                        .Body
                                        .DescendantNodes()
                                        .OfType<ReturnStatementSyntax>()
                                        .Where(node => methodDecl.DirectlyContains(node))
                                        .ToArray();

            newMethod = newMethod.TrackNodes(returnStatements);

            if (isVoid)
            {
                var loggingInvocation = GetLoggingStatementWithDictionaryState(methodDecl, LogMethodReturnName);

                loggingInvocation = loggingInvocation.WithTrailingTrivia(SF.LineFeed);

                foreach(var statement in returnStatements)
                {
                    newMethod = newMethod.WithTracking(x => x.InsertNodesBefore, statement, new[] { loggingInvocation });
                }

                return newMethod;
            }

            var resultBuffVarBame = "result";
            var isResultBuffVarCreated = false;
            foreach (var statement in returnStatements)
            {
                switch (statement.Expression)
                {
                    case IdentifierNameSyntax id:             
                        var loggingInvocation = GetLoggingStatementWithSingleState(
                                                                methodDecl, 
                                                                LogMethodReturnName, 
                                                                id.ChildTokens().Single());

                        loggingInvocation = loggingInvocation
                                                    .WithLeadingTrivia(SF.LineFeed)
                                                    .WithTrailingTrivia(SF.LineFeed);

                        newMethod = newMethod.WithTracking(x => x.InsertNodesBefore, statement, new[] { loggingInvocation });
                        break;

                    default:
                        if (isResultBuffVarCreated == false)
                        {
                            var declaration = LocalVariableExtensions
                                                    .LocalVairableDeclaration(
                                                            methodDecl.ReturnType,
                                                            resultBuffVarBame,
                                                            statement.Expression)
                                                    .WithTrailingTrivia(SF.EndOfLine("\r\n"));

                            newMethod = newMethod.WithTracking(x => x.InsertNodesBefore, statement, new[] { declaration });
                            isResultBuffVarCreated = true;
                        }
                        else
                        {
                            var assignment = SF.ExpressionStatement(
                                                    SF.AssignmentExpression(
                                                        SyntaxKind.SimpleAssignmentExpression,
                                                        SF.IdentifierName(resultBuffVarBame),
                                                        statement.Expression))
                                                    .WithTrailingTrivia(SF.EndOfLine("\r\n"));

                            newMethod = newMethod.WithTracking(x => x.InsertNodesBefore, statement, new[] { assignment });
                        }
                     
                        var logExpression = GetLoggingStatementWithSingleState(
                                                                            methodDecl,
                                                                            LogMethodReturnName,
                                                                            SF.Identifier(resultBuffVarBame))
                                                .WithTrailingTrivia(SF.EndOfLine("\r\n"));

                        newMethod = newMethod.WithTracking(x => x.InsertNodesBefore, statement, new[] { logExpression });
                        var newReturn = SF.ReturnStatement(SF.IdentifierName(" result"));

                        newMethod = newMethod.WithTracking(x => x.ReplaceNode, statement, new[] { newReturn });
                        break;
                }
            }

            return newMethod;
        }

      
    }
}