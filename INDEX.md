## .clinerules/AI_SYSTEM_PROMPT.md - AI System Prompt
  - Configuration file defining the AI assistant's behavior for the OpenLMStudio project. Contains rules for tool usage, file reading protocols, and C# coding standards (records, pattern matching, SOLID principles). Sets the AI to act as a senior .NET developer with specific guidelines for commit discipline and dependency injection.
## .gitignore - Git Ignore Configuration
  - Standard .NET/.NET 8 gitignore with Visual Studio, NuGet, and Rider exclusions. Covers build outputs (Debug/Release/bin/obj), IDE files (.vs/, .vscode/, .idea/), NuGet packages, native libraries, and platform-specific artifacts (.DS_Store, .apk). Project-specific patterns include OpenLMStudio.* and net8.0/.
## build.ps1 - PowerShell Build Script
  - Cross-platform build script for OpenLMStudio targeting Windows ARM64, macOS Apple Silicon, Linux x64, and Android ARM64. Uses `dotnet publish` with the Desktop project, supports `-RID` parameter for runtime selection, `-Clean` flag, and copies OpenLMStudio.exe to project root for win-x64. Suppresses AVLN3001 warning.
## build.sh - Bash Build Script
  - Cross-platform build script mirroring build.ps1 for Linux/macOS. Uses `set -euo pipefail` for strict error handling. Supports `-RID` and `-Configuration` flags via argument parsing, `--clean` flag. Same `dotnet publish` invocation with `/nowarn:AVLN3001` targeting `src/Desktop/OpenLMStudio.Desktop.csproj`. Copies OpenLMStudio.exe to project root for win-x64.
## Directory.Build.props - MSBuild Directory Build Properties
  - Root-level MSBuild props file that centralizes output paths for all sub-projects. Sets `BaseOutputPath` to `build/bin/` and `BaseIntermediateOutputPath` to `build/obj/$(MSBuildProjectName)/` to avoid MSB3539 warnings and prevent attribute conflicts. Imported early by MSBuild before SDK imports.
## README.md - Project README
  - 157-line project overview covering OpenLMStudio's purpose (cross-platform .NET 8 + Avalonia LLM interface), key features table (inference server, GGUF loading, image generation, agentic harness, etc.), architecture diagram (Application/Domain/Infrastructure/Desktop layers), tech stack, build/run instructions, publish commands for win-x64/osx-arm64/linux-x64, OpenAI-compatible API usage examples, development status table for all components, and links to documentation subdirectory.
