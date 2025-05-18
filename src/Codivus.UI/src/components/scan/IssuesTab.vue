<template>
  <div class="issues-tab-with-sidebar">
    <!-- File Tree Sidebar -->
    <FileTreeSidebar
      :collapsed="sidebarCollapsed"
      :loading="loadingFileTree"
      :fileTree="filteredFileTree"
      :selectedFile="selectedFile"
      :filesWithIssues="filteredFilesWithIssues"
      :expandedFolders="expandedFolders"
      :totalFiles="totalFiles"
      :filteredFiles="filteredFiles"
      :showAllFiles="showAllFiles"
      @toggle-sidebar="$emit('toggleSidebar')"
      @select-file="$emit('selectFile', $event)"
      @toggle-folder="$emit('toggleFolder', $event)"
      @toggle-show-all="toggleShowAllFiles"
    />
    
    <!-- Issues Content -->
    <div class="issues-main-content">
      <!-- Debug Information -->
      <div v-if="debugMode" class="debug-info">
        <h4>Debug Information:</h4>
        <p>Loading Issues: {{ loadingIssues }}</p>
        <p>Issues Array Length: {{ issues.length }}</p>
        <p>Filtered Issues Length: {{ filteredIssues.length }}</p>
        <p>Selected File: {{ selectedFile || 'All files' }}</p>
        <p>Scan Status: {{ scan?.status }}</p>
        <p>Scan Issues Found: {{ scan?.issuesFound }}</p>
        <p>Active Filters: Search='{{ searchQuery }}', Severity='{{ severityFilter }}', Category='{{ categoryFilter }}'</p>
        <button @click="$emit('refreshIssues')" class="btn btn-secondary btn-sm" style="margin-top: 0.5rem;">
          Force Refresh Issues
        </button>
      </div>
      
      <div v-if="loadingIssues" class="loading-indicator">
        <span class="mdi mdi-loading mdi-spin"></span> Loading issues...
      </div>
      
      <div v-else-if="!issues.length" class="empty-state">
        <span class="mdi mdi-check-circle"></span>
        <p>No issues found during this scan.</p>
      </div>
      
      <div v-else class="issues-content">
        <!-- Selected File Display -->
        <div v-if="selectedFile" class="selected-file-header">
          <span class="mdi mdi-file-document-outline"></span>
          <span class="selected-file-name">{{ selectedFile }}</span>
          <button class="clear-selection-btn" @click="$emit('clearFileSelection')">
            <span class="mdi mdi-close"></span>
            Show all files
          </button>
        </div>
        
        <!-- Two Column Layout when file is selected -->
        <div v-if="selectedFile" class="file-and-issues-layout">
          <!-- Source Code Viewer -->
          <div class="source-code-section">
            <SourceCodeViewer
              :fileName="selectedFile"
              :fileContent="fileContent"
              :fileSize="fileSize"
              :loading="loadingFileContent"
              :error="fileContentError"
              :issues="issues"
              @retry="fetchFileContent"
            />
          </div>
          
          <!-- Issues Section -->
          <div class="issues-section">
            <!-- Debug selected file info -->
            <div v-if="debugMode" class="debug-info" style="margin-bottom: 1rem;">
              <h5>File Issues Debug:</h5>
            <p>Selected File: {{ selectedFile }}</p>
            <p>Normalized Selected: {{ normalizeFilePath(selectedFile) }}</p>
            <p>Total Issues: {{ issues.length }}</p>
            <p>Issues for this file: {{ selectedFileIssues.length }}</p>
            <div v-if="issues.length > 0">
              <p>Sample issue paths:</p>
              <ul>
                <li v-for="(issue, index) in issues.slice(0, 5)" :key="index">
                  <strong>Issue {{ index + 1 }}:</strong><br>
                  Original: <code>{{ issue.originalFilePath || issue.filePath }}</code><br>
                  Normalized: <code>{{ issue.filePath }}</code><br>
                  Matches selected: {{ issue.filePath === selectedFile ? 'YES' : 'NO' }}
                </li>
              </ul>
            </div>
            <p>Direct matches: {{ issues.filter(i => i.filePath === selectedFile).length }}</p>
            <p>Normalized matches: {{ issues.filter(i => normalizeFilePath(i.filePath) === normalizeFilePath(selectedFile)).length }}</p>
            <button @click="testNormalization" class="btn btn-sm btn-info">Test Path Normalization</button>
          </div>
          
          <div class="issues-section-header">
            <h4>Issues in this file ({{ selectedFileIssues.length }})</h4>
          </div>
            
            <div v-if="!selectedFileIssues.length" class="no-issues">
              <span class="mdi mdi-check-circle"></span>
              <p>No issues found in this file.</p>
            </div>
            
            <IssuesList v-else :issues="selectedFileIssues" />
          </div>
        </div>
        
        <!-- Issues List View when no file is selected -->
        <div v-else class="issues-list-view">
          <!-- Issues Filter -->
          <IssuesFilter
            :searchQuery="searchQuery"
            :severityFilter="severityFilter"
            :categoryFilter="categoryFilter"
            :filteredCount="filteredIssues.length"
            :totalCount="displayedIssues.length"
            @update:searchQuery="$emit('update:searchQuery', $event)"
            @update:severityFilter="$emit('update:severityFilter', $event)"
            @update:categoryFilter="$emit('update:categoryFilter', $event)"
            @clear-filters="clearFilters"
          />
          
          <!-- No Results Message -->
          <div v-if="!filteredIssues.length" class="no-results">
            <span class="mdi mdi-filter-outline"></span>
            <p>No issues match the current filters.</p>
          </div>
          
          <!-- Issues List -->
          <IssuesList v-else :issues="filteredIssues" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, ref, watch } from 'vue'
