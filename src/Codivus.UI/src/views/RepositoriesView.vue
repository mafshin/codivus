<template>
  <div class="repositories-view">
    <div class="page-header">
      <h1 class="page-title">Repositories</h1>
      <router-link to="/repositories/add" class="btn btn-primary">
        <span class="mdi mdi-plus"></span> Add Repository
      </router-link>
    </div>
    
    <div class="search-filter">
      <div class="search-box">
        <span class="mdi mdi-magnify"></span>
        <input 
          type="text" 
          v-model="searchQuery" 
          placeholder="Search repositories..." 
          class="search-input"
        />
        <span 
          v-if="searchQuery" 
          class="mdi mdi-close clear-search" 
          @click="searchQuery = ''"
        ></span>
      </div>
      
      <div class="filter-dropdown">
        <select v-model="typeFilter" class="filter-select">
          <option value="all">All Types</option>
          <option value="Local">Local</option>
          <option value="GitHub">GitHub</option>
        </select>
      </div>
      
      <div class="filter-dropdown">
        <select v-model="sortOption" class="filter-select">
          <option value="name-asc">Name (A-Z)</option>
          <option value="name-desc">Name (Z-A)</option>
          <option value="date-desc">Newest First</option>
          <option value="date-asc">Oldest First</option>
          <option value="last-scan-desc">Recently Scanned</option>
        </select>
      </div>
    </div>
    
    <div v-if="loading" class="loading-container">
      <span class="mdi mdi-loading mdi-spin"></span>
      <span>Loading repositories...</span>
    </div>
    
    <div v-else-if="filteredRepositories.length === 0" class="empty-state">
      <div v-if="searchQuery || typeFilter !== 'all'" class="no-results">
        <span class="mdi mdi-file-search-outline"></span>
        <h3>No matching repositories found</h3>
        <p>Try adjusting your search or filters.</p>
        <button class="btn btn-secondary" @click="clearFilters">Clear Filters</button>
      </div>
      <div v-else class="no-repositories">
        <span class="mdi mdi-source-repository"></span>
        <h3>No repositories added yet</h3>
        <p>Get started by adding your first repository.</p>
        <router-link to="/repositories/add" class="btn btn-primary">
          Add Repository
        </router-link>
      </div>
    </div>
    
    <div v-else class="repositories-list">
      <div 
        v-for="repository in filteredRepositories" 
        :key="repository.id" 
        class="repository-card"
        @click="navigateToRepository(repository.id)"
      >
        <div class="repository-header">
          <div class="repository-icon">
            <span v-if="repository.type === 'Local'" class="mdi mdi-folder"></span>
            <span v-else class="mdi mdi-github"></span>
          </div>
          <div class="repository-title">
            <h3>{{ repository.name }}</h3>
            <span class="repository-type">{{ repository.type }}</span>
          </div>
          <div class="repository-actions">
            <button class="action-btn" @click.stop="showDeleteModal(repository)">
              <span class="mdi mdi-delete"></span>
            </button>
          </div>
        </div>
        
        <div class="repository-body">
          <div class="location">
            <span class="label">Location:</span>
            <span class="value">{{ repository.location }}</span>
          </div>
          
          <div v-if="repository.description" class="description">
            <span class="label">Description:</span>
            <span class="value">{{ repository.description }}</span>
          </div>
          
          <div class="dates">
            <div class="added-date">
              <span class="label">Added:</span>
              <span class="value">{{ formatDate(repository.addedAt) }}</span>
            </div>
            
            <div v-if="repository.lastScanAt" class="last-scan">
              <span class="label">Last Scan:</span>
              <span class="value">{{ formatDate(repository.lastScanAt) }}</span>
            </div>
          </div>
        </div>
        
        <div class="repository-footer">
          <button class="btn btn-primary btn-sm" @click.stop="startScan(repository.id)">
            <span class="mdi mdi-radar"></span> Scan
          </button>
          <button class="btn btn-secondary btn-sm" @click.stop="navigateToRepository(repository.id)">
            <span class="mdi mdi-eye"></span> View Details
          </button>
        </div>
      </div>
    </div>
    
    <!-- Delete Confirmation Modal -->
    <div v-if="showDelete" class="modal-overlay" @click="showDelete = false">
      <div class="modal-container" @click.stop>
        <div class="modal-header">
          <h3>Delete Repository</h3>
          <button class="close-btn" @click="showDelete = false">
            <span class="mdi mdi-close"></span>
          </button>
        </div>
        
        <div class="modal-body">
          <p>
            Are you sure you want to delete <strong>{{ selectedRepository.name }}</strong>?
          </p>
          <p class="warning">
            <span class="mdi mdi-alert"></span>
            This action cannot be undone. All scans and issues associated with this repository will be deleted.
          </p>
        </div>
        
        <div class="modal-footer">
          <button class="btn btn-secondary" @click="showDelete = false">Cancel</button>
          <button 
            class="btn btn-danger" 
            @click="deleteRepository" 
            :disabled="deleting"
          >
            <span v-if="deleting" class="mdi mdi-loading mdi-spin"></span>
            Delete
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useRepositoryStore } from '@/store/repository'
import { useScanningStore } from '@/store/scanning'
import { useSettingsStore } from '@/store/settings'

