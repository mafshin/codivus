# Codivus.CLI

This project has been initialized with Codivus for AI-powered code analysis.

## Getting Started

### Scanning Your Code

```bash
# Scan the entire repository
codivus scan repo --path .

# Scan specific files
codivus scan file src/main.cs

# Scan with graph analysis
codivus scan repo --path . --enable-graph
```

### Graph Analysis

```bash
# View graph metrics
codivus graph metrics --repository your-repo-id

# Generate graph visualization
codivus graph visualize --repository your-repo-id --output graph.svg

# Analyze code complexity
codivus graph analyze --repository your-repo-id --type complexity
```

### Managing Issues

```bash
# List all issues
codivus issues list

# Show specific issue details
codivus issues show <issue-id>

# Export issues to file
codivus issues export --output issues.json
```

### Configuration

```bash
# Show current settings
codivus settings show

# Set configuration values
codivus settings set scan.maxFileSize 5242880

# Initialize configuration
codivus settings init
```

## Project Structure

- `.codivus/` - Codivus configuration and cache
- `.codivusignore` - Files and patterns to exclude from scanning

## Template: basic

This project uses the 'basic' template configuration.

For more information, visit the [Codivus documentation](https://docs.codivus.com).
