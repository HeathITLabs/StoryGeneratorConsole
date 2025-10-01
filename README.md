# StoryGeneratorConsole

This is the code block that represents the suggested code change:

Note: The current code registers services directly; options binding may be added later.

## Usage
- Answer the initial questions to refine the premise (or type `start` to begin).
- Choose the next action from the list or type a custom action.
- The app can generate an image for the latest story part when available.
- Continue until the primary objective is achieved.

## Project layout (high level)
- `StoryGeneratorConsole.csproj` — Console entry point and DI composition
- `StoryGenerator.Presentation.Console/Program.cs` — UI loop and flow orchestration
- `StoryGenerator.Application/` — Flow engine and flow contracts/implementations
- `StoryGenerator.Core/` — Domain models and shared abstractions (if present)
- `StoryGenerator.AI.Services/` — Integrations (OpenAI, ComfyUI)

## Development
- Build: `dotnet build -c Debug`
- Run: `dotnet run`
- Release: `dotnet build -c Release`

Key files:
- `Program.cs` — Host setup, logging, and main loop
- `FlowEngine.cs` — Runtime flow registry and execution
- Flows — `DescriptionFlow`, `BeginStoryFlow`, `ContinueStoryFlow`, `ImageGenerationFlow`

## Troubleshooting
- Missing API key: Set `OpenAI__ApiKey` or add it to `appsettings.json`.
- Connection issues (ComfyUI): Ensure the base URL is reachable (default `http://localhost:8188`).
- SDK mismatch: Confirm `.NET 9` is installed (`dotnet --info`).

## License
Add your license here (e.g., MIT).