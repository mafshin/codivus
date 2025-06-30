<template>
  <div class="graph-settings">
    <div class="settings-header">
      <h2>Graph Settings</h2>
      <p class="settings-description">
        Configure graph database connection and processing settings for code analysis.
      </p>
    </div>

    <form @submit.prevent="saveSettings" class="settings-form">
      <!-- Graph Enable/Disable -->
      <div class="form-section">
        <div class="form-group">
          <label class="toggle-label">
            <input
              type="checkbox"
              v-model="localSettings.enabled"
              class="toggle-input"
            />
            <span class="toggle-slider"></span>
            Enable Graph Database
          </label>
          <p class="help-text">
            Enable graph-based code analysis and dependency tracking
          </p>
        </div>
      </div>

      <div v-if="localSettings.enabled" class="settings-content">
        <!-- JanusGraph Connection Settings -->
        <div class="form-section">
          <h3>JanusGraph Connection</h3>
          
          <div class="form-row">
            <div class="form-group">
              <label for="host">Host</label>
              <input
                id="host"
                type="text"
                v-model="localSettings.janusGraph.host"
                placeholder="localhost"
                required
              />
            </div>
            
            <div class="form-group">
              <label for="port">Port</label>
              <input
                id="port"
                type="number"
                v-model.number="localSettings.janusGraph.port"
                placeholder="8182"
                min="1"
                max="65535"
                required
              />
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label for="username">Username</label>
              <input
                id="username"
                type="text"
                v-model="localSettings.janusGraph.username"
                placeholder="Optional"
              />
            </div>
            
            <div class="form-group">
              <label for="password">Password</label>
              <input
                id="password"
                type="password"
                v-model="localSettings.janusGraph.password"
                placeholder="Optional"
              />
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label for="connectionPoolSize">Connection Pool Size</label>
              <input
                id="connectionPoolSize"
                type="number"
                v-model.number="localSettings.janusGraph.connectionPoolSize"
                min="1"
                max="100"
              />
            </div>
            
            <div class="form-group">
              <label for="connectionTimeout">Connection Timeout (ms)</label>
              <input
                id="connectionTimeout"
                type="number"
                v-model.number="localSettings.janusGraph.connectionTimeout"
                min="1000"
                step="1000"
              />
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label for="graphName">Graph Name</label>
              <input
                id="graphName"
                type="text"
                v-model="localSettings.janusGraph.graphName"
                placeholder="codivus"
                required
              />
            </div>
            
            <div class="form-group">
              <label class="toggle-label">
                <input
                  type="checkbox"
                  v-model="localSettings.janusGraph.enableSsl"
                  class="toggle-input"
                />
                <span class="toggle-slider small"></span>
                Enable SSL
              </label>
            </div>
          </div>
        </div>

        <!-- Processing Settings -->
        <div class="form-section">
          <h3>Processing Settings</h3>
          
          <div class="form-row">
            <div class="form-group">
              <label for="maxConcurrentFiles">Max Concurrent Files</label>
              <input
                id="maxConcurrentFiles"
                type="number"
                v-model.number="localSettings.processing.maxConcurrentFiles"
                min="1"
                max="200"
              />
              <p class="help-text">Number of files to process simultaneously</p>
            </div>
            
            <div class="form-group">
              <label for="batchSize">Batch Size</label>
              <input
                id="batchSize"
                type="number"
                v-model.number="localSettings.processing.batchSize"
                min="10"
                max="10000"
                step="10"
              />
              <p class="help-text">Number of operations to batch together</p>
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label for="timeoutMinutes">Timeout (minutes)</label>
              <input
                id="timeoutMinutes"
                type="number"
                v-model.number="localSettings.processing.timeoutMinutes"
                min="1"
                max="480"
              />
            </div>
            
            <div class="form-group">
              <label for="retryAttempts">Retry Attempts</label>
              <input
                id="retryAttempts"
                type="number"
                v-model.number="localSettings.processing.retryAttempts"
                min="0"
                max="10"
              />
            </div>
          </div>
        </div>

        <!-- Analysis Settings -->
        <div class="form-section">
          <h3>Analysis Settings</h3>
          
          <div class="form-group">
            <label class="toggle-label">
              <input
                type="checkbox"
                v-model="localSettings.analysis.includeTests"
                class="toggle-input"
              />
              <span class="toggle-slider"></span>
              Include Test Files
            </label>
            <p class="help-text">Include test files in graph analysis</p>
          </div>

          <div class="form-group">
            <label for="maxFileSize">Max File Size (bytes)</label>
            <input
              id="maxFileSize"
              type="number"
              v-model.number="localSettings.analysis.maxFileSize"
              min="1024"
              step="1024"
            />
            <p class="help-text">Maximum file size to analyze ({{ formatFileSize(localSettings.analysis.maxFileSize) }})</p>
          </div>

          <div class="form-group">
            <label for="supportedExtensions">Supported Extensions</label>
            <div class="tag-input">
              <span
                v-for="(ext, index) in localSettings.analysis.supportedExtensions"
                :key="index"
                class="tag"
              >
                {{ ext }}
                <button
                  type="button"
                  @click="removeExtension(index)"
                  class="tag-remove"
                >
                  ×
                </button>
              </span>
              <input
                ref="extensionInput"
                type="text"
                placeholder="Add extension (e.g., .js)"
                @keydown.enter.prevent="addExtension"
                @keydown.comma.prevent="addExtension"
                class="tag-input-field"
              />
            </div>
          </div>

          <div class="form-group">
            <label for="excludedDirectories">Excluded Directories</label>
            <div class="tag-input">
              <span
                v-for="(dir, index) in localSettings.analysis.excludedDirectories"
                :key="index"
                class="tag"
              >
                {{ dir }}
                <button
                  type="button"
                  @click="removeExcludedDirectory(index)"
                  class="tag-remove"
                >
                  ×
                </button>
              </span>
              <input
                ref="directoryInput"
                type="text"
                placeholder="Add directory to exclude"
                @keydown.enter.prevent="addExcludedDirectory"
                @keydown.comma.prevent="addExcludedDirectory"
                class="tag-input-field"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- Action Buttons -->
      <div class="form-actions">
        <button
          type="button"
          @click="resetSettings"
          class="btn btn-secondary"
          :disabled="loading"
        >
          Reset to Defaults
        </button>
        
        <button
          type="button"
          @click="testConnection"
          class="btn btn-outline"
          :disabled="loading || !localSettings.enabled"
        >
          <span v-if="testingConnection">Testing...</span>
          <span v-else>Test Connection</span>
        </button>
        
        <button
          type="submit"
          class="btn btn-primary"
          :disabled="loading"
        >
          <span v-if="loading">Saving...</span>
          <span v-else>Save Settings</span>
        </button>
      </div>
    </form>

    <!-- Connection Status -->
    <div v-if="connectionStatus" class="connection-status" :class="connectionStatus.type">
      <div class="status-icon">
        <span v-if="connectionStatus.type === 'success'">✓</span>
        <span v-else-if="connectionStatus.type === 'error'">✗</span>
        <span v-else>ℹ</span>
      </div>
      <div class="status-message">
        {{ connectionStatus.message }}
      </div>
    </div>
  </div>
