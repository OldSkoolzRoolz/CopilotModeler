// Project Name: CopilotModeler
// File Name: Training.cs
// Author:  Kyle Crowder
// Github:  OldSkoolzRoolz
// Distributed under Open Source License
// Do not remove file headers




#region

using System.Text.Json;

using CopilotModeler.Data;
using CopilotModeler.Data.Models;
using CopilotModeler.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

#endregion



namespace CopilotModeler.Training;


/// <summary>
///     Represents a trainer for conceptual ML.NET models.
/// </summary>
public class Trainer
{

    /// <summary>
    ///     Prepares and retrieves training data for conceptual ML.NET model training.
    /// </summary>
    /// <param name="logger">
    ///     The logger instance used to log information and errors during data preparation.
    /// </param>
    /// <param name="dbContextFactory">
    ///     A factory for creating instances of <see cref="AIDbContext" /> to access the database.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains a list of
    ///     <see cref="CodeQualityInput" /> objects, which serve as input data for ML.NET training.
    /// </returns>
    /// <exception cref="Exception">
    ///     Thrown when an error occurs while processing code snippets or accessing the database.
    /// </exception>
    internal static async Task<List<CodeQualityInput>> GetTrainingDataAsync(ILogger logger, IDbContextFactory<AIDbContext> dbContextFactory)
    {
        // 5. Conceptual ML.NET Model Training
        logger.LogInformation("Preparing data for conceptual ML.NET training...");
        var trainingData = new List<CodeQualityInput>();
        using var context = await dbContextFactory.CreateDbContextAsync();

        // Fetch some normalized code snippets for training
        if (context.CodeSnippets != null)
        {
            var allSnippets = await context.CodeSnippets.Where(cs => cs.NormalizedCode != null && cs.MetricsJson != null).ToListAsync();

            foreach (var snippet in allSnippets)
                try
                {
                    // Deserialize metrics to populate ML.NET input
                    var metrics = JsonSerializer.Deserialize<Metrics>(snippet.MetricsJson!);
                    if (metrics != null && metrics.CyclomaticComplexity > 0 && metrics.LineCount > 0)

                                // Populate all available features for training
                        trainingData.Add(new CodeQualityInput
                        {
                                    NormalizedCode = snippet.NormalizedCode!,
                                    CyclomaticComplexity = metrics.CyclomaticComplexity,
                                    LineCount = metrics.LineCount,
                                    MethodCount = metrics.MethodCount,
                                    TotalParameterCount = metrics.TotalParameterCount,
                                    ClassCount = metrics.ClassCount,
                                    PropertyCount = metrics.PropertyCount,
                                    FieldCount = metrics.FieldCount,
                                    AverageMethodLength = (float)metrics.AverageMethodLength,
                                    MaxMethodLength = metrics.MaxMethodLength,
                                    MinMethodLength = metrics.MinMethodLength,
                                    SourceOrigin = snippet.SourceOrigin ?? "Unknown",

                                    // TODO: Replace with real label if available
                                    IsHighQuality = new Random().NextDouble() > 0.5
                        });
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error deserializing metrics for snippet {snippet.Id}: {ex.Message}");
                }
        }

        return trainingData;
    }






    /// <summary>
    ///     Trains a machine learning model for code quality analysis using the provided training data
    ///     and saves the trained model to a file. Additionally, demonstrates loading the model and
    ///     making a sample prediction.
    /// </summary>
    /// <param name="trainingData">
    ///     A list of <see cref="CodeQualityInput" /> objects representing the training data for the model.
    /// </param>
    /// <param name="logger">
    ///     An instance of <see cref="ILogger{MlNetTrainer}" /> used for logging information and errors
    ///     during the training and saving process.
    /// </param>
    /// <param name="mlNetTrainer"></param>
    internal static void TrainAndSaveModels(IEnumerable<CodeQualityInput> trainingData, ILogger<Program> logger, MlNetTrainer mlNetTrainer)
    {
        try
        {
            if (trainingData.Any())
            {
                var trainedModel = mlNetTrainer.TrainCodeQualityModel(trainingData);

                if (trainedModel != null) mlNetTrainer.SaveModel(trainedModel, "code_quality_model.zip");
                /*
                    // Example of loading and making a prediction
                    var loadedModel = mlNetTrainer.LoadModel("code_quality_model.zip");
                    var predictionEngine = mlNetTrainer.CreatePredictionEngine(loadedModel);

                    var sampleInput = new CodeQualityInput
                    {
                                NormalizedCode = "public class MyClass { public void MyMethod() { if (true) { } } }",
                                CyclomaticComplexity = 2.0f,
                                LineCount = 5.0f
                    };
                    var prediction = predictionEngine.Predict(sampleInput);
                    logger.LogInformation($"Sample Prediction: IsHighQuality = {prediction.Prediction}, Score = {prediction.Score}");
                    */
            }
            else
            {
                logger.LogInformation("No suitable data found for ML.NET training.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"An unhandled error occurred during the model building process: {ex.Message}");
        }

        logger.LogInformation("AI Coding Agent Model Builder Process Complete.");
    }

}