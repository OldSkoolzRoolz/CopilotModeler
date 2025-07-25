// Project Name: CopilotModeler
// File Name: AIDbContext.cs
// Author:  Kyle Crowder
// Github:  OldSkoolzRoolz
// Distributed under Open Source License
// Do not remove file headers




#region

using CopilotModeler.Data.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

#endregion



namespace CopilotModeler.Data;


/// <summary>
///     Factory class for creating instances of <see cref="AIDbContext" /> at design time.
/// </summary>
/// <remarks>
///     This class is primarily used by tools such as Entity Framework Core migrations to
///     create a database context instance when the application is not running.
///     It retrieves the connection string from the configuration file and configures the
///     <see cref="AIDbContext" /> with the appropriate options.
/// </remarks>
public class AIDbContextFactory : IDesignTimeDbContextFactory<AIDbContext>
{

    /// <summary>
    ///     Creates a new instance of <see cref="AIDbContext" /> with the provided arguments.
    /// </summary>
    /// <param name="args">Command-line arguments (not used).</param>
    /// <returns>An instance of <see cref="AIDbContext" />.</returns>
    public AIDbContext CreateDbContext(string[] args)
    {
        // Use IConfiguration to load the connection string from a configuration file.
        var configuration = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", false, true).Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<AIDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AIDbContext(optionsBuilder.Options, configuration);
    }

}


/// <summary>
///     Entity Framework Core database context for the AI Coding Helper application.
///     Manages persistence and relationships for code snippets, chat conversations,
///     and chat messages.
/// </summary>
public class AIDbContext : DbContext
{

    private readonly IConfiguration? _configuration;






    /// <summary>
    ///     Initializes a new instance of the <see cref="AIDbContext" /> class.
    /// </summary>
    /// <param name="options">The options to be used by a <see cref="DbContext" />.</param>
    /// <param name="configuration">The configuration instance for connection strings (optional).</param>
    public AIDbContext(DbContextOptions<AIDbContext> options, IConfiguration? configuration = null) : base(options)
    {
        _configuration = configuration;
    }






    /// <summary>
    ///     Gets or sets the code snippets in the database.
    /// </summary>
    public DbSet<CodeSnippet>? CodeSnippets { get; set; }

    /// <summary>
    ///     Gets or sets the chat conversations in the database.
    /// </summary>
    public DbSet<ChatConversation>? ChatConversations { get; set; }

    /// <summary>
    ///     Gets or sets the chat messages in the database.
    /// </summary>
    public DbSet<ChatMessage>? ChatMessages { get; set; }






    /// <summary>
    ///     Configures the database context with the connection string from configuration.
    /// </summary>
    /// <param name="optionsBuilder">The options builder for the context.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_configuration != null)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

            optionsBuilder.UseSqlServer(connectionString);
        }
    }






    /// <summary>
    ///     Configures the entity relationships and properties.
    /// </summary>
    /// <param name="modelBuilder">The model builder for the context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure relationships
        _ = modelBuilder.Entity<ChatConversation>().HasMany(c => c.Messages).WithOne(m => m.Conversation).HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade); // Cascade delete messages with conversation

        _ = modelBuilder.Entity<ChatMessage>().HasOne(m => m.ReferencedCodeSnippet).WithMany(cs => cs.RelatedChatMessages).HasForeignKey(m => m.ReferencedCodeSnippetId).OnDelete(DeleteBehavior.SetNull); // Set null if code snippet is deleted

        _ = modelBuilder.Entity<ChatConversation>().HasOne(cc => cc.AssociatedCodeSnippet).WithMany() // No navigation property back from CodeSnippet to ChatConversation
                    .HasForeignKey(cc => cc.AssociatedCodeSnippetId).OnDelete(DeleteBehavior.SetNull); // Set null if code snippet is deleted

        // Configure CodeSnippet properties
        _ = modelBuilder.Entity<CodeSnippet>().Property(cs => cs.RawCode).HasColumnType("nvarchar(max)");
        _ = modelBuilder.Entity<CodeSnippet>().Property(cs => cs.ASTJson).HasColumnType("nvarchar(max)");
        _ = modelBuilder.Entity<CodeSnippet>().Property(cs => cs.CFGJson).HasColumnType("nvarchar(max)");
        _ = modelBuilder.Entity<CodeSnippet>().Property(cs => cs.DFGJson).HasColumnType("nvarchar(max)");
        _ = modelBuilder.Entity<CodeSnippet>().Property(cs => cs.MetricsJson).HasColumnType("nvarchar(max)");
        _ = modelBuilder.Entity<CodeSnippet>().Property(cs => cs.NormalizedCode).HasColumnType("nvarchar(max)");
        _ = modelBuilder.Entity<CodeSnippet>().Property(cs => cs.AnonymizationMapJson).HasColumnType("nvarchar(max)");

        // For Embeddings, if using SQL Server, VARBINARY(MAX) is a common way to store byte arrays.
        // You'd need a ValueConverter if you want to work with float[] directly in C#.
        // Example ValueConverter (needs to be registered in OnModelCreating):
        /*
        modelBuilder.Entity<CodeSnippet>()
            .Property(cs => cs.Embeddings)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), // Convert float[] to JSON string
                v => JsonSerializer.Deserialize<float[]>(v, (JsonSerializerOptions?)null)! // Convert JSON string to float[]
            )
            .HasColumnType("nvarchar(max)"); // Store as string (JSON) in DB
        */
        // Or if storing directly as byte[], ensure the property type is byte[] and column type is VARBINARY(MAX)
        // as defined in the model.
    }

}