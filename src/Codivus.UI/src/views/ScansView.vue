<template>
  <div class="scans-view">
    <div class="page-header">
      <h1 class="page-title">Scan History</h1>
      <div class="page-actions">
        <button 
          class="btn btn-secondary"
          @click="refreshAllData"
          :disabled="refreshing"
        >
          <span class="mdi" :class="refreshing ? 'mdi-loading mdi-spin' : 'mdi-refresh'"></span>
          Refresh
        </button>
        <!-- Debug Toggle Button -->
        <button 
          class="btn btn-outline-secondary"
          @click="showDebugInfo = !showDebugInfo"
        >
          <span class="mdi mdi-bug"></span>
          {{ showDebugInfo ? 'Hide' : 'Show' }} Debug
        </button>
      </div>
    </div>

    <!-- Debug Information Panel -->
    <div v-if="showDebugInfo" class="debug-panel">
      <h3>Debug Information</h3>
      <div class="debug-stats">
        <div><strong>Total Scans:</strong> {{ allScans.length }}</div>
        <div><strong>Filtered Scans:</strong> {{ filteredScans.length }}</div>
        <div><strong>Deletable Scans:</strong> {{ deletableScansCount }}</div>
        <div><strong>Active Deletions:</strong> {{ deletingScans.size }}</div>
      </div>
      <div class="debug-actions">
        <button class="btn btn-sm btn-warning" @click="forceClearDeleteState">
          Clear Delete State
        </button>
        <button class="btn btn-sm btn-info" @click="logScanStates">
          Log Scan States
        </button>
      </div>
      <div class="debug-scan-list">
        <h4>Scan Details:</h4>
        <div v-for="scan in allScans" :key="scan.id" class="debug-scan-item">
          <strong>{{ getRepositoryName(scan.repositoryId) }}</strong> - 
          Status: <code>{{ scan.status }}</code> ({{ mapScanStatus(scan.status) }}) - 
          Can Delete: <span :class="canDeleteScan(scan) ? 'text-success' : 'text-danger'">{{ canDeleteScan(scan) }}</span> - 
          Reason: <small>{{ canDeleteScan(scan) ? 'OK' : getDeleteDisabledReason(scan) }}</small>
        </div>
      </div>
    </div>
    
    <div class="search-filter">
      <div class="search-box">
        <span class="mdi mdi-magnify"></span>
        <input 
          type="text" 
          v-model="searchQuery" 
          placeholder="Search scans..." 
          class="search-input"
        />
        <span 
          v-if="searchQuery" 
          class="mdi mdi-close clear-search" 
          @click="searchQuery = ''"
        ></span>
      </div>
      
      <div class="filter-dropdown">
        <select v-model="repositoryFilter" class="filter-select">
          <option value="all">All Repositories</option>
          <option 
            v-for="repo in repositories" 
            :key="repo.id" 
            :value="repo.id"
          >
            {{ repo.name }}
          </option>
        </select>
      </div>
      
      <div class="filter-dropdown">
        <select v-model="statusFilter" class="filter-select">
          <option value="all">All Statuses</option>
          <option value="Completed">Completed</option>
          <option value="InProgress">In Progress</option>
          <option value="Failed">Failed</option>
          <option value="Canceled">Canceled</option>
          <option value="Paused">Paused</option>
        </select>
      </div>
      
      <div class="filter-dropdown">
        <select v-model="sortOption" class="filter-select">
          <option value="date-desc">Newest First</option>
          <option value="date-asc">Oldest First</option>
          <option value="issues-desc">Most Issues</option>
          <option value="issues-asc">Least Issues</option>
          <option value="progress-desc">Most Progress</option>
          <option value="progress-asc">Least Progress</option>
        </select>
      </div>
    </div>
    
    <div v-if="loading" class="loading-container">
      <span class="mdi mdi-loading mdi-spin"></span>
      <span>Loading scans...</span>
    </div>
    
    <!-- Error Display -->
    <div v-if="deleteError" class="alert alert-danger">
      <span class="mdi mdi-alert-circle"></span>
      <strong>Delete Error:</strong> {{ deleteError }}
      <button class="btn btn-sm btn-outline-danger" @click="deleteError = ''">
        <span class="mdi mdi-close"></span>
      </button>
    </div>
    
    <div v-else-if="filteredScans.length === 0" class="empty-state">
      <div v-if="searchQuery || repositoryFilter !== 'all' || statusFilter !== 'all'" class="no-results">
        <span class="mdi mdi-file-search-outline"></span>
        <h3>No matching scans found</h3>
        <p>Try adjusting your search or filters.</p>
        <button class="btn btn-secondary" @click="clearFilters">Clear Filters</button>
      </div>
      <div v-else class="no-scans">
        <span class="mdi mdi-radar"></span>
        <h3>No scans performed yet</h3>
        <p>Scans will appear here when you analyze repositories.</p>
        <router-link to="/repositories" class="btn btn-primary">
          View Repositories
        </router-link>
      </div>
    </div>
    
    <div v-else class="scans-list">
      <div 
        v-for="scan in filteredScans" 
        :key="scan.id" 
        class="scan-card"
        @click="navigateToScan(scan.id)"
      >
        <div class="scan-status" :class="'status-' + (mapScanStatus(scan.status) ? String(mapScanStatus(scan.status)).toLowerCase() : 'unknown')">
          <span class="mdi" :class="getStatusIcon(scan.status)"></span>
        </div>
        <div class="scan-content">
          <div class="scan-header">
            <h3 class="scan-repository">{{ getRepositoryName(scan.repositoryId) }}</h3>
            <span class="scan-date">{{ formatDate(scan.startedAt) }}</span>
          </div>
          <div class="scan-progress">
            <div class="progress-bar">
              <div class="progress-value" :style="{ width: calculateProgress(scan) + '%' }"></div>
            </div>
            <span class="progress-text">{{ calculateProgress(scan) }}%</span>
          </div>
          <div class="scan-meta">
            <span class="scan-issues">{{ scan.issuesFound || 0 }} issues</span>
            <span 
              class="scan-status-text"
              :class="'status-' + (mapScanStatus(scan.status) ? String(mapScanStatus(scan.status)).toLowerCase() : 'unknown')"
            >
              {{ mapScanStatus(scan.status) || 'Unknown' }}
            </span>
            <!-- Debug Info for each scan -->
            <span v-if="showDebugInfo" class="debug-info">
              ID: {{ scan.id.substring(0, 8) }}... | Can Delete: {{ canDeleteScan(scan) }}
            </span>
          </div>
          <div class="scan-actions">
            <button 
              v-if="mapScanStatus(scan.status) === 'InProgress'"
              class="btn btn-sm btn-warning" 
              @click.stop="pauseScan(scan.id)"
            >
              <span class="mdi mdi-pause"></span> Pause
            </button>
            <button 
              v-if="mapScanStatus(scan.status) === 'Paused'"
              class="btn btn-sm btn-primary" 
              @click.stop="resumeScan(scan.id)"
            >
              <span class="mdi mdi-play"></span> Resume
            </button>
            <button 
              v-if="mapScanStatus(scan.status) === 'InProgress' || mapScanStatus(scan.status) === 'Paused'"
              class="btn btn-sm btn-danger" 
              @click.stop="cancelScan(scan.id)"
            >
              <span class="mdi mdi-stop"></span> Cancel
            </button>
            <!-- Enhanced Delete Button with better conditions -->
            <button 
              v-if="canDeleteScan(scan)"
              class="btn btn-sm btn-danger" 
              @click.stop="deleteScanWithConfirmation(scan)"
              :disabled="deletingScans.has(scan.id)"
              :title="`Delete scan ${scan.id}`"
            >
              <span 
                class="mdi" 
                :class="deletingScans.has(scan.id) ? 'mdi-loading mdi-spin' : 'mdi-delete'"
              ></span> 
              {{ deletingScans.has(scan.id) ? 'Deleting...' : 'Delete' }}
            </button>
            
            <!-- Temporary Debug/Fallback Delete Button -->
            <button 
              v-if="!canDeleteScan(scan) && showDebugInfo"
              class="btn btn-sm btn-outline-danger" 
              @click.stop="deleteScanWithConfirmation(scan)"
              :title="`Force delete scan ${scan.id} - Status: ${mapScanStatus(scan.status)}`"
            >
              <span class="mdi mdi-delete"></span> Force Delete (Debug)
            </button>
            
            <!-- Debug: Show why delete button is hidden -->
            <span v-else-if="showDebugInfo" class="debug-delete-reason">
              {{ getDeleteDisabledReason(scan) }}
            </span>
            <button 
              class="btn btn-sm btn-secondary"
              @click.stop="navigateToScan(scan.id)"
            >
              <span class="mdi mdi-eye"></span> View Details
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useRepositoryStore } from '@/store/repository'
import { useScanningStore } from '@/store/scanning'

