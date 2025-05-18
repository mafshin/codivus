<template>
  <div class="issues-list">
    <div 
      v-for="issue in issues" 
      :key="issue.id || `${issue.fileName}-${issue.lineNumber}-${issue.columnNumber}`"
      class="issue-item"
    >
      <div class="issue-header">
        <div class="issue-title">
          <span 
            class="issue-severity" 
            :class="'severity-' + (issue.severity ? String(issue.severity).toLowerCase() : 'unknown')"
          >
            {{ issue.severity || 'Unknown' }}
          </span>
          <span class="issue-category-badge">{{ issue.category || 'Other' }}</span>
          <span class="issue-title-text">{{ issue.title || issue.description }}</span>
        </div>
        <div class="issue-location">
          <span class="mdi mdi-file-document-outline"></span>
          <span class="file-name">{{ issue.filePath || 'Unknown file' }}</span>
          <span v-if="issue.lineNumber" class="line-info">:{{ issue.lineNumber }}</span>
          <span v-if="issue.columnNumber" class="line-info">:{{ issue.columnNumber }}</span>
        </div>
      </div>
      
      <div class="issue-content">
        <div v-if="issue.description && issue.description !== issue.title" class="issue-description">
          {{ issue.description }}
        </div>
        
        <div v-if="issue.suggestion" class="issue-suggestion">
          <div class="suggestion-header">
            <span class="mdi mdi-lightbulb-outline"></span>
            Suggested Fix
          </div>
          <div class="suggestion-content">
            {{ issue.suggestion }}
          </div>
        </div>
        
        <div class="issue-metadata">
          <div class="issue-meta-item">
            <span class="meta-label">Detection Method:</span>
            <span class="meta-value">{{ issue.detectionMethod || 'Unknown' }}</span>
          </div>
          <div v-if="issue.ruleId" class="issue-meta-item">
            <span class="meta-label">Rule ID:</span>
            <span class="meta-value">{{ issue.ruleId }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
defineProps({
  issues: {
    type: Array,
    default: () => []
  }
})
</script>

<style lang="scss" scoped>
.issues-list {
  .issue-item {
    border: 1px solid var(--border-color);
    border-radius: var(--border-radius);
    margin-bottom: 1rem;
    overflow: hidden;
    transition: box-shadow var(--transition-speed);
    
    &:hover {
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    }
    
    .issue-header {
      background: #f8f9fa;
      padding: 1rem;
      border-bottom: 1px solid var(--border-color);
      
      .issue-title {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        margin-bottom: 0.5rem;
        
        .issue-severity {
          padding: 0.25rem 0.5rem;
          border-radius: 4px;
          font-size: 0.75rem;
          font-weight: 700;
          text-transform: uppercase;
          
          &.severity-critical {
            background: rgba(138, 43, 226, 0.1);
            color: #8A2BE2;
          }
          
          &.severity-high {
            background: rgba(220, 53, 69, 0.1);
            color: var(--danger-color);
          }
          
          &.severity-medium {
            background: rgba(255, 193, 7, 0.1);
            color: var(--warning-color);
          }
          
          &.severity-low {
            background: rgba(255, 193, 7, 0.1);
            color: #e6a003;
          }
          
          &.severity-info {
            background: rgba(23, 162, 184, 0.1);
            color: var(--info-color);
          }
          
          &.severity-unknown {
            background: rgba(108, 117, 125, 0.1);
            color: var(--secondary-color);
          }
        }
        
        .issue-category-badge {
          padding: 0.25rem 0.5rem;
          background: rgba(74, 108, 247, 0.1);
          color: var(--primary-color);
          border-radius: 4px;
          font-size: 0.75rem;
          font-weight: 500;
        }
        
        .issue-title-text {
          font-size: 1.1rem;
          font-weight: 600;
          color: var(--text-color);
        }
      }
      
      .issue-location {
        display: flex;
        align-items: center;
        font-family: 'JetBrains Mono', monospace;
        font-size: 0.875rem;
        color: var(--secondary-color);
        
        .mdi {
          margin-right: 0.5rem;
        }
        
        .file-name {
          color: var(--text-color);
          font-weight: 500;
        }
        
        .line-info {
          color: var(--text-muted);
        }
      }
    }
    
    .issue-content {
      padding: 1rem;
      
      .issue-description {
        margin-bottom: 1rem;
        line-height: 1.6;
      }
      
      .issue-suggestion {
        background: rgba(40, 167, 69, 0.05);
        border-left: 3px solid var(--success-color);
        border-radius: 0 4px 4px 0;
        margin-bottom: 1rem;
        
        .suggestion-header {
          display: flex;
          align-items: center;
          padding: 0.75rem 1rem 0.5rem;
          font-weight: 600;
          color: var(--success-color);
          
          .mdi {
            margin-right: 0.5rem;
          }
        }
        
        .suggestion-content {
          padding: 0 1rem 0.75rem;
          line-height: 1.6;
        }
      }
      
      .issue-metadata {
        display: flex;
        flex-wrap: wrap;
        gap: 1rem;
        padding-top: 0.75rem;
        border-top: 1px solid var(--border-color);
        
        .issue-meta-item {
          display: flex;
          align-items: center;
          font-size: 0.875rem;
          
          .meta-label {
            color: var(--text-muted);
            margin-right: 0.5rem;
          }
          
          .meta-value {
            font-family: 'JetBrains Mono', monospace;
            color: var(--text-color);
            font-weight: 500;
          }
        }
      }
    }
  }
}
</style>
