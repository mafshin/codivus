<template>
  <div class="source-code-viewer">
    <div v-if="loading" class="loading-indicator">
      <span class="mdi mdi-loading mdi-spin"></span>
      Loading file content...
    </div>
    
    <div v-else-if="error" class="error-state">
      <span class="mdi mdi-alert-circle"></span>
      <p>{{ error }}</p>
      <button class="btn btn-secondary btn-sm" @click="$emit('retry')">
        <span class="mdi mdi-refresh"></span>
        Retry
      </button>
    </div>
    
    <div v-else-if="fileContent !== null" class="code-container">
      <div class="code-header">
        <div class="file-info">
          <span class="mdi mdi-file-document-outline"></span>
          <span class="file-name">{{ displayFileName }}</span>
          <span v-if="fileSize" class="file-size">({{ formatFileSize(fileSize) }})</span>
        </div>
        <div class="code-actions">
          <span v-if="issuesInFile.length" class="issue-count">
            {{ issuesInFile.length }} issue{{ issuesInFile.length !== 1 ? 's' : '' }}
          </span>
          <button class="btn btn-sm btn-secondary" @click="downloadFile">
            <span class="mdi mdi-download"></span>
            Download
          </button>
        </div>
      </div>
      
      <div class="code-content" ref="codeContainer">
        <div class="line-numbers" ref="lineNumbers">
          <span 
            v-for="lineNum in lineCount" 
            :key="lineNum"
            class="line-number"
            :class="getLineClasses(lineNum)"
            :title="getLineTitle(lineNum)"
            @mouseenter="hoveredLine = lineNum"
            @mouseleave="hoveredLine = null"
            @click="handleLineClick(lineNum)"
          >
            {{ lineNum }}
          </span>
        </div>
        <pre class="code-block" ref="codeBlock">
          <code 
            :class="`language-${detectedLanguage}`"
            v-html="highlightedCode"
            @click="handleCodeClick"
          ></code>
        </pre>
      </div>
      
      <!-- Issue details modal/popup -->
      <div 
        v-if="selectedIssue" 
        class="issue-detail-modal"
        @click="closeIssueDetail"
      >
        <div class="issue-detail-content" @click.stop>
          <div class="issue-detail-header">
            <div class="issue-meta">
              <span class="severity-badge" :class="`severity-${getSeverityClass(selectedIssue.severity)}`">
                {{ selectedIssue.severity }}
              </span>
              <span class="category-badge">{{ selectedIssue.category }}</span>
              <span class="line-info">Line {{ selectedIssue.lineNumber }}</span>
            </div>
            <button class="close-btn" @click="closeIssueDetail">
              <span class="mdi mdi-close"></span>
            </button>
          </div>
          
          <div class="issue-detail-body">
            <h3>{{ selectedIssue.title }}</h3>
            
            <div v-if="selectedIssue.description && selectedIssue.description !== selectedIssue.title" class="issue-description">
              <h4>Description</h4>
              <p>{{ selectedIssue.description }}</p>
            </div>
            
            <div v-if="selectedIssue.suggestion" class="issue-suggestion">
              <h4>Suggested Fix</h4>
              <p>{{ selectedIssue.suggestion }}</p>
            </div>
            
            <div class="issue-metadata">
              <div v-if="selectedIssue.ruleId" class="meta-item">
                <span class="meta-label">Rule ID:</span>
                <span class="meta-value">{{ selectedIssue.ruleId }}</span>
              </div>
              <div class="meta-item">
                <span class="meta-label">Detection Method:</span>
                <span class="meta-value">{{ selectedIssue.detectionMethod || 'Unknown' }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
      
      <!-- Issue tooltips for hover -->
      <div 
        v-for="issue in issuesInFile" 
        :key="`tooltip-${issue.id || issue.lineNumber}`"
        class="issue-tooltip"
        :class="`severity-${getSeverityClass(issue.severity)}`"
        :style="getIssueTooltipPosition(issue)"
        v-show="hoveredLine === issue.lineNumber && !selectedIssue"
      >
        <div class="tooltip-content">
          <div class="tooltip-header">
            <span class="severity-badge">{{ issue.severity }}</span>
            <span class="category-badge">{{ issue.category }}</span>
          </div>
          <div class="tooltip-title">{{ issue.title }}</div>
          <div class="tooltip-hint">Click for details</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted, nextTick } from 'vue'