import FileTreeSidebar from './FileTreeSidebar.vue'
import IssuesFilter from './IssuesFilter.vue'
import IssuesList from './IssuesList.vue'
import SourceCodeViewer from './SourceCodeViewer.vue'

const props = defineProps({
  scan: {
    type: Object,
    default: null
  },
  loadingIssues: {
    type: Boolean,
    default: false
  },
  loadingFileTree: {
    type: Boolean,
    default: false
  },
  issues: {
    type: Array,
    default: () => []
  },
  sidebarCollapsed: {
    type: Boolean,
    default: false
  },
  selectedFile: {
    type: String,
    default: null
  },
  fileTree: {
    type: Array,
    default: () => []
  },
  expandedFolders: {
    type: Set,
    default: () => new Set()
  },
  searchQuery: {
    type: String,
    default: ''
  },
  severityFilter: {
    type: String,
    default: 'all'
  },
  categoryFilter: {
    type: String,
    default: 'all'
  },
  debugMode: {
    type: Boolean,
    default: false
  },
  // File content props
  fileContent: {
    type: String,
    default: null
  },
  fileSize: {
    type: Number,
    default: null
  },
  fileContentError: {
    type: String,
    default: null
  }
})

// Local reactive state
const loadingFileContent = ref(false)
const showAllFiles = ref(false)

const emit = defineEmits([
  'toggleSidebar',
  'selectFile',
  'toggleFolder',
  'clearFileSelection',
  'refreshIssues',
  'update:searchQuery',
  'update:severityFilter',
  'update:categoryFilter',
  'fetchFileContent'
])

// Computed properties
const filteredIssues = computed(() => {
  let result = props.issues || []
  
  // Filter by selected file first
  if (props.selectedFile) {
    result = result.filter(issue => issue.filePath === props.selectedFile)
  }
  
  // Then apply other filters
  return result.filter(issue => {
    // Search filter
    if (props.searchQuery) {
      const searchLower = props.searchQuery.toLowerCase()
      const searchFields = [
        issue.title,
        issue.description,
        issue.filePath,
        issue.category,
        issue.ruleId
      ].filter(Boolean).join(' ').toLowerCase()
      
      if (!searchFields.includes(searchLower)) {
        return false
      }
    }
    
    // Severity filter
    if (props.severityFilter !== 'all' && issue.severity !== props.severityFilter) {
      return false
    }
    
    // Category filter
    if (props.categoryFilter !== 'all' && issue.category !== props.categoryFilter) {
      return false
    }
    
    return true
  })
})

// Issues filtered just by severity and category (not search or file)
const baseFilteredIssues = computed(() => {
  return props.issues.filter(issue => {
    // Severity filter
    if (props.severityFilter !== 'all' && issue.severity !== props.severityFilter) {
      return false
    }
    
    // Category filter
    if (props.categoryFilter !== 'all' && issue.category !== props.categoryFilter) {
      return false
    }
    
    return true
  })
})

// Files that have issues matching the current filters
const filteredFilesWithIssues = computed(() => {
  const files = new Set()
  baseFilteredIssues.value.forEach(issue => {
    if (issue.filePath) {
      // Store both original and normalized paths to handle different formats
      files.add(issue.filePath)
      files.add(normalizeFilePath(issue.filePath))
    }
  })
  return files
})

