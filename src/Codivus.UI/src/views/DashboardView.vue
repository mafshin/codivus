<template>
  <div class="dashboard">
    <h1 class="page-title">Dashboard</h1>
    
    <div class="dashboard-summary">
      <div class="summary-card">
        <div class="summary-icon">
          <span class="mdi mdi-source-repository"></span>
        </div>
        <div class="summary-content">
          <h3 class="summary-value">{{ repositories.length }}</h3>
          <p class="summary-label">Repositories</p>
        </div>
      </div>
      
      <div class="summary-card">
        <div class="summary-icon">
          <span class="mdi mdi-radar"></span>
        </div>
        <div class="summary-content">
          <h3 class="summary-value">{{ totalScans }}</h3>
          <p class="summary-label">Total Scans</p>
        </div>
      </div>
      
      <div class="summary-card">
        <div class="summary-icon">
          <span class="mdi mdi-alert-circle"></span>
        </div>
        <div class="summary-content">
          <h3 class="summary-value">{{ totalIssues }}</h3>
          <p class="summary-label">Issues Found</p>
        </div>
      </div>
      
      <div class="summary-card">
        <div class="summary-icon">
          <span class="mdi mdi-chart-timeline-variant"></span>
        </div>
        <div class="summary-content">
          <h3 class="summary-value">{{ activeScans }}</h3>
          <p class="summary-label">Active Scans</p>
        </div>
      </div>
    </div>
    
    <div class="dashboard-content">
      <div class="dashboard-section repositories-section">
        <div class="section-header">
          <h2 class="section-title">Recent Repositories</h2>
          <router-link to="/repositories" class="btn btn-primary">View All</router-link>
        </div>
        
        <div v-if="loadingRepositories" class="loading-indicator">
          <span class="mdi mdi-loading mdi-spin"></span> Loading repositories...
        </div>
        
        <div v-else-if="repositories.length === 0" class="empty-state">
          <p>No repositories added yet.</p>
          <router-link to="/repositories/add" class="btn btn-primary">Add Repository</router-link>
        </div>
        
        <div v-else class="repositories-list">
          <div v-for="repo in recentRepositories" :key="repo.id" class="repository-card" @click="navigateToRepository(repo.id)">
            <div class="repository-icon">
              <span v-if="repo.type === 'Local'" class="mdi mdi-folder"></span>
              <span v-else class="mdi mdi-github"></span>
            </div>
            <div class="repository-content">
              <h3 class="repository-name">{{ repo.name }}</h3>
              <p class="repository-path">{{ repo.location }}</p>
              <div class="repository-meta">
                <span class="repository-type">{{ repo.type }}</span>
                <span v-if="repo.lastScanAt" class="repository-last-scan">
                  Last scan: {{ formatDate(repo.lastScanAt) }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
      
      <div class="dashboard-section scans-section">
        <div class="section-header">
          <h2 class="section-title">Recent Scans</h2>
          <router-link to="/scans" class="btn btn-primary">View All</router-link>
        </div>
        
        <div v-if="loadingScans" class="loading-indicator">
          <span class="mdi mdi-loading mdi-spin"></span> Loading scans...
        </div>
        
        <div v-else-if="recentScans.length === 0" class="empty-state">
          <p>No scans performed yet.</p>
        </div>
        
        <div v-else class="scans-list">
          <div 
            v-for="scan in recentScans" 
            :key="scan.id || Math.random()" 
            class="scan-card" 
            @click="navigateToScan(scan.id)"
            v-if="scan && typeof scan === 'object'"
          >
            <div class="scan-status" :class="getStatusClass(scan.status)">
              <span class="mdi" :class="getStatusIcon(scan.status)"></span>
            </div>
            <div class="scan-content">
              <h3 class="scan-repository">{{ getRepositoryName(scan.repositoryId) }}</h3>
              <div class="scan-progress">
                <div class="progress-bar">
                  <div class="progress-value" :style="{ width: calculateProgress(scan) + '%' }"></div>
                </div>
                <span class="progress-text">{{ calculateProgress(scan) }}%</span>
              </div>
              <div class="scan-meta">
                <span class="scan-issues">{{ scan.issuesFound || 0 }} issues</span>
                <span class="scan-date">{{ formatDate(scan.startedAt) }}</span>
              </div>
            </div>
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
const loadingRepositories = ref(false)
const loadingScans = ref(false)

// Computed properties
const repositories = computed(() => repositoryStore.repositories)

const recentRepositories = computed(() => {
  return repositories.value
    .slice()
    .sort((a, b) => new Date(b.addedAt) - new Date(a.addedAt))
    .slice(0, 5)
})

const allScans = computed(() => {
  try {
    const scansObj = scanningStore.scans || {}
    const scansArray = Object.values(scansObj)
    return scansArray.filter(scan => {
      if (!scan || typeof scan !== 'object') {
        console.warn('Invalid scan object found in store:', scan)
        return false
      }
      return true
    })
  } catch (error) {
    console.error('Error processing scans:', error)
    return []
  }
})

const recentScans = computed(() => {
  return allScans.value
    .filter(scan => {
      // Debug logging to understand the scan object structure
      if (!scan) {
        console.warn('Found null/undefined scan in allScans')
        return false
      }
      if (!scan.startedAt && !scan.createdAt) {
        console.warn('Scan missing both startedAt and createdAt:', scan)
        return false
      }
      return true
    })
    .slice()
    .sort((a, b) => {
      const aDate = new Date(a.startedAt || a.createdAt)
      const bDate = new Date(b.startedAt || b.createdAt)
      return bDate - aDate
    })
    .slice(0, 5)
})

const totalScans = computed(() => allScans.value.length)

const totalIssues = computed(() => {
  return allScans.value.reduce((total, scan) => {
    const issues = scan && typeof scan.issuesFound === 'number' ? scan.issuesFound : 0
    return total + issues
  }, 0)
})

const activeScans = computed(() => {
  return allScans.value.filter(scan => 
    scan && scan.status && (scan.status === 'InProgress' || scan.status === 'Initializing')
  ).length
})

// Methods
const navigateToRepository = (id) => {
  router.push({ name: 'RepositoryDetails', params: { id } })
}

const navigateToScan = (id) => {
  router.push({ name: 'ScanDetails', params: { id } })
}

const getRepositoryName = (repositoryId) => {
  if (!repositoryId) return 'Unknown Repository'
  const repository = repositoryStore.getRepositoryById(repositoryId)
  return repository ? repository.name : 'Unknown Repository'
}

const calculateProgress = (scan) => {
  if (!scan || !scan.status) return 0
  if (scan.status === 'Completed') return 100
  if (scan.status === 'Failed' || scan.status === 'Canceled') return 0
  
  const totalFiles = scan.totalFiles || 0
  const scannedFiles = scan.scannedFiles || 0
  
  if (totalFiles > 0) {
    return Math.round((scannedFiles / totalFiles) * 100)
  }
  
  return 0
}

const getStatusClass = (status) => {
  if (status === null || status === undefined) {
    return 'status-unknown'
  }
  return 'status-' + String(status).toLowerCase()
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

// Fetch data on component mount
onMounted(async () => {
  loadingRepositories.value = true
  try {
    await repositoryStore.fetchRepositories()
    
    // For each repository, fetch recent scans
    loadingScans.value = true
    for (const repo of repositories.value) {
      try {
        await scanningStore.fetchScansByRepository(repo.id)
      } catch (error) {
        console.warn(`Failed to fetch scans for repository ${repo.id}:`, error)
        // Continue with other repositories even if one fails
      }
    }
  } catch (error) {
    console.error('Error loading dashboard data:', error)
  } finally {
    loadingRepositories.value = false
    loadingScans.value = false
  }
})
</script>

<style lang="scss" scoped>
.dashboard {
  .page-title {
    margin-bottom: 1.5rem;
  }
  
  .dashboard-summary {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
    gap: 1.5rem;
    margin-bottom: 2rem;
    
    .summary-card {
      background: white;
      border-radius: var(--border-radius);
      box-shadow: var(--box-shadow);
      padding: 1.5rem;
      display: flex;
      align-items: center;
      
      .summary-icon {
        width: 48px;
        height: 48px;
        background: rgba(74, 108, 247, 0.1);
        border-radius: 12px;
        display: flex;
        align-items: center;
        justify-content: center;
        margin-right: 1rem;
        
        .mdi {
          font-size: 24px;
          color: var(--primary-color);
        }
      }
      
      .summary-content {
        flex: 1;
        
        .summary-value {
          font-size: 1.75rem;
          font-weight: 700;
          margin: 0;
          line-height: 1.2;
        }
        
        .summary-label {
          margin: 0;
          color: var(--text-muted);
          font-size: 0.875rem;
        }
      }
    }
  }
  
  .dashboard-content {
    display: grid;
    grid-template-columns: 1fr;
    gap: 1.5rem;
    
    @media (min-width: 992px) {
      grid-template-columns: 1fr 1fr;
    }
    
    .dashboard-section {
      background: white;
      border-radius: var(--border-radius);
      box-shadow: var(--box-shadow);
      padding: 1.5rem;
      
      .section-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 1.5rem;
        
        .section-title {
          margin: 0;
          font-size: 1.25rem;
        }
      }
      
      .loading-indicator, .empty-state {
        padding: 2rem 0;
        text-align: center;
        color: var(--text-muted);
        
        .mdi-loading {
          animation: spin 1s linear infinite;
          font-size: 24px;
          margin-right: 0.5rem;
        }
      }
      
      .empty-state {
        .btn {
          margin-top: 1rem;
        }
      }
    }
    
    .repositories-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      
      .repository-card {
        padding: 1rem;
        border-radius: var(--border-radius);
        border: 1px solid var(--border-color);
        display: flex;
        align-items: center;
        cursor: pointer;
        transition: all var(--transition-speed);
        
        &:hover {
          border-color: var(--primary-color);
          transform: translateY(-2px);
        }
        
        .repository-icon {
          width: 40px;
          height: 40px;
          border-radius: 8px;
          background: rgba(74, 108, 247, 0.1);
          display: flex;
          align-items: center;
          justify-content: center;
          margin-right: 1rem;
          
          .mdi {
            font-size: 20px;
            color: var(--primary-color);
          }
        }
        
        .repository-content {
          flex: 1;
          
          .repository-name {
            margin: 0 0 0.25rem;
            font-size: 1rem;
          }
          
          .repository-path {
            margin: 0 0 0.5rem;
            font-size: 0.75rem;
            color: var(--text-muted);
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
          }
          
          .repository-meta {
            display: flex;
            justify-content: space-between;
            font-size: 0.75rem;
            
            .repository-type {
              background: rgba(74, 108, 247, 0.1);
              padding: 0.25rem 0.5rem;
              border-radius: 4px;
              color: var(--primary-color);
            }
            
            .repository-last-scan {
              color: var(--text-muted);
            }
          }
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
        align-items: center;
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
              color: var(--text-muted);
            }
          }
          
          .mdi {
            font-size: 20px;
          }
        }
        
        .scan-content {
          flex: 1;
          
          .scan-repository {
            margin: 0 0 0.5rem;
            font-size: 1rem;
          }
          
          .scan-progress {
            margin-bottom: 0.5rem;
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
            
            .scan-issues {
              background: rgba(74, 108, 247, 0.1);
              padding: 0.25rem 0.5rem;
              border-radius: 4px;
              color: var(--primary-color);
            }
            
            .scan-date {
              color: var(--text-muted);
            }
          }
        }
      }
    }
  }
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
