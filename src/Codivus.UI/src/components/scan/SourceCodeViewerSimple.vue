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
          <span class="file-name">{{ fileName }}</span>
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
          >
            {{ lineNum }}
          </span>
        </div>
        <pre class="code-block" ref="codeBlock">
          <code 
            :class="`language-${detectedLanguage}`"
            v-html="highlightedCode"
          ></code>
        </pre>
      </div>
      
      <!-- Issue tooltips -->
      <div 
        v-for="issue in issuesInFile" 
        :key="issue.id || `${issue.lineNumber}-${issue.columnNumber}`"
        class="issue-tooltip"
        :class="`severity-${(issue.severity || 'unknown').toLowerCase()}`"
        :style="getIssueTooltipPosition(issue)"
        v-show="hoveredLine === issue.lineNumber"
      >
        <div class="tooltip-content">
          <div class="tooltip-header">
            <span class="severity-badge">{{ issue.severity }}</span>
            <span class="category-badge">{{ issue.category }}</span>
          </div>
          <div class="tooltip-title">{{ issue.title }}</div>
          <div v-if="issue.description && issue.description !== issue.title" class="tooltip-description">
            {{ issue.description }}
          </div>
          <div v-if="issue.suggestion" class="tooltip-suggestion">
            <strong>Suggestion:</strong> {{ issue.suggestion }}
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted, nextTick } from 'vue'

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

// Computed properties
const lineCount = computed(() => {
  return props.fileContent ? props.fileContent.split('\n').length : 0
})

const issuesInFile = computed(() => {
  return props.issues.filter(issue => issue.fileName === props.fileName)
})

const detectedLanguage = computed(() => {
  const extension = props.fileName?.split('.').pop()?.toLowerCase()
  
  const languageMap = {
    'js': 'javascript',
    'jsx': 'javascript',
    'ts': 'typescript',
    'tsx': 'typescript',
    'py': 'python',
    'java': 'java',
    'cs': 'csharp',
    'php': 'php',
    'sh': 'bash',
    'bash': 'bash',
    'json': 'json',
    'css': 'css',
    'md': 'markdown',
    'markdown': 'markdown',
    'xml': 'markup',
    'html': 'markup',
    'htm': 'markup',
    'yml': 'yaml',
    'yaml': 'yaml',
    'scss': 'css',
    'sass': 'css'
  }
  
  return languageMap[extension] || 'text'
})

