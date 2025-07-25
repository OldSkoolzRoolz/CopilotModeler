// Project Name: CopilotModeler
// File Name: RoslynCodeAnalyzer.cs
// Author:  Kyle Crowder
// Github:  OldSkoolzRoolz
// Distributed under Open Source License
// Do not remove file headers




#region

using System.Text.Json;

using CopilotModeler.Data.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#endregion



namespace CopilotModeler.Services;


/// <summary>
///     Provides advanced static analysis of C# code using the Roslyn compiler platform.
///     Extracts and serializes code features such as the Abstract Syntax Tree (AST),
///     Control Flow Graph (CFG), Data Flow Graph (DFG), code metrics, and performs
///     code normalization and anonymization for downstream AI/ML tasks.
/// </summary>
public class RoslynCodeAnalyzer
{



    /// <summary>
    ///     Initializes a new instance of the <see cref="RoslynCodeAnalyzer" /> class.
    /// </summary>
    /// <param name="logger">The logger used for diagnostic and debug output.</param>
    internal RoslynCodeAnalyzer(ILogger<RoslynCodeAnalyzer> logger)
    {
        _logger = logger ?? NullLogger<RoslynCodeAnalyzer>.Instance;
    }






    private static ILogger<RoslynCodeAnalyzer> _logger = NullLogger<RoslynCodeAnalyzer>.Instance;






    /// <summary>
    ///     Creates a Roslyn <see cref="Document" /> instance from the provided source code.
    /// </summary>
    /// <param name="code">
    ///     The source code to be included in the document.
    /// </param>
    /// <param name="fileName">
    ///     The name of the in-memory file to associate with the document. Defaults to "InMemoryFile.cs".
    /// </param>
    /// <returns>
    ///     A <see cref="Document" /> containing the specified source code.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="code" /> is <c>null</c>.
    /// </exception>
    public static Document CreateDocumentFromCode(string code, string fileName = "InMemoryFile.cs")
    {
        if (code == null)
            throw new ArgumentNullException(nameof(code), "Source code cannot be null.");

        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("InMemoryProject", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, fileName, SourceText.From(code));

        _logger?.LogInformation("Document created from code: {FileName}", fileName);

        return document;
    }






    /// <summary>
    ///     Converts an array of <see cref="float" /> values to an array of <see cref="byte" /> values.
    /// </summary>
    /// <param name="floats">
    ///     The array of <see cref="float" /> values to be converted.
    ///     Each float in the input array is represented as 4 bytes in the resulting byte array.
    /// </param>
    /// <returns>
    ///     A <see cref="byte" /> array containing the binary representation of the input <see cref="float" /> array.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when the input <paramref name="floats" /> array is <c>null</c>.
    /// </exception>
    public static byte[] FloatArrayToByteArray(float[] floats)
    {
        if (floats == null) throw new ArgumentNullException(nameof(floats), "Input array cannot be null.");

        var bytes = new byte[floats.Length * sizeof(float)];
        Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);

