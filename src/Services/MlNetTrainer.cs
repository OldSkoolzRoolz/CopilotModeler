// Project Name: CopilotModeler
// File Name: MlNetTrainer.cs
// Author:  Kyle Crowder
// Github:  OldSkoolzRoolz
// Distributed under Open Source License
// Do not remove file headers




#region

using CopilotModeler.Data.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML;

#endregion



namespace CopilotModeler.Services;


/// <summary>
///     Zero-dependency ML.NET trainer for code quality prediction.
/// </summary>
public class MlNetTrainer
{

    private readonly ILogger<MlNetTrainer> _logger;
    private readonly MLContext _mlContext;






    /// <summary>
    ///     Initializes a new instance of the <see cref="MlNetTrainer" /> class.
    /// </summary>
    /// <param name="logger">
    ///     The <see cref="ILogger{TCategoryName}" /> instance used for logging ML.NET events and messages.
    /// </param>
    public MlNetTrainer(ILogger<MlNetTrainer> logger)
    {
        _logger = logger ?? NullLogger<MlNetTrainer>.Instance;
        _mlContext = new MLContext();
        _mlContext.Log += (sender, e) => _logger.LogInformation($"ML.NET: {e.Message}");
    }






    /// <summary>
    ///     Creates a PredictionEngine for a given model.
    /// </summary>
    /// <param name="model">The trained ITransformer model.</param>
    /// <returns>A PredictionEngine instance.</returns>
    public PredictionEngine<CodeQualityInput, CodeQualityPrediction> CreatePredictionEngine(ITransformer model)
    {
        return _mlContext.Model.CreatePredictionEngine<CodeQualityInput, CodeQualityPrediction>(model);
    }






    /// <summary>
    ///     Loads an ML.NET model from a file.
    /// </summary>
    /// <param name="modelPath">The file path of the model.</param>
    /// <returns>The loaded ITransformer model.</returns>
    public ITransformer LoadModel(string modelPath)
    {
        return _mlContext.Model.Load(modelPath, out _);
    }






    /// <summary>
    ///     Saves the trained ML.NET model.
    /// </summary>
    /// <param name="model">The trained ITransformer model.</param>
    /// <param name="modelPath">The file path to save the model to.</param>
    public void SaveModel(ITransformer model, string modelPath)
    {
        if (model != null)
        {
            _mlContext.Model.Save(model, null, modelPath);
            _logger.LogInformation($"ML.NET model saved to {modelPath}");
        }
    }






    /// <summary>
    ///     Trains a binary classification model to predict code quality based on the provided training data.
    /// </summary>
    /// <param name="trainingData">
    ///     A collection of <see cref="CodeQualityInput" /> instances representing the training dataset.
    ///     Each instance includes features such as normalized code, cyclomatic complexity, line count,
    ///     and other code metrics.
    /// </param>
    /// <returns>
    ///     An <see cref="ITransformer" /> representing the trained ML.NET model, or <c>null</c> if the
    ///     training data is empty.
    /// </returns>
    /// <remarks>
    ///     The method constructs a training pipeline that includes text featurization, categorical encoding,
    ///     numeric feature concatenation, and a binary classification trainer using SDCA logistic regression.
    ///     The trained model can be used for predicting whether code is of high quality.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    ///     Thrown if <paramref name="trainingData" /> is <c>null</c>.
    /// </exception>
    public ITransformer? TrainCodeQualityModel(IEnumerable<CodeQualityInput> trainingData)
    {
        if (!trainingData.Any())
        {
            _logger.LogWarning("No training data provided for ML.NET model. Skipping training.");

            return null;
        }

        // Log the number of training samples
        _logger.LogInformation($"ML.NET: Starting training with {trainingData.Count()} samples.");

        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

        // Log pipeline construction
        _logger.LogInformation("ML.NET: Building training pipeline...");

        var textPipeline = _mlContext.Transforms.Text.FeaturizeText("CodeTextFeatures", nameof(CodeQualityInput.NormalizedCode));

        var categoricalPipeline = _mlContext.Transforms.Categorical.OneHotEncoding("SourceOriginEncoded", nameof(CodeQualityInput.SourceOrigin));

        var numericFeatures = new[]
        {
                    nameof(CodeQualityInput.CyclomaticComplexity),
                    nameof(CodeQualityInput.LineCount),
                    nameof(CodeQualityInput.MethodCount),
                    nameof(CodeQualityInput.TotalParameterCount),
                    nameof(CodeQualityInput.ClassCount),
                    nameof(CodeQualityInput.PropertyCount),
                    nameof(CodeQualityInput.FieldCount),
                    nameof(CodeQualityInput.AverageMethodLength),
                    nameof(CodeQualityInput.MaxMethodLength),
                    nameof(CodeQualityInput.MinMethodLength)
        };

        var numericPipeline = _mlContext.Transforms.Concatenate("NumericFeatures", numericFeatures);

        var featurePipeline = _mlContext.Transforms.Concatenate("Features", "CodeTextFeatures", "NumericFeatures", "SourceOriginEncoded");

        var trainer = _mlContext.BinaryClassification.Trainers.SdcaLogisticRegression();

        var pipeline = textPipeline.Append(categoricalPipeline).Append(numericPipeline).Append(featurePipeline).Append(trainer);

        // Log before training starts
        _logger.LogInformation("ML.NET: Training pipeline constructed. Beginning model training...");

        var model = pipeline.Fit(dataView);

        // Log after training completes
        _logger.LogInformation("ML.NET: Model training complete.");

        // Optionally, evaluate the model on the training data and log metrics
        var predictions = model.Transform(dataView);
        var metrics = _mlContext.BinaryClassification.Evaluate(predictions);

        _logger.LogInformation($"ML.NET: Training metrics - Accuracy: {metrics.Accuracy:P2}, AUC: {metrics.AreaUnderRocCurve:P2}, F1 Score: {metrics.F1Score:P2}");

        return model;
    }

}