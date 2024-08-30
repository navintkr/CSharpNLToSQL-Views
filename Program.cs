// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Plugins;

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(0)
        .AddDebug();
});

// Create kernel
var builder = Kernel.CreateBuilder();
builder.WithCompletionService();
builder.Services.AddLogging(c => c.AddDebug().SetMinimumLevel(LogLevel.Trace));
var serviceProvider = builder.Services.BuildServiceProvider();

// Replace with your own schema. Make sure column Type is included in the schema
string schema = @$"
    CREATE VIEW [dbo].[retailProductsView] AS SELECT [asin] [nvarchar](max) NULL, [title] [nvarchar](max) NULL, [imgUrl] [nvarchar](max) NULL, [productURL] [nvarchar](max) NULL, [stars] [nvarchar](max) NULL, [reviews] [nvarchar](max) NULL,	[price] [nvarchar](max) NULL, [listPrice] [nvarchar](max) NULL,	[category_id] [nvarchar](max) NULL,	[isBestSeller] [nvarchar](max) NULL, [boughtInLastMonth] [nvarchar](max) NULL FROM dbo.retailProducts;. Example Query: SELECT asin, title, imgUrl FROM [dbo].[retailProductsView];
";

string connectionString = Env.Var("ConnectionStrings:Database")!;

builder.Plugins.AddFromObject(new NlpToSqlPlugin(connectionString, schema, serviceProvider, loggerFactory), nameof(NlpToSqlPlugin));
var kernel = builder.Build();

// Create chat history
ChatHistory history = [];

// Get chat completion service
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

// Start the conversation
while (true)
{
    // Get user input
    Console.Write("User > ");
    history.AddUserMessage(Console.ReadLine()!);

    // Enable auto function calling
    OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
    {
        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
    };

    // Get the response from the AI
    var result = chatCompletionService.GetStreamingChatMessageContentsAsync(
        history,
        executionSettings: openAIPromptExecutionSettings,
        kernel: kernel);

    // Stream the results
    string fullMessage = "";
    var first = true;
    await foreach (var content in result)
    {
        if (content.Role.HasValue && first)
        {
            Console.Write("Assistant > ");
            first = false;
        }
        Console.Write(content.Content);
        fullMessage += content.Content;
    }
    Console.WriteLine();

    // Add the message from the agent to the chat history
    history.AddAssistantMessage(fullMessage);
}