// Filtered file tree based on current filters
const filteredFileTree = computed(() => {
  if (showAllFiles.value || (!props.severityFilter || props.severityFilter === 'all') && (!props.categoryFilter || props.categoryFilter === 'all')) {
    return props.fileTree
  }
  
  return filterTreeNodes(props.fileTree, filteredFilesWithIssues.value)
})

// Files that should be shown in the tree based on filters
const filteredFiles = computed(() => {
  if (showAllFiles.value) {
    return null // Show all files
  }
  return filteredFilesWithIssues.value
})

// Helper function to normalize issue file paths (bypass double filtering)
const selectedFileIssues = computed(() => {
  if (!props.selectedFile) return []
  
  const normalizedSelectedFile = normalizeFilePath(props.selectedFile)
  
  // Don't use filteredIssues here as it already filters by selectedFile
  // Instead, filter all issues directly by the selected file and other active filters
  return props.issues.filter(issue => {
    // Normalize both paths for comparison
    const normalizedIssueFile = normalizeFilePath(issue.filePath)
    
    // Must match the selected file (try multiple comparison methods)
    const pathsMatch = normalizedIssueFile === normalizedSelectedFile ||
                      issue.filePath === props.selectedFile ||
                      normalizedIssueFile.endsWith(normalizedSelectedFile) ||
                      normalizedSelectedFile.endsWith(normalizedIssueFile)
    
    if (!pathsMatch) {
      return false
    }
    
    // Apply search filter
    if (props.searchQuery) {
      const searchLower = props.searchQuery.toLowerCase()
      const searchFields = [
        issue.title,
        issue.description,
        issue.filePath,
        issue.category,
        issue.ruleId
      ].filter(Boolean).join(' ').toLowerCase()
      
      if (!searchFields.includes(searchLower)) {
        return false
      }
    }
    
    // Apply severity filter
    if (props.severityFilter !== 'all' && issue.severity !== props.severityFilter) {
      return false
    }
    
    // Apply category filter
    if (props.categoryFilter !== 'all' && issue.category !== props.categoryFilter) {
      return false
    }
    
    return true
  })
})

// Issues displayed before file selection (for filter count)
const displayedIssues = computed(() => {
  let result = props.issues || []
  
  // Filter by selected file if any
  if (props.selectedFile) {
    result = result.filter(issue => issue.filePath === props.selectedFile)
  }
  
  return result
})

// Files that have any issues (unfiltered)
const filesWithIssues = computed(() => {
  const files = new Set()
  props.issues.forEach(issue => {
    if (issue.filePath) {
      // Store both original and normalized paths
      files.add(issue.filePath)
      files.add(normalizeFilePath(issue.filePath))
    }
  })
  return files
})

// Total files count from tree
const totalFiles = computed(() => {
  let count = 0
  const countFiles = (nodes) => {
    if (!Array.isArray(nodes)) return
    
    nodes.forEach(node => {
      if (node.type === 'file') {
        count++
      } else if (node.type === 'directory' && node.children) {
        countFiles(node.children)
      }
    })
  }
  countFiles(props.fileTree)
  return count
})

// Helper function to filter tree nodes
function filterTreeNodes(nodes, filesWithIssues) {
  if (!Array.isArray(nodes)) return []
  
  return nodes.reduce((filtered, node) => {
    if (node.type === 'file') {
      // Include file if it has issues matching the filters
      // Check both original path and normalized path
      const normalizedPath = normalizeFilePath(node.path)
      if (filesWithIssues.has(node.path) || filesWithIssues.has(normalizedPath)) {
        filtered.push(node)
      }
    } else if (node.type === 'directory' && node.children) {
      // Recursively filter children
      const filteredChildren = filterTreeNodes(node.children, filesWithIssues)
      // Include directory if it has any children with issues
      if (filteredChildren.length > 0) {
        filtered.push({
          ...node,
          children: filteredChildren
        })
      }
    }
    return filtered
  }, [])
}

// Helper function to normalize file paths for comparison
function normalizeFilePath(path) {
  if (!path) return ''
  // Remove leading slashes and normalize separators
  return path.replace(/^\/+/, '').replace(/\\/g, '/')
}

function clearFilters() {
  // Emit updates for all filters to the parent component
  emit('update:searchQuery', '')
  emit('update:severityFilter', 'all')
  emit('update:categoryFilter', 'all')
}

function toggleShowAllFiles() {
  showAllFiles.value = !showAllFiles.value
}

