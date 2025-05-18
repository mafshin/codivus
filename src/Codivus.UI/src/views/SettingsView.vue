<template>
  <div class="settings-view">
    <h1 class="page-title">Settings</h1>
    
    <div class="settings-container">
      <div class="settings-section">
        <h2 class="section-title">LLM Provider</h2>
        <div class="section-content">
          <div class="form-group">
            <label for="provider-type">Provider Type</label>
            <select 
              id="provider-type" 
              v-model="llmProviderType"
              @change="handleLlmProviderChange"
            >
              <option value="Ollama">Ollama</option>
              <option value="LmStudio">LM Studio</option>
            </select>
          </div>
          
          <div class="form-group">
            <label for="model">Model</label>
            <select 
              id="model" 
              v-model="selectedLlmModel"
              :disabled="checkingModels || !availableModels.length"
            >
              <option v-if="checkingModels" value="">Checking available models...</option>
              <option 
                v-else-if="!availableModels.length" 
                value=""
              >
                No models available
              </option>
              <option 
                v-for="model in availableModels" 
                :key="model" 
                :value="model"
              >
                {{ model }}
              </option>
            </select>
            <div class="model-actions">
              <button 
                class="btn btn-sm btn-secondary"
                @click="refreshAvailableModels"
                :disabled="checkingModels"
              >
                <span v-if="checkingModels" class="mdi mdi-loading mdi-spin"></span>
                <span v-else class="mdi mdi-refresh"></span>
                Refresh
              </button>
            </div>
          </div>
          
          <div v-if="!availableModels.length" class="provider-error">
            <p>
              <span class="mdi mdi-alert-circle"></span>
              Cannot connect to {{ llmProviderType }} provider. Make sure it's running and accessible.
            </p>
            <ul class="provider-instructions">
              <li v-if="llmProviderType === 'Ollama'">
                <a href="https://github.com/ollama/ollama" target="_blank">Ollama</a> should be running at <code>http://localhost:11434</code>
              </li>
              <li v-if="llmProviderType === 'LmStudio'">
                <a href="https://lmstudio.ai/" target="_blank">LM Studio</a> should be running with the local server enabled at <code>http://localhost:1234</code>
              </li>
              <li>Check the API configuration in appsettings.json</li>
            </ul>
          </div>
        </div>
      </div>
      
      <div class="settings-section">
        <h2 class="section-title">Scanning Settings</h2>
        <div class="section-content">
          <div class="form-group">
            <label for="min-severity">Minimum Severity</label>
            <select id="min-severity" v-model="minimumSeverity">
              <option value="Info">Info</option>
              <option value="Low">Low</option>
              <option value="Medium">Medium</option>
              <option value="High">High</option>
              <option value="Critical">Critical</option>
            </select>
          </div>
          
          <div class="form-group">
            <label for="max-concurrent">Maximum Concurrent Tasks</label>
            <select id="max-concurrent" v-model="maxConcurrentTasks">
              <option value="1">1</option>
              <option value="2">2</option>
              <option value="4">4</option>
              <option value="8">8</option>
              <option value="16">16</option>
            </select>
          </div>
          
          <div class="form-group issue-categories-group">
            <label>Issue Categories</label>
            <div class="categories-list">
              <div 
                v-for="category in issueCategories" 
                :key="category" 
                class="category-checkbox"
              >
                <input 
                  type="checkbox" 
                  :id="'category-' + category" 
                  :value="category" 
                  v-model="selectedCategories"
                />
                <label :for="'category-' + category">{{ category }}</label>
              </div>
            </div>
          </div>
        </div>
      </div>
      
      <div class="settings-section">
        <h2 class="section-title">UI Settings</h2>
        <div class="section-content">
          <div class="toggle-group">
            <div class="toggle-switch">
              <label>
                <span class="toggle-label">Show Only Files with Issues</span>
                <div class="toggle-wrapper">
                  <input 
                    type="checkbox" 
                    class="toggle-input" 
                    v-model="showOnlyIssues"
                  />
                  <div class="toggle-background"></div>
                  <div class="toggle-handle"></div>
                </div>
              </label>
            </div>
            
            <div class="toggle-switch">
              <label>
                <span class="toggle-label">Expand All Files by Default</span>
                <div class="toggle-wrapper">
                  <input 
                    type="checkbox" 
                    class="toggle-input" 
                    v-model="expandAll"
                  />
                  <div class="toggle-background"></div>
                  <div class="toggle-handle"></div>
                </div>
              </label>
            </div>
            
            <div class="toggle-switch">
              <label>
                <span class="toggle-label">Dark Mode</span>
                <div class="toggle-wrapper">
                  <input 
                    type="checkbox" 
                    class="toggle-input" 
                    v-model="darkMode"
                  />
                  <div class="toggle-background"></div>
                  <div class="toggle-handle"></div>
                </div>
              </label>
            </div>
          </div>
        </div>
      </div>
      
      <div class="settings-section">
        <h2 class="section-title">About</h2>
        <div class="section-content">
          <div class="about-info">
            <h3>Codivus - AI-Enabled Code Scanning Solution</h3>
            <p class="version">Version 0.1.0</p>
            <p>
              Codivus is a modern, AI-powered code scanning solution designed to analyze 
              code repositories for potential issues, vulnerabilities, and improvement opportunities.
            </p>
            <p>
              <a href="https://github.com/codivus" target="_blank">
                <span class="mdi mdi-github"></span> GitHub
              </a>
              <a href="https://github.com/codivus/docs" target="_blank">
                <span class="mdi mdi-book-open-page-variant"></span> Documentation
              </a>
              <a href="https://github.com/codivus/issues" target="_blank">
                <span class="mdi mdi-bug"></span> Report an Issue
              </a>
            </p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useSettingsStore } from '@/store/settings'
