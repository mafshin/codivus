using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;
using System.Text;
using Codivus.Graph.Interfaces;
using Codivus.Graph.Models;

namespace Codivus.Graph.Services;

/// <summary>
/// Roslyn-based analyzer for extracting symbols and relationships from C# code
/// </summary>
public class RoslynAnalyzer : IRoslynAnalyzer
{
    private readonly ILogger<RoslynAnalyzer> _logger;

    public RoslynAnalyzer(ILogger<RoslynAnalyzer> logger)
    {
        _logger = logger;
    }

    public async Task<CodeAnalysisResult> AnalyzeFileAsync(
        string filePath, 
        string repositoryId,
        string? projectPath = null,
        CancellationToken cancellationToken = default)
    {
        var result = new CodeAnalysisResult
        {
            FileId = Guid.NewGuid().ToString(),
            FilePath = filePath,
            RepositoryId = repositoryId,
            ProjectId = projectPath != null ? Path.GetFileNameWithoutExtension(projectPath) : null
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

            _logger.LogDebug("Analyzing file {FilePath}", filePath);

            // Read and parse the file
            var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);
            result.Metrics.FileSizeBytes = Encoding.UTF8.GetByteCount(sourceCode);
            result.Metrics.LinesOfCode = sourceCode.Split('\n').Length;

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, path: filePath);
            
            // Create a compilation for semantic analysis
            var compilation = CSharpCompilation.Create(
                assemblyName: Path.GetFileNameWithoutExtension(filePath),
                syntaxTrees: new[] { syntaxTree },
                references: GetBasicReferences());

            var semanticModel = compilation.GetSemanticModel(syntaxTree);

            // Extract symbols
            result.Nodes = ExtractSymbols(syntaxTree, semanticModel, result.FileId, repositoryId).ToList();
            
            // Update metrics based on extracted symbols
            UpdateMetricsFromNodes(result.Metrics, result.Nodes);

            // Detect relationships
            result.Relationships = DetectRelationships(syntaxTree, semanticModel, result.Nodes).ToList();

            _logger.LogDebug("Extracted {NodeCount} nodes and {RelationshipCount} relationships from {FilePath}",
                result.Nodes.Count, result.Relationships.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze file {FilePath}", filePath);
            result.Errors.Add($"Analysis failed: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.AnalysisTime = stopwatch.Elapsed;
        }

        return result;
    }

    public async Task<IEnumerable<CodeAnalysisResult>> AnalyzeProjectAsync(
        string projectPath,
        string repositoryId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CodeAnalysisResult>();

        try
        {
            _logger.LogInformation("Analyzing project {ProjectPath}", projectPath);

            using var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);

            foreach (var document in project.Documents)
            {
                if (document.FilePath != null && document.FilePath.EndsWith(".cs"))
                {
                    var result = await AnalyzeDocumentAsync(document, repositoryId, projectPath, cancellationToken);
                    results.Add(result);
                }
            }

            _logger.LogInformation("Analyzed {FileCount} files in project {ProjectPath}", results.Count, projectPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze project {ProjectPath}", projectPath);
            
            // Create an error result
            var errorResult = new CodeAnalysisResult
            {
                ProjectId = Path.GetFileNameWithoutExtension(projectPath),
                RepositoryId = repositoryId
            };
            errorResult.Errors.Add($"Project analysis failed: {ex.Message}");
            results.Add(errorResult);
        }

        return results;
    }

    public async Task<IEnumerable<CodeAnalysisResult>> AnalyzeSolutionAsync(
        string solutionPath,
        string repositoryId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CodeAnalysisResult>();

        try
        {
            _logger.LogInformation("Analyzing solution {SolutionPath}", solutionPath);

            using var workspace = MSBuildWorkspace.Create();
            var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);

            foreach (var project in solution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    if (document.FilePath != null && document.FilePath.EndsWith(".cs"))
                    {
                        var result = await AnalyzeDocumentAsync(document, repositoryId, project.FilePath, cancellationToken);
                        results.Add(result);
                    }
                }
            }

            _logger.LogInformation("Analyzed {FileCount} files in solution {SolutionPath}", results.Count, solutionPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze solution {SolutionPath}", solutionPath);
            
            // Create an error result
            var errorResult = new CodeAnalysisResult
            {
                RepositoryId = repositoryId
            };
            errorResult.Errors.Add($"Solution analysis failed: {ex.Message}");
            results.Add(errorResult);
        }

