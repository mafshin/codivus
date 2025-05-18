<template>
  <div class="scan-details-view">
    <!-- Error Message -->
    <ErrorAlert 
      :show="showError" 
      :message="errorMessage" 
      @close="clearError" 
    />
    
    <!-- Connection Status -->
    <OfflineIndicator :isOnline="isOnline" />
    
    <!-- Loading Indicator -->
    <LoadingIndicator 
      v-if="loading"
      :isOnline="isOnline"
      message="Loading scan details..." 
    />
    
    <template v-else-if="scan">
      <!-- Page Header -->
      <ScanHeader
        :scan="scan"
        :repositoryName="repositoryName"
        :operationLoading="operationLoading"
        @pause-scan="pauseScan"
        @resume-scan="resumeScan"
        @cancel-scan="cancelScan"
        @delete-scan="deleteScan"
        @refresh-data="refreshData"
        @go-back="goBack"
      />
      
      <!-- Progress Bar for Active Scans -->
      <ScanProgress :scan="scan" />
      
      <!-- Summary Statistics -->
      <ScanStats :scan="scan" />
      
      <!-- Tab Navigation -->
      <ScanTabs v-model:activeTab="activeTab" />
      
      <!-- Tab Content Container -->
      <div class="tab-content">
        <!-- Issues Tab -->
        <IssuesTab
          v-if="activeTab === 'issues'"
          :scan="scan"
          :loadingIssues="loadingIssues"
          :loadingFileTree="loadingFileTree"
          :issues="issues"
          :sidebarCollapsed="sidebarCollapsed"
          :selectedFile="selectedFile"
          :fileTree="fileTree"
          :expandedFolders="expandedFolders"
          :searchQuery="issueSearchQuery"
          :severityFilter="issueSeverityFilter"
          :categoryFilter="issueCategoryFilter"
          :debugMode="!!$route.query.debug"
          :fileContent="fileContent"
          :fileSize="fileSize"
          :fileContentError="fileContentError"
          @toggleSidebar="toggleSidebar"
          @selectFile="selectFile"
          @toggleFolder="toggleFolder"
          @clearFileSelection="clearFileSelection"
          @refreshIssues="fetchIssues"
          @update:searchQuery="issueSearchQuery = $event"
          @update:severityFilter="issueSeverityFilter = $event"
          @update:categoryFilter="issueCategoryFilter = $event"
          @fetchFileContent="fetchFileContent"
        />
        
        <!-- Configuration Tab -->
        <ConfigTab
          v-if="activeTab === 'config'"
          :loading="loadingConfig"
          :scanConfig="scanConfig"
        />
      </div>
    </template>
    
    <!-- Not Found State -->
    <NotFound v-else />
  </div>
</template>

<script setup>
import { ref, onMounted, computed, watch, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useScanningStore } from '@/store/scanning'
import api from '@/services/api'

// Import components
import ErrorAlert from '@/components/scan/ErrorAlert.vue'
import OfflineIndicator from '@/components/scan/OfflineIndicator.vue'
import LoadingIndicator from '@/components/scan/LoadingIndicator.vue'
import ScanHeader from '@/components/scan/ScanHeader.vue'
import ScanProgress from '@/components/scan/ScanProgress.vue'
import ScanStats from '@/components/scan/ScanStats.vue'
import ScanTabs from '@/components/scan/ScanTabs.vue'
import IssuesTab from '@/components/scan/IssuesTab.vue'
import ConfigTab from '@/components/scan/ConfigTab.vue'
import NotFound from '@/components/scan/NotFound.vue'

const route = useRoute()
const router = useRouter()
const scanningStore = useScanningStore()
const scanId = computed(() => route.params.id)

// State variables
const loading = ref(true)
const scan = ref(null)
const repositoryName = ref('')
const activeTab = ref('issues')
const loadingIssues = ref(false)
const loadingConfig = ref(false)
const loadingFileTree = ref(false)
const issues = ref([])
const scanConfig = ref(null)
const issueSearchQuery = ref('')
const issueSeverityFilter = ref('all')
const issueCategoryFilter = ref('all')

// File tree and sidebar state
const sidebarCollapsed = ref(false)
const selectedFile = ref(null)
const fileTree = ref([])
const expandedFolders = ref(new Set())

// File content state
const fileContent = ref(null)
const fileSize = ref(null)
const fileContentError = ref(null)

// Error handling
const errorMessage = ref('')
const showError = ref(false)