// Test path normalization for debugging
function testNormalization() {
  console.log('Testing path normalization:')
  console.log('Selected file:', props.selectedFile)
  console.log('Normalized selected:', normalizeFilePath(props.selectedFile))
  
  if (props.issues.length > 0) {
    console.log('Sample issue paths:')
    props.issues.slice(0, 5).forEach((issue, index) => {
      console.log(`  Issue ${index + 1}:`)
      console.log(`    Original: ${issue.originalFilePath || issue.filePath}`)
      console.log(`    Normalized: ${issue.filePath}`)
      console.log(`    Matches: ${issue.filePath === props.selectedFile}`)
    })
  }
}

// Fetch file content when a file is selected
async function fetchFileContent() {
  if (!props.selectedFile || !props.scan?.repositoryId) {
    return
  }
  
  loadingFileContent.value = true
  
  try {
    // Emit to parent to fetch file content
    emit('fetchFileContent', props.selectedFile)
  } catch (error) {
    console.error('Error fetching file content:', error)
  } finally {
    loadingFileContent.value = false
  }
}

// Watch for selected file changes
watch(() => props.selectedFile, (newFile) => {
  if (newFile) {
    fetchFileContent()
  }
}, { immediate: true })

// Watch for file content updates to stop loading
watch(() => props.fileContent, () => {
  loadingFileContent.value = false
})

watch(() => props.fileContentError, () => {
  loadingFileContent.value = false
})

// Expose functions for template usage
defineExpose({
  normalizeFilePath
})
</script>

<style lang="scss" scoped>
.issues-tab-with-sidebar {
  display: flex;
  gap: 0;
  height: calc(100vh - 400px);
  min-height: 600px;
}

.issues-main-content {
  flex: 1;
  overflow: hidden;
  background: white;
  display: flex;
  flex-direction: column;
  
  .selected-file-header {
    background: rgba(74, 108, 247, 0.05);
    border: 1px solid rgba(74, 108, 247, 0.2);
    border-radius: var(--border-radius);
    padding: 0.75rem 1rem;
    margin: 1rem 1rem 0;
    display: flex;
    align-items: center;
    gap: 0.5rem;
    
    .mdi {
      color: var(--primary-color);
    }
    
    .selected-file-name {
      flex: 1;
      font-family: 'JetBrains Mono', monospace;
      font-size: 0.875rem;
      font-weight: 500;
      color: var(--primary-color);
    }
    
    .clear-selection-btn {
      background: none;
      border: none;
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
      cursor: pointer;
      color: var(--text-muted);
      font-size: 0.75rem;
      transition: all var(--transition-speed);
      display: flex;
      align-items: center;
      gap: 0.25rem;
      
      &:hover {
        background: rgba(74, 108, 247, 0.1);
        color: var(--primary-color);
      }
    }
  }
  
  .no-results {
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 2rem 0;
    color: var(--text-muted);
    text-align: center;
    margin: 1rem;
    
    .mdi {
      font-size: 2rem;
      margin-bottom: 0.5rem;
    }
  }
  
  .issues-content {
    flex: 1;
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }
  
  .file-and-issues-layout {
    flex: 1;
    display: flex;
    gap: 1rem;
    padding: 1rem;
    overflow: hidden;
    
    .source-code-section {
      flex: 1;
      min-width: 0;
      overflow: hidden;
    }
    
    .issues-section {
      width: 400px;
      overflow-y: auto;
      border-left: 1px solid var(--border-color);
      padding-left: 1rem;
      
      .issues-section-header {
        margin-bottom: 1rem;
        
        h4 {
          margin: 0;
          color: var(--text-color);
          font-size: 1.1rem;
        }
      }
      
      .no-issues {
        display: flex;
        flex-direction: column;
        align-items: center;
        padding: 2rem 0;
        color: var(--text-muted);
        text-align: center;
        
        .mdi {
          font-size: 2rem;
          margin-bottom: 0.5rem;
          color: var(--success-color);
        }
      }
    }
  }
  
  .issues-list-view {
    flex: 1;
    overflow-y: auto;
    padding: 1rem;
  }
  
  .debug-info {
    background: #f8f9fa;
    border: 1px solid #dee2e6;
    border-radius: var(--border-radius);
    padding: 1rem;
    margin: 1rem;
    
    h4 {
      margin: 0 0 0.75rem;
      color: #6c757d;
    }
    
    p {
      margin: 0.25rem 0;
      font-family: 'JetBrains Mono', monospace;
      font-size: 0.875rem;
      color: #495057;
    }
  }
  
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
}
</style>