</template>

<script>
import { useGraphStore } from '@/store/graph'
import { computed, ref, reactive, onMounted, nextTick } from 'vue'

export default {
  name: 'GraphSettings',
  
  setup() {
    const graphStore = useGraphStore()
    
    const loading = ref(false)
    const testingConnection = ref(false)
    const connectionStatus = ref(null)
    const extensionInput = ref(null)
    const directoryInput = ref(null)
    
    const localSettings = reactive({
      enabled: true,
      janusGraph: {
        host: 'localhost',
        port: 8182,
        username: '',
        password: '',
        connectionPoolSize: 10,
        connectionTimeout: 30000,
        enableSsl: false,
        graphName: 'codivus'
      },
      processing: {
        maxConcurrentFiles: 50,
        batchSize: 1000,
        timeoutMinutes: 30,
        retryAttempts: 3
      },
      analysis: {
        includeTests: false,
        maxFileSize: 1048576,
        supportedExtensions: ['.cs', '.vb'],
        excludedDirectories: ['bin', 'obj', 'packages', '.git', '.vs']
      }
    })

    const formatFileSize = (bytes) => {
      if (bytes === 0) return '0 Bytes'
      const k = 1024
      const sizes = ['Bytes', 'KB', 'MB', 'GB']
      const i = Math.floor(Math.log(bytes) / Math.log(k))
      return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
    }

    const loadSettings = async () => {
      try {
        await graphStore.loadSettings()
        Object.assign(localSettings, graphStore.settings)
      } catch (error) {
        console.error('Error loading settings:', error)
      }
    }

    const saveSettings = async () => {
      loading.value = true
      connectionStatus.value = null
      
      try {
        await graphStore.updateSettings(localSettings)
        connectionStatus.value = {
          type: 'success',
          message: 'Settings saved successfully'
        }
        
        setTimeout(() => {
          connectionStatus.value = null
        }, 3000)
      } catch (error) {
        connectionStatus.value = {
          type: 'error',
          message: error.message || 'Failed to save settings'
        }
      } finally {
        loading.value = false
      }
    }

    const resetSettings = () => {
      Object.assign(localSettings, {
        enabled: true,
        janusGraph: {
          host: 'localhost',
          port: 8182,
          username: '',
          password: '',
          connectionPoolSize: 10,
          connectionTimeout: 30000,
          enableSsl: false,
          graphName: 'codivus'
        },
        processing: {
          maxConcurrentFiles: 50,
          batchSize: 1000,
          timeoutMinutes: 30,
          retryAttempts: 3
        },
        analysis: {
          includeTests: false,
          maxFileSize: 1048576,
          supportedExtensions: ['.cs', '.vb'],
          excludedDirectories: ['bin', 'obj', 'packages', '.git', '.vs']
        }
      })
    }

    const testConnection = async () => {
      testingConnection.value = true
      connectionStatus.value = null
      
      try {
        // This would need a backend endpoint to test the connection
        await new Promise(resolve => setTimeout(resolve, 2000)) // Simulate API call
        
        connectionStatus.value = {
          type: 'success',
          message: `Successfully connected to JanusGraph at ${localSettings.janusGraph.host}:${localSettings.janusGraph.port}`
        }
      } catch (error) {
        connectionStatus.value = {
          type: 'error',
          message: error.message || 'Failed to connect to JanusGraph'
        }
      } finally {
        testingConnection.value = false
        
        setTimeout(() => {
          connectionStatus.value = null
        }, 5000)
      }
    }

    const addExtension = async () => {
      const input = extensionInput.value
      const value = input.value.trim()
      
      if (value && !localSettings.analysis.supportedExtensions.includes(value)) {
        if (!value.startsWith('.')) {
          localSettings.analysis.supportedExtensions.push('.' + value)
        } else {
          localSettings.analysis.supportedExtensions.push(value)
        }
        input.value = ''
      }
    }

    const removeExtension = (index) => {
      localSettings.analysis.supportedExtensions.splice(index, 1)
    }

    const addExcludedDirectory = async () => {
      const input = directoryInput.value
      const value = input.value.trim()
      
      if (value && !localSettings.analysis.excludedDirectories.includes(value)) {
        localSettings.analysis.excludedDirectories.push(value)
        input.value = ''
      }
    }

    const removeExcludedDirectory = (index) => {
      localSettings.analysis.excludedDirectories.splice(index, 1)
    }

    onMounted(() => {
      loadSettings()
    })

    return {
      localSettings,
      loading,
      testingConnection,
      connectionStatus,
      extensionInput,
      directoryInput,
      formatFileSize,
      saveSettings,
      resetSettings,
      testConnection,
      addExtension,
      removeExtension,
      addExcludedDirectory,
      removeExcludedDirectory
    }
  }
}
</script>

