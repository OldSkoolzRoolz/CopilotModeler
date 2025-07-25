// Project Name: CopilotModeler
// File Name: DataModels.cs
// Author:  Kyle Crowder
// Github:  OldSkoolzRoolz
// Distributed under Open Source License
// Do not remove file headers




#region

using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.ML.Data;

#endregion



namespace CopilotModeler.Data.Models;


/// <summary>
///     Represents a code snippet and its associated analysis artifacts, metadata,
///     and embeddings for storage and retrieval in the database.
///     •	Intent:
///     To persist a code snippet along with its analysis results(AST, CFG, DFG, metrics), embeddings for semantic search,
///     and metadata such as file location, hash, and source.
///     •	Key Features:
///     •	Stores original and normalized code.
///     •	Holds serialized analysis artifacts (AST, CFG, DFG, metrics).
///     •	Supports embeddings for ML/semantic search.
///     •	Maintains deduplication via code hash.
///     •	Tracks source and modification time.
///     •	Links to related chat messages.
/// </summary>
public class CodeSnippet
{

    /// <summary>
    ///     Gets or sets the unique identifier for the code snippet.
    /// </summary>
    /// <remarks>
    ///     This property serves as the primary key for the <see cref="CodeSnippet" /> entity.
    ///     It uniquely identifies each code snippet stored in the database.
    /// </remarks>
    public int Id { get; set; } // Primary Key

    /// <summary>
    ///     Gets or sets the original C# code.
    /// </summary>
    public string RawCode { get; set; } = string.Empty; // Original C# code

    /// <summary>
    ///     Gets or sets the original file path of the code snippet.
    /// </summary>
    public string? FilePath { get; set; } // Original file path

    /// <summary>
    ///     Gets or sets the start line in the original file.
    /// </summary>
    public int? LineStart { get; set; } // Start line in original file

    /// <summary>
    ///     Gets or sets the end line in the original file.
    /// </summary>
    public int? LineEnd { get; set; } // End line in original file

    /// <summary>
    ///     Gets or sets the serialized Abstract Syntax Tree (AST) in JSON format.
    /// </summary>
    public string? ASTJson { get; set; } // Abstract Syntax Tree

    /// <summary>
    ///     Gets or sets the serialized Control Flow Graph (CFG) in JSON format.
    /// </summary>
    public string? CFGJson { get; set; } // Control Flow Graph

    /// <summary>
    ///     Gets or sets the serialized Data Flow Graph (DFG) in JSON format.
    /// </summary>
    public string? DFGJson { get; set; } // Data Flow Graph

    /// <summary>
    ///     Gets or sets the embeddings for semantic search.
    ///     Stored as VARBINARY(MAX) for SQL Server to hold byte array.
    /// </summary>
    [Column(TypeName = "VARBINARY(MAX)")] // For SQL Server
    public byte[]? Embeddings { get; set; }

    /// <summary>
    ///     Gets or sets the serialized metrics (Cyclomatic Complexity, Line Count, etc.) in JSON format.
    /// </summary>
    public string? MetricsJson { get; set; } // Serialized metrics (Cyclomatic Complexity, Line Count, etc.)

    /// <summary>
    ///     Gets or sets the anonymized/cleaned code for training.
    /// </summary>
    public string? NormalizedCode { get; set; } // Anonymized/cleaned code for training

    /// <summary>
    ///     Gets or sets the mapping for anonymized identifiers in JSON format.
    /// </summary>
    public string? AnonymizationMapJson { get; set; } // Mapping for anonymized identifiers

    /// <summary>
    ///     Gets or sets the hash of RawCode for deduplication.
    /// </summary>
    public string Hash { get; set; } = string.Empty; // Hash of RawCode for deduplication

    /// <summary>
    ///     Gets or sets the last modification timestamp.
    /// </summary>
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow; // Last modification timestamp

    /// <summary>
    ///     Gets or sets the origin of the code (e.g., "GitHub", "StackOverflow").
    /// </summary>
    public string? SourceOrigin { get; set; } // Origin of the code (e.g., "GitHub", "StackOverflow")

    /// <summary>
    ///     Gets or sets the collection of related chat messages if a snippet is referenced in chat.
    /// </summary>
    public ICollection<ChatMessage> RelatedChatMessages { get; set; } = [];

}


/// <summary>
///     Represents a chat conversation, including its messages, associated user,
///     optional summary, and linkage to a related code snippet.
///     intent: To model a user/AI/system conversation, optionally linked to a code snippet, for use in chat-based code
///     review, assistance, or annotation scenarios.
/// </summary>
public class ChatConversation
{

    /// <summary>
    ///     Gets or sets the unique identifier for the chat conversation.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the start time of the conversation.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the end time of the conversation, if ended.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    ///     Gets or sets the user identifier associated with the conversation.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the optional summary of the conversation.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    ///     Gets or sets the ID of the associated code snippet, if any.
    /// </summary>
    public int? AssociatedCodeSnippetId { get; set; }

    /// <summary>
    ///     Gets or sets the associated code snippet for the conversation.
    /// </summary>
    public CodeSnippet? AssociatedCodeSnippet { get; set; } // Navigation property

    /// <summary>
    ///     Gets or sets the collection of messages in the conversation.
    /// </summary>
    public ICollection<ChatMessage> Messages { get; set; } = [];

}


/// <summary>
///     Represents a single message within a chat conversation, including sender role,
///     content, optional code snippet reference, and content embedding for semantic search.
///     •	Intent:
///     To store individual chat messages, their metadata, and relationships to conversations and code snippets, supporting
///     both plain text and code content.
/// </summary>
public class ChatMessage
{

