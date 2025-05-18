<template>
  <div class="file-tree-sidebar" :class="{ collapsed: collapsed }">
    <div class="sidebar-header">
      <h3>Repository Files</h3>
      <button 
        class="collapse-btn"
        @click="$emit('toggle-sidebar')"
        :title="collapsed ? 'Expand sidebar' : 'Collapse sidebar'"
      >
        <span class="mdi" :class="collapsed ? 'mdi-chevron-right' : 'mdi-chevron-left'"></span>
      </button>
    </div>
    
    <div v-if="!collapsed" class="sidebar-content">
      <div v-if="loading" class="loading-indicator">
        <span class="mdi mdi-loading mdi-spin"></span>
        <span>Loading files...</span>
      </div>
      
      <div v-else-if="!fileTree.length" class="empty-files">
        <span class="mdi mdi-folder-open-outline"></span>
        <p>No files available</p>
      </div>
      
      <div v-else class="file-tree">
        <div class="file-stats">
          <span class="total-files">{{ totalFiles }} files</span>
          <span class="files-with-issues">{{ filesWithIssues.size }} with issues</span>
        </div>
        
        <div v-if="filteredFiles" class="filter-toggle">
          <div class="filter-info">
            <span class="mdi mdi-filter"></span>
            <span>Showing {{ filteredFiles.size }} files with filtered issues</span>
          </div>
          <button 
            class="btn btn-sm btn-secondary toggle-all-btn"
            @click="$emit('toggle-show-all')"
            :title="showAllFiles ? 'Show only files with matching issues' : 'Show all files'"
          >
            <span class="mdi" :class="showAllFiles ? 'mdi-filter' : 'mdi-folder-multiple'"></span>
            {{ showAllFiles ? 'Show Filtered' : 'Show All' }}
          </button>
        </div>
        
        <FileTreeNode 
          v-for="node in fileTree"
          :key="node.path"
          :node="node"
          :selected-file="selectedFile"
          :files-with-issues="filesWithIssues"
          :expanded-folders="expandedFolders"
          @select-file="$emit('select-file', $event)"
          @toggle-folder="$emit('toggle-folder', $event)"
        />
      </div>
    </div>
  </div>
</template>

<script setup>
import FileTreeNode from '@/components/FileTreeNode.vue'

defineProps({
  collapsed: {
    type: Boolean,
    default: false
  },
  loading: {
    type: Boolean,
    default: false
  },
  fileTree: {
    type: Array,
    default: () => []
  },
  selectedFile: {
    type: String,
    default: null
  },
  filesWithIssues: {
    type: Set,
    default: () => new Set()
  },
  expandedFolders: {
    type: Set,
    default: () => new Set()
  },
  totalFiles: {
    type: Number,
    default: 0
  },
  filteredFiles: {
    type: Set,
    default: null
  },
  showAllFiles: {
    type: Boolean,
    default: false
  }
})

defineEmits([
  'toggle-sidebar',
  'select-file',
  'toggle-folder',
  'toggle-show-all'
])
</script>

<style lang="scss" scoped>
.file-tree-sidebar {
  width: 300px;
  background: #f8f9fa;
  border-right: 1px solid var(--border-color);
  display: flex;
  flex-direction: column;
  transition: width 0.3s ease;
  
  &.collapsed {
    width: 40px;
    
    .sidebar-header h3 {
      display: none;
    }
  }
  
  .sidebar-header {
    padding: 1rem;
    border-bottom: 1px solid var(--border-color);
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: white;
    
    h3 {
      margin: 0;
      font-size: 1rem;
      color: var(--text-color);
    }
    
    .collapse-btn {
      background: none;
      border: none;
      padding: 0.25rem;
      border-radius: 4px;
      cursor: pointer;
      color: var(--text-muted);
      transition: all var(--transition-speed);
      
      &:hover {
        background: rgba(74, 108, 247, 0.1);
        color: var(--primary-color);
      }
      
      .mdi {
        font-size: 20px;
      }
    }
  }
  
  .sidebar-content {
    flex: 1;
    overflow-y: auto;
    padding: 1rem;
    
    .loading-indicator {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 2rem 0;
      color: var(--text-muted);
      
      .mdi {
        font-size: 2rem;
        margin-bottom: 0.5rem;
      }
    }
    
    .empty-files {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 2rem 0;
      color: var(--text-muted);
      text-align: center;
      
      .mdi {
        font-size: 2rem;
        margin-bottom: 0.5rem;
      }
    }
    
    .file-stats {
      display: flex;
      justify-content: space-between;
      padding: 0.5rem 0;
      margin-bottom: 1rem;
      border-bottom: 1px solid #e0e0e0;
      
      .total-files, .files-with-issues {
        font-size: 0.75rem;
        color: var(--text-muted);
      }
      
      .files-with-issues {
        color: var(--warning-color);
        font-weight: 500;
      }
    }
    
    .filter-toggle {
      background: rgba(74, 108, 247, 0.05);
      border: 1px solid rgba(74, 108, 247, 0.2);
      border-radius: var(--border-radius);
      padding: 0.75rem;
      margin-bottom: 1rem;
      
      .filter-info {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        margin-bottom: 0.75rem;
        font-size: 0.875rem;
        color: var(--primary-color);
        
        .mdi {
          color: var(--primary-color);
        }
      }
      
      .toggle-all-btn {
        width: 100%;
        font-size: 0.75rem;
        padding: 0.5rem;
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 0.5rem;
        border: 1px solid var(--primary-color);
        background: white;
        color: var(--primary-color);
        transition: all var(--transition-speed);
        
        &:hover {
          background: var(--primary-color);
          color: white;
        }
      }
    }
  }
}
</style>
