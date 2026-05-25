# Example Chat Transcripts

This directory contains example chat transcripts for OpenLMStudio. Each file is a complete `Chat` object in JSON format that can be loaded directly via `LoadChatAsync`.

## Files

| # | File | Scenario |
|---|------|----------|
| 1 | `01-basic-chat.json` | Simple user/assistant back-and-forth about C# fundamentals |
| 2 | `02-agentic-coding-workflow.json` | Agent uses tools (search_files, read_file, write_to_file, replace_in_file, execute_command) to refactor a codebase |
| 3 | `03-image-generation.json` | User asks for logo designs, AI generates multiple images with different prompts/parameters |
| 4 | `04-pasted-image-analysis.json` | User pastes screenshots into chat, AI analyzes UI bugs and suggests XAML fixes |
| 5 | `05-multi-tool-debugging.json` | Agent chains search_files → read_file → replace_in_file to find and fix a NullReferenceException |
| 6 | `06-code-review.json` | AI reviews a PR with detailed inline comments on code changes |
| 7 | `07-agentic-task-execution.json` | Agent orchestrates a multi-step deployment with subagents and browser sessions |
| 8 | `08-browser-session.json` | Agent browses multiple websites to research LLM inference optimization |
| 9 | `09-mixed-content.json` | Long conversation with code blocks, image generation, tool calls, and user interactions |

## Usage

Each file can be loaded directly:

```csharp
var chat = await conversationManager.LoadChatAsync(chatId);
```

Or imported from the file path:

```csharp
var chat = await conversationManager.ImportChatAsync("path/to/example.json");
```

## Format

All files follow the `Chat` model schema with:
- `Id` (GUID)
- `Name`
- `Description`
- `StoragePath`
- `ModelId`
- `SystemPrompt`
- `Temperature`
- `MaxTokens`
- `Tags`
- `Messages` (array of `Message` objects)
- `ImageOutputs` (array of `ImageOutput` objects, when applicable)

Each `Message` contains:
- `Id` (GUID)
- `Role` (User/Assistant/System/Tool)
- `Content` (text with markdown)
- `ToolCalls` (array of `ToolCall` objects)
- `TokenCount`
- `CreatedAt`

Each `ToolCall` contains:
- `Id`
- `FunctionName`
- `ArgumentsJson`
- `Result`

## Generating More Examples

To create a new example:
1. Copy any existing file as a template
2. Update the `Id` to a new GUID
3. Update the `Name` and `Description`
4. Modify the `Messages` array
5. Add `ImageOutputs` if the chat includes image generation