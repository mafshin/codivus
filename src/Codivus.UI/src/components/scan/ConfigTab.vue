<template>
  <div class="config-tab">
    <div v-if="loading" class="loading-indicator">
      <span class="mdi mdi-loading mdi-spin"></span> Loading configuration...
    </div>
    <div v-else-if="!scanConfig" class="empty-state">
      <span class="mdi mdi-cog"></span>
      <p>Scan configuration not available.</p>
    </div>
    <div v-else class="config-content">
      <div class="config-section">
        <h3>General Settings</h3>
        <div class="config-item">
          <div class="config-label">Configuration Name</div>
          <div class="config-value">{{ scanConfig.name }}</div>
        </div>
        <div class="config-item">
          <div class="config-label">Minimum Severity</div>
          <div class="config-value">{{ scanConfig.minimumSeverity }}</div>
        </div>
        <div class="config-item">
          <div class="config-label">Maximum Concurrent Tasks</div>
          <div class="config-value">{{ scanConfig.maxConcurrentTasks }}</div>
        </div>
      </div>
      
      <div class="config-section">
        <h3>LLM Settings</h3>
        <div class="config-item">
          <div class="config-label">Use AI Analysis</div>
          <div class="config-value">{{ scanConfig.useAi ? 'Yes' : 'No' }}</div>
        </div>
        <div class="config-item">
          <div class="config-label">LLM Provider</div>
          <div class="config-value">{{ scanConfig.llmProvider }}</div>
        </div>
        <div class="config-item">
          <div class="config-label">LLM Model</div>
          <div class="config-value">{{ scanConfig.llmModel }}</div>
        </div>
      </div>
      
      <div class="config-section">
        <h3>Filters</h3>
        <div class="config-item">
          <div class="config-label">Include Extensions</div>
          <div class="config-value">
            <div class="config-list">
              <span 
                v-for="ext in scanConfig.includeExtensions" 
                :key="ext" 
                class="config-tag"
              >
                {{ ext }}
              </span>
            </div>
          </div>
        </div>
        <div class="config-item">
          <div class="config-label">Exclude Extensions</div>
          <div class="config-value">
            <div class="config-list">
              <span 
                v-for="ext in scanConfig.excludeExtensions" 
                :key="ext" 
                class="config-tag"
              >
                {{ ext }}
              </span>
            </div>
          </div>
        </div>
        <div class="config-item">
          <div class="config-label">Exclude Directories</div>
          <div class="config-value">
            <div class="config-list">
              <span 
                v-for="dir in scanConfig.excludeDirectories" 
                :key="dir" 
                class="config-tag"
              >
                {{ dir }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
defineProps({
  loading: {
    type: Boolean,
    default: false
  },
  scanConfig: {
    type: Object,
    default: null
  }
})
</script>

<style lang="scss" scoped>
.config-tab {
  .loading-indicator, .empty-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 4rem 0;
    color: var(--text-muted);
    text-align: center;
    
    .mdi {
      font-size: 3rem;
      margin-bottom: 1rem;
    }
    
    p {
      margin-bottom: 1.5rem;
    }
  }
  
  .config-content {
    .config-section {
      margin-bottom: 2rem;
      
      &:last-child {
        margin-bottom: 0;
      }
      
      h3 {
        font-size: 1.25rem;
        margin-bottom: 1rem;
        padding-bottom: 0.5rem;
        border-bottom: 1px solid var(--border-color);
      }
      
      .config-item {
        margin-bottom: 1rem;
        
        &:last-child {
          margin-bottom: 0;
        }
        
        .config-label {
          font-weight: 500;
          margin-bottom: 0.5rem;
        }
        
        .config-value {
          // Default styling for config values
        }
        
        .config-list {
          display: flex;
          flex-wrap: wrap;
          gap: 0.5rem;
          
          .config-tag {
            display: inline-block;
            padding: 0.25rem 0.5rem;
            background: rgba(74, 108, 247, 0.1);
            color: var(--primary-color);
            border-radius: 4px;
            font-family: 'JetBrains Mono', monospace;
            font-size: 0.75rem;
          }
        }
      }
    }
  }
}
</style>