        return bytes;
    }






    /// <summary>
    ///     Analyzes a C# code document and extracts various code features including AST, CFG, DFG, metrics, normalized code,
    ///     and anonymization map.
    /// </summary>
    /// <param name="doccode">The C# code document to analyze.</param>
    /// <returns>
    ///     A <see cref="CodeAnalysisResult" /> containing AST JSON, CFG JSON, DFG JSON, Metrics JSON, Normalized Code, and
    ///     Anonymization Map JSON.
    /// </returns>
    public async Task<CodeAnalysisResult> AnalyzeCodeAsync(Document doccode)
    {
        try
        {
            var tree = await doccode.GetSyntaxTreeAsync();
            var root = tree?.GetCompilationUnitRoot();

            if (root.ToFullString() == "") throw new ArgumentNullException(nameof(root), "Root cannot be null.");
            if (tree.Length == 0) throw new ArgumentNullException(nameof(tree), "Tree cannot be null.");

            _ = await doccode.GetSemanticModelAsync();

            var astJson = ExtractCommonAstPropertiesJson(root);
            _logger.LogDebug("AST extracted.");

            var (cfgJson, dfgJson) = ExtractCfgDfg(tree, root);
            _logger.LogDebug("CFG and DFG conceptual extraction done.");

            var metricsJson = ExtractMetricsJson(root);
            _logger.LogDebug("Metrics extracted.");

            var codeText = (await doccode.GetTextAsync()).ToString();
            var (normalizedCode, anonymizationMapJson) = AnonymizeAndNormalizeCode(root, codeText);
            _logger.LogDebug("Code anonymized and normalized.");


            return new CodeAnalysisResult
            {
                        ASTJson = astJson,
                        CFGJson = cfgJson,
                        DFGJson = dfgJson,
                        MetricsJson = metricsJson,
                        NormalizedCode = normalizedCode,
                        AnonymizationMapJson = anonymizationMapJson

                        //    Embeddings = FloatArrayToByteArray(embeddings)
            };
        }
        catch (JsonException jex)
        {
            _logger.LogError("A JSON error occurred while analyzing the code. Path={0} on Line Num {1}", jex.Path, jex.LineNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError($"An error occurred while analyzing the code: {ex.Message}");
        }

        return new CodeAnalysisResult
        {
                    ASTJson = null,
                    CFGJson = null,
                    DFGJson = null,
                    MetricsJson = null,
                    NormalizedCode = null,
                    AnonymizationMapJson = null
        };
    }






    /// <summary>
    ///     Anonymizes and normalizes the provided C# code by renaming identifiers and removing comments.
    ///     This is useful for preparing code for AI/ML tasks where sensitive or identifiable information
    ///     needs to be obfuscated while preserving the structural integrity of the code.
    /// </summary>
    /// <param name="root">
    ///     The root of the syntax tree representing the parsed C# code.
    /// </param>
    /// <param name="code">
    ///     The original C# code as a string.
    /// </param>
    /// <returns>
    ///     A tuple containing:
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 <c>normalizedCode</c>: The anonymized and normalized version of the input code.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <c>anonymizationMapJson</c>: A JSON string representing the mapping of original identifiers
    ///                 to their anonymized counterparts.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </returns>
    /// <remarks>
    ///     This method performs basic anonymization by renaming identifiers and removing comments.
    ///     It is not designed for production-level anonymization and may not handle all edge cases.
    /// </remarks>
    public (string normalizedCode, string anonymizationMapJson) AnonymizeAndNormalizeCode(CompilationUnitSyntax root, string code)
    {
        var anonymizedCode = code;
        var anonymizationMap = new Dictionary<string, string>();

        // Simple variable renaming example (very basic, not production-ready)
        var identifiers = root.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken));
        var anonCounter = 0;

        foreach (var identifier in identifiers.Distinct())
            if (!anonymizationMap.ContainsKey(identifier.Text) && !IsKeyword(identifier.Text))
            {
                var newName = $"anonVar_{anonCounter++}";
                anonymizationMap[identifier.Text] = newName;
                anonymizedCode = anonymizedCode.Replace(identifier.Text, newName);
            }

        // Remove comments (simplified)
        anonymizedCode = RemoveComments(anonymizedCode);

        var anonymizationMapJson = JsonSerializer.Serialize(anonymizationMap, new JsonSerializerOptions { WriteIndented = true });

        return (anonymizedCode, anonymizationMapJson);
    }






    /// <summary>
    ///     Extracts the Control Flow Graph (CFG) and Data Flow Graph (DFG) representations
    ///     from the provided syntax tree and root node of a C# code document.
    /// </summary>
    /// <param name="tree">
    ///     The syntax tree representing the structure of the C# code document.
    /// </param>
    /// <param name="root">
    ///     The root node of the syntax tree, typically a <see cref="CompilationUnitSyntax" />.
    /// </param>
    /// <returns>
    ///     A tuple containing two JSON strings:
    ///     <list type="bullet">
    ///         <item>
    ///             <description><c>cfgJson</c>: The serialized representation of the Control Flow Graph (CFG).</description>
    ///         </item>
    ///         <item>
    ///             <description><c>dfgJson</c>: The serialized representation of the Data Flow Graph (DFG).</description>
    ///         </item>
    ///     </list>
    /// </returns>
    /// <remarks>
    ///     This method performs static analysis using Roslyn's semantic model to extract
    ///     control flow and data flow information for each method in the provided syntax tree.
    ///     The extracted CFG and DFG are serialized into JSON format for further processing.
    /// </remarks>
    internal (string cfgJson, string dfgJson) ExtractCfgDfg(SyntaxTree tree, CompilationUnitSyntax root)
    {
        var cfgList = new List<object>();
        var dfgList = new List<object>();

        // Create a compilation for semantic analysis
        var compilation = CSharpCompilation.Create("AdHocCompilation").AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(typeof(Console).Assembly.Location)).AddSyntaxTrees(tree);

        var semanticModel = compilation.GetSemanticModel(tree);

        // Process each method
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            // --- CFG Extraction ---
            var cfgEntry = new
            {
                        Method = method.Identifier.Text,
                        Blocks = new List<object>()
            };

            // Only analyze control flow for block-bodied methods
            if (method.Body != null)
            {
                var controlFlow = semanticModel.AnalyzeControlFlow(method.Body);

                if (controlFlow != null && controlFlow.Succeeded)
                {
                    var blockId = 0;
                    var blockMap = new Dictionary<SyntaxNode, int>();
                    foreach (var stmt in method.Body.Statements) blockMap[stmt] = blockId++;

                    var blocks = new List<object>();

                    foreach (var stmt in method.Body.Statements)
                    {
                        var successors = new List<int>();
                        var nextStmt = stmt.GetNextStatement();
                        if (nextStmt != null && blockMap.TryGetValue(nextStmt, out var nextId)) successors.Add(nextId);
                        blocks.Add(new
                        {
                                    Id = blockMap[stmt],
                                    Kind = stmt.Kind().ToString(),
                                    Successors = successors
                        });
                    }

                    cfgEntry = new
                    {
                                Method = method.Identifier.Text,
                                Blocks = blocks
                    };
                }
            }
            else if (method.ExpressionBody != null)
            {
                // For expression-bodied members, treat the expression as a single "block"
                var blocks = new List<object>
                {
                            new
                            {
                                        Id = 0,
                                        Kind = method.ExpressionBody.Expression.Kind().ToString(),
                                        Successors = new List<int>()
                            }
                };
                cfgEntry = new
                {
                            Method = method.Identifier.Text,
                            Blocks = blocks
                };
            }

            cfgList.Add(cfgEntry);

            // --- DFG Extraction ---
            DataFlowAnalysis? dataFlow = null;

            if (method.Body != null)
            {
                dataFlow = semanticModel.AnalyzeDataFlow(method.Body);
            }
            else if (method.ExpressionBody != null)
            {
                // Roslyn's AnalyzeDataFlow expects a StatementSyntax, so for expression-bodied members,
                // wrap the expression in an ExpressionStatementSyntax if possible.
                var expr = method.ExpressionBody.Expression;
                var exprStatement = SyntaxFactory.ExpressionStatement(expr);

                try
                {
                    dataFlow = semanticModel.AnalyzeDataFlow(exprStatement);
                }
                catch (Exception)
                {
                    dataFlow = null;
                }
            }


            if (dataFlow != null && dataFlow.Succeeded)
                dfgList.Add(new
                {
                            Method = method.Identifier.Text,
                            ReadInside = dataFlow.ReadInside.Select(s => s.Name).Distinct().ToList(),
                            WrittenInside = dataFlow.WrittenInside.Select(s => s.Name).Distinct().ToList()
                });
            else
                dfgList.Add(new
                {
                            Method = method.Identifier.Text,
                            ReadInside = new List<string>(),
                            WrittenInside = new List<string>()
                });
        }

        var cfgJson = JsonSerializer.Serialize(cfgList, new JsonSerializerOptions { WriteIndented = true });
        var dfgJson = JsonSerializer.Serialize(dfgList, new JsonSerializerOptions { WriteIndented = true });

        return (cfgJson, dfgJson);
    }






    /// <summary>
    ///     Extracts the most common AST node properties for ML learning and serializes the result as JSON.
    ///     How to use:
    ///     •	Call TestExtractCommonAstPropertiesJson with a C# code string to see the structured AST output in your logs.
    ///     •	The ExtractCommonAstPropertiesJson method can be used in your analysis pipeline to generate ML-friendly AST
    ///     representations.
    ///     What’s included in the AST output:
    ///     •	Kind: Node type(e.g., ClassDeclaration, MethodDeclaration)
    ///     •	Name: Identifier/name if present
    ///     •	Type: Type information if present
    ///     •	Value: Literal value if present
    ///     •	Modifiers: Modifiers(e.g., public, static)
    ///     •	Parameters: For methods, an array of parameter names and types
    ///     •	Children: Recursively serialized child nodes
    /// </summary>
    /// <param name="root">The root syntax node (typically CompilationUnitSyntax).</param>
    /// <returns>JSON string representing the structured AST with common properties.</returns>
    public string ExtractCommonAstPropertiesJson(SyntaxNode root)
    {
        static object SerializeNode(SyntaxNode node)
        {
            // Try to get identifier/name if present
            string? name = null;
            if (node is BaseTypeDeclarationSyntax typeDecl)
                name = typeDecl.Identifier.Text;
            else if (node is MethodDeclarationSyntax methodDecl)
                name = methodDecl.Identifier.Text;
            else if (node is PropertyDeclarationSyntax propDecl)
                name = propDecl.Identifier.Text;
            else if (node is VariableDeclaratorSyntax varDecl)
                name = varDecl.Identifier.Text;
            else if (node is ParameterSyntax paramDecl) name = paramDecl.Identifier.Text;

            // Try to get type if present
            string? type = null;
            if (node is VariableDeclarationSyntax varType)
                type = varType.Type.ToString();
            else if (node is ParameterSyntax paramType)
                type = paramType.Type?.ToString();
            else if (node is MethodDeclarationSyntax methodType)
                type = methodType.ReturnType.ToString();
            else if (node is PropertyDeclarationSyntax propType) type = propType.Type.ToString();

            // Try to get value if present
            string? value = null;
            if (node is LiteralExpressionSyntax literal) value = literal.Token.ValueText;

            // Modifiers (public, static, etc.)
            string[]? modifiers = null;
            if (node is MemberDeclarationSyntax member) modifiers = member.Modifiers.Select(m => m.Text).ToArray();

            // Parameters (for methods)
            object[]? parameters = null;
            if (node is MethodDeclarationSyntax methodParams)
                parameters = methodParams.ParameterList.Parameters.Select(p => new { Name = p.Identifier.Text, Type = p.Type?.ToString() }).ToArray();

            // Recursively serialize children
            var children = node.ChildNodes().Select(SerializeNode).ToList();

            return new
            {
                        Kind = node.Kind().ToString(),
                        Name = name,
                        Type = type,
                        Value = value,
                        Modifiers = modifiers,
                        Parameters = parameters,
                        Children = children
            };
        }


        var astObject = SerializeNode(root);

        return JsonSerializer.Serialize(astObject, new JsonSerializerOptions { WriteIndented = true });
    }






    /// <summary>
    ///     Extracts a set of common code metrics from the syntax tree for use in AI/ML training and code analysis.
    ///     <para>
    ///         <b>Use Cases:</b>
    ///         <list type="bullet">
    ///             <item>Feature extraction for code quality prediction models.</item>
    ///             <item>Code similarity, classification, or clustering tasks.</item>
    ///             <item>Code summarization and search.</item>
    ///         </list>
    ///     </para>
    ///     <para>
    ///         <b>Extracted Metrics:</b>
    ///         <list type="bullet">
    ///             <item><c>LineCount</c>: Total number of lines in the file.</item>
    ///             <item><c>MethodCount</c>: Number of method declarations.</item>
    ///             <item><c>TotalParameterCount</c>: Total number of parameters across all methods.</item>
    ///             <item><c>ClassCount</c>: Number of class declarations.</item>
    ///             <item><c>PropertyCount</c>: Number of property declarations.</item>
    ///             <item><c>FieldCount</c>: Number of field declarations.</item>
    ///             <item><c>AverageMethodLength</c>: Average number of lines per method.</item>
    ///             <item><c>MaxMethodLength</c>: Maximum number of lines in a single method.</item>
    ///             <item><c>MinMethodLength</c>: Minimum number of lines in a single method.</item>
    ///             <item><c>CyclomaticComplexity</c>: (Approximate) Cyclomatic complexity summed across all methods.</item>
    ///         </list>
    ///     </para>
    ///     <b>Intent:</b>
    ///     To provide a rich, ML-friendly set of code metrics that capture structural and complexity-related aspects of the
    ///     code, enabling downstream AI models to learn from code characteristics.
    /// </summary>
    /// <param name="root">The root syntax node (typically <see cref="CompilationUnitSyntax" />).</param>
    /// <returns>JSON string representing the extracted code metrics.</returns>
    public string ExtractMetricsJson(CompilationUnitSyntax root)
    {
        var lineCount = root.GetText().Lines.Count;
        var methodDeclarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
        var propertyDeclarations = root.DescendantNodes().OfType<PropertyDeclarationSyntax>().ToList();
        var fieldDeclarations = root.DescendantNodes().OfType<FieldDeclarationSyntax>().ToList();

        var totalParameters = methodDeclarations.Sum(m => m.ParameterList.Parameters.Count);
        var methodLengths = methodDeclarations.Select(m =>
        {
            var span = m.Body != null ? m.Body.GetLocation().GetLineSpan() : m.ExpressionBody != null ? m.ExpressionBody.GetLocation().GetLineSpan() : (FileLinePositionSpan?)null;

            return span != null ? span.Value.EndLinePosition.Line - span.Value.StartLinePosition.Line + 1 : 0;
        }).Where(len => len > 0).ToList();

        var avgMethodLength = methodLengths.Count > 0 ? methodLengths.Average() : 0;
        var maxMethodLength = methodLengths.Count > 0 ? methodLengths.Max() : 0;
        var minMethodLength = methodLengths.Count > 0 ? methodLengths.Min() : 0;

        // Approximate cyclomatic complexity: 1 + number of branch points (if, for, while, case, catch, etc.)
        var cyclomaticComplexity = 0;

        foreach (var method in methodDeclarations)
        {
            var complexity = 1 + method.DescendantNodes().Count(n => n is IfStatementSyntax || n is ForStatementSyntax || n is ForEachStatementSyntax || n is WhileStatementSyntax || n is DoStatementSyntax || n is CaseSwitchLabelSyntax || n is CatchClauseSyntax || n is ConditionalExpressionSyntax || (n is BinaryExpressionSyntax bex && (bex.IsKind(SyntaxKind.LogicalAndExpression) || bex.IsKind(SyntaxKind.LogicalOrExpression))));
            cyclomaticComplexity += complexity;
        }

        var metrics = new Metrics
        {
                    LineCount = lineCount,
                    MethodCount = methodDeclarations.Count,
                    TotalParameterCount = totalParameters,
                    ClassCount = classDeclarations.Count,
                    PropertyCount = propertyDeclarations.Count,
                    FieldCount = fieldDeclarations.Count,
                    AverageMethodLength = avgMethodLength,
                    MaxMethodLength = maxMethodLength,
                    MinMethodLength = minMethodLength,
                    CyclomaticComplexity = cyclomaticComplexity
        };

        return JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true });
    }






    private bool IsKeyword(string text)
    {
        var syntaxKind = SyntaxFacts.GetKeywordKind(text);

        // Simple check for common C# keywords. A full list would be needed for robustness.
        return SyntaxFacts.IsReservedKeyword(syntaxKind) || SyntaxFacts.IsContextualKeyword(syntaxKind);
    }






    private string RemoveComments(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot().WithoutTrivia();
        var rewriter = new CommentRemoverRewriter();
        var newRoot = rewriter.Visit(root);

        return newRoot.ToFullString();
    }






    // A simple SyntaxRewriter to remove comments
    private class CommentRemoverRewriter : CSharpSyntaxRewriter
    {

        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
                return default; // Remove the trivia

            return base.VisitTrivia(trivia);
        }

    }

}