const router = useRouter()
const repositoryStore = useRepositoryStore()
const scanningStore = useScanningStore()

// Reactive state
const loading = ref(true)
const refreshing = ref(false)
const searchQuery = ref('')
const repositoryFilter = ref('all')
const statusFilter = ref('all')
const sortOption = ref('date-desc')
const deletingScans = ref(new Set())
const deleteError = ref('')
const showDebugInfo = ref(false)

// Computed properties
const repositories = computed(() => repositoryStore.repositories)

// Status mapping function to convert numeric status to string
const mapScanStatus = (numericStatus) => {
  const statusMap = {
    0: 'Pending',
    1: 'Initializing', 
    2: 'InProgress',
    3: 'Paused',
    4: 'Canceled',
    5: 'Completed',
    6: 'Failed'
  }
  return statusMap[numericStatus] || numericStatus
}

const allScans = computed(() => {
  const scans = Object.values(scanningStore.scans || {})
  console.log('Computing allScans:', scans.length, 'scans')
  // Log first scan for debugging
  if (scans.length > 0) {
    console.log('Sample scan:', {
      id: scans[0].id,
      status: scans[0].status,
      statusType: typeof scans[0].status,
      repositoryId: scans[0].repositoryId
    })
  }
  return scans
})

const deletableScansCount = computed(() => {
  return allScans.value.filter(scan => canDeleteScan(scan)).length
})

