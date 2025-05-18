# Codivus Development Guide

This guide provides information on the project structure, development workflow, and guidelines for contributing to Codivus.

## Project Structure

```
codivus/
├── Codivus.API/         # C# .NET 8 Backend API
│   ├── Controllers/     # API Endpoints
│   ├── Services/        # Business Logic
│   ├── Models/          # Data Transfer Objects
│   └── LLM/             # LLM Integration Services
│
├── Codivus.Core/        # Shared Core Library
│   ├── Models/          # Domain Models
│   ├── Interfaces/      # Contracts and Interfaces
│   └── Analyzers/       # Code Analysis Logic
│
├── Codivus.UI/          # Vue.js Frontend
│   ├── src/             # Source Files
│   │   ├── components/  # Vue Components
│   │   ├── views/       # Page Views
│   │   ├── store/       # State Management
│   │   └── services/    # API Communication
│   └── public/          # Static Assets
│
└── docs/                # Documentation
```

## Backend Development (.NET 8)

### API Structure

- **Controllers**: Handle HTTP requests and responses
- **Services**: Implement business logic
- **Models**: Define data structures for API communication
- **LLM**: Integration with Ollama and LMStudio

### Key Components

1. **RepositoryController**: Manages repository operations (scanning, viewing)
2. **ScanningService**: Orchestrates the scanning process
3. **LlmProviderFactory**: Creates appropriate LLM provider instances
4. **IssueHunterService**: Integrates with IssueHunter for analysis

### Coding Standards

- Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use dependency injection for services
- Write unit tests for all business logic
- Use async/await for I/O operations

## Frontend Development (Vue.js)

### Component Structure

- **Layout Components**: Define the overall structure
- **Dashboard Components**: Real-time scanning visualization
- **Repository Components**: Repository visualization and navigation
- **Issue Components**: Display and filtering of detected issues

### State Management

- Use Pinia for state management
- Define separate stores for:
  - Repository data
  - Scanning status
  - Issue tracking
  - User preferences

### API Communication

- Use Axios for REST API calls

### Coding Standards

- Follow the [Vue.js Style Guide](https://vuejs.org/style-guide/)
- Use TypeScript for type safety
- Component names should be PascalCase and multi-word
- Test components with Vitest

## LLM Integration

### Supported Providers

1. **Ollama**: 
   - API Integration via HTTP
   - Recommended models: CodeLlama variants

2. **LMStudio**:
   - OpenAI-compatible API integration
   - Support for various code-specific models

### Integration Strategy

- Abstract LLM providers behind a common interface
- Support provider-specific configurations
- Implement robust error handling and rate limiting
- Cache responses where appropriate

## IssueHunter Integration

IssueHunter is integrated as a code analysis tool with the following capabilities:

- Security vulnerability detection
- Code quality assessment
- Performance optimization suggestions
- Dependency analysis

## Development Workflow

1. **Feature Branches**: Create feature branches from `main`
2. **Pull Requests**: Submit PRs for code review
3. **CI/CD**: Automated testing and deployment
4. **Documentation**: Update docs with code changes

## Running Tests

### Backend Tests

```
cd Codivus.API.Tests
dotnet test
```

### Frontend Tests

```
cd Codivus.UI
npm run test
```

## Building for Production

### Backend

```
cd Codivus.API
dotnet publish -c Release
```

### Frontend

```
cd Codivus.UI
npm run build
```

## Additional Resources

- [.NET 8 Documentation](https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-8)
- [Vue.js Documentation](https://vuejs.org/guide/introduction.html)

