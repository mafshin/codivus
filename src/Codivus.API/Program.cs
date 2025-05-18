using Codivus.API.Analyzers;
using Codivus.API.Data;
using Codivus.API.LLM;
using Codivus.API.Middleware;
using Codivus.API.Services;
using Codivus.Core.Interfaces;
using Microsoft.OpenApi.Models;
using Serilog;
using System.IO.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/codivus-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Codivus API",
        Version = "v1",
        Description = "AI-Enabled Code Scanning Solution API"
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://localhost:8080")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Configure HTTP clients
builder.Services.AddHttpClient("Ollama");
builder.Services.AddHttpClient("LMStudio");

// Add services
builder.Services.AddOptions();

// Configure LLM provider options
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("LLM:Ollama"));
builder.Services.Configure<LmStudioOptions>(builder.Configuration.GetSection("LLM:LMStudio"));

// Register data store
builder.Services.AddSingleton<JsonDataStore>();

// Register core services
builder.Services.AddSingleton<IFileSystem, FileSystem>();
builder.Services.AddSingleton<IRepositoryService, RepositoryService>();
builder.Services.AddSingleton<IScanningService, ScanningService>();
builder.Services.AddSingleton<IIssueHunterAnalyzer, IssueHunterAnalyzer>();

// Register LLM providers
builder.Services.AddTransient<ILlmProvider, OllamaProvider>();
builder.Services.AddTransient<ILlmProvider, LmStudioProvider>();
builder.Services.AddSingleton<LlmProviderFactory>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowVueApp");

// Use custom error handling middleware
app.UseErrorHandling();

app.UseAuthorization();
app.MapControllers();
try
{
    Log.Information("Starting Codivus API");
    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