const filteredScans = computed(() => {
  let result = [...allScans.value]
  
  // Apply search filter
  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(scan => {
      // Get the repository name for this scan
      const repo = repositories.value.find(r => r.id === scan.repositoryId)
      const repoName = repo ? repo.name.toLowerCase() : ''
      
      // Check if the search query matches the repository name
      return repoName.includes(query)
    })
  }
  
  // Apply repository filter
  if (repositoryFilter.value !== 'all') {
    result = result.filter(scan => scan.repositoryId === repositoryFilter.value)
  }
  
  // Apply status filter
  if (statusFilter.value !== 'all') {
    result = result.filter(scan => {
      const stringStatus = mapScanStatus(scan.status)
      return stringStatus === statusFilter.value
    })
  }
  
  // Apply sorting
  switch (sortOption.value) {
    case 'date-desc':
      result.sort((a, b) => new Date(b.startedAt || b.createdAt) - new Date(a.startedAt || a.createdAt))
      break
    case 'date-asc':
      result.sort((a, b) => new Date(a.startedAt || a.createdAt) - new Date(b.startedAt || b.createdAt))
      break
    case 'issues-desc':
      result.sort((a, b) => (b.issuesFound || 0) - (a.issuesFound || 0))
      break
    case 'issues-asc':
      result.sort((a, b) => (a.issuesFound || 0) - (b.issuesFound || 0))
      break
    case 'progress-desc':
      result.sort((a, b) => calculateProgress(b) - calculateProgress(a))
      break
    case 'progress-asc':
      result.sort((a, b) => calculateProgress(a) - calculateProgress(b))
      break
  }
  
  return result
})

// Methods
async function refreshAllData() {
  refreshing.value = true
  try {
    console.log('Refreshing all data...')
    
    // Fetch repositories if not already loaded
    if (repositories.value.length === 0) {
      console.log('Fetching repositories...')
      await repositoryStore.fetchRepositories()
      console.log('Repositories loaded:', repositories.value.length)
    }
    
    // Fetch scans for each repository
    console.log('Fetching scans for', repositories.value.length, 'repositories')
    for (const repo of repositories.value) {
      try {
        console.log(`Fetching scans for repository ${repo.id} (${repo.name})`)
        await scanningStore.fetchScansByRepository(repo.id)
        const scansForRepo = Object.values(scanningStore.scans || {}).filter(scan => scan.repositoryId === repo.id)
        console.log(`Found ${scansForRepo.length} scans for repository ${repo.name}`)
      } catch (error) {
        console.error(`Error fetching scans for repository ${repo.id}:`, error)
      }
    }
    
    console.log('Total scans loaded:', Object.keys(scanningStore.scans || {}).length)
    console.log('All scans:', Object.values(scanningStore.scans || {}))
  } catch (error) {
    console.error('Error refreshing data:', error)
  } finally {
    refreshing.value = false
  }
}

// Debug functions
const forceClearDeleteState = () => {
  deletingScans.value.clear()
  deleteError.value = ''
  console.log('Cleared delete state')
}