// Loading states for individual operations
const operationLoading = ref({
  pausing: false,
  resuming: false,
  canceling: false,
  deleting: false,
  refreshing: false
})

// Connection status
const isOnline = ref(navigator.onLine)

// Lifecycle hooks
onMounted(async () => {
  // Clear any existing data first
  scan.value = null
  issues.value = []
  scanConfig.value = null
  errorMessage.value = ''
  showError.value = false
  
  // Setup connection monitoring
  window.addEventListener('online', handleOnline)
  window.addEventListener('offline', handleOffline)
  
  await fetchScanDetails()
})

onUnmounted(() => {
  // Cleanup connection listeners
  window.removeEventListener('online', handleOnline)
  window.removeEventListener('offline', handleOffline)
})

// If scanId changes, refresh data
watch(scanId, async () => {
  // Clear file content when scan changes
  fileContent.value = null
  fileSize.value = null
  fileContentError.value = null
  await fetchScanDetails()
})

// Watch for tab changes to load tab-specific data
watch(activeTab, async (newTab) => {
  console.log('Tab changed to:', newTab)
  if (newTab === 'issues' && scan.value) {
    console.log('Loading issues for issues tab. Current issues length:', issues.value.length)
    // Always fetch issues when switching to issues tab to ensure fresh data
    await fetchIssues()
    // Also fetch file tree if not already loaded
    if (!fileTree.value.length) {
      await fetchFileTree()
    }
  } else if (newTab === 'config' && !scanConfig.value && scan.value) {
    await fetchScanConfig()
  }
})

// Error handling methods
function handleError(error, operation) {
  console.error(`Error during ${operation}:`, error)
  errorMessage.value = `Failed to ${operation}. Please try again.`
  showError.value = true
  
  // Auto-hide error after 5 seconds
  setTimeout(() => {
    showError.value = false
  }, 5000)
}

function clearError() {
  showError.value = false
  errorMessage.value = ''
}

// Connection status handlers
function handleOnline() {
  isOnline.value = true
  clearError()
  
  // Refresh data when coming back online
  if (scan.value) {
    refreshData()
  }
}

function handleOffline() {
  isOnline.value = false
}

// Retry mechanism
async function fetchWithRetry(fetchFunction, retries = 3) {
  let lastError
  
  for (let i = 0; i < retries; i++) {
    try {
      return await fetchFunction()
    } catch (error) {
      lastError = error
      if (i < retries - 1) {
        // Wait before retrying (exponential backoff)
        await new Promise(resolve => setTimeout(resolve, Math.pow(2, i) * 1000))
      }
    }
  }
  
  throw lastError
}

// Methods

async function refreshData() {
  operationLoading.value.refreshing = true
  try {
    await fetchScanDetails()
    if (activeTab.value === 'issues' && scan.value) {
      await fetchIssues()
    }
  } catch (error) {
    handleError(error, 'refresh data')
  } finally {
    operationLoading.value.refreshing = false
  }
}

async function fetchScanDetails() {
  loading.value = true
  clearError()
  
  try {
    await fetchWithRetry(async () => {
      console.log(`Fetching scan details for ID: ${scanId.value}`)
      const response = await api.getScanProgress(scanId.value)
      console.log('Scan details response:', response)
      
      if (!response.data || !response.data.id) {
        throw new Error('Invalid scan data received')
      }
      
      scan.value = response.data
      
      // Get repository name
      if (scan.value && scan.value.repositoryId) {
        try {
          const repoResponse = await api.getRepository(scan.value.repositoryId)
          repositoryName.value = repoResponse.data?.name || 'Unknown Repository'
        } catch (error) {
          console.error('Error fetching repository details:', error)
          repositoryName.value = 'Unknown Repository'
        }
      }
      
      // If current tab is issues, fetch issues (always refresh when loading scan details)
      if (activeTab.value === 'issues') {
        console.log('Fetching issues because we are on issues tab')
        await fetchIssues()
        // Also fetch file tree for the sidebar
        await fetchFileTree()
      } else if (activeTab.value === 'config') {
        await fetchScanConfig()
      }
    })
  } catch (error) {
    console.error('Error fetching scan details:', error)
    scan.value = null
    handleError(error, 'fetch scan details')
    
    // Special handling for 404 errors
    if (error.response?.status === 404) {
      scan.value = null
    }
  } finally {
    loading.value = false
  }
}

