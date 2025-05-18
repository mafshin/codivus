<template>
  <div class="repository-details-view">
    <div v-if="loading" class="loading-container">
      <span class="mdi mdi-loading mdi-spin"></span>
      <span>Loading repository details...</span>
    </div>
    
    <template v-else-if="repository">
      <div class="page-header">
        <div class="page-title-container">
          <h1 class="page-title">{{ repository.name }}</h1>
          <div class="repository-meta">
            <span class="repository-type">{{ repository.type }}</span>
            <span class="repository-location">{{ repository.location }}</span>
          </div>
        </div>
        
        <div class="page-actions">
          <button class="btn btn-primary" @click="startScan">
            <span class="mdi mdi-radar"></span> Scan Repository
          </button>
          <button class="btn btn-secondary" @click="refreshData">
            <span class="mdi mdi-refresh"></span> Refresh
          </button>
          <button class="btn btn-secondary" @click="goBack">
            <span class="mdi mdi-arrow-left"></span> Back
          </button>
        </div>
      </div>
      
      <div class="repository-tabs">
        <button 
          class="tab-button" 
          :class="{ active: activeTab === 'structure' }"
          @click="activeTab = 'structure'"
        >
          <span class="mdi mdi-file-tree"></span> File Structure
        </button>
        <button 
          class="tab-button" 
          :class="{ active: activeTab === 'scans' }"
          @click="activeTab = 'scans'"
        >
          <span class="mdi mdi-history"></span> Scan History
        </button>
        <button 
          class="tab-button" 
          :class="{ active: activeTab === 'issues' }"
          @click="activeTab = 'issues'"
        >
          <span class="mdi mdi-alert-circle"></span> Issues
        </button>
        <button 
          class="tab-button" 
          :class="{ active: activeTab === 'settings' }"
          @click="activeTab = 'settings'"
        >
          <span class="mdi mdi-cog"></span> Settings
        </button>
      </div>
      
      <div class="tab-content">
        <!-- Structure Tab -->
