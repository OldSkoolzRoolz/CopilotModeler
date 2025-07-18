// Project Name: DataModelerTests
// File Name: MlNetTrainerTest.cs
// Author:  Kyle Crowder
// Github:  OldSkoolzRoolz
// Distributed under Open Source License
// Do not remove file headers




using CopilotModeler.Data.Models;
using CopilotModeler.Services;

using JetBrains.Annotations;

using Microsoft.Extensions.Logging;
using Microsoft.ML;

using Moq;



namespace DataModelerTests.Services;


[TestClass]
[TestSubject(typeof(MlNetTrainer))]
public class MlNetTrainerTest
{

    private Mock<ILogger<MlNetTrainer>> _mockLogger = null!;
    private MlNetTrainer _trainer = null!;






    [TestMethod]
    public void CreatePredictionEngine_NullModel_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _trainer.CreatePredictionEngine(null!));
    }






    [TestMethod]
    public void CreatePredictionEngine_ValidModel_ReturnsPredictionEngine()
    {
        var mlContext = new MLContext();
        var data = new List<CodeQualityInput>
        {
                    new() { NormalizedCode = "SampleCode", CyclomaticComplexity = 1, LineCount = 10 }
        };
        var dataView = mlContext.Data.LoadFromEnumerable(data);
        var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(CodeQualityInput.NormalizedCode)).Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression());
        var model = pipeline.Fit(dataView);

        var predictionEngine = _trainer.CreatePredictionEngine(model);

        Assert.IsNotNull(predictionEngine);
    }






    [TestMethod]
    public void LoadModel_InvalidPath_ThrowsFileNotFoundException()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _trainer.LoadModel("InvalidPath"));
    }






    [TestMethod]
    public void LoadModel_ValidPath_ReturnsModel()
    {
        // Arrange
        var modelPath = Path.GetTempFileName();
        var mlContext = new MLContext();
        var data = new List<CodeQualityInput>
        {
                    new() { NormalizedCode = "SampleCode", CyclomaticComplexity = 1, LineCount = 10 }
        };
        var dataView = mlContext.Data.LoadFromEnumerable(data);
        var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(CodeQualityInput.NormalizedCode)).Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression());
        var model = pipeline.Fit(dataView);
        mlContext.Model.Save(model, dataView.Schema, modelPath);

        // Act
        var loadedModel = _trainer.LoadModel(modelPath);

        // Assert
        Assert.IsNotNull(loadedModel);
    }






    [TestMethod]
    public void SaveModel_NullModel_DoesNotThrow()
    {
        // Act
        _trainer.SaveModel(null!, "DummyPath");

        // Assert
        _mockLogger.Verify(logger => logger.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Never);
    }






    [TestMethod]
    public void SaveModel_ValidModel_SavesSuccessfully()
    {
        // Arrange
        var modelPath = Path.GetTempFileName();
        var mlContext = new MLContext();
        var data = new List<CodeQualityInput>
        {
                    new() { NormalizedCode = "SampleCode", CyclomaticComplexity = 1, LineCount = 10 }
        };
        var dataView = mlContext.Data.LoadFromEnumerable(data);
        var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(CodeQualityInput.NormalizedCode)).Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression());
        var model = pipeline.Fit(dataView);

        // Act
        _trainer.SaveModel(model, modelPath);

        // Assert
        Assert.IsTrue(File.Exists(modelPath));
    }






    [TestInitialize]
    public void TestInitialize()
    {
        _mockLogger = new Mock<ILogger<MlNetTrainer>>();
        _trainer = new MlNetTrainer(_mockLogger.Object);
    }






    [TestMethod]
    public void TrainCodeQualityModel_EmptyData_ReturnsNull()
    {
        // Arrange
        var trainingData = new List<CodeQualityInput>();

        // Act
        var model = _trainer.TrainCodeQualityModel(trainingData);

        // Assert
        Assert.IsNull(model);
        _mockLogger.Verify(logger => logger.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }






    [TestMethod]
    public void TrainCodeQualityModel_NullData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _trainer.TrainCodeQualityModel(null!));

    }

}