async function fetchIssues() {
  if (!scan.value) {
    console.warn('Cannot fetch issues: scan data not available')
    return
  }
  
  loadingIssues.value = true
  clearError()
  
  try {
    console.log(`Fetching issues for scan ID: ${scanId.value}`)
    console.log('Scan status:', scan.value.status)
    console.log('Scan issuesFound count:', scan.value.issuesFound)
    
    const response = await api.getScanIssues(scanId.value)
    console.log('Issues API response:', {
      status: response.status,
      dataType: typeof response.data,
      isArray: Array.isArray(response.data),
      length: response.data?.length || 0,
      data: response.data
    })
    
    // Handle different response formats
    let issuesData = []
    if (Array.isArray(response.data)) {
      issuesData = response.data
    } else if (response.data && Array.isArray(response.data.issues)) {
      issuesData = response.data.issues
    } else if (response.data && response.data.data && Array.isArray(response.data.data)) {
      issuesData = response.data.data
    } else {
      console.warn('Unexpected issues response format:', response.data)
      issuesData = []
    }
    
    console.log('Raw issues data before normalization:', {
      length: issuesData.length,
      sampleIssue: issuesData[0] || null,
      sampleFilePath: issuesData[0]?.filePath || null
    })
    
    // Normalize file paths in issues (convert absolute paths to relative)
    issuesData = issuesData.map(issue => {
      if (issue.filePath) {
        // Convert absolute path to relative path
        const normalizedPath = normalizeIssueFilePath(issue.filePath)
        return {
          ...issue,
          filePath: normalizedPath,
          // Keep original path for debugging if needed
          originalFilePath: issue.filePath
        }
      }
      return issue
    })
    
    console.log('Processed issues data after normalization:', {
      length: issuesData.length,
      sampleIssue: issuesData[0] || null,
      sampleFilePath: issuesData[0]?.filePath || null,
      sampleOriginalFilePath: issuesData[0]?.originalFilePath || null
    })
    
    issues.value = issuesData
    
    // Update the store as well
    scanningStore.scanIssues[scanId.value] = issues.value
    
    // Log filter state
    console.log('Current filters:', {
      search: issueSearchQuery.value,
      severity: issueSeverityFilter.value,
      category: issueCategoryFilter.value
    })
    
  } catch (error) {
    console.error('Error fetching scan issues:', error)
    if (error.response) {
      console.error('Response status:', error.response.status)
      console.error('Response data:', error.response.data)
    }
    issues.value = []
    handleError(error, 'fetch scan issues')
  } finally {
    loadingIssues.value = false
  }
}

async function fetchScanConfig() {
  if (!scan.value || !scan.value.configurationId) return
  
  loadingConfig.value = true
  try {
    const response = await api.getScanConfiguration(scan.value.configurationId)
    scanConfig.value = response.data
  } catch (error) {
    console.error('Error fetching scan configuration:', error)
    scanConfig.value = null
  } finally {
    loadingConfig.value = false
  }
}

// Scan control actions
async function pauseScan() {
  operationLoading.value.pausing = true
  try {
    await scanningStore.pauseScan(scanId.value)
    // Refresh scan data after action
    await fetchScanDetails()
  } catch (error) {
    handleError(error, 'pause scan')
  } finally {
    operationLoading.value.pausing = false
  }
}

async function resumeScan() {
  operationLoading.value.resuming = true
  try {
    await scanningStore.resumeScan(scanId.value)
    // Refresh scan data after action
    await fetchScanDetails()
  } catch (error) {
    handleError(error, 'resume scan')
  } finally {
    operationLoading.value.resuming = false
  }
}

async function cancelScan() {
  if (confirm('Are you sure you want to cancel this scan? This action cannot be undone.')) {
    operationLoading.value.canceling = true
    try {
      await scanningStore.cancelScan(scanId.value)
      // Refresh scan data after action
      await fetchScanDetails()
    } catch (error) {
      handleError(error, 'cancel scan')
    } finally {
      operationLoading.value.canceling = false
    }
  }
}

async function deleteScan() {
  if (confirm('Are you sure you want to delete this scan? This will also delete all related issues and cannot be undone.')) {
    operationLoading.value.deleting = true
    try {
      await scanningStore.deleteScan(scanId.value)
      // Navigate back to scans list after successful deletion
      router.push('/scans')
    } catch (error) {
      handleError(error, 'delete scan')
    } finally {
      operationLoading.value.deleting = false
    }
  }
}