const logScanStates = () => {
  console.log('=== SCAN STATES DEBUG ===')
  console.log('Total scans:', allScans.value.length)
  
  allScans.value.forEach(scan => {
    const stringStatus = mapScanStatus(scan.status)
    console.log(`Scan ${scan.id.substring(0, 8)}:`, {
      numericStatus: scan.status,
      stringStatus: stringStatus,
      canDelete: canDeleteScan(scan),
      reason: canDeleteScan(scan) ? 'OK' : getDeleteDisabledReason(scan),
      isDeleting: deletingScans.value.has(scan.id)
    })
  })
  
  console.log('Deletable scans:', deletableScansCount.value)
  console.log('Currently deleting:', Array.from(deletingScans.value))
}

const clearFilters = () => {
  searchQuery.value = ''
  repositoryFilter.value = 'all'
  statusFilter.value = 'all'
}

const getRepositoryName = (repositoryId) => {
  const repository = repositoryStore.getRepositoryById(repositoryId)
  return repository ? repository.name : 'Unknown Repository'
}

const calculateProgress = (scan) => {
  const stringStatus = mapScanStatus(scan.status)
  
  if (stringStatus === 'Completed') return 100
  if (stringStatus === 'Failed' || stringStatus === 'Canceled') return 0
  
  if (scan.totalFiles > 0) {
    return Math.round((scan.scannedFiles / scan.totalFiles) * 100)
  }
  
  return 0
}

const getStatusIcon = (status) => {
  const stringStatus = mapScanStatus(status)
  
  switch (stringStatus) {
    case 'InProgress': return 'mdi-progress-clock'
    case 'Initializing': return 'mdi-timer-sand'
    case 'Completed': return 'mdi-check-circle'
    case 'Failed': return 'mdi-alert-circle'
    case 'Paused': return 'mdi-pause-circle'
    case 'Canceled': return 'mdi-stop-circle'
    case 'Pending': return 'mdi-clock-outline'
    default: return 'mdi-help-circle'
  }
}