const router = useRouter()
const repositoryStore = useRepositoryStore()
const scanningStore = useScanningStore()
const settingsStore = useSettingsStore()

// Reactive state
const loading = ref(true)
const searchQuery = ref('')
const typeFilter = ref('all')
const sortOption = ref('name-asc')
const showDelete = ref(false)
const selectedRepository = ref({})
const deleting = ref(false)

// Computed properties
const repositories = computed(() => repositoryStore.repositories)

const filteredRepositories = computed(() => {
  let result = [...repositories.value]
  
  // Apply search filter
  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(repo => 
      repo.name.toLowerCase().includes(query) || 
      repo.location.toLowerCase().includes(query) ||
      (repo.description && repo.description.toLowerCase().includes(query))
    )
  }
  
  // Apply type filter
  if (typeFilter.value !== 'all') {
    result = result.filter(repo => repo.type === typeFilter.value)
  }
  
  // Apply sorting
  switch (sortOption.value) {
    case 'name-asc':
      result.sort((a, b) => a.name.localeCompare(b.name))
      break
    case 'name-desc':
      result.sort((a, b) => b.name.localeCompare(a.name))
      break
    case 'date-desc':
      result.sort((a, b) => new Date(b.addedAt) - new Date(a.addedAt))
      break
    case 'date-asc':
      result.sort((a, b) => new Date(a.addedAt) - new Date(b.addedAt))
      break
    case 'last-scan-desc':
      result.sort((a, b) => {
        if (!a.lastScanAt) return 1
        if (!b.lastScanAt) return -1
        return new Date(b.lastScanAt) - new Date(a.lastScanAt)
      })
      break
  }
  
  return result
})

// Methods
const formatDate = (dateString) => {
  if (!dateString) return 'Never'
  
  const date = new Date(dateString)
  return date.toLocaleDateString(undefined, { 
    year: 'numeric', 
    month: 'short', 
    day: 'numeric' 
  })
}

const navigateToRepository = (id) => {
  router.push({ name: 'RepositoryDetails', params: { id } })
}

const clearFilters = () => {
  searchQuery.value = ''
  typeFilter.value = 'all'
}

const showDeleteModal = (repository) => {
  selectedRepository.value = repository
  showDelete.value = true
}

const deleteRepository = async () => {
  if (!selectedRepository.value.id) return
  
  deleting.value = true
  
  try {
    await repositoryStore.deleteRepository(selectedRepository.value.id)
    showDelete.value = false
  } catch (error) {
    console.error('Error deleting repository:', error)
    // Show error notification
  } finally {
    deleting.value = false
  }
}

