import { defineStore } from 'pinia'

export const useSettingsStore = defineStore({
  id: 'settings',
  
  state: () => ({
    llmProviderType: 'Ollama',  // Default provider
    llmModels: {
      'Ollama': [],
      'LmStudio': []
    },
    selectedLlmModel: '',
    issueCategories: [
      'Security',
      'Performance',
      'Quality',
      'Architecture',
      'Dependency',
      'Testing',
      'Documentation',
      'Accessibility',
      'Other'
    ],
    minimumSeverity: 'Low',
    maxConcurrentTasks: 4,
    showOnlyIssues: false,
    expandAll: false,
    darkMode: false
  }),
  
  getters: {
    availableModels: (state) => {
      return state.llmModels[state.llmProviderType] || []
    }
  },
  
  actions: {
    setLlmProviderType(providerType) {
      this.llmProviderType = providerType
      
      // Reset selected model to first available for the new provider
      const models = this.llmModels[providerType] || []
      if (models.length > 0) {
        this.selectedLlmModel = models[0]
      }
    },
    
    setLlmModel(model) {
      this.selectedLlmModel = model
    },
    
    setMinimumSeverity(severity) {
      this.minimumSeverity = severity
    },
    
    setMaxConcurrentTasks(count) {
      this.maxConcurrentTasks = count
    },
    
    toggleShowOnlyIssues() {
      this.showOnlyIssues = !this.showOnlyIssues
    },
    
    toggleExpandAll() {
      this.expandAll = !this.expandAll
    },
    
    toggleDarkMode() {
      this.darkMode = !this.darkMode
      
      // Apply dark mode to the document
      if (this.darkMode) {
        document.documentElement.classList.add('dark-mode')
      } else {
        document.documentElement.classList.remove('dark-mode')
      }
    },
    
    // Build default scan configuration based on current settings
    buildDefaultScanConfiguration(repositoryId) {
      // Map issue categories to numeric enum values
      const categoryMap = {
        'Security': 0,
        'Performance': 1,
        'Quality': 2, 
        'Architecture': 3,
        'Dependency': 4,
        'Testing': 5,
        'Documentation': 6,
        'Accessibility': 7,
        'Other': 8
      };
      
      // Convert string categories to numeric enum values
      const numericCategories = this.issueCategories.map(cat => categoryMap[cat] || 0);
      
      // Convert string severity to numeric enum value
      const severityMap = {
        'Low': 0,
        'Medium': 1,
        'High': 2,
        'Critical': 3
      };
      
      const numericSeverity = severityMap[this.minimumSeverity] || 0;
      
      // Convert provider type to numeric enum
      const providerMap = {
        'Ollama': 0,
        'LmStudio': 1
      };
      
      const numericProvider = providerMap[this.llmProviderType] || 0;
      
      return {
        name: 'Default Scan',
        repositoryId: repositoryId,
        includeExtensions: ['.cs', '.js', '.ts', '.vue', '.py', '.java', '.go', '.rb', '.php', '.c', '.cpp', '.h', '.hpp'],
        excludeExtensions: ['.min.js', '.min.css', '.svg', '.png', '.jpg', '.jpeg', '.gif', '.ico', '.woff', '.woff2', '.ttf', '.eot'],
        excludeDirectories: ['node_modules', 'bin', 'obj', 'dist', 'build', 'target', 'vendor', 'packages', '.git', '.vs', '.idea'],
        includeCategories: numericCategories,
        minimumSeverity: numericSeverity,
        maxFileSizeBytes: 1024 * 1024, // 1MB
        useAi: true,
        llmProvider: numericProvider,
        llmModel: this.selectedLlmModel,
        useIssueHunter: true,
        suggestFixes: true,
        maxConcurrentTasks: this.maxConcurrentTasks
      }
    },
    
    // Update available models for a provider
    updateAvailableModels(providerType, models) {
      this.llmModels[providerType] = models
    }
  },
  
  // Persist settings in localStorage
  persist: {
    enabled: true,
    strategies: [
      {
        key: 'codivus-settings',
        storage: localStorage
      }
    ]
  }
})
