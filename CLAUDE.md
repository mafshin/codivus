# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Codivus is an AI-powered code scanning solution with:
- **Backend**: ASP.NET Core 8 Web API (C#) in `src/Codivus.API/`
- **Frontend**: Vue.js 3 SPA with Vite in `src/Codivus.UI/`
- **Core Library**: Shared models and interfaces in `src/Codivus.Core/`
- **Tests**: xUnit test suite in `src/Codivus.API.Tests/`

## Essential Commands

### Backend Development
```bash
# From src/Codivus.API/
dotnet restore              # Restore packages
dotnet run                  # Run API (https://localhost:5001)
dotnet build               # Build project
dotnet publish -c Release  # Build for production

# From src/Codivus.API.Tests/
dotnet test                # Run all tests
dotnet test --filter "FullyQualifiedName~RepositoryService"  # Run specific tests
```

### Frontend Development
```bash
# From src/Codivus.UI/
npm install    # Install dependencies
npm run dev    # Run dev server (http://localhost:3000)
npm run build  # Build for production
npm run lint   # Run linter
```

## Architecture Overview

### API Structure
- **Controllers**: RESTful endpoints for repositories, scanning, LLM providers
- **Services**: Business logic (RepositoryService, ScanningService)
- **LLM Integration**: Factory pattern for Ollama/LMStudio providers
- **Data Storage**: JSON-based file storage via JsonDataStore
- **Middleware**: Global error handling

### Frontend Structure
- **State Management**: Pinia stores (repository, scanning, settings)
- **Router**: Vue Router for SPA navigation
- **Components**: Modular Vue components in `src/components/`
- **API Service**: Axios-based API client in `src/services/api.js`
- **Real-time Updates**: WebSocket support for scan progress

### Key Technologies
- **LLM Integration**: Supports Ollama and LMStudio for local AI models
- **Git Operations**: LibGit2Sharp for repository management
- **Resilience**: Polly for retry policies
- **Logging**: Serilog with file and console sinks
- **Code Highlighting**: PrismJS for syntax highlighting
- **Visualizations**: D3.js for repository visualization

## Important Configuration

### Backend (appsettings.json)
- LLM providers configuration (model names, endpoints, tokens)
- Storage settings (default: FileSystem)
- Logging configuration with Serilog

### Frontend (vite.config.js)
- API proxy configuration for development
- WebSocket proxy for real-time features

## Testing Approach
- Unit tests use xUnit with Moq for mocking
- File system operations mocked with System.IO.Abstractions
- Test naming convention: MethodName_Scenario_ExpectedResult