const startScan = async (repositoryId) => {
  try {
    // Create default scan configuration
    const defaultConfig = settingsStore.buildDefaultScanConfiguration(repositoryId)
    
    // Start the scan
    const scan = await scanningStore.startScan(repositoryId, defaultConfig)
    
    // Navigate to scan details
    router.push({ name: 'ScanDetails', params: { id: scan.id } })
  } catch (error) {
    console.error('Error starting scan:', error)
    // Show error notification
  }
}

// Lifecycle hooks
onMounted(async () => {
  try {
    await repositoryStore.fetchRepositories()
  } catch (error) {
    console.error('Error fetching repositories:', error)
  } finally {
    loading.value = false
  }
})
</script>

<style lang="scss" scoped>
.repositories-view {
  .page-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 1.5rem;
    
    .page-title {
      margin: 0;
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
    
    .mdi-file-search-outline, .mdi-source-repository {
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
  
  .repositories-list {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
    gap: 1.5rem;
    
    .repository-card {
      background: white;
      border-radius: var(--border-radius);
      box-shadow: var(--box-shadow);
      overflow: hidden;
      transition: transform var(--transition-speed), box-shadow var(--transition-speed);
      cursor: pointer;
      
      &:hover {
        transform: translateY(-4px);
        box-shadow: var(--box-shadow-lg);
      }
      
      .repository-header {
        display: flex;
        align-items: center;
        padding: 1rem;
        border-bottom: 1px solid var(--border-color);
        
        .repository-icon {
          width: 40px;
          height: 40px;
          border-radius: 8px;
          background: rgba(74, 108, 247, 0.1);
          display: flex;
          align-items: center;
          justify-content: center;
          
          .mdi {
            font-size: 20px;
            color: var(--primary-color);
          }
        }
        
        .repository-title {
          flex: 1;
          margin-left: 0.75rem;
          
          h3 {
            margin: 0 0 0.25rem;
            font-size: 1.125rem;
          }
          
          .repository-type {
            display: inline-block;
            font-size: 0.75rem;
            padding: 0.125rem 0.375rem;
            background: rgba(74, 108, 247, 0.1);
            color: var(--primary-color);
            border-radius: 4px;
          }
        }
        
        .repository-actions {
          .action-btn {
            background: none;
            border: none;
            color: var(--text-muted);
            cursor: pointer;
            padding: 0.25rem;
            font-size: 1.25rem;
            border-radius: 4px;
            
            &:hover {
              color: var(--danger-color);
              background: rgba(220, 53, 69, 0.1);
            }
          }
        }
      }
      
      .repository-body {
        padding: 1rem;
        
        .location, .description {
          margin-bottom: 0.75rem;
          
          .label {
            font-weight: 500;
            font-size: 0.875rem;
            display: block;
            margin-bottom: 0.25rem;
          }
          
          .value {
            font-size: 0.875rem;
            color: var(--text-muted);
            display: block;
            word-break: break-all;
          }
        }
        
        .dates {
          display: flex;
          gap: 1rem;
          margin-top: 1rem;
          
          .added-date, .last-scan {
            flex: 1;
            
            .label {
              font-size: 0.75rem;
              color: var(--text-muted);
              display: block;
              margin-bottom: 0.25rem;
            }
            
            .value {
              font-size: 0.875rem;
              font-weight: 500;
            }
          }
        }
      }
      
      .repository-footer {
        padding: 1rem;
        background: #f9f9f9;
        display: flex;
        gap: 0.75rem;
        
        .btn {
          flex: 1;
        }
      }
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
      max-width: 500px;
      background: white;
      border-radius: var(--border-radius);
      box-shadow: var(--box-shadow-lg);
      overflow: hidden;
      
      .modal-header {
        padding: 1rem;
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
        
        .warning {
          margin-top: 1rem;
          color: var(--danger-color);
          display: flex;
          align-items: center;
          
          .mdi-alert {
            margin-right: 0.5rem;
          }
        }
      }
      
      .modal-footer {
        padding: 1rem;
        border-top: 1px solid var(--border-color);
        display: flex;
        justify-content: flex-end;
        gap: 0.75rem;
      }
    }
  }
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.btn-sm {
  padding: 0.375rem 0.75rem;
  font-size: 0.875rem;
}
</style>
