# Codivus Graph Tests

This directory contains unit tests and integration tests for the Codivus Graph module.

## Quick Reference

```bash
# Run all tests (local development)
dotnet test

# Run CI-compatible tests (excludes Docker services)
dotnet test --filter "Category!=RequiresDocker"

# Run only LLM integration tests (requires LLM service)
dotnet test --filter "Category=LLM"

# Run Phase 7 LLM unit tests only
dotnet test --filter "GraphEmbeddingServiceTests|ContextualPromptBuilderTests|GraphEnhancedScanningServiceTests|EnhancedScanningControllerTests"
```

## Test Structure

### Unit Tests (37 tests)
- **GraphEmbeddingServiceTests.cs** - Context extraction and embeddings
- **ContextualPromptBuilderTests.cs** - LLM prompt generation
- **GraphEnhancedScanningServiceTests.cs** - Code scanning with mocked LLM
- **EnhancedScanningControllerTests.cs** - API endpoints

### Integration Tests (6 tests) 
- **LLMConnectivityTests.cs** - Basic LLM connectivity and code analysis
- **LLMHttpIntegrationTests.cs** - Complex security analysis workflows

**⚠️ Integration tests require OpenAI-compatible LLM at `http://localhost:1234`**

## CI/CD Compatibility

Integration tests are tagged with `[Trait("Category", "RequiresDocker")]` and excluded from GitHub Actions using:

```bash
dotnet test --filter "Category!=RequiresDocker"
```

This ensures CI/CD pipelines run successfully without external service dependencies.

**For complete testing documentation, see the main [README.md](../../README.md#-testing) in the repository root.**