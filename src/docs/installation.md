# Codivus Installation Guide

This guide provides instructions for setting up and running the Codivus code scanning solution.

## Prerequisites

- **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Node.js** v16+ - [Download](https://nodejs.org/)
- **npm** v7+
- **Local LLM provider** - Either:
  - **Ollama** - [Installation Guide](https://github.com/ollama/ollama)
  - **LMStudio** - [Download](https://lmstudio.ai/)

## Backend Setup

1. Navigate to the API project directory:
   ```
   cd Codivus.API
   ```

2. Restore .NET packages:
   ```
   dotnet restore
   ```

3. Update the configuration in `appsettings.json` with your LLM provider details.

4. Run the API:
   ```
   dotnet run
   ```

5. The API will be available at `https://localhost:5001` and `http://localhost:5000`.

## Frontend Setup

1. Navigate to the UI project directory:
   ```
   cd Codivus.UI
   ```

2. Install dependencies:
   ```
   npm install
   ```

3. Start the development server:
   ```
   npm run dev
   ```

4. The UI will be available at `http://localhost:3000`.

## LLM Provider Configuration

### Ollama

1. Install Ollama following the [official guide](https://github.com/ollama/ollama).
2. Pull a code-specific model:
   ```
   ollama pull codellama:7b-instruct
   ```
3. Configure the API endpoint in Codivus's `appsettings.json`.

### LMStudio

1. Install LMStudio from [the official website](https://lmstudio.ai/).
2. Load a code-specific model (e.g., CodeLlama).
3. Start the local inference server.
4. Configure the API endpoint in Codivus's `appsettings.json`.

## Troubleshooting

- **API Connection Issues**: Ensure that CORS is properly configured if running front-end and back-end on different ports.
- **LLM Provider Connection**: Verify that your LLM provider is running and accessible from the backend.
- **GitHub Authentication**: For scanning GitHub repositories, ensure your GitHub credentials or tokens are correctly set up.

## Next Steps

See the [Development Guide](./development.md) for information on project structure and how to extend functionality.