    /// <summary>
    ///     Gets or sets the unique identifier for the chat message.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the identifier of the conversation this message belongs to.
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    ///     Gets or sets the conversation navigation property.
    /// </summary>
    [ForeignKey("ConversationId")]
    public ChatConversation Conversation { get; set; } = default!; // Required navigation property

    /// <summary>
    ///     Gets or sets the timestamp when the message was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets the sender role (e.g., "User", "AI", "System", "Tool").
    /// </summary>
    public string SenderRole { get; set; } = string.Empty; // e.g., "User", "AI", "System", "Tool"

    /// <summary>
    ///     Gets or sets the content of the chat message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the content embedding for semantic search.
    /// </summary>
    [Column(TypeName = "VARBINARY(MAX)")] // For SQL Server
    public byte[]? ContentEmbedding { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the message contains a code snippet.
    /// </summary>
    public bool IsCodeSnippet { get; set; }

    /// <summary>
    ///     Gets or sets the referenced code snippet identifier, if any.
    /// </summary>
    public int? ReferencedCodeSnippetId { get; set; }

    /// <summary>
    ///     Gets or sets the referenced code snippet navigation property.
    /// </summary>
    public CodeSnippet? ReferencedCodeSnippet { get; set; } // Navigation property

}


/// <summary>
///     Represents a collection of metrics used to analyze and evaluate code quality and structure.
/// </summary>
public class Metrics
{

    /// <summary>
    ///     Gets or sets the total number of lines in the code.
    /// </summary>
    public int LineCount { get; set; }

    /// <summary>
    ///     Gets or sets the total number of methods in the code.
    /// </summary>
    public int MethodCount { get; set; }

    /// <summary>
    ///     Gets or sets the total number of parameters across all methods.
    /// </summary>
    public int TotalParameterCount { get; set; }

    /// <summary>
    ///     Gets or sets the total number of classes in the code.
    /// </summary>
    public int ClassCount { get; set; }

    /// <summary>
    ///     Gets or sets the total number of properties in the code.
    /// </summary>
    public int PropertyCount { get; set; }

    /// <summary>
    ///     Gets or sets the total number of fields in the code.
    /// </summary>
    public int FieldCount { get; set; }

    /// <summary>
    ///     Gets or sets the average length of methods in lines.
    /// </summary>
    public double AverageMethodLength { get; set; }

    /// <summary>
    ///     Gets or sets the maximum method length in lines.
    /// </summary>
    public int MaxMethodLength { get; set; }

    /// <summary>
    ///     Gets or sets the minimum method length in lines.
    /// </summary>
    public int MinMethodLength { get; set; }

    /// <summary>
    ///     Gets or sets the cyclomatic complexity of the code.
    /// </summary>
    public int CyclomaticComplexity { get; set; }

}


/// <summary>
///     Expanded input model for ML.NET training, including more code metrics and source information.
///     <para>
///         This class is used as an input schema for ML.NET pipelines to evaluate code quality.
///         It contains normalized code, various code metrics, the source origin, and a label indicating high quality.
///     </para>
/// </summary>
public class CodeQualityInput
{

    /// <summary>
    ///     Gets or sets the normalized (anonymized/cleaned) code used for training.
    /// </summary>
    [LoadColumn(0)]
    public string NormalizedCode { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the cyclomatic complexity of the code.
    /// </summary>
    [LoadColumn(1)]
    public float CyclomaticComplexity { get; set; }

    /// <summary>
    ///     Gets or sets the total number of lines in the code.
    /// </summary>
    [LoadColumn(2)]
    public float LineCount { get; set; }

    /// <summary>
    ///     Gets or sets the total number of methods in the code.
    /// </summary>
    [LoadColumn(3)]
    public float MethodCount { get; set; }

    /// <summary>
    ///     Gets or sets the total number of parameters across all methods.
    /// </summary>
    [LoadColumn(4)]
    public float TotalParameterCount { get; set; }

    /// <summary>
    ///     Gets or sets the total number of classes in the code.
    /// </summary>
    [LoadColumn(5)]
    public float ClassCount { get; set; }

    /// <summary>
    ///     Gets or sets the total number of properties in the code.
    /// </summary>
    [LoadColumn(6)]
    public float PropertyCount { get; set; }

    /// <summary>
    ///     Gets or sets the total number of fields in the code.
    /// </summary>
    [LoadColumn(7)]
    public float FieldCount { get; set; }

    /// <summary>
    ///     Gets or sets the average length of methods in lines.
    /// </summary>
    [LoadColumn(8)]
    public float AverageMethodLength { get; set; }

    /// <summary>
    ///     Gets or sets the maximum method length in lines.
    /// </summary>
    [LoadColumn(9)]
    public float MaxMethodLength { get; set; }

    /// <summary>
    ///     Gets or sets the minimum method length in lines.
    /// </summary>
    [LoadColumn(10)]
    public float MinMethodLength { get; set; }

    /// <summary>
    ///     Gets or sets the origin of the code (e.g., "GitHub", "StackOverflow").
    ///     Used as a categorical feature.
    /// </summary>
    [LoadColumn(11)]
    public string? SourceOrigin { get; set; } // Categorical feature

    /// <summary>
    ///     Gets or sets a value indicating whether the code is considered high quality.
    ///     Used as the label for ML.NET training.
    /// </summary>
    [ColumnName("Label")]
    public bool IsHighQuality { get; set; }

}