<div v-if="activeTab === 'structure'" class="structure-tab">
          <div v-if="loadingStructure" class="loading-indicator">
            <span class="mdi mdi-loading mdi-spin"></span> Loading repository structure...
          </div>
          <div v-else-if="!repositoryStructure" class="empty-state">
            <span class="mdi mdi-folder-outline"></span>
            <p>Repository structure not available.</p>
            <button class="btn btn-primary" @click="fetchRepositoryStructure">
              Refresh Structure
            </button>
          </div>
          <div v-else class="structure-tree">
            <div class="structure-tree-header">
              <h3>Repository Files</h3>
              <div class="structure-actions">
                <button class="btn btn-sm btn-secondary" @click="fetchRepositoryStructure">
                  <span class="mdi mdi-refresh"></span> Refresh
                </button>
                <button class="btn btn-sm btn-secondary" @click="toggleExpandAll">
                  {{ settingsStore.expandAll ? 'Collapse All' : 'Expand All' }}
                </button>
              </div>
            </div>
            
            <!-- Repository information card -->
            <div class="repository-info-card">
              <div class="info-row">
                <div class="info-label">Location:</div>
                <div class="info-value location-path">
                  <span class="mdi mdi-folder"></span>
                  {{ repository.location }}
                </div>
              </div>
              <div class="info-row">
                <div class="info-label">Type:</div>
                <div class="info-value">
                  <span v-if="repository.type === 'Local'" class="mdi mdi-git"></span>
                  <span v-else class="mdi mdi-github"></span>
                  {{ repository.type }} Repository
                </div>
              </div>
              <div class="info-row" v-if="repository.defaultBranch">
                <div class="info-label">Branch:</div>
                <div class="info-value">
                  <span class="mdi mdi-source-branch"></span>
                  {{ repository.defaultBranch }}
                </div>
              </div>
              <div class="info-row" v-if="repository.description">
                <div class="info-label">Description:</div>
                <div class="info-value description">{{ repository.description }}</div>
              </div>
              <div class="info-row">
                <div class="info-label">Added:</div>
                <div class="info-value">
                  <span class="mdi mdi-calendar"></span>
                  {{ formatDate(repository.addedAt) }}
                </div>
              </div>
              <div class="info-row" v-if="repositoryStructure?.children">
                <div class="info-label">Files:</div>
                <div class="info-value">
                  <span class="mdi mdi-file-multiple"></span>
                  {{ countFiles(repositoryStructure) }} files in {{ countDirectories(repositoryStructure) }} directories
                </div>
              </div>
            </div>
            
            <div class="file-structure-container" v-if="repositoryStructure">
              <h4>File Structure:</h4>
              <FileTree 
                :structure="repositoryStructure" 
                :expand-all="settingsStore.expandAll"
                :loading="loadingStructure"
                :error="structureError"
                @refresh="fetchRepositoryStructure"
                @retry="fetchRepositoryStructure"
                @file-selected="onFileSelected"
              />
            </div>
            <div v-else-if="!loadingStructure && !structureError" class="empty-structure">  
              <span class="mdi mdi-folder-open"></span>
              <p>Repository appears to be empty.</p>
              <button class="btn btn-primary" @click="fetchRepositoryStructure">
                <span class="mdi mdi-refresh"></span> Refresh Structure
              </button>
            </div>
            <div v-else-if="structureError" class="error-structure">
              <span class="mdi mdi-alert-circle"></span>
              <p>Error loading repository structure: {{ structureError }}</p>
              <button class="btn btn-primary" @click="fetchRepositoryStructure">
                <span class="mdi mdi-refresh"></span> Retry
              </button>
            </div>
          </div>
        </div>
        
        <!-- Scans Tab -->
        <div v-if="activeTab === 'scans'" class="scans-tab">
          <div v-if="loadingScans" class="loading-indicator">
            <span class="mdi mdi-loading mdi-spin"></span> Loading scan history...
          </div>
          <div v-else-if="!scans.length" class="empty-state">
            <span class="mdi mdi-radar"></span>
            <p>No scans have been performed yet.</p>
            <button class="btn btn-primary" @click="startScan">
              Start New Scan
            </button>
          </div>
          <div v-else class="scans-list">
            <div 
              v-for="scan in scans" 
              :key="scan.id" 
              class="scan-card"
              @click="navigateToScan(scan.id)"
            >
              <div class="scan-status" :class="'status-' + (scan.status ? String(scan.status).toLowerCase() : 'unknown')">
                <span class="mdi" :class="getStatusIcon(scan.status)"></span>
              </div>
              <div class="scan-content">
                <div class="scan-header">
                  <h3 class="scan-title">Scan #{{ scan.id.substr(0, 8) }}</h3>
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
                    :class="'status-' + (scan.status ? String(scan.status).toLowerCase() : 'unknown')"
                  >
                    {{ scan.status || 'Unknown' }}
                  </span>
                </div>
                <div class="scan-actions">
                  <button 
                    v-if="scan.status === 'InProgress'"
                    class="btn btn-sm btn-warning" 
                    @click.stop="pauseScan(scan.id)"
                  >
                    <span class="mdi mdi-pause"></span> Pause
                  </button>
                  <button 
                    v-if="scan.status === 'Paused'"
                    class="btn btn-sm btn-primary" 
                    @click.stop="resumeScan(scan.id)"
                  >
                    <span class="mdi mdi-play"></span> Resume
                  </button>
                  <button 
                    v-if="scan.status === 'InProgress' || scan.status === 'Paused'"
                    class="btn btn-sm btn-danger" 
                    @click.stop="cancelScan(scan.id)"
                  >
                    <span class="mdi mdi-stop"></span> Cancel
                  </button>
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
        
        <!-- Issues Tab -->
        <div v-if="activeTab === 'issues'" class="issues-tab">
          <div v-if="loadingIssues" class="loading-indicator">
            <span class="mdi mdi-loading mdi-spin"></span> Loading issues...
          </div>
          <div v-else-if="!issues.length" class="empty-state">
            <span class="mdi mdi-check-circle"></span>
            <p>No issues have been found in this repository.</p>
            <button class="btn btn-primary" @click="startScan">
              Start New Scan
            </button>
          </div>
          <div v-else class="issues-list">
            <!-- Placeholder for issues list -->
            <div class="issues-placeholder">
              <p>This is where the issues list would be displayed.</p>
              <p>In a fully implemented version, this would show a list of issues with filtering and sorting options.</p>
            </div>
          </div>
        </div>
        
        <!-- Settings Tab -->
        <div v-if="activeTab === 'settings'" class="settings-tab">
          <!-- Placeholder for settings -->
          <div class="settings-placeholder">
            <p>This is where the repository-specific settings would be displayed.</p>
            <p>In a fully implemented version, this would include scan configurations, exclusion patterns, etc.</p>
          </div>
        </div>
      </div>
    </template>
    
    <div v-else class="not-found">
      <span class="mdi mdi-alert-circle"></span>
      <h2>Repository Not Found</h2>
      <p>The repository you're looking for doesn't exist or has been deleted.</p>
      <router-link to="/repositories" class="btn btn-primary">
        Back to Repositories
      </router-link>
    </div>
    
    <!-- Scan Configuration Modal -->
    <div v-if="showScanConfigModal" class="modal-overlay" @click="showScanConfigModal = false">
      <div class="modal-container" @click.stop>
        <div class="modal-header">
          <h3>Start New Scan</h3>
          <button class="close-btn" @click="showScanConfigModal = false">
            <span class="mdi mdi-close"></span>
          </button>
        </div>
        
        <div class="modal-body">
          <!-- Placeholder for scan configuration form -->
          <p>This is where the scan configuration form would be displayed.</p>
          <p>In a fully implemented version, this would include options for:</p>
          <ul>
            <li>LLM Provider selection</li>
            <li>Issue categories to scan for</li>
            <li>Minimum severity level</li>
            <li>File inclusion/exclusion patterns</li>
          </ul>
        </div>
        
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="showScanConfigModal = false">Cancel</button>
          <button 
            class="btn btn-primary" 
            @click="startScanWithDefaultConfig" 
            :disabled="scanning"
          >
            <span v-if="scanning" class="mdi mdi-loading mdi-spin"></span>
            Start Scan
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useRepositoryStore } from '@/store/repository'
import { useScanningStore } from '@/store/scanning'
import { useSettingsStore } from '@/store/settings'
import FileTree from '@/components/repository/FileTree.vue'

