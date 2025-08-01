// Project Name: CopilotModeler
// File Name: Program.cs
// Author:  Kyle Crowder
// Github:  OldSkoolzRoolz
// Distributed under Open Source License
// Do not remove file headers




#region

using CopilotModeler.Data;
using CopilotModeler.DataExtraction;
using CopilotModeler.Services;
using CopilotModeler.Training;

using DataExtraction;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Octokit;

#endregion



namespace CopilotModeler
{


// For serializing AST/CFG/DFG/Metrics to JSON


    /// <summary>
    ///     Main orchestration class for the AI Coding Agent Model Builder.
    ///     Handles configuration, dependency injection, data extraction from GitHub,
    ///     code analysis, database persistence, and ML.NET model training.
    /// </summary>
    public class Program
    {



        /// <summary>
        ///     Builds and configures the application's <see cref="IConfigurationRoot" />.
        /// </summary>
        /// <remarks>
        ///     This method sets up the configuration system for the application by:
        ///     - Setting the base path to the current directory.
        ///     - Loading configuration settings from the "appsettings.json" file.
        ///     - Adding environment variables to the configuration.
        /// </remarks>
        /// <returns>
        ///     An instance of <see cref="IConfigurationRoot" /> containing the application's configuration settings.
        /// </returns>
        private static IConfigurationRoot BuildConfigurationRoot()
        {
            return new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", false).AddEnvironmentVariables().Build();
        }






        /// <summary>
        ///     Configures and builds the service provider for dependency injection.
        /// </summary>
        /// <param name="configuration">
        ///     The configuration root containing application settings, such as connection strings and API keys.
        /// </param>
        /// <returns>
        ///     A fully configured <see cref="ServiceProvider" /> instance for resolving application dependencies.
        /// </returns>
        private static ServiceProvider BuildServiceProvider(IConfigurationRoot configuration)
        {
            return new ServiceCollection().AddLogging(builder =>
                        {
                            _ = builder.AddConsole();
                            _ = builder.AddDebug();
                        }).AddSingleton<IConfiguration>(configuration).AddDbContext<AIDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"))).AddDbContextFactory<AIDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"))).AddTransient<GitHubExtractor>(sp =>
                        {
                            var logger = sp.GetRequiredService<ILogger<GitHubExtractor>>();

                            return new GitHubExtractor(configuration.GetSection("GitHubApiKey").Value, logger);
                        }).AddTransient<RoslynCodeAnalyzer>() // Roslyn analyzer service
                        .AddTransient<MlNetTrainer>() // ML.NET trainer service
                        .BuildServiceProvider();
        }






        /// <summary>
        ///     Ensures that the database is properly set up by applying any pending migrations.
        ///     Logs the status of the migration process and handles any errors that occur during the operation.
        /// </summary>
        /// <param name="serviceProvider">
        ///     The <see cref="ServiceProvider" /> instance used to resolve the required services.
        /// </param>
        /// <param name="logger">
        ///     The <see cref="ILogger{TCategoryName}" /> instance used for logging migration status and errors.
        /// </param>
        /// <returns>
        ///     A <see cref="Task{TResult}" /> that represents the asynchronous operation.
        ///     Returns <c>true</c> if an error occurs during the migration process; otherwise, <c>false</c>.
        /// </returns>
        private static async Task<bool> EnsureDatabaseStatus(ServiceProvider serviceProvider, ILogger<Program> logger)
        {
            // 3. Ensure Database is Created/Migrated
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AIDbContext>();

            try
            {
                await dbContext.Database.MigrateAsync(); // Apply pending migrations
                logger.LogInformation("Database migration complete.");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error during database migration: {ex.Message}");

                return true;
            }

            return false;
        }






        /// <summary>
        ///     /     The main entry point for the AI Coding Agent Model Builder application.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            // 1. Setup Configuration
            var configuration = BuildConfigurationRoot();

            // 2. Setup Dependency Injection
            var serviceProvider = BuildServiceProvider(configuration);

            // included for migration purposes
            await Host.CreateDefaultBuilder().Build().StartAsync();


            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Starting AI Coding Agent Model Builder...");
            var factory = serviceProvider.GetRequiredService<ILoggerFactory>();


            // Will create the database if it does not exist and apply any pending migrations.
            if (await EnsureDatabaseStatus(serviceProvider, logger)) return; // Exit if database setup fails

            var gitHubExtractor = serviceProvider.GetRequiredService<GitHubExtractor>();
            var roslynAnalyzer = serviceProvider.GetRequiredService<RoslynCodeAnalyzer>();
            var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<AIDbContext>>();
            var mlNetTrainer = serviceProvider.GetRequiredService<MlNetTrainer>();

            if (args.Length < 1)
            {
                Console.WriteLine("USAGE: dotnet run <command> <optional options> -- Must provide at least a command");

                return;
            }

            var command = args[0];
            var options = new ExtractOptions();

            // Read optional arguments for the extract command defaults are set if not provided
            options.ReadArgs(args[1..]);

            switch (command.ToLower())
            {
                case "extract":

                    try
                    {
                        var repos = await gitHubExtractor.SearchCsharpRepositoriesAsync(options.MinStars, options.NumResultsPerPage, options.NumPages, options.SearchTerm!);

                        await DataMining.ProcessReposRawAsync(logger, dbContextFactory, gitHubExtractor, roslynAnalyzer, repos);
                    }
                    catch (RateLimitExceededException)
                    {
                        await gitHubExtractor.PrintRateLimitInfoAsync();
                    }
                    catch (Exception)
                    {
                        logger.LogError("Unexpected unhandled Error training model ");
                    }

                    break;

                case "train":

                    try
                    {
                        // Process local projects
                        var trainingData = await Trainer.GetTrainingDataAsync(logger, dbContextFactory);
                        Trainer.TrainAndSaveModels(trainingData, logger, mlNetTrainer);
                    }
                    catch (Exception)
                    {
                        logger.LogError("Unexpected unhandled Error training model ");
                    }

                    break;

                case "test":

                    // Run tests
                    Console.WriteLine("Not Implemented.....");

                    break;

                default:

                    Console.WriteLine($"Unknown command: {command}");

                    break;
            }
        }

    }


}


