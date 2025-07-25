// Project Name: CopilotModeler
// File Name: InputModel.cs
// Author:  Kyle Crowder
// Github:  OldSkoolzRoolz
// Distributed under Open Source License
// Do not remove file headers




#region

using Microsoft.ML.Data;

#endregion



namespace CopilotModeler.Data.Models;


/// <summary>
///     /// Represents the input data for code quality prediction in ML.NET.
/// </summary>
public class CodeQualityPrediction
{

    /// <summary>
    ///     /// Gets or sets the unique identifier for the code snippet.
    /// </summary>
    [ColumnName("PredictedLabel")]
    public bool Prediction { get; set; }

    /// <summary>
    ///     /// Gets or sets the score of the prediction, indicating the confidence level of the model.
    /// </summary>
    public float Score { get; set; }

}