const route = useRoute()
const router = useRouter()
const repositoryStore = useRepositoryStore()
const scanningStore = useScanningStore()
const settingsStore = useSettingsStore()

// Reactive state
const loading = ref(true)
const loadingStructure = ref(false)
const loadingScans = ref(false)
const loadingIssues = ref(false)
const activeTab = ref('structure')
const showScanConfigModal = ref(false)
const scanning = ref(false)
const structureError = ref(null)
const refreshInterval = ref(null)

// Computed properties
const repository = computed(() => repositoryStore.currentRepository)
const repositoryStructure = computed(() => repositoryStore.repositoryStructure)
const scans = computed(() => scanningStore.getScansByRepositoryId(route.params.id) || [])
const issues = computed(() => []) // In a real app, this would be populated with issues

// Helper functions for repository structure
const countFiles = (structure) => {
  if (!structure) return 0
  
  let count = 0
  
  // Count direct file children
  if (structure.children) {
    structure.children.forEach(child => {
      if (!child.isDirectory) {
        count++
      } else {
        // Recursively count files in subdirectories
        count += countFiles(child)
      }
    })
  }
  
  return count
}

const countDirectories = (structure) => {
  if (!structure) return 0
  
  let count = 0
  
  // Count direct directory children
  if (structure.children) {
    structure.children.forEach(child => {
      if (child.isDirectory) {
        count++
        // Recursively count subdirectories
        count += countDirectories(child)
      }
    })
  }
  
  return count
}

