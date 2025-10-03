# StoryGeneratorConsole

Interactive, console-based story generation with AI-assisted narration and optional image generation. Built on .NET 9 with dependency injection, typed logging, and a Spectre.Console UI.

## Features
- Guided premise creation, branching choices, and story continuation
- Flow-based architecture:
  - Description (premise)
  - Begin (opening)
  - Continue (next steps)
  - Image (optional image for latest scene)
- Console UI with panels, spinners, and prompts
- Configurable via `appsettings.json` and environment variables

## Requirements
- .NET 9 SDK
- (Optional) OpenAI API key for text generation
- (Optional) Running ComfyUI instance for image generation

## Quick start
- dotnet --version
- dotnet restore
- dotnet run --project StoryGeneratorConsole.csproj

## Visual Studio 2022:
- Open the folder or `StoryGeneratorConsole.csproj`
- Set `StoryGeneratorConsole` as startup project
- Start debugging (F5)

## Configuration
The app loads `appsettings.json` and environment variables.

Example `appsettings.json`:
- 
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