// Import core Prism with only essential components
import Prism from 'prismjs'

// Import Prism theme
import 'prismjs/themes/prism.css'

// Import only the most basic components that are guaranteed to work
import 'prismjs/components/prism-javascript'
import 'prismjs/components/prism-json'
import 'prismjs/components/prism-css'

const props = defineProps({
  fileName: {
    type: String,
    required: true
  },
  fileContent: {
    type: String,
    default: null
  },
  fileSize: {
    type: Number,
    default: null
  },
  loading: {
    type: Boolean,
    default: false
  },
  error: {
    type: String,
    default: null
  },
  issues: {
    type: Array,
    default: () => []
  }
})

defineEmits(['retry'])

// Refs
const codeContainer = ref(null)
const lineNumbers = ref(null)
const codeBlock = ref(null)
const hoveredLine = ref(null)
const selectedIssue = ref(null)

// Computed properties
const displayFileName = computed(() => {
  // Extract just the filename from the full path
  return props.fileName?.split('/').pop() || props.fileName || 'Unknown File'
})

const lineCount = computed(() => {
  return props.fileContent ? props.fileContent.split('\n').length : 0
})

const issuesInFile = computed(() => {
  return props.issues.filter(issue => issue.filePath === props.fileName)
})

const detectedLanguage = computed(() => {
  const extension = props.fileName?.split('.').pop()?.toLowerCase()
  
  const languageMap = {
    'js': 'javascript',
    'jsx': 'javascript',
    'ts': 'javascript',
    'tsx': 'javascript',
    'json': 'json',
    'css': 'css',
    'scss': 'css',
    'sass': 'css'
  }
  
  return languageMap[extension] || 'markup'
})

// Syntax highlighting with issue markers
const highlightedCode = computed(() => {
  if (!props.fileContent) return ''
  
  try {
    const languageName = detectedLanguage.value
    let grammar = Prism.languages[languageName] || Prism.languages.markup
    
    // Apply syntax highlighting
    let highlighted = Prism.highlight(props.fileContent, grammar, languageName)
    
    // Add issue markers to lines with issues
    const lines = highlighted.split('\n')
    issuesInFile.value.forEach(issue => {
      const lineIndex = (issue.lineNumber || 1) - 1
      if (lineIndex >= 0 && lineIndex < lines.length) {
        const issueId = issue.id || `${issue.lineNumber}-${issue.columnNumber || 0}`
        lines[lineIndex] = `<span class="line-with-issue" data-line="${issue.lineNumber}" data-issue-id="${issueId}">${lines[lineIndex]}</span>`
      }
    })
    
    return lines.join('\n')
  } catch (error) {
    console.error('Error highlighting code:', error)
    // Fallback to plain text with issue markers
    const lines = props.fileContent.split('\n')
    issuesInFile.value.forEach(issue => {
      const lineIndex = (issue.lineNumber || 1) - 1
      if (lineIndex >= 0 && lineIndex < lines.length) {
        const issueId = issue.id || `${issue.lineNumber}-${issue.columnNumber || 0}`
        lines[lineIndex] = `<span class="line-with-issue" data-line="${issue.lineNumber}" data-issue-id="${issueId}">${lines[lineIndex]}</span>`
      }
    })
    return lines.join('\n')
  }
})

// Helper function to safely get severity class
function getSeverityClass(severity) {
  if (!severity) return 'unknown'
  const severityStr = String(severity).toLowerCase()
  // Ensure it's a valid CSS class name
  return severityStr.replace(/[^a-z0-9-]/g, '-')
}

// Methods
function getLineClasses(lineNumber) {
  const hasIssue = issuesInFile.value.some(issue => issue.lineNumber === lineNumber)
  return {
    'has-issue': hasIssue,
    'line-hovered': hoveredLine.value === lineNumber,
    'line-clickable': hasIssue
  }
}

function getLineTitle(lineNumber) {
  const issues = issuesInFile.value.filter(issue => issue.lineNumber === lineNumber)
  if (issues.length === 0) return null
  
  if (issues.length === 1) {
    return `${issues[0].severity}: ${issues[0].title} (Click for details)`
  }
  
  return `${issues.length} issues on this line (Click for details)`
}

function getIssueTooltipPosition(issue) {
  const lineHeight = 20
  const top = (issue.lineNumber - 1) * lineHeight
  
  return {
    top: `${top}px`,
    left: '100%',
    marginLeft: '10px'
  }
}

