// Project Name: CopilotModeler
// File Name: DataMining.cs
// Author:  Kyle Crowder
// Github:  OldSkoolzRoolz
// Distributed under Open Source License
// Do not remove file headers




#region

using System.Security.Cryptography;
using System.Text;

using CopilotModeler.Data;
using CopilotModeler.Data.Models;
using CopilotModeler.Services;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Octokit;

using Project = Microsoft.CodeAnalysis.Project;

#endregion



namespace CopilotModeler.DataExtraction;


/// <summary>
///     Provides methods for data mining and analysis of C# code from MSBuild projects and GitHub repositories.
/// </summary>
public class DataMining
{



    /// <summary>
    ///     Analyzes an MSBuild project by processing its documents, extracting code features,
    ///     and persisting the results to the database.
    /// </summary>
    /// <param name="project">
    ///     The MSBuild project to analyze, represented as a Roslyn <see cref="Microsoft.CodeAnalysis.Project" />.
    /// </param>
    /// <param name="roslynAnalyzer">
    ///     An instance of <see cref="RoslynCodeAnalyzer" /> used for static code analysis.
    /// </param>
    /// <param name="dbContextFactory">
    ///     A factory for creating instances of <see cref="AIDbContext" /> to interact with the database.
    /// </param>
    /// <param name="logger">
    ///     A logger instance for logging information, warnings, and errors during the analysis process.
    /// </param>
    /// <returns>
    ///     A <see cref="Task" /> representing the asynchronous operation.
    /// </returns>
    /// <remarks>
    ///     This method processes each document in the project, performs static analysis using the Roslyn analyzer,
    ///     and saves the extracted data to the database. If the project contains no documents, an error is logged.
    /// </remarks>
    internal static async Task AnalyzeMSbuildProject(Project project, RoslynCodeAnalyzer roslynAnalyzer, IDbContextFactory<AIDbContext> dbContextFactory, ILogger<Program> logger)
    {
        using var dbContext = await dbContextFactory.CreateDbContextAsync();

        try
        {
            if (project.HasDocuments)
                foreach (var doc in project.Documents)
                {
                    if (!doc.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;

                    logger.LogInformation($"Starting analyzing document {doc.Name}");


                    CodeAnalysisResult analysisResult;

                    try
                    {
                        analysisResult = await roslynAnalyzer.AnalyzeCodeAsync(doc);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Analyzer error");

                        throw;
                    }

                    _ = dbContext.CodeSnippets?.Add(new CodeSnippet
                    {
                                SourceOrigin = $"GitHub:{project.FilePath}",
                                RawCode = doc.GetTextAsync().Result.ToString(),
                                Hash = doc.GetHashCode().ToString(),
                                FilePath = project.FilePath,
                                LineStart = null,
                                LineEnd = null,
                                LastModifiedDate = File.GetLastWriteTimeUtc(project.FilePath!),
                                ASTJson = analysisResult.ASTJson,
                                CFGJson = analysisResult.CFGJson,
                                DFGJson = analysisResult.DFGJson,
                                Embeddings = new byte[]
                                {
                                },
                                MetricsJson = analysisResult.MetricsJson,
                                NormalizedCode = analysisResult.NormalizedCode,
                                AnonymizationMapJson = analysisResult.AnonymizationMapJson
                    });
                }
            else
                logger.LogError("No documents found in project");
        }
        catch (Exception exc)
        {
            logger.LogError(exc, "Error processing documents");
        }

        _ = await dbContext.SaveChangesAsync();

        logger.LogInformation($"Finished processing documents for project {project.Name}");
    }






    /// <summary>
    ///     Computes the SHA-256 hash of the provided string.
    /// </summary>
    /// <param name="rawData">The input string to compute the hash for.</param>
    /// <returns>A string representing the hexadecimal SHA-256 hash of the input data.</returns>
    internal static string CalculateSha256Hash(string rawData)
    {
        using var sha256Hash = SHA256.Create();
        var bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        StringBuilder builder = new();
        for (var i = 0; i < bytes.Length; i++) _ = builder.Append(bytes[i].ToString("x2"));

        return builder.ToString();
    }






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
    public static Document CreateDocumentFromCode(string code, string fileName = "InMemoryFile.cs")
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("InMemoryProject", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, fileName, SourceText.From(code));

        return document;
    }