/// <summary>
///     Encapsulates the results of code analysis, including serialized representations
///     of the AST, CFG, DFG, code metrics, normalized code, and the anonymization map.
/// </summary>
public class CodeAnalysisResult
{

    /// <summary>
    ///     Gets or sets the JSON representation of the Abstract Syntax Tree (AST).
    /// </summary>
    public string? ASTJson { get; set; }

    /// <summary>
    ///     Gets or sets the JSON representation of the Control Flow Graph (CFG).
    /// </summary>
    public string? CFGJson { get; set; }

    /// <summary>
    ///     Gets or sets the JSON representation of the Data Flow Graph (DFG).
    /// </summary>
    public string? DFGJson { get; set; }

    /// <summary>
    ///     Gets or sets the JSON representation of the extracted code metrics.
    /// </summary>
    public string? MetricsJson { get; set; }

    /// <summary>
    ///     Gets or sets the normalized and anonymized version of the analyzed code.
    /// </summary>
    public string? NormalizedCode { get; set; }

    /// <summary>
    ///     Gets or sets the JSON representation of the anonymization map.
    /// </summary>
    public string? AnonymizationMapJson { get; set; }

    /// <summary>
    ///     Gets or sets the byte array representing code embeddings, if available.
    /// </summary>
    public byte[]? Embeddings { get; set; }

}


