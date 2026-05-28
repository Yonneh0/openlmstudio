using Microsoft.Extensions.DependencyInjection;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Application.Types;
using OpenLMStudio.Application.Services;
using OpenLMStudio.Application.Services.Agent;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Application;

/// <summary>
/// Service collection extensions for the application layer.
/// Provides dependency injection configuration for application-layer types only.
/// Infrastructure implementation registrations are handled in Desktop or Infrastructure projects.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all application-layer DTOs and transient services with the dependency injection container.
    /// Domain interface implementations should be registered separately via AddInfrastructureServices.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddApplicationTypes(this IServiceCollection services)
    {
        // Chat request/response DTOs (transient - created per operation)
        services.AddTransient<ChatCompletionRequest>();
        services.AddTransient<ChatMessage>();
        services.AddTransient<ChatChoice>();
        services.AddTransient<ChatCompletionResponse>();

        // Streaming event handler for real-time token delivery
        services.AddTransient<StreamingEventHandler>();

        // Agent types
        services.AddTransient<AgentTaskRequest>();
        services.AddTransient<AgentTaskResult>();
        services.AddTransient<AgentToolCallRecord>();
        services.AddTransient<AgentMessageExchange>();
        services.AddTransient<AgentPlan>();

        // Agent Task Management types
        services.AddSingleton<AgentAutoApprovalSettings>();
        services.AddSingleton<AgentBrowserSettings>();
        services.AddSingleton<AgentFocusChainSettings>();
        services.AddSingleton<AgentTaskProgress>();
        services.AddSingleton<AgentTaskChecklistItem>();
        services.AddSingleton<HookResult>();

        // Agent Task Management services
        services.AddTransient<IAgentTaskManager, AgentTaskManager>();
        services.AddTransient<IAgentTaskCheckpointService, AgentTaskCheckpointService>();
        services.AddTransient<IAgentTaskStateService, AgentTaskStateService>();
        services.AddTransient<IAgentTaskProgressService, AgentTaskProgressService>();
        // AgentTaskAutoApprover registered below as singleton (not transient)
        services.AddTransient<IAgentTaskContextManager, AgentTaskContextManager>();
        services.AddTransient<IAgentTaskHookService, AgentTaskHookService>();

        // Context management services
        services.AddTransient<IContextWindowBudgeter, ContextWindowBudgeter>();
        services.AddTransient<ITokenEstimator, TokenEstimator>();
        services.AddTransient<IContextCompressor, ContextCompressor>();
        services.AddTransient<IContextRelevanceEngine, ContextRelevanceEngine>();
        services.AddTransient<IContextManipulator, ContextManipulator>();
        services.AddSingleton<ContextSnapshotManager>();
        services.AddSingleton<SystemPromptGenerator>();

        // Additional agent services
        services.AddSingleton<AgentSessionService>();

        // ToolAvailabilityRegistry holds tool definitions for the agent
        services.AddSingleton<Domain.Models.ToolAvailabilityRegistry>(resolver =>
        {
            var registry = new Domain.Models.ToolAvailabilityRegistry();
            PopulateToolDefinitions(registry);
            return registry;
        });

        // AgentToolExecutor orchestrates all agent tool execution
        services.AddSingleton<Domain.Interfaces.IAgentToolExecutor>(resolver =>
        {
            var fileSystem = resolver.GetService<Domain.Interfaces.IFileSystemService>();
            var commandExecutor = resolver.GetService<Domain.Interfaces.ICommandExecutor>();
            var browserService = resolver.GetService<Domain.Interfaces.IBrowserService>();
            var mcpService = resolver.GetService<Domain.Interfaces.IMcpService>();
            var webSearchService = resolver.GetService<Domain.Interfaces.IWebSearchService>();
            var patchService = resolver.GetService<PatchService>();
            var webFetchService = resolver.GetService<WebFetchService>();
            var questionService = resolver.GetService<QuestionService>();
            var toolRegistry = resolver.GetService<Domain.Models.ToolAvailabilityRegistry>();
            var logger = resolver.GetService<Microsoft.Extensions.Logging.ILogger<AgentToolExecutor>>();
            if (fileSystem == null || commandExecutor == null || browserService == null ||
                mcpService == null || webSearchService == null || patchService == null ||
                webFetchService == null || questionService == null || toolRegistry == null)
            {
                throw new InvalidOperationException("Required services not found for AgentToolExecutor.");
            }
            return new AgentToolExecutor(fileSystem, commandExecutor, browserService, mcpService, webSearchService, patchService, webFetchService, questionService, toolRegistry, logger);
        });

        services.AddSingleton<AgentTaskAutoApprover>();
        services.AddSingleton<AttemptCompletion>();
        services.AddSingleton<NewTask>();

        return services;
    }

    /// <summary>
    /// Registers the OpenAI-compatible API endpoint handler with the application pipeline.
    /// This method should be called on IApplicationBuilder during app configuration.
    /// </summary>
    public static void ConfigureOpenApiEndpoints(this IServiceCollection services)
    {
        // The endpoint handler is a static class - no DI registration needed
        // It will be invoked directly via the application builder extension method

        // Register any dependencies the OpenAPI endpoints need to resolve from DI
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>();

        return;
    }

    /// <summary>
    /// Convenience method to register all Application and Infrastructure services at once.
    /// </summary>
    public static IServiceCollection AddOpenLMStudioServices(this IServiceCollection services)
    {
        services.AddApplicationTypes();

        // NOTE: ITaskContextReinjectionService, ITaskContextInheritor, ITaskContextPruner
        // are registered in Infrastructure.DependencyInjection, not here.

        return services;
    }

    /// <summary>
    /// Populates the ToolAvailabilityRegistry with all available tool definitions.
    /// </summary>
    private static void PopulateToolDefinitions(Domain.Models.ToolAvailabilityRegistry registry)
    {
        // File operations
        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "write_to_file",
            Description = "Write content to a file within the agent sandbox.",
            RequiredParameters = new List<string> { "path", "content" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "path", "The file path to write to." },
                { "content", "The content to write to the file." },
                { "workingDirectory", "The working directory for the operation." },
                { "agentIgnoreRules", "Agent ignore rules for the operation." }
            }
        });

        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "replace_in_file",
            Description = "Safely replace content in a file within the agent sandbox.",
            RequiredParameters = new List<string> { "path", "diff" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "path", "The file path to modify." },
                { "diff", "The diff to apply." },
                { "workingDirectory", "The working directory for the operation." },
                { "agentIgnoreRules", "Agent ignore rules for the operation." }
            }
        });

        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "read_file",
            Description = "Read the contents of a file within the agent sandbox.",
            RequiredParameters = new List<string> { "path" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "path", "The file path to read." },
                { "startLine", "The 1-based line number to start reading from." },
                { "endLine", "The 1-based line number to stop reading at." },
                { "workingDirectory", "The working directory for the operation." },
                { "agentIgnoreRules", "Agent ignore rules for the operation." }
            }
        });

        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "search_files",
            Description = "Search for files matching a pattern within the agent sandbox.",
            RequiredParameters = new List<string> { "path", "regex" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "path", "The directory path to search in." },
                { "regex", "The regular expression pattern to search for." },
                { "filePattern", "The file pattern to match." },
                { "workingDirectory", "The working directory for the operation." },
                { "agentIgnoreRules", "Agent ignore rules for the operation." }
            }
        });

        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "list_files",
            Description = "List files and directories within the agent sandbox.",
            RequiredParameters = new List<string> { "path" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "path", "The directory path to list." },
                { "recursive", "Whether to list files recursively." },
                { "workingDirectory", "The working directory for the operation." },
                { "agentIgnoreRules", "Agent ignore rules for the operation." }
            }
        });

        // Command execution
        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "execute_command",
            Description = "Execute a command in the sandboxed environment.",
            RequiredParameters = new List<string> { "command", "requires_approval" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "command", "The command to execute." },
                { "requires_approval", "Whether the command requires explicit user approval." },
                { "timeout", "The timeout in seconds." },
                { "workingDirectory", "The working directory for the operation." }
            }
        });

        // Browser actions
        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "browser_action",
            Description = "Perform a browser action (launch, click, type, scroll, close).",
            RequiredParameters = new List<string> { "action" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "action", "The browser action to perform (launch, click, type, scroll_down, scroll_up, close)." },
                { "url", "The URL to navigate to." },
                { "coordinate", "The coordinate to click." },
                { "text", "The text to type." }
            }
        });

        // MCP tools
        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "use_mcp_tool",
            Description = "Use a tool provided by a connected MCP server.",
            RequiredParameters = new List<string> { "server_name", "tool_name", "arguments" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "server_name", "The name of the MCP server providing the tool." },
                { "tool_name", "The name of the tool to execute." },
                { "arguments", "A JSON object containing the tool's input parameters." }
            }
        });

        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "access_mcp_resource",
            Description = "Access a resource provided by a connected MCP server.",
            RequiredParameters = new List<string> { "server_name", "uri" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "server_name", "The name of the MCP server providing the resource." },
                { "uri", "The URI identifying the specific resource to access." }
            }
        });

        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "load_mcp_documentation",
            Description = "Load documentation about creating MCP servers.",
            RequiredParameters = new List<string>(),
            ParameterSchema = new Dictionary<string, string>()
        });

        // Plan/Act mode tools
        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "plan_mode_respond",
            Description = "Respond to the user's inquiry in an effort to plan a solution to the user's task.",
            RequiredParameters = new List<string> { "response" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "response", "The response to provide to the user." },
                { "needs_more_exploration", "Set to true if you need to do more exploration." }
            }
        });

        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "act_mode_respond",
            Description = "Respond to the user's inquiry in an effort to plan a solution to the user's task.",
            RequiredParameters = new List<string> { "response" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "response", "The response to provide to the user." }
            }
        });

        // Completion tools
        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "attempt_completion",
            Description = "Present the result of the task to the user.",
            RequiredParameters = new List<string> { "result" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "result", "The final result of the task." },
                { "command", "A CLI command to execute to show the result." }
            }
        });

        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "new_task",
            Description = "Create a new task with preloaded context.",
            RequiredParameters = new List<string> { "context" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "context", "The context to preload into the new task." }
            }
        });

        // Skill and subagent tools
        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "use_skill",
            Description = "Activate a skill.",
            RequiredParameters = new List<string> { "skill_name" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "skill_name", "The name of the skill to activate." }
            }
        });

        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "use_subagents",
            Description = "Run parallel subagents.",
            RequiredParameters = new List<string> { "prompt_1" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "prompt_1", "The prompt for subagent 1." },
                { "prompt_2", "The prompt for subagent 2." },
                { "prompt_3", "The prompt for subagent 3." },
                { "prompt_4", "The prompt for subagent 4." },
                { "prompt_5", "The prompt for subagent 5." }
            }
        });

        // Patch and explanation tools
        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "apply_patch",
            Description = "Apply a patch to a file.",
            RequiredParameters = new List<string> { "input" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "input", "The patch to apply." },
                { "workingDirectory", "The working directory for the operation." }
            }
        });

        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "generate_explanation",
            Description = "Generate an explanation for code changes between two git refs.",
            RequiredParameters = new List<string> { "title", "from_ref" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "title", "A descriptive title for the diff view." },
                { "from_ref", "The git reference for the 'before' state." },
                { "to_ref", "The git reference for the 'after' state." }
            }
        });

        // Web tools
        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "web_fetch",
            Description = "Fetch content from a URL.",
            RequiredParameters = new List<string> { "url", "prompt" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "url", "The URL to fetch." },
                { "prompt", "The prompt for the fetch." }
            }
        });

        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "web_search",
            Description = "Perform a web search.",
            RequiredParameters = new List<string> { "query" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "query", "The search query." },
                { "allowedDomains", "List of allowed domains." },
                { "blockedDomains", "List of blocked domains." }
            }
        });

        // Question tool
        registry.Register(new Domain.Models.ToolDefinition
        {
            Name = "ask_followup_question",
            Description = "Ask the user a question to gather additional information.",
            RequiredParameters = new List<string> { "question" },
            ParameterSchema = new Dictionary<string, string>
            {
                { "question", "The question to ask the user." },
                { "options", "An array of 2-5 options for the user to choose from." }
            }
        });
    }
}