// Auto-refresh functionality
const startAutoRefresh = () => {
  if (refreshInterval.value) {
    clearInterval(refreshInterval.value)
  }
  
  refreshInterval.value = setInterval(async () => {
    // Only refresh active scans
    const activeScans = scans.value.filter(scan => 
      scan.status === 'InProgress' || scan.status === 'Initializing'
    )
    
    if (activeScans.length > 0) {
      try {
        await fetchScans()
      } catch (error) {
        console.warn('Error auto-refreshing scans:', error)
      }
    }
  }, 5000) // Refresh every 5 seconds
}

const stopAutoRefresh = () => {
  if (refreshInterval.value) {
    clearInterval(refreshInterval.value)
    refreshInterval.value = null
  }
}

// Enhanced repository structure rendering
const formatFileSize = (bytes) => {
  if (!bytes) return '0 B'
  
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(1024))
  return `${(bytes / Math.pow(1024, i)).toFixed(2)} ${sizes[i]}`
}

// Methods
const goBack = () => {
  router.push({ name: 'Repositories' })
}

const refreshData = async () => {
  try {
    await Promise.all([
      fetchRepositoryStructure(),
      fetchScans()
    ])
  } catch (error) {
    console.error('Error refreshing data:', error)
  }
}

const fetchRepository = async () => {
  console.log('Fetching repository for ID:', route.params.id)
  try {
    await repositoryStore.fetchRepository(route.params.id)
    console.log('Repository fetched successfully:', repository.value)
  } catch (error) {
    console.error('Error fetching repository:', error)
    throw error // Re-throw to handle in onMounted
  }
}

const fetchRepositoryStructure = async () => {
  loadingStructure.value = true
  structureError.value = null
  try {
    console.log('Fetching repository structure for:', route.params.id)
    await repositoryStore.fetchRepositoryStructure(route.params.id)
    console.log('Repository structure fetched successfully')
  } catch (error) {
    console.error('Error fetching repository structure:', error)
    structureError.value = error.message || 'Failed to load repository structure'
  } finally {
    loadingStructure.value = false
  }
}

const fetchScans = async () => {
  loadingScans.value = true
  try {
    await scanningStore.fetchScansByRepository(route.params.id)
  } catch (error) {
    console.error('Error fetching scans:', error)
  } finally {
    loadingScans.value = false
  }
}

const startScan = () => {
  showScanConfigModal.value = true
}

const startScanWithDefaultConfig = async () => {
  scanning.value = true
  showScanConfigModal.value = false
  
  try {
    // Create default scan configuration
    const defaultConfig = settingsStore.buildDefaultScanConfiguration(route.params.id)
    console.log('Starting scan with config:', JSON.stringify(defaultConfig))
    
    // Start the scan
    const scan = await scanningStore.startScan(route.params.id, defaultConfig)
    console.log('Scan started successfully:', scan)
    
    // Navigate to scan details
    if (scan && scan.id) {
      router.push({ name: 'ScanDetails', params: { id: scan.id } })
    } else {
      console.error('Scan object missing ID:', scan)
      alert('Scan started but no scan ID was returned. Check console for details.')
    }
  } catch (error) {
    console.error('Error starting scan:', error)
    alert(`Failed to start scan: ${error.message || 'Unknown error'}`)
  } finally {
    scanning.value = false
  }
}

const pauseScan = async (scanId) => {
  try {
    await scanningStore.pauseScan(scanId)
    await fetchScans() // Refresh scans after action
  } catch (error) {
    console.error('Error pausing scan:', error)
  }
}

const resumeScan = async (scanId) => {
  try {
    await scanningStore.resumeScan(scanId)
    await fetchScans() // Refresh scans after action
  } catch (error) {
    console.error('Error resuming scan:', error)
  }
}

const cancelScan = async (scanId) => {
  try {
    await scanningStore.cancelScan(scanId)
    await fetchScans() // Refresh scans after action
  } catch (error) {
    console.error('Error canceling scan:', error)
  }
}