    /// <summary>
    ///     Loads a solution or project file into a Roslyn <see cref="Solution" /> instance.
    /// </summary>
    /// <param name="solutionFile">
    ///     The path to the solution (.sln) or project (.csproj) file to be loaded.
    /// </param>
    /// <returns>
    ///     A <see cref="Solution" /> instance representing the loaded solution or project.
    /// </returns>
    public static Solution LoadSolutionFile(string solutionFile)
    {
        var workspace = MSBuildWorkspace.Create();
        workspace.LoadMetadataForReferencedProjects = true;

        if (solutionFile.EndsWith("sln", StringComparison.OrdinalIgnoreCase))
            workspace.OpenSolutionAsync(solutionFile).Wait();
        else if (solutionFile.EndsWith("csproj", StringComparison.OrdinalIgnoreCase))
            workspace.OpenProjectAsync(solutionFile).Wait();

        return workspace.CurrentSolution;
    }






    /// <summary>
    ///     Processes a list of public C# repositories from GitHub by analyzing their code and storing the results in a
    ///     database.This method attempts to load project/sln file from archive. can be problematic, needs more testing.
    /// </summary>
    /// <param name="csharpRepos">A list of GitHub repositories containing C# projects to be processed.</param>
    /// <param name="logger">The logger instance for logging information and errors during processing.</param>
    /// <param name="gitHubExtractor">The service responsible for extracting repository data from GitHub.</param>
    /// <param name="roslynAnalyzer">The analyzer used to process and analyze the C# code in the repositories.</param>
    /// <param name="dbContextFactory">A factory for creating database context instances to store analysis results.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async Task ProcessGitHubRepoAsync(List<Repository> csharpRepos, ILogger<Program> logger, GitHubExtractor gitHubExtractor, RoslynCodeAnalyzer roslynAnalyzer, IDbContextFactory<AIDbContext> dbContextFactory)
    {
        foreach (var repo in csharpRepos)
            if (!repo.Private)
            {
                using var context = await dbContextFactory.CreateDbContextAsync();

                if (context.CodeSnippets != null && !await context.CodeSnippets.AnyAsync(cs => cs.SourceOrigin == $"GitHub:{repo.Id}"))
                {
                    var solutionPath = await gitHubExtractor.ExtractDefaultBranchProjectAsync(repo);


                    var proj = Directory.EnumerateFiles(solutionPath, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();

                    if (proj is not null)
                    {
                        var sol = LoadSolutionFile(proj);

                        if (sol != null)
                            try
                            {
                                foreach (var project in sol.Projects)
                                    await AnalyzeMSbuildProject(project, roslynAnalyzer, dbContextFactory, logger).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Error processing a project");
                            }
                    }
                }
            }
    }






    /// <summary>
    ///     Processes a list of GitHub repositories by extracting C# file paths, analyzing their content,
    ///     and persisting the results into the database.
    /// </summary>
    /// <param name="logger">
    ///     The logger instance used for logging information during the processing of repositories.
    /// </param>
    /// <param name="dbContextFactory">
    ///     A factory for creating instances of <see cref="AIDbContext" /> to interact with the database.
    /// </param>
    /// <param name="gitHubExtractor">
    ///     The service responsible for extracting file paths and file content from GitHub repositories.
    /// </param>
    /// <param name="roslynAnalyzer">
    ///     The analyzer used for processing and analyzing the content of C# files.
    /// </param>
    /// <param name="csharpRepos">
    ///     A list of GitHub repositories to be processed.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    internal static async Task ProcessReposRawAsync(ILogger logger, IDbContextFactory<AIDbContext> dbContextFactory, GitHubExtractor gitHubExtractor, RoslynCodeAnalyzer roslynAnalyzer, List<Repository> csharpRepos)
    {
        foreach (var repo in csharpRepos)
        {
            using var context = await dbContextFactory.CreateDbContextAsync();

            // Check if repository already exists to avoid duplicates
            if (context.CodeSnippets != null && !await context.CodeSnippets.AnyAsync(cs => cs.SourceOrigin == $"GitHub:{repo.Id}"))
            {
                logger.LogInformation("Processing repository: {RepoFullName}", repo.FullName);

                var filePaths = await gitHubExtractor.GetCSharpRepositoryFilePaths(repo.Id, repo.Owner.Login, repo.Name);

                foreach (var fileItem in filePaths)
                {
                    // Limit file size for content extraction to avoid excessive data/rate limits
                    if (fileItem.Size is > 0 and < 200 * 1024) // Max 200KB file content
                    {
                        var fileContent = await gitHubExtractor.GetFileContentAsync(repo, fileItem);

                        if (!string.IsNullOrEmpty(fileContent))
                        {
                            var codeHash = CalculateSha256Hash(fileContent);

                            try
                            {
                                // Check for duplicate code snippets by hash
                                if (await context.CodeSnippets.AnyAsync(cs => cs.Hash == codeHash))
                                {
                                    logger.LogInformation("Skipping duplicate code snippet (hash: {CodeHash}) from {FileItemPath}", codeHash, fileItem.Path);

                                    continue;
                                }

                                var doc = CreateDocumentFromCode(fileContent);
                                CodeAnalysisResult? analysisResult = null;

                                // Perform Roslyn analysis
                                analysisResult = await roslynAnalyzer.AnalyzeCodeAsync(doc);

                                // Create CodeSnippet entity
                                var codeSnippet = new CodeSnippet
                                {
                                            RawCode = fileContent,
                                            FilePath = fileItem.Path,
                                            LineStart = 0, // Roslyn analysis is on snippet, so line numbers are relative
                                            LineEnd = fileContent.Split('\n').Length, //not accurate but a placeholder
                                            ASTJson = analysisResult.ASTJson,
                                            CFGJson = analysisResult.CFGJson,
                                            DFGJson = analysisResult.DFGJson,
                                            MetricsJson = analysisResult.MetricsJson,
                                            NormalizedCode = analysisResult.NormalizedCode,
                                            AnonymizationMapJson = analysisResult.AnonymizationMapJson,
                                            Hash = codeHash,
                                            LastModifiedDate = repo.UpdatedAt.DateTime,
                                            SourceOrigin = $"GitHub:{repo.Id}",
                                            Embeddings = analysisResult.Embeddings
                                };

                                _ = context.CodeSnippets.Add(codeSnippet);
                                await context.SaveChangesAsync(); // Save all changes to the database after processing the entire repository Save on db resources
                            }
                            catch (Exception ex) //
                            {
                                logger.LogError($"Error saving code snippet from {repo.FullName}/{fileItem.Path}: {ex.Message}");
                                await context.SaveChangesAsync(); // Ensure we save even if there's an error

                                continue; // Skip to next file if error occurs TEMP redundant flow control change
                            }
                        }
                    }
                    else
                    {
                        logger.LogWarning("Skipping large file content for {FileItemPath} ({FileItemSize} KB)", fileItem.Path, fileItem.Size / 1024);
                    }

                    logger.LogInformation("Finished analyzing the file {0}", fileItem.Path);
                } // ForEach fileItem in filePaths

                logger.LogInformation("Finished processing all files in repository {RepoFullName}", repo.FullName);
                await context.SaveChangesAsync(); // Save all changes to the database after processing the entire repository Save on db resources
            }
            else
            {
                logger.LogInformation("Repository {RepoFullName} already processed. Skipping.", repo.FullName);
            }
        }
    }

}