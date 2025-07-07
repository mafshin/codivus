# 🔍 Codivus - AI-Enabled Code Scanning Solution

<img src="src/docs/header.png" alt="placeholder"  height="300">

A modern, AI-powered code scanning solution designed to analyze code repositories for potential issues, vulnerabilities, and improvement opportunities. 
The name Codivus is made from “Code” + Latin vivus (alive), breathing insight into your code.

## 🚀 Features

- **Real-time scanning dashboard**: Monitor scanning progress in real-time
- **Interactive repository visualization**: Browse repository file structure through an interactive graph
- **AI-powered analysis**: Leverage Ollama and LMStudio for advanced code analysis
- **Graph-based code analysis**: Advanced semantic code understanding with Neo4j
- **Contextual prompt building**: Enhanced LLM prompts using code graph context
- **Symbol relationship detection**: Automatic detection of code dependencies and relationships
- **IssueHunter integration**: Advanced issue detection and categorization
- **Local and GitHub repository support**: Scan code from local paths or GitHub repositories
- **Modern tech stack**: Vue.js front-end, C# .NET 8 back-end

## 🏗️ Architecture

Codivus consists of five main components:

1. **Codivus.UI**: Vue.js-based front-end application
2. **Codivus.API**: C# .NET 8 RESTful API
3. **Codivus.Core**: Core library containing shared business logic and models
4. **Codivus.Graph**: Neo4j-based graph analysis engine
5. **Codivus.CLI**: Command-line interface for repository management

### Architecture Flow Diagram

![Codivus Architecture Flow](./codivus-flow-diagram.svg)

The diagram above shows the complete flow of Codivus, including:
- Frontend components and user interactions
- API endpoints and controllers
- Business logic services
- LLM integration with Ollama and LMStudio
- Data persistence layer
- External system interactions

To regenerate the diagram from source:
```bash
# Install D2 (https://d2lang.com/tour/install)
# Then run:
d2 codivus-flow-diagram.d2 codivus-flow-diagram.svg
```

## 📝 Requirements

- **.NET 8 SDK**
- **Node.js** v16+
- **npm** v7+
- **Local LLM provider** (Ollama or LMStudio)
- **Neo4j** (required for graph analysis features)

## 🔧 Setup and Installation

See [Installation Guide](./docs/installation.md) for detailed setup instructions.

## 🧪 Development

See [Development Guide](./docs/development.md) for information on project structure and contribution guidelines.

## 🧪 Testing

### Test Categories

#### Unit Tests (Default)
- **What**: Tests with mocked dependencies
- **When**: Run in all environments (local, CI/CD)
- **Coverage**: Core business logic, controllers, services

#### Integration Tests  
- **What**: Tests requiring external services
- **When**: Local development only
- **Types**: Neo4j database tests, LLM service tests

### Commands for Different Environments

#### For GitHub Actions / CI/CD
```bash
# Use this command in GitHub Actions workflows
dotnet test --logger "console;verbosity=normal" --filter "Category!=RequiresDocker"
```

This excludes all tests that require external Docker services, ensuring CI/CD pipelines run successfully without service dependencies.

#### For Local Development

```bash
# Run all tests (including integration)
dotnet test

# Unit tests only
dotnet test --filter "Category!=Integration"

# LLM integration tests only
dotnet test --filter "Category=LLM"

# Phase 7 LLM unit tests
dotnet test --filter "GraphEmbeddingServiceTests|ContextualPromptBuilderTests|GraphEnhancedScanningServiceTests|EnhancedScanningControllerTests"
```

### Integration Test Requirements

#### LLM Integration Tests
- **Service**: OpenAI-compatible LLM at `http://localhost:1234`
- **Tests**: 6 tests covering connectivity, code analysis, security analysis
- **Status**: ✅ Working with real LLM calls

#### Neo4j Integration Tests
- **Service**: Neo4j at `localhost:7687`
- **Tests**: 9 tests covering graph operations, storage, and query services
- **Status**: ✅ Migrated from JanusGraph to Neo4j

### Test Coverage Summary

- **Total Tests**: ~250 tests across all projects
- **Unit Tests**: ~240 tests (run in CI/CD)
- **Integration Tests**: ~15 tests (local development only)
- **Graph Module**: 43 tests total (37 unit + 6 integration)
- **Components**: API, Core, Graph, and UI test suites

## 📚 Documentation

Complete documentation can be found in the [docs](./docs/) directory.

## 📄 License

MIT