import api from '@/services/api'

const settingsStore = useSettingsStore()

// Reactive state
const llmProviderType = ref(settingsStore.llmProviderType)
const selectedLlmModel = ref(settingsStore.selectedLlmModel)
const minimumSeverity = ref(settingsStore.minimumSeverity)
const maxConcurrentTasks = ref(settingsStore.maxConcurrentTasks)
const issueCategories = ref([...settingsStore.issueCategories])
const selectedCategories = ref([...settingsStore.issueCategories])
const showOnlyIssues = ref(settingsStore.showOnlyIssues)
const expandAll = ref(settingsStore.expandAll)
const darkMode = ref(settingsStore.darkMode)
const checkingModels = ref(false)

// Computed properties
const availableModels = computed(() => {
  return settingsStore.availableModels
})

// Watchers
watch(llmProviderType, (newValue) => {
  settingsStore.setLlmProviderType(newValue)
})

watch(selectedLlmModel, (newValue) => {
  if (newValue) {
    settingsStore.setLlmModel(newValue)
  }
})

watch(minimumSeverity, (newValue) => {
  settingsStore.setMinimumSeverity(newValue)
})

watch(maxConcurrentTasks, (newValue) => {
  settingsStore.setMaxConcurrentTasks(parseInt(newValue))
})

watch(showOnlyIssues, (newValue) => {
  if (settingsStore.showOnlyIssues !== newValue) {
    settingsStore.toggleShowOnlyIssues()
  }
})

watch(expandAll, (newValue) => {
  if (settingsStore.expandAll !== newValue) {
    settingsStore.toggleExpandAll()
  }
})

watch(darkMode, (newValue) => {
  if (settingsStore.darkMode !== newValue) {
    settingsStore.toggleDarkMode()
  }
})

// Methods
const handleLlmProviderChange = async () => {
  refreshAvailableModels()
}

const refreshAvailableModels = async () => {
  checkingModels.value = true
  
  try {
    // Call the API to get available models for the selected provider
    const response = await api.getLlmModels(llmProviderType.value)
    
    if (response && response.data) {
      // Update the store with models from the API
      settingsStore.updateAvailableModels(llmProviderType.value, response.data)
    } else {
      // If we got an empty response, update with empty array
      settingsStore.updateAvailableModels(llmProviderType.value, [])
    }
  } catch (error) {
    console.error('Error fetching models:', error)
    // If provider is not available, update with empty array
    settingsStore.updateAvailableModels(llmProviderType.value, [])
  } finally {
    checkingModels.value = false
  }
}

// Lifecycle hooks
onMounted(async () => {
  // Refresh available models on mount
  await refreshAvailableModels()
})
</script>