        return results;
    }

    public IEnumerable<CodeNode> ExtractSymbols(
        SyntaxTree syntaxTree, 
        SemanticModel semanticModel, 
        string fileId,
        string repositoryId)
    {
        var nodes = new List<CodeNode>();
        var root = syntaxTree.GetRoot();

        // Create file node
        var fileNode = new CodeNode
        {
            Id = fileId,
            Name = Path.GetFileName(syntaxTree.FilePath ?? "unknown"),
            FullName = syntaxTree.FilePath ?? "unknown",
            NodeType = NodeType.File,
            RepositoryId = repositoryId,
            FileId = fileId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        nodes.Add(fileNode);

        // Extract namespaces
        var namespaceVisitor = new NamespaceExtractor(semanticModel, fileId, repositoryId);
        namespaceVisitor.Visit(root);
        nodes.AddRange(namespaceVisitor.Nodes);

        // Extract types (classes, interfaces, structs, enums)
        var typeVisitor = new TypeExtractor(semanticModel, fileId, repositoryId);
        typeVisitor.Visit(root);
        nodes.AddRange(typeVisitor.Nodes);

        // Extract members (methods, properties, fields)
        var memberVisitor = new MemberExtractor(semanticModel, fileId, repositoryId);
        memberVisitor.Visit(root);
        nodes.AddRange(memberVisitor.Nodes);

        return nodes;
    }

    public IEnumerable<CodeRelationship> DetectRelationships(
        SyntaxTree syntaxTree,
        SemanticModel semanticModel,
        IEnumerable<CodeNode> nodes)
    {
        var relationships = new List<CodeRelationship>();
        var nodeDict = nodes.ToDictionary(n => n.FullName, n => n);
        var root = syntaxTree.GetRoot();

        // Detect various types of relationships
        var relationshipDetector = new RelationshipDetector(semanticModel, nodeDict);
        relationshipDetector.Visit(root);
        relationships.AddRange(relationshipDetector.Relationships);

        return relationships;
    }

    private async Task<CodeAnalysisResult> AnalyzeDocumentAsync(
        Document document,
        string repositoryId,
        string? projectPath,
        CancellationToken cancellationToken)
    {
        var result = new CodeAnalysisResult
        {
            FileId = Guid.NewGuid().ToString(),
            FilePath = document.FilePath ?? document.Name,
            RepositoryId = repositoryId,
            ProjectId = projectPath != null ? Path.GetFileNameWithoutExtension(projectPath) : null
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            if (syntaxTree != null && semanticModel != null)
            {
                // Calculate metrics
                var sourceText = await document.GetTextAsync(cancellationToken);
                result.Metrics.FileSizeBytes = Encoding.UTF8.GetByteCount(sourceText.ToString());
                result.Metrics.LinesOfCode = sourceText.Lines.Count;

                // Extract symbols and relationships
                result.Nodes = ExtractSymbols(syntaxTree, semanticModel, result.FileId, repositoryId).ToList();
                UpdateMetricsFromNodes(result.Metrics, result.Nodes);
                result.Relationships = DetectRelationships(syntaxTree, semanticModel, result.Nodes).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze document {FilePath}", document.FilePath);
            result.Errors.Add($"Document analysis failed: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.AnalysisTime = stopwatch.Elapsed;
        }

        return result;
    }

    private static void UpdateMetricsFromNodes(FileAnalysisMetrics metrics, IEnumerable<CodeNode> nodes)
    {
        foreach (var node in nodes)
        {
            switch (node.NodeType)
            {
                case NodeType.Namespace:
                    metrics.NamespaceCount++;
                    break;
                case NodeType.Type:
                    metrics.TypeCount++;
                    break;
                case NodeType.Method:
                    metrics.MethodCount++;
                    break;
                case NodeType.Property:
                    metrics.PropertyCount++;
                    break;
                case NodeType.Field:
                    metrics.FieldCount++;
                    break;
            }
        }
    }

    private static IEnumerable<MetadataReference> GetBasicReferences()
    {
        var refs = new List<MetadataReference>();
        
        // Add essential .NET references
        var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
        if (assemblyPath != null)
        {
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Private.CoreLib.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Console.dll")));
            refs.Add(MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")));
        }

        return refs;
    }
}