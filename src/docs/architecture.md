# Codivus - Technical Architecture Specification

## 1. System Overview

Codivus is an AI-enabled code scanning solution designed to analyze code repositories for issues, vulnerabilities, and improvement opportunities. It leverages local LLM providers (Ollama, LMStudio) to perform in-depth code analysis while maintaining data privacy and security.

## 2. Architecture Diagram

```
┌───────────────────┐        ┌───────────────────┐        ┌───────────────────┐
│                   │        │                   │        │                   │
│    Vue.js UI      │◄──────►│    .NET 8 API     │◄──────►│   LLM Provider    │
│                   │        │                   │        │ (Ollama/LMStudio) │
└───────────────────┘        └───────────────────┘        └───────────────────┘
                                      ▲                            ▲
                                      │                            │
                                      ▼                            │
                             ┌───────────────────┐                 │
                             │                   │                 │
                             │  Repository Data  │─────────────────┘
                             │ (Local/GitHub)    │
                             │                   │
                             └───────────────────┘
```

## 3. Component Architecture

### 3.1 Frontend Architecture (Vue.js)

The frontend is built as a single-page application (SPA) using Vue.js 3 with the Composition API and TypeScript.

#### Core Components:

- **App.vue**: Root component managing routing and global layout
- **DashboardView.vue**: Main dashboard showing scanning status and repository overview
- **RepositoryView.vue**: Repository file structure visualization and navigation
- **IssueListView.vue**: Display and filtering of detected issues
- **ScanConfigView.vue**: Configuration of scanning parameters and LLM settings

#### State Management:

- **Pinia Stores**:
  - `repositoryStore`: Manages repository data and file structure
  - `scanningStore`: Handles scanning state and progress
  - `issueStore`: Stores and categorizes detected issues
  - `settingsStore`: Manages user preferences and configurations



### 3.2 Backend Architecture (.NET 8)

The backend is implemented as a RESTful API using ASP.NET Core 8.

#### Core Components:

- **Controllers**:
  - `RepositoryController`: Handle repository operations (add, list, details)
  - `ScanController`: Manage scanning operations (start, stop, status)
  - `IssueController`: Retrieve and filter detected issues
  - `SettingsController`: Configure system and LLM settings

- **Services**:
  - `RepositoryService`: Repository management (local and GitHub)
  - `ScanningService`: Orchestrate scanning operations
  - `IssueHunterService`: Issue detection and categorization
  - `LlmService`: Communication with LLM providers



#### Data Models:

- `Repository`: Repository information and metadata
- `ScanConfiguration`: Scanning parameters and settings
- `CodeIssue`: Detected code issues with details
- `ScanningProgress`: Real-time scanning status information

### 3.3 Core Library

Shared library for common functionality and models:

- **Models**: Domain entities and DTOs
- **Interfaces**: Service contracts and abstractions
- **Utilities**: Helper functions and extensions

### 3.4 LLM Integration

Abstraction layer for working with multiple LLM providers:

- `ILlmProvider`: Common interface for LLM providers
- `OllamaProvider`: Implementation for Ollama
- `LmStudioProvider`: Implementation for LMStudio
- `LlmPromptBuilder`: Generator for effective LLM prompts
- `LlmResponseParser`: Parser for structured LLM responses

## 4. Data Flow

### 4.1 Repository Scanning Process

1. User initiates scan from UI, selecting repository and scan configuration
2. API receives request and validates parameters
3. ScanningService begins file traversal and extraction
4. Files are processed in batches for efficient analysis
5. Each file is analyzed using:
   - LLM-based code analysis
   - IssueHunter integration
   - Pattern matching for known issues
6. Detected issues are categorized and stored

8. When completed, final report is generated

### 4.2 Data Storage

- In-memory storage for scanning session data
- File-based storage for repository information and scan results
- Optional database support for persistent storage

## 5. Security Considerations

- All code analysis is performed locally, no code leaves the system
- GitHub access uses secure authentication (OAuth, PAT)
- API endpoints secured with appropriate authorization
- Input validation to prevent injection attacks
- Sensitive configuration data stored securely

## 6. Scaling and Performance

- Parallel processing of repository files
- Batching of LLM requests for efficiency
- Caching of common analysis results
- Progressive loading of large repositories
- Resource throttling to prevent system overload

## 7. Error Handling and Resilience

- Graceful degradation when LLM is unavailable
- Automatic retry for transient failures
- Comprehensive error logging and monitoring
- Session recovery for interrupted scans

## 8. Future Extensibility

- Plugin architecture for additional analyzers
- Support for additional LLM providers
- CI/CD integration capabilities
- Team collaboration features
- Custom rule definition for issue detection

## 9. Technology Choices Rationale

### Frontend

- **Vue.js**: Modern, reactive framework with excellent component system
- **TypeScript**: Static typing for improved maintainability
- **Pinia**: Modular state management with TypeScript support
- **D3.js**: Advanced visualization for repository graphs

### Backend

- **.NET 8**: Modern, high-performance framework with excellent async support

- **System.IO.Abstractions**: Testable file system operations
- **LibGit2Sharp**: Git repository operations

### LLM Integration

- **REST Client**: Simple HTTP-based integration with LLM providers
- **HttpClientFactory**: Efficient HTTP client management
- **Polly**: Resilience and transient fault handling

## 10. Monitoring and Diagnostics

- Comprehensive logging with structured data
- Performance metrics collection
- Health check endpoints
- Error tracking and reporting

## 11. Deployment Considerations

- Docker containerization support
- Configuration externalization
- Environment-specific settings
- Resource requirements documentation