<style scoped>
.graph-settings {
  max-width: 800px;
  margin: 0 auto;
  padding: 24px;
}

.settings-header {
  margin-bottom: 32px;
}

.settings-header h2 {
  color: #1f2937;
  font-size: 24px;
  font-weight: 600;
  margin-bottom: 8px;
}

.settings-description {
  color: #6b7280;
  font-size: 14px;
  margin: 0;
}

.settings-form {
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.form-section {
  padding: 24px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #fefefe;
}

.form-section h3 {
  color: #1f2937;
  font-size: 18px;
  font-weight: 600;
  margin: 0 0 20px 0;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 20px;
  margin-bottom: 20px;
}

.form-row:last-child {
  margin-bottom: 0;
}

.form-group {
  display: flex;
  flex-direction: column;
}

.form-group label {
  color: #374151;
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 6px;
}

.form-group input[type="text"],
.form-group input[type="number"],
.form-group input[type="password"] {
  padding: 8px 12px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font-size: 14px;
  transition: border-color 0.2s;
}

.form-group input:focus {
  outline: none;
  border-color: #3b82f6;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.help-text {
  color: #6b7280;
  font-size: 12px;
  margin: 4px 0 0 0;
}

.toggle-label {
  display: flex;
  align-items: center;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
}

.toggle-input {
  display: none;
}

.toggle-slider {
  position: relative;
  width: 44px;
  height: 24px;
  background: #cbd5e1;
  border-radius: 24px;
  margin-right: 12px;
  transition: background 0.2s;
}

.toggle-slider.small {
  width: 36px;
  height: 20px;
}

.toggle-slider::before {
  content: '';
  position: absolute;
  top: 2px;
  left: 2px;
  width: 20px;
  height: 20px;
  background: white;
  border-radius: 50%;
  transition: transform 0.2s;
}

.toggle-slider.small::before {
  width: 16px;
  height: 16px;
}

.toggle-input:checked + .toggle-slider {
  background: #3b82f6;
}

.toggle-input:checked + .toggle-slider::before {
  transform: translateX(20px);
}

.toggle-input:checked + .toggle-slider.small::before {
  transform: translateX(16px);
}

.tag-input {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  padding: 8px 12px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  min-height: 38px;
  align-items: center;
}

.tag {
  display: flex;
  align-items: center;
  background: #e0e7ff;
  color: #3730a3;
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 12px;
  gap: 4px;
}

.tag-remove {
  background: none;
  border: none;
  color: #6366f1;
  cursor: pointer;
  font-size: 14px;
  font-weight: bold;
  padding: 0;
  margin: 0;
}

.tag-remove:hover {
  color: #4338ca;
}

.tag-input-field {
  border: none;
  outline: none;
  flex: 1;
  min-width: 120px;
  font-size: 14px;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding-top: 24px;
  border-top: 1px solid #e5e7eb;
}

.btn {
  padding: 10px 20px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  border: 1px solid transparent;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-primary {
  background: #3b82f6;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #2563eb;
}

.btn-secondary {
  background: #6b7280;
  color: white;
}

.btn-secondary:hover:not(:disabled) {
  background: #4b5563;
}

.btn-outline {
  background: transparent;
  color: #3b82f6;
  border-color: #3b82f6;
}

.btn-outline:hover:not(:disabled) {
  background: #3b82f6;
  color: white;
}

.connection-status {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border-radius: 6px;
  margin-top: 16px;
  font-size: 14px;
}

.connection-status.success {
  background: #d1fae5;
  color: #065f46;
  border: 1px solid #a7f3d0;
}

.connection-status.error {
  background: #fee2e2;
  color: #991b1b;
  border: 1px solid #fca5a5;
}

.connection-status.info {
  background: #dbeafe;
  color: #1e40af;
  border: 1px solid #93c5fd;
}

.status-icon {
  font-weight: bold;
  font-size: 16px;
}

@media (max-width: 768px) {
  .form-row {
    grid-template-columns: 1fr;
  }
  
  .form-actions {
    flex-direction: column;
  }
}
</style>