const navigateToScan = (scanId) => {
  router.push({ name: 'ScanDetails', params: { id: scanId } })
}

const onFileSelected = (file) => {
  console.log('File selected:', file)
  // TODO: Implement file content viewer or other file actions
  // For now, we could show a toast or highlight the selected file
}

const toggleExpandAll = () => {
  settingsStore.toggleExpandAll()
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

const calculateProgress = (scan) => {
  if (scan.status === 'Completed') return 100
  if (scan.status === 'Failed' || scan.status === 'Canceled') return 0
  
  if (scan.totalFiles > 0) {
    return Math.round((scan.scannedFiles / scan.totalFiles) * 100)
  }
  
  return 0
}

const getStatusIcon = (status) => {
  if (!status) return 'mdi-help-circle'
  
  switch (status) {
    case 'InProgress': return 'mdi-progress-clock'
    case 'Initializing': return 'mdi-timer-sand'
    case 'Completed': return 'mdi-check-circle'
    case 'Failed': return 'mdi-alert-circle'
    case 'Paused': return 'mdi-pause-circle'
    case 'Canceled': return 'mdi-stop-circle'
    default: return 'mdi-help-circle'
  }
}

// Lifecycle hooks
onMounted(async () => {
  console.log('Repository details component mounted, ID:', route.params.id)
  
  // Set a timeout to prevent infinite loading
  const loadingTimeout = setTimeout(() => {
    if (loading.value) {
      console.error('Repository loading timed out after 10 seconds')
      loading.value = false
      structureError.value = 'Loading timed out. Please refresh the page.'
    }
  }, 10000) // 10 second timeout
  
  try {
    // Start loading
    loading.value = true
    
    // Fetch repository
    await fetchRepository()
    
    if (repository.value) {
      console.log('Repository loaded, starting background operations')
      
      // Start both operations in parallel without blocking main loading
      Promise.all([
        fetchRepositoryStructure().catch(error => {
          console.error('Error loading repository structure:', error)
        }),
        fetchScans().catch(error => {
          console.error('Error loading scans:', error)
        })
      ])
      
      // Start auto-refresh for active scans
      startAutoRefresh()
    } else {
      console.warn('Repository not found or failed to load')
    }
  } catch (error) {
    console.error('Error in repository details initialization:', error)
  } finally {
    // Always stop loading regardless of what happens
    clearTimeout(loadingTimeout)
    loading.value = false
    console.log('Repository details loading completed')
  }
})

onUnmounted(() => {
  stopAutoRefresh()
})
</script>

<style lang="scss" scoped>
.repository-details-view {
  .repository-info-card {
    margin-bottom: 1.5rem;
    padding: 1.25rem;
    background: #f8f9fa;
    border-radius: var(--border-radius);
    border-left: 4px solid var(--primary-color);
    
    .info-row {
      display: flex;
      margin-bottom: 0.75rem;
      
      &:last-child {
        margin-bottom: 0;
      }
      
      .info-label {
        width: 120px;
        font-weight: 500;
        color: var(--text-color);
      }
      
      .info-value {
        flex: 1;
        color: var(--text-muted);
        display: flex;
        align-items: center;
        
        .mdi {
          margin-right: 0.5rem;
          font-size: 1.125rem;
        }
        
        &.location-path {
          word-break: break-all;
        }
        
        &.description {
          font-style: italic;
        }
      }
    }
  }
  
  .file-structure-container {
    margin-top: 1.5rem;
    
    h4 {
      margin-bottom: 1rem;
      font-size: 1.125rem;
    }
  }
  
  .empty-structure, .error-structure {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 2rem;
    color: var(--text-muted);
    
    .mdi {
      font-size: 3rem;
      margin-bottom: 0.75rem;
      
      &.mdi-alert-circle {
        color: var(--danger-color);
      }
    }
    
    p {
      margin-bottom: 1rem;
      text-align: center;
    }
  }
  
  .page-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    margin-bottom: 1.5rem;
    
    .page-title-container {
      .page-title {
        margin: 0 0 0.5rem;
      }
      
      .repository-meta {
        display: flex;
        align-items: center;
        gap: 1rem;
        font-size: 0.875rem;
        
        .repository-type {
          display: inline-block;
          padding: 0.25rem 0.5rem;
          background: rgba(74, 108, 247, 0.1);
          color: var(--primary-color);
          border-radius: 4px;
          font-weight: 500;
        }
        
        .repository-location {
          color: var(--text-muted);
          word-break: break-all;
        }
      }
    }
    
    .page-actions {
      display: flex;
      gap: 0.75rem;
    }
  }
  
  .repository-tabs {
    display: flex;
    margin-bottom: 1.5rem;
    border-bottom: 1px solid var(--border-color);
    
    .tab-button {
      padding: 0.75rem 1.25rem;
      background: none;
      border: none;
      border-bottom: 2px solid transparent;
      cursor: pointer;
      font-weight: 500;
      color: var(--text-muted);
      transition: all var(--transition-speed);
      display: flex;
      align-items: center;
      
      .mdi {
        margin-right: 0.5rem;
      }
      
      &:hover {
        color: var(--text-color);
      }
      
      &.active {
        color: var(--primary-color);
        border-bottom-color: var(--primary-color);
      }
    }
  }
  
  .tab-content {
    background: white;
    border-radius: var(--border-radius);
    box-shadow: var(--box-shadow);
    padding: 1.5rem;
    min-height: 400px;
    
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
      
      .btn {
        min-width: 150px;
      }
    }
    
    .structure-tree {
      .structure-tree-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 1.5rem;
        
        h3 {
          margin: 0;
        }
        
        .structure-actions {
          display: flex;
          align-items: center;
          gap: 1rem;
        }
      }
    }
    
    .scans-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      
      .scan-card {
        padding: 1rem;
        border-radius: var(--border-radius);
        border: 1px solid var(--border-color);
        display: flex;
        align-items: flex-start;
        cursor: pointer;
        transition: all var(--transition-speed);
        
        &:hover {
          border-color: var(--primary-color);
          transform: translateY(-2px);
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
            
            .scan-title {
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
            }
          }
          
          .scan-actions {
            display: flex;
            gap: 0.5rem;
          }
        }
      }
    }
    
    .issues-placeholder, .settings-placeholder {
      padding: 2rem;
      background: #f9f9f9;
      border-radius: var(--border-radius);
      text-align: center;
      color: var(--text-muted);
    }
  }
  
  .not-found {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 4rem 0;
    text-align: center;
    
    .mdi {
      font-size: 4rem;
      color: var(--danger-color);
      margin-bottom: 1rem;
    }
    
    h2 {
      margin-bottom: 0.5rem;
    }
    
    p {
      color: var(--text-muted);
      margin-bottom: 1.5rem;
    }
  }
  
  .modal-overlay {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
    
    .modal-container {
      width: 100%;
      max-width: 600px;
      background: white;
      border-radius: var(--border-radius);
      box-shadow: var(--box-shadow-lg);
      overflow: hidden;
      
      .modal-header {
        padding: 1rem 1.5rem;
        border-bottom: 1px solid var(--border-color);
        display: flex;
        align-items: center;
        justify-content: space-between;
        
        h3 {
          margin: 0;
          font-size: 1.25rem;
        }
        
        .close-btn {
          background: none;
          border: none;
          padding: 0.25rem;
          font-size: 1.25rem;
          cursor: pointer;
          color: var(--text-muted);
          border-radius: 4px;
          
          &:hover {
            color: var(--text-color);
            background: #f1f3f5;
          }
        }
      }
      
      .modal-body {
        padding: 1.5rem;
      }
      
      .modal-footer {
        padding: 1rem 1.5rem;
        border-top: 1px solid var(--border-color);
        display: flex;
        justify-content: flex-end;
        gap: 0.75rem;
      }
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

.btn-sm {
  padding: 0.375rem 0.75rem;
  font-size: 0.875rem;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