namespace DataExtraction
{


    /// <summary>
    ///     Represents options for extracting repositories from GitHub.
    /// </summary>
    public class ExtractOptions
    {

        /// <summary>
        ///     Gets or sets the minimum number of stars a repository must have to be included in the search.
        ///     Defaults to 500 if not specified.
        /// </summary>
        public int MinStars { get; set; }

        /// <summary>
        ///     Gets or sets the number of results to return per page.
        ///     Defaults to 25 if not specified.
        /// </summary>
        public int NumResultsPerPage { get; set; }

        /// <summary>
        ///     Gets or sets the number of pages of results to retrieve.
        ///     Defaults to 2 if not specified.
        /// </summary>
        public int NumPages { get; set; }

        /// <summary>
        ///     Gets or sets the search term to use for querying repositories.
        ///     Defaults to "pushed:>2025-01-01" if not specified.
        /// </summary>
        public string? SearchTerm { get; set; }






        /// <summary>
        ///     Reads up to 4 arguments and sets properties.
        ///     If an argument is missing, the property returns a default value.
        ///     If an argument is not a valid int (for int properties), the property returns a default value.
        /// </summary>
        /// <param name="args">The array of arguments to read.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="args" /> is null.</exception>
        public void ReadArgs(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            MinStars = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]) && int.TryParse(args[0], out var minStars) ? minStars : 500;
            NumResultsPerPage = args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]) && int.TryParse(args[1], out var numResultsPerPage) ? numResultsPerPage : 25;
            NumPages = args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]) && int.TryParse(args[2], out var numPages) ? numPages : 2;
            SearchTerm = args.Length > 3 && !string.IsNullOrWhiteSpace(args[3]) ? args[3] : "pushed:>2025-01-01";
        }

    }


}