/// <summary>
///     Provides extension methods for analyzing and manipulating C# syntax elements
///     using the Roslyn compiler platform. These methods enhance the capabilities
///     of working with syntax trees and nodes, enabling advanced code analysis and
///     transformation scenarios.
/// </summary>
public static class AnalyzerExtensions
{

    /// <summary>
    ///     Retrieves the next statement in the same block as the specified statement.
    /// </summary>
    /// <param name="stmt">
    ///     The current statement for which the next statement is to be retrieved.
    /// </param>
    /// <returns>
    ///     The next <see cref="StatementSyntax" /> in the block if it exists; otherwise,
    ///     returns the current statement or the last statement in the block.
    /// </returns>
    /// <remarks>
    ///     This method assumes that the provided statement is part of a <see cref="BlockSyntax" />.
    ///     If the statement is not within a block, the method returns the statement itself.
    /// </remarks>
    public static StatementSyntax GetNextStatement(this StatementSyntax stmt)
    {
        if (stmt.Parent is not BlockSyntax parent)
            return stmt; // Return the current statement if parent is null to avoid null dereference

        var idx = parent.Statements.IndexOf(stmt);

        return idx >= 0 && idx < parent.Statements.Count - 1 ? parent.Statements[idx + 1] : parent.Statements.Last();
    }

}