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
              <hr style="margin: 0.5rem 0;">
              <div v-if="issues.length > 0">
                <p>Sample issue paths:</p>
                <ul>
                  <li v-for="(issue, index) in issues.slice(0, 5)" :key="index">
                    <strong>Issue {{ index + 1 }}:</strong><br>
                    Original: <code>{{ issue.originalFilePath || issue.filePath }}</code><br>
                    Normalized: <code>{{ normalizeFilePath(issue.filePath) }}</code><br>
                    Exact matches selected: {{ issue.filePath === selectedFile ? 'YES' : 'NO' }}<br>
                    Normalized matches: {{ normalizeFilePath(issue.filePath) === normalizeFilePath(selectedFile) ? 'YES' : 'NO' }}
                  </li>
                </ul>
              </div>
              <p>Direct matches: {{ issues.filter(i => i.filePath === selectedFile).length }}</p>
              <p>Normalized matches: {{ issues.filter(i => normalizeFilePath(i.filePath) === normalizeFilePath(selectedFile)).length }}</p>
              <button @click="testNormalization" class="btn btn-sm btn-info" style="margin-right: 0.5rem;">Test Path Normalization</button>
              <button @click="forceRecomputeIssues" class="btn btn-sm btn-warning">Force Recompute</button>
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
// Use a reactive ref for selected file issues instead of computed
const selectedFileIssues = ref([])

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

// Function to calculate issues for the selected file
function calculateSelectedFileIssues() {
  // Clear issues if no file is selected
  if (!props.selectedFile) {
    selectedFileIssues.value = []
    return
  }
  
  const currentSelectedFile = props.selectedFile
  const currentIssues = props.issues
  const currentSearchQuery = props.searchQuery
  const currentSeverityFilter = props.severityFilter
  const currentCategoryFilter = props.categoryFilter
  
  // Debug logging
  if (props.debugMode) {
    console.log('calculateSelectedFileIssues - selectedFile:', currentSelectedFile)
    console.log('calculateSelectedFileIssues - total issues:', currentIssues.length)
  }
  
  // Filter all issues to find those that match the selected file
  const matchingIssues = currentIssues.filter(issue => {
    // Check if the issue belongs to the selected file
    if (!issue.filePath) return false
    
    // Try multiple path comparison methods to handle different formats
    const issueFilePath = issue.filePath
    const selectedFilePath = currentSelectedFile
    
    // Normalize both paths for comparison
    const normalizedIssuePath = normalizeFilePath(issueFilePath)
    const normalizedSelectedPath = normalizeFilePath(selectedFilePath)
    
    // Check for exact matches (original and normalized)
    if (issueFilePath === selectedFilePath || normalizedIssuePath === normalizedSelectedPath) {
      return applySearchAndFilters(issue, currentSearchQuery, currentSeverityFilter, currentCategoryFilter)
    }
    
    // Check if paths match when removing leading path components
    // This handles cases where one path is relative and the other is absolute
    const issuePathParts = normalizedIssuePath.split('/')
    const selectedPathParts = normalizedSelectedPath.split('/')
    
    // If one path ends with the other, consider it a match
    if (issuePathParts.length >= selectedPathParts.length) {
      const issuePathSuffix = issuePathParts.slice(-selectedPathParts.length).join('/')
      if (issuePathSuffix === normalizedSelectedPath) {
        return applySearchAndFilters(issue, currentSearchQuery, currentSeverityFilter, currentCategoryFilter)
      }
    }
    
    if (selectedPathParts.length >= issuePathParts.length) {
      const selectedPathSuffix = selectedPathParts.slice(-issuePathParts.length).join('/')
      if (selectedPathSuffix === normalizedIssuePath) {
        return applySearchAndFilters(issue, currentSearchQuery, currentSeverityFilter, currentCategoryFilter)
      }
    }
    
    return false
  })

  // Update the reactive ref
  selectedFileIssues.value = matchingIssues
  
  // Debug logging
  if (props.debugMode) {
    console.log('calculateSelectedFileIssues - matching issues:', matchingIssues.length)
  }
}

// Helper function to apply search and filter criteria to an issue
function applySearchAndFilters(issue, searchQuery, severityFilter, categoryFilter) {
  // Apply search filter
  if (searchQuery) {
    const searchLower = searchQuery.toLowerCase()
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
  if (severityFilter !== 'all' && issue.severity !== severityFilter) {
    return false
  }
  
  // Apply category filter
  if (categoryFilter !== 'all' && issue.category !== categoryFilter) {
    return false
  }
  
  return true
}

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
  // Remove leading slashes, normalize separators, and remove trailing slashes for consistent comparison
  return path.replace(/^[/\\]+/, '').replace(/\\/g, '/').replace(/\/+$/, '').toLowerCase()
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
      console.log(`    Normalized: ${normalizeFilePath(issue.filePath)}`)
      console.log(`    Exact matches: ${issue.filePath === props.selectedFile}`)
      console.log(`    Normalized matches: ${normalizeFilePath(issue.filePath) === normalizeFilePath(props.selectedFile)}`)
    })
  }
}

// Force recompute issues for debugging
function forceRecomputeIssues() {
  console.log('Force recompute triggered')
  console.log('Current selectedFile:', props.selectedFile)
  console.log('Current issues:', props.issues.length)
  console.log('Current selectedFileIssues before recalculation:', selectedFileIssues.value.length)
  
  // Force recalculation
  calculateSelectedFileIssues()
  
  console.log('Current selectedFileIssues after recalculation:', selectedFileIssues.value.length)
  
  // Also emit refresh to parent
  emit('refreshIssues')
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

// Watch for selected file changes to help with debugging
watch(() => props.selectedFile, (newFile, oldFile) => {
  if (props.debugMode) {
    console.log('Watch: selectedFile changed from', oldFile, 'to', newFile)
  }
  // Recalculate issues for the new selected file
  calculateSelectedFileIssues()
  
  if (newFile) {
    fetchFileContent()
  }
}, { immediate: true })

// Watch for issues changes
watch(() => props.issues, (newIssues) => {
  if (props.debugMode) {
    console.log('Watch: issues changed, new length:', newIssues.length)
    console.log('Watch: current selectedFile:', props.selectedFile)
  }
  // Recalculate issues when the issues array changes
  calculateSelectedFileIssues()
}, { deep: true })

// Watch for filter changes
watch([() => props.searchQuery, () => props.severityFilter, () => props.categoryFilter], () => {
  if (props.debugMode) {
    console.log('Watch: filters changed')
  }
  // Recalculate issues when filters change
  calculateSelectedFileIssues()
})

// Watch for file content updates to stop loading
watch(() => props.fileContent, () => {
  loadingFileContent.value = false
})

watch(() => props.fileContentError, () => {
  loadingFileContent.value = false
})

// Expose functions for template usage
defineExpose({
  normalizeFilePath,
  testNormalization,
  forceRecomputeIssues,
  calculateSelectedFileIssues
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