// Simple syntax highlighting without PrismJS
const highlightedCode = computed(() => {
  if (!props.fileContent) return ''
  
  try {
    // Simple highlighting for common patterns
    let content = props.fileContent
    
    // Escape HTML
    content = content
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;')
    
    // Apply basic highlighting based on file type
    if (detectedLanguage.value === 'javascript' || detectedLanguage.value === 'typescript') {
      // Highlight keywords
      content = content.replace(/\b(function|const|let|var|if|else|for|while|return|class|extends|import|export|from|default)\b/g, '<span class="keyword">$1</span>')
      // Highlight strings
      content = content.replace(/(["'])((?:\\.|(?!\1)[^\\])*?)\1/g, '<span class="string">$1$2$1</span>')
      // Highlight comments
      content = content.replace(/\/\/.*$/gm, '<span class="comment">$&</span>')
      content = content.replace(/\/\*[\s\S]*?\*\//g, '<span class="comment">$&</span>')
    } else if (detectedLanguage.value === 'python') {
      // Highlight keywords
      content = content.replace(/\b(def|class|if|elif|else|for|while|return|import|from|as|with|try|except|finally|and|or|not|in|is|lambda)\b/g, '<span class="keyword">$1</span>')
      // Highlight strings
      content = content.replace(/(["'])((?:\\.|(?!\1)[^\\])*?)\1/g, '<span class="string">$1$2$1</span>')
      // Highlight comments
      content = content.replace(/#.*$/gm, '<span class="comment">$&</span>')
    } else if (detectedLanguage.value === 'json') {
      // Highlight JSON
      content = content.replace(/"([^"]+)":/g, '<span class="property">"$1"</span>:')
      content = content.replace(/:\s*"([^"]*)"/g, ': <span class="string">"$1"</span>')
      content = content.replace(/:\s*(true|false|null|\d+)/g, ': <span class="value">$1</span>')
    }
    
    // Add issue markers to lines with issues
    const lines = content.split('\\n')
    issuesInFile.value.forEach(issue => {
      const lineIndex = (issue.lineNumber || 1) - 1
      if (lineIndex >= 0 && lineIndex < lines.length) {
        lines[lineIndex] = `<span class="line-with-issue" data-line="${issue.lineNumber}">${lines[lineIndex]}</span>`
      }
    })
    
    return lines.join('\\n')
  } catch (error) {
    console.error('Error processing code:', error)
    // Fallback to plain text with issue markers
    const lines = props.fileContent.split('\\n')
    issuesInFile.value.forEach(issue => {
      const lineIndex = (issue.lineNumber || 1) - 1
      if (lineIndex >= 0 && lineIndex < lines.length) {
        lines[lineIndex] = `<span class="line-with-issue" data-line="${issue.lineNumber}">${lines[lineIndex]}</span>`
      }
    })
    return lines.join('\\n')
  }
})

// Methods
function getLineClasses(lineNumber) {
  const hasIssue = issuesInFile.value.some(issue => issue.lineNumber === lineNumber)
  return {
    'has-issue': hasIssue,
    'line-hovered': hoveredLine.value === lineNumber
  }
}

function getLineTitle(lineNumber) {
  const issues = issuesInFile.value.filter(issue => issue.lineNumber === lineNumber)
  if (issues.length === 0) return null
  
  if (issues.length === 1) {
    return `${issues[0].severity}: ${issues[0].title}`
  }
  
  return `${issues.length} issues on this line`
}

function getIssueTooltipPosition(issue) {
  // Basic positioning
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
  a.download = props.fileName.split('/').pop()
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  window.URL.revokeObjectURL(url)
}

// Setup line hover events
function setupLineHoverEvents() {
  if (!lineNumbers.value) return
  
  const lineNumberElements = lineNumbers.value.querySelectorAll('.line-number')
  lineNumberElements.forEach((element, index) => {
    const lineNumber = index + 1
    
    element.addEventListener('mouseenter', () => {
      hoveredLine.value = lineNumber
    })
    
    element.addEventListener('mouseleave', () => {
      hoveredLine.value = null
    })
  })
}

// Watch for content changes to re-setup hover events
watch([() => props.fileContent, issuesInFile], async () => {
  await nextTick()
  setupLineHoverEvents()
}, { immediate: true })

onMounted(() => {
  setupLineHoverEvents()
})
</script>

<style lang="scss" scoped>
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
          
          &.has-issue {
            background: rgba(255, 193, 7, 0.1);
            color: var(--warning-color);
            font-weight: 600;
            
            &::before {
              content: '●';
              margin-right: 0.5rem;
              color: var(--danger-color);
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
          
          // Simple syntax highlighting styles
          :deep(.keyword) {
            color: #0000ff;
            font-weight: bold;
          }
          
          :deep(.string) {
            color: #008000;
          }
          
          :deep(.comment) {
            color: #808080;
            font-style: italic;
          }
          
          :deep(.property) {
            color: #0451a5;
          }
          
          :deep(.value) {
            color: #d63200;
          }
          
          // Issue line highlighting
          :deep(.line-with-issue) {
            background: rgba(255, 193, 7, 0.1);
            display: block;
            margin: 0 -1rem;
            padding: 0 1rem;
            border-left: 3px solid var(--warning-color);
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
      
      .tooltip-description {
        color: var(--text-muted);
        line-height: 1.5;
        margin-bottom: 0.5rem;
      }
      
      .tooltip-suggestion {
        background: rgba(40, 167, 69, 0.05);
        border-left: 3px solid var(--success-color);
        padding: 0.5rem;
        border-radius: 0 4px 4px 0;
        font-size: 0.875rem;
        
        strong {
          color: var(--success-color);
        }
      }
    }
  }
}
</style>