function formatFileSize(bytes) {
  if (bytes === 0) return '0 Bytes'
  
  const k = 1024
  const sizes = ['Bytes', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

function downloadFile() {
  const blob = new Blob([props.fileContent], { type: 'text/plain' })
  const url = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = displayFileName
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  window.URL.revokeObjectURL(url)
}

// Handle line number clicks
function handleLineClick(lineNumber) {
  const issues = issuesInFile.value.filter(issue => issue.lineNumber === lineNumber)
  if (issues.length >= 1) {
    selectedIssue.value = issues[0]
  }
}

// Handle clicks on issue markers in the code
function handleCodeClick(event) {
  const target = event.target
  const issueSpan = target.closest('.line-with-issue')
  
  if (issueSpan) {
    const issueId = issueSpan.getAttribute('data-issue-id')
    const lineNumber = parseInt(issueSpan.getAttribute('data-line'))
    
    // Find the issue by ID or line number
    let issue = issuesInFile.value.find(i => {
      const id = i.id || `${i.lineNumber}-${i.columnNumber || 0}`
      return id === issueId
    })
    
    if (!issue && lineNumber) {
      issue = issuesInFile.value.find(i => i.lineNumber === lineNumber)
    }
    
    if (issue) {
      selectedIssue.value = issue
    }
  }
}

// Close issue detail modal
function closeIssueDetail() {
  selectedIssue.value = null
}

// Setup watchers
watch([() => props.fileContent, issuesInFile], async () => {
  await nextTick()
}, { immediate: true })

onMounted(() => {
  // Any setup code if needed
})
</script>

<style lang="scss" scoped>
@keyframes pulse {
  0% { opacity: 1; }
  50% { opacity: 0.5; }
  100% { opacity: 1; }
}

.source-code-viewer {
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  overflow: hidden;
  font-family: 'JetBrains Mono', 'Cascadia Code', 'Fira Code', monospace;
  
  .loading-indicator,
  .error-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 3rem 0;
    color: var(--text-muted);
    text-align: center;
    
    .mdi {
      font-size: 2.5rem;
      margin-bottom: 1rem;
    }
    
    p {
      margin-bottom: 1.5rem;
    }
  }
  
  .code-container {
    .code-header {
      background: #f8f9fa;
      border-bottom: 1px solid var(--border-color);
      padding: 0.75rem 1rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      
      .file-info {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        
        .mdi {
          color: var(--primary-color);
        }
        
        .file-name {
          font-weight: 600;
          color: var(--text-color);
        }
        
        .file-size {
          color: var(--text-muted);
          font-size: 0.875rem;
        }
      }
      
      .code-actions {
        display: flex;
        align-items: center;
        gap: 1rem;
        
        .issue-count {
          color: var(--warning-color);
          font-size: 0.875rem;
          font-weight: 500;
        }
      }
    }
    
    .code-content {
      display: flex;
      max-height: 600px;
      overflow: auto;
      position: relative;
      
      .line-numbers {
        background: #f8f9fa;
        border-right: 1px solid var(--border-color);
        padding: 1rem 0.75rem;
        user-select: none;
        min-width: 60px;
        
        .line-number {
          display: block;
          height: 20px;
          line-height: 20px;
          color: var(--text-muted);
          text-align: right;
          font-size: 0.875rem;
          cursor: pointer;
          padding-right: 0.5rem;
          transition: all var(--transition-speed);
          
          &.line-clickable {
            cursor: pointer;
          }
          
          &.has-issue {
            background: rgba(255, 193, 7, 0.1);
            color: var(--warning-color);
            font-weight: 600;
            
            &::before {
              content: '●';
              margin-right: 0.5rem;
              color: var(--danger-color);
              animation: pulse 2s infinite;
            }
          }
          
          &.line-hovered {
            background: rgba(74, 108, 247, 0.1);
          }
          
          &:hover {
            background: rgba(74, 108, 247, 0.05);
          }
        }
      }
      
      .code-block {
        flex: 1;
        margin: 0;
        padding: 1rem;
        background: white;
        font-size: 0.875rem;
        line-height: 20px;
        overflow: visible;
        white-space: pre;
        
        code {
          display: block;
          
          // Issue line highlighting
          :deep(.line-with-issue) {
            background: rgba(255, 193, 7, 0.1);
            display: block;
            margin: 0 -1rem;
            padding: 0 1rem;
            border-left: 3px solid var(--warning-color);
            cursor: pointer;
            transition: all var(--transition-speed);
            
            &:hover {
              background: rgba(255, 193, 7, 0.15);
              border-left-color: var(--danger-color);
            }
          }
        }
      }
    }
  }
  
  .issue-detail-modal {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: rgba(0, 0, 0, 0.5);
    display: flex;
    justify-content: center;
    align-items: center;
    z-index: 2000;
    
    .issue-detail-content {
      background: white;
      border-radius: var(--border-radius);
      box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
      max-width: 600px;
      max-height: 80vh;
      width: 90%;
      overflow-y: auto;
      
      .issue-detail-header {
        padding: 1.5rem 1.5rem 1rem;
        border-bottom: 1px solid var(--border-color);
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        
        .issue-meta {
          display: flex;
          align-items: center;
          gap: 0.75rem;
          flex-wrap: wrap;
          
          .severity-badge {
            padding: 0.375rem 0.75rem;
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
          
          .category-badge {
            padding: 0.375rem 0.75rem;
            background: rgba(74, 108, 247, 0.1);
            color: var(--primary-color);
            border-radius: 4px;
            font-size: 0.75rem;
            font-weight: 500;
          }
          
          .line-info {
            font-family: 'JetBrains Mono', monospace;
            color: var(--text-muted);
            font-size: 0.875rem;
          }
        }
        
        .close-btn {
          background: none;
          border: none;
          padding: 0.5rem;
          border-radius: 4px;
          cursor: pointer;
          color: var(--text-muted);
          transition: all var(--transition-speed);
          
          &:hover {
            background: rgba(220, 53, 69, 0.1);
            color: var(--danger-color);
          }
          
          .mdi {
            font-size: 1.25rem;
          }
        }
      }
      
      .issue-detail-body {
        padding: 1.5rem;
        
        h3 {
          margin: 0 0 1.5rem;
          color: var(--text-color);
          font-size: 1.375rem;
          font-weight: 600;
        }
        
        h4 {
          margin: 1.5rem 0 0.75rem;
          color: var(--text-color);
          font-size: 1rem;
          font-weight: 600;
        }
        
        .issue-description p {
          margin: 0;
          color: var(--text-color);
          line-height: 1.6;
        }
        
        .issue-suggestion {
          background: rgba(40, 167, 69, 0.05);
          border: 1px solid rgba(40, 167, 69, 0.2);
          border-radius: var(--border-radius);
          padding: 1rem;
          
          p {
            margin: 0;
            color: var(--text-color);
            line-height: 1.6;
          }
        }
        
        .issue-metadata {
          margin-top: 1.5rem;
          padding-top: 1rem;
          border-top: 1px solid var(--border-color);
          
          .meta-item {
            display: flex;
            align-items: center;
            margin-bottom: 0.5rem;
            
            .meta-label {
              font-weight: 500;
              color: var(--text-muted);
              min-width: 120px;
            }
            
            .meta-value {
              font-family: 'JetBrains Mono', monospace;
              color: var(--text-color);
              font-size: 0.875rem;
            }
          }
        }
      }
    }
  }
  
  .issue-tooltip {
    position: absolute;
    z-index: 1000;
    background: white;
    border: 1px solid var(--border-color);
    border-radius: var(--border-radius);
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    min-width: 300px;
    max-width: 500px;
    
    .tooltip-content {
      padding: 1rem;
      
      .tooltip-header {
        display: flex;
        gap: 0.5rem;
        margin-bottom: 0.5rem;
        
        .severity-badge,
        .category-badge {
          padding: 0.25rem 0.5rem;
          border-radius: 4px;
          font-size: 0.75rem;
          font-weight: 600;
          text-transform: uppercase;
        }
        
        .severity-badge {
          background: rgba(220, 53, 69, 0.1);
          color: var(--danger-color);
        }
        
        .category-badge {
          background: rgba(74, 108, 247, 0.1);
          color: var(--primary-color);
        }
      }
      
      .tooltip-title {
        font-weight: 600;
        color: var(--text-color);
        margin-bottom: 0.5rem;
      }
      
      .tooltip-hint {
        font-size: 0.75rem;
        color: var(--primary-color);
        font-style: italic;
        margin-top: 0.25rem;
      }
    }
  }
}
</style>