const formatDate = (dateString) => {
  if (!dateString) return 'N/A'
  
  const date = new Date(dateString)
  return date.toLocaleDateString(undefined, { 
    year: 'numeric', 
    month: 'short', 
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const pauseScan = async (scanId) => {
  try {
    await scanningStore.pauseScan(scanId)
  } catch (error) {
    console.error('Error pausing scan:', error)
  }
}

const resumeScan = async (scanId) => {
  try {
    await scanningStore.resumeScan(scanId)
  } catch (error) {
    console.error('Error resuming scan:', error)
  }
}

const cancelScan = async (scanId) => {
  try {
    await scanningStore.cancelScan(scanId)
  } catch (error) {
    console.error('Error canceling scan:', error)
  }
}

// Enhanced delete functionality
function canDeleteScan(scan) {
  if (!scan || scan.status === null || scan.status === undefined) return false
  
  // Map numeric status to string
  const stringStatus = mapScanStatus(scan.status)
  
  const deletableStatuses = ['Completed', 'Failed', 'Canceled', 'Paused']
  const isStatusDeletable = deletableStatuses.includes(stringStatus)
  const isNotCurrentlyDeleting = !deletingScans.value.has(scan.id)
  
  return isStatusDeletable && isNotCurrentlyDeleting
}

function getDeleteDisabledReason(scan) {
  if (!scan) return 'No scan data'
  if (scan.status === null || scan.status === undefined) return 'Unknown status'
  if (deletingScans.value.has(scan.id)) return 'Currently deleting'
  
  // Map numeric status to string
  const stringStatus = mapScanStatus(scan.status)
  
  const deletableStatuses = ['Completed', 'Failed', 'Canceled', 'Paused']
  if (!deletableStatuses.includes(stringStatus)) {
    return `Status '${stringStatus}' (${scan.status}) not deletable`
  }
  
  return 'Should be deletable'
}

async function deleteScanWithConfirmation(scan) {
  const stringStatus = mapScanStatus(scan.status)
  
  const confirmMessage = `Are you sure you want to delete this scan?\n\n` +
    `Repository: ${getRepositoryName(scan.repositoryId)}\n` +
    `Status: ${stringStatus}\n` +
    `Issues Found: ${scan.issuesFound || 0}\n` +
    `Started: ${formatDate(scan.startedAt)}\n\n` +
    `This will also delete all related issues and cannot be undone.`
  
  if (!confirm(confirmMessage)) {
    return
  }
  
  await deleteScan(scan.id)
}

const deleteScan = async (scanId) => {
  // Clear any previous error
  deleteError.value = ''
  
  // Add to deleting set to show loading state
  deletingScans.value.add(scanId)
  
  try {
    console.log(`Starting delete process for scan ${scanId}`)
    
    // Call the store delete method
    await scanningStore.deleteScan(scanId)
    
    console.log(`Successfully deleted scan ${scanId}`)
    
  } catch (error) {
    console.error('Error deleting scan:', error)
    
    // Set user-friendly error message
    if (error.response?.status === 404) {
      deleteError.value = `Scan not found. It may have already been deleted.`
    } else if (error.response?.status === 400) {
      deleteError.value = `Cannot delete scan: ${error.response.data || error.message}`
    } else if (error.response?.status >= 500) {
      deleteError.value = `Server error occurred while deleting scan. Please try again.`
    } else {
      deleteError.value = `Failed to delete scan: ${error.message}`
    }
    
  } finally {
    // Remove from deleting set
    deletingScans.value.delete(scanId)
  }
}

const navigateToScan = (scanId) => {
  router.push({ name: 'ScanDetails', params: { id: scanId } })
}

// Lifecycle hooks
onMounted(async () => {
  loading.value = true
  
  try {
    await refreshAllData()
  } catch (error) {
    console.error('Error loading scans:', error)
  } finally {
    loading.value = false
  }
})
</script>

<style lang="scss" scoped>
.scans-view {
  .page-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 1.5rem;
    
    .page-actions {
      display: flex;
      gap: 0.75rem;
    }
  }
  
  .debug-panel {
    background: #f8f9fa;
    border: 1px solid #dee2e6;
    border-radius: var(--border-radius);
    padding: 1rem;
    margin-bottom: 1.5rem;
    
    h3 {
      margin-bottom: 0.75rem;
      color: #495057;
    }
    
    .debug-stats {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 0.5rem;
      margin-bottom: 1rem;
      
      div {
        font-size: 0.875rem;
      }
    }
    
    .debug-actions {
      display: flex;
      gap: 0.5rem;
      margin-bottom: 1rem;
    }
    
    .debug-scan-list {
      h4 {
        margin-bottom: 0.5rem;
        color: #495057;
      }
      
      .debug-scan-item {
        padding: 0.25rem 0;
        font-size: 0.875rem;
        border-bottom: 1px solid #e9ecef;
        
        &:last-child {
          border-bottom: none;
        }
        
        code {
          background: #e9ecef;
          padding: 0.125rem 0.25rem;
          border-radius: 3px;
          font-size: 0.75rem;
        }
        
        .text-success {
          color: #28a745;
          font-weight: bold;
        }
        
        .text-danger {
          color: #dc3545;
          font-weight: bold;
        }
      }
    }
  }
  
  .search-filter {
    display: flex;
    flex-wrap: wrap;
    gap: 1rem;
    margin-bottom: 1.5rem;
    
    .search-box {
      flex: 1;
      min-width: 250px;
      position: relative;
      
      .mdi-magnify {
        position: absolute;
        left: 12px;
        top: 50%;
        transform: translateY(-50%);
        color: var(--text-muted);
      }
      
      .search-input {
        width: 100%;
        padding: 0.625rem 2.5rem 0.625rem 2.5rem;
        border-radius: var(--border-radius);
        border: 1px solid var(--border-color);
        
        &:focus {
          outline: none;
          border-color: var(--primary-color);
        }
      }
      
      .clear-search {
        position: absolute;
        right: 12px;
        top: 50%;
        transform: translateY(-50%);
        color: var(--text-muted);
        cursor: pointer;
        
        &:hover {
          color: var(--text-color);
        }
      }
    }
    
    .filter-dropdown {
      width: 150px;
      
      .filter-select {
        width: 100%;
        padding: 0.625rem;
        border-radius: var(--border-radius);
        border: 1px solid var(--border-color);
        appearance: none;
        background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24'%3E%3Cpath fill='%236c757d' d='M7 10l5 5 5-5z'/%3E%3C/svg%3E");
        background-repeat: no-repeat;
        background-position: right 8px center;
      }
    }
  }
  
  .loading-container {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 4rem 0;
    color: var(--text-muted);
    
    .mdi-loading {
      font-size: 3rem;
      margin-bottom: 1rem;
      animation: spin 1s linear infinite;
    }
  }
  
  .empty-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 4rem 0;
    text-align: center;
    
    .mdi-file-search-outline, .mdi-radar {
      font-size: 3rem;
      color: var(--secondary-color);
      margin-bottom: 1rem;
    }
    
    h3 {
      margin-bottom: 0.5rem;
    }
    
    p {
      color: var(--text-muted);
      margin-bottom: 1.5rem;
    }
  }
  
  .scans-list {
    display: flex;
    flex-direction: column;
    gap: 1rem;
    
    .scan-card {
      padding: 1rem;
      background: white;
      border-radius: var(--border-radius);
      box-shadow: var(--box-shadow);
      display: flex;
      align-items: flex-start;
      cursor: pointer;
      transition: all var(--transition-speed);
      
      &:hover {
        transform: translateY(-2px);
        box-shadow: var(--box-shadow-lg);
      }
      
      .scan-status {
        width: 40px;
        height: 40px;
        border-radius: 8px;
        display: flex;
        align-items: center;
        justify-content: center;
        margin-right: 1rem;
        flex-shrink: 0;
        
        &.status-inprogress {
          background: rgba(255, 193, 7, 0.1);
          
          .mdi {
            color: var(--warning-color);
          }
        }
        
        &.status-completed {
          background: rgba(40, 167, 69, 0.1);
          
          .mdi {
            color: var(--success-color);
          }
        }
        
        &.status-failed, &.status-canceled {
          background: rgba(220, 53, 69, 0.1);
          
          .mdi {
            color: var(--danger-color);
          }
        }
        
        &.status-paused {
          background: rgba(108, 117, 125, 0.1);
          
          .mdi {
            color: var(--secondary-color);
          }
        }
        
        &.status-unknown {
          background: rgba(108, 117, 125, 0.1);
          
          .mdi {
            color: var(--secondary-color);
          }
        }
        
        .mdi {
          font-size: 20px;
        }
      }
      
      .scan-content {
        flex: 1;
        
        .scan-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 0.75rem;
          
          .scan-repository {
            margin: 0;
            font-size: 1.125rem;
          }
          
          .scan-date {
            font-size: 0.75rem;
            color: var(--text-muted);
          }
        }
        
        .scan-progress {
          margin-bottom: 0.75rem;
          display: flex;
          align-items: center;
          
          .progress-bar {
            flex: 1;
            height: 6px;
            background: #e9ecef;
            border-radius: 3px;
            margin-right: 0.5rem;
            overflow: hidden;
            
            .progress-value {
              height: 100%;
              background: var(--primary-color);
              border-radius: 3px;
            }
          }
          
          .progress-text {
            font-size: 0.75rem;
            font-weight: 500;
            width: 40px;
            text-align: right;
          }
        }
        
        .scan-meta {
          display: flex;
          justify-content: space-between;
          font-size: 0.75rem;
          margin-bottom: 0.75rem;
          
          .scan-issues {
            background: rgba(74, 108, 247, 0.1);
            padding: 0.25rem 0.5rem;
            border-radius: 4px;
            color: var(--primary-color);
          }
          
          .scan-status-text {
            font-weight: 500;
            
            &.status-inprogress {
              color: var(--warning-color);
            }
            
            &.status-completed {
              color: var(--success-color);
            }
            
            &.status-failed, &.status-canceled {
              color: var(--danger-color);
            }
            
            &.status-paused {
              color: var(--secondary-color);
            }
            
            &.status-unknown {
              color: var(--secondary-color);
            }
          }
          
          .debug-info {
            font-size: 0.75rem;
            color: #6c757d;
            font-family: monospace;
            display: block;
            margin-top: 0.25rem;
          }
        }
        
        .scan-actions {
          display: flex;
          gap: 0.5rem;
          flex-wrap: wrap;
          
          .debug-delete-reason {
            font-size: 0.75rem;
            color: #dc3545;
            font-style: italic;
            align-self: center;
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

.alert {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  margin-bottom: 1rem;
  border-radius: var(--border-radius);
  
  &.alert-danger {
    background-color: #f8d7da;
    border: 1px solid #f1aeb5;
    color: #721c24;
  }
  
  button {
    margin-left: auto;
  }
}

.btn-outline-danger {
  border: 1px solid #dc3545;
  color: #dc3545;
  background: transparent;
  
  &:hover {
    background: #dc3545;
    color: white;
  }
}

.btn-outline-secondary {
  border: 1px solid var(--secondary-color);
  color: var(--secondary-color);
  background: transparent;
  
  &:hover {
    background: var(--secondary-color);
    color: white;
  }
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