<style lang="scss" scoped>
.settings-view {
  .page-title {
    margin-bottom: 1.5rem;
  }
  
  .settings-container {
    display: flex;
    flex-direction: column;
    gap: 1.5rem;
    max-width: 800px;
  }
  
  .settings-section {
    background: white;
    border-radius: var(--border-radius);
    box-shadow: var(--box-shadow);
    overflow: hidden;
    
    .section-title {
      margin: 0;
      padding: 1rem 1.5rem;
      font-size: 1.25rem;
      border-bottom: 1px solid var(--border-color);
    }
    
    .section-content {
      padding: 1.5rem;
    }
    
    .form-group {
      margin-bottom: 1.5rem;
      
      &:last-child {
        margin-bottom: 0;
      }
      
      label {
        display: block;
        margin-bottom: 0.5rem;
        font-weight: 500;
      }
      
      select, input {
        width: 100%;
        padding: 0.75rem;
        border: 1px solid var(--border-color);
        border-radius: var(--border-radius);
        font-family: inherit;
        font-size: inherit;
        
        &:focus {
          outline: none;
          border-color: var(--primary-color);
        }
        
        &:disabled {
          background-color: #f9f9f9;
          cursor: not-allowed;
        }
      }
      
      .model-actions {
        margin-top: 0.75rem;
        display: flex;
        justify-content: flex-end;
      }
    }
    
    .issue-categories-group {
      .categories-list {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
        gap: 0.75rem;
        
        .category-checkbox {
          display: flex;
          align-items: center;
          
          input[type="checkbox"] {
            width: auto;
            margin-right: 0.5rem;
          }
        }
      }
    }
    
    .toggle-group {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      
      .toggle-switch {
        label {
          display: flex;
          justify-content: space-between;
          align-items: center;
          cursor: pointer;
          
          .toggle-label {
            font-weight: 500;
          }
          
          .toggle-wrapper {
            position: relative;
            width: 50px;
            height: 24px;
            
            .toggle-input {
              position: absolute;
              opacity: 0;
              width: 0;
              height: 0;
              
              &:checked + .toggle-background {
                background-color: var(--primary-color);
              }
              
              &:checked + .toggle-background + .toggle-handle {
                transform: translateX(26px);
              }
            }
            
            .toggle-background {
              position: absolute;
              top: 0;
              left: 0;
              right: 0;
              bottom: 0;
              background-color: #ccc;
              border-radius: 12px;
              transition: background-color var(--transition-speed);
            }
            
            .toggle-handle {
              position: absolute;
              top: 2px;
              left: 2px;
              width: 20px;
              height: 20px;
              background-color: white;
              border-radius: 50%;
              transition: transform var(--transition-speed);
            }
          }
        }
      }
    }
    
    .provider-error {
      margin-top: 1rem;
      padding: 1rem;
      background-color: rgba(220, 53, 69, 0.1);
      border-left: 4px solid var(--danger-color);
      border-radius: 4px;
      
      p {
        margin: 0 0 0.75rem;
        display: flex;
        align-items: center;
        color: var(--danger-color);
        
        .mdi-alert-circle {
          margin-right: 0.5rem;
        }
      }
      
      .provider-instructions {
        margin: 0;
        padding-left: 1.5rem;
        
        li {
          margin-bottom: 0.5rem;
          
          &:last-child {
            margin-bottom: 0;
          }
        }
        
        a {
          font-weight: 500;
        }
        
        code {
          background-color: rgba(0, 0, 0, 0.05);
          padding: 0.125rem 0.25rem;
          border-radius: 3px;
        }
      }
    }
    
    .about-info {
      h3 {
        margin: 0 0 0.5rem;
      }
      
      .version {
        font-size: 0.875rem;
        color: var(--text-muted);
        margin-bottom: 1rem;
      }
      
      p {
        margin-bottom: 1rem;
        
        &:last-child {
          margin-bottom: 0;
        }
        
        a {
          display: inline-block;
          margin-right: 1.5rem;
          text-decoration: none;
          
          .mdi {
            margin-right: 0.25rem;
          }
        }
      }
    }
  }
}

.btn-sm {
  padding: 0.375rem 0.75rem;
  font-size: 0.875rem;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