function goBack() {
  router.push('/scans')
}

// File tree and sidebar methods
function toggleSidebar() {
  sidebarCollapsed.value = !sidebarCollapsed.value
}

function selectFile(fileName) {
  selectedFile.value = fileName
  // Clear previous file content when selecting a new file
  fileContent.value = null
  fileSize.value = null
  fileContentError.value = null
}

function clearFileSelection() {
  selectedFile.value = null
  // Clear file content when clearing selection
  fileContent.value = null
  fileSize.value = null
  fileContentError.value = null
}

function toggleFolder(folderPath) {
  if (expandedFolders.value.has(folderPath)) {
    expandedFolders.value.delete(folderPath)
  } else {
    expandedFolders.value.add(folderPath)
  }
}

async function fetchFileTree() {
  if (!scan.value || !scan.value.repositoryId) return
  
  loadingFileTree.value = true
  try {
    console.log('Fetching file tree for repository:', scan.value.repositoryId)
    const response = await api.getRepositoryStructure(scan.value.repositoryId)
    
    console.log('Raw repository structure response:', response.data)
    
    // The API returns a single root object with nested children
    const rootStructure = response.data
    if (rootStructure) {
      // Convert the API response to our expected format
      fileTree.value = convertApiStructureToTree(rootStructure)
    } else {
      fileTree.value = []
    }
    
    console.log('File tree built:', fileTree.value)
  } catch (error) {
    console.error('Error fetching file tree:', error)
    fileTree.value = []
  } finally {
    loadingFileTree.value = false
  }
}

// Convert API structure to our expected tree format
function convertApiStructureToTree(node) {
  if (!node) return []
  
  // If this is the root node with children, return the children
  if (node.children && Array.isArray(node.children)) {
    return node.children.map(child => convertApiNodeToTreeNode(child))
  }
  
  // If this is a single node, wrap it in an array
  return [convertApiNodeToTreeNode(node)]
}

// Convert a single API node to our tree node format
function convertApiNodeToTreeNode(apiNode) {
  const treeNode = {
    name: apiNode.name,
    path: apiNode.path || apiNode.name,
    type: apiNode.isDirectory ? 'directory' : 'file'
  }
  
  // Recursively convert children if they exist
  if (apiNode.children && Array.isArray(apiNode.children) && apiNode.children.length > 0) {
    treeNode.children = apiNode.children.map(child => convertApiNodeToTreeNode(child))
  }
  
  return treeNode
}

// Helper function to normalize issue file paths (convert absolute to relative)
function normalizeIssueFilePath(filePath) {
  if (!filePath) return null
  
  // Remove leading slashes to make it relative
  let normalized = filePath.replace(/^\/+/, '')
  
  // Handle Windows-style paths
  normalized = normalized.replace(/^[A-Za-z]:\\/, '')
  
  // Normalize path separators first
  normalized = normalized.replace(/\\/g, '/')

  return normalized;
}

// Fetch file content
async function fetchFileContent(fileName) {
  if (!scan.value?.repositoryId || !fileName) {
    console.warn('Cannot fetch file content: missing repository ID or file name')
    return
  }
  
  fileContentError.value = null
  
  try {
    console.log('Fetching file content for:', fileName)
    const response = await api.getFileContent(scan.value.repositoryId, fileName)
    
    if (typeof response.data === 'string') {
      fileContent.value = response.data
      fileSize.value = new Blob([response.data]).size
    } else if (response.data && typeof response.data.content === 'string') {
      fileContent.value = response.data.content
      fileSize.value = response.data.size || new Blob([response.data.content]).size
    } else {
      throw new Error('Invalid file content format received')
    }
    
    console.log('File content loaded successfully. Size:', fileSize.value)
  } catch (error) {
    console.error('Error fetching file content:', error)
    fileContent.value = null
    fileSize.value = null
    
    if (error.response?.status === 404) {
      fileContentError.value = 'File not found'
    } else if (error.response?.status === 413) {
      fileContentError.value = 'File too large to display'
    } else {
      fileContentError.value = error.message || 'Failed to load file content'
    }
  }
}
</script>

<style lang="scss" scoped>
.scan-details-view {
  .tab-content {
    background: white;
    border-radius: var(--border-radius);
    box-shadow: var(--box-shadow);
    padding: 1.5rem;
    min-height: 400px;
  }
}
</style>
