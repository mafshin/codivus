<template>
  <div class="file-tree-container">
    <div class="tree-controls" v-if="structure && structure.children && structure.children.length > 0">
      <div class="controls-left">
        <button @click="expandAll" class="btn btn-sm btn-secondary" :disabled="isExpandAll">
          <span class="mdi mdi-unfold-more-horizontal"></span> Expand All
        </button>
        <button @click="collapseAll" class="btn btn-sm btn-secondary" :disabled="!isExpandAll">
          <span class="mdi mdi-unfold-less-horizontal"></span> Collapse All
        </button>
      </div>
      <div class="controls-right">
        <span class="file-count">
          {{ getTotalFilesCount() }} files
        </span>
      </div>
    </div>
    
    <div class="file-tree-content" :class="{ 'has-content': hasValidStructure }">
      <template v-if="loading">
        <div class="loading-state">
          <span class="mdi mdi-loading mdi-spin"></span>
          <p>Loading repository structure...</p>
        </div>
      </template>
      
      <template v-else-if="error">
        <div class="error-state">
          <span class="mdi mdi-alert-circle"></span>
          <p>{{ error }}</p>
          <button @click="$emit('retry')" class="btn btn-primary">
            <span class="mdi mdi-refresh"></span> Retry
          </button>
        </div>
      </template>
      
      <template v-else-if="hasValidStructure">
        <div class="tree-wrapper">
          <FileTreeItem 
            v-for="item in sortedRootItems" 
            :key="item.id || item.name || Math.random()"
            :item="item"
            :level="0"
            :default-expanded="isExpandAll"
            @file-selected="onFileSelected"
          />
        </div>
      </template>
      
      <template v-else>
        <div class="empty-tree">
          <span class="mdi mdi-folder-open"></span>
          <h3>No files found</h3>
          <p v-if="!structure">Repository structure not loaded</p>
          <p v-else-if="!structure.children">Repository is empty</p>
          <p v-else>All files may be hidden or filtered</p>
          <button @click="$emit('refresh')" class="btn btn-outline-primary">
            <span class="mdi mdi-refresh"></span> Refresh
          </button>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, nextTick } from 'vue'
import FileTreeItem from './FileTreeItem.vue'

const props = defineProps({
  structure: {
    type: Object,
    default: null,
    validator: (value) => {
      return value === null || (typeof value === 'object' && value !== null)
    }
  },
  expandAll: {
    type: Boolean,
    default: false
  },
  loading: {
    type: Boolean,
    default: false
  },
  error: {
    type: String,
    default: null
  }
})

const emit = defineEmits(['retry', 'refresh', 'file-selected'])

const isExpandAll = ref(props.expandAll)

// Watch for changes in the expandAll prop
watch(() => props.expandAll, (newVal) => {
  isExpandAll.value = newVal
})

// Check if we have a valid structure to display
const hasValidStructure = computed(() => {
  return props.structure && 
         props.structure.children && 
         Array.isArray(props.structure.children) && 
         props.structure.children.length > 0
})

// Sort and filter root items (directories first, then alphabetically)
const sortedRootItems = computed(() => {
  if (!hasValidStructure.value) return []
  
  const items = [...props.structure.children]
  return items.sort((a, b) => {
    // Directories first
    if (a.isDirectory && !b.isDirectory) return -1
    if (!a.isDirectory && b.isDirectory) return 1
    
    // Then alphabetically
    const aName = a.name || ''
    const bName = b.name || ''
    return aName.localeCompare(bName, undefined, { 
      numeric: true, 
      sensitivity: 'base' 
    })
  })
})

// Count total files recursively
const getTotalFilesCount = () => {
  if (!hasValidStructure.value) return 0
  
  const countFiles = (items) => {
    let count = 0
    for (const item of items) {
      if (item.isDirectory) {
        if (item.children && Array.isArray(item.children)) {
          count += countFiles(item.children)
        }
      } else {
        count++
      }
    }
    return count
  }
  
  return countFiles(props.structure.children)
}

// Control methods
const expandAll = async () => {
  isExpandAll.value = true
  await nextTick()
  // Allow parent component to react
  emit('expand-all')
}

const collapseAll = async () => {
  isExpandAll.value = false
  await nextTick()
  // Allow parent component to react
  emit('collapse-all')
}

// Handle file selection
const onFileSelected = (file) => {
  emit('file-selected', file)
}

// Expose methods for parent component
defineExpose({
  expandAll,
  collapseAll,
  isExpandAll: computed(() => isExpandAll.value)
})
</script>

<style scoped>
.file-tree-container {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--border-color, #dee2e6);
  border-radius: 8px;
  overflow: hidden;
  background-color: white;
  font-family: 'SF Mono', 'Monaco', 'Inconsolata', 'Fira Code', 'Droid Sans Mono', 'Source Code Pro', monospace;
}

.tree-controls {
  padding: 12px;
  border-bottom: 1px solid var(--border-color, #dee2e6);
  background-color: #f8f9fa;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.controls-left {
  display: flex;
  gap: 8px;
}

.controls-right {
  .file-count {
    font-size: 12px;
    color: #6c757d;
    font-weight: 500;
  }
}

.file-tree-content {
  min-height: 200px;
  max-height: 600px;
  overflow-y: auto;
  overflow-x: auto;
  
  &.has-content {
    min-height: auto;
  }
}

.tree-wrapper {
  padding: 8px 0;
}

.loading-state,
.error-state,
.empty-tree {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 20px;
  text-align: center;
  color: #6c757d;
  
  .mdi {
    font-size: 48px;
    margin-bottom: 16px;
    opacity: 0.7;
    
    &.mdi-loading {
      animation: spin 1s linear infinite;
    }
    
    &.mdi-alert-circle {
      color: #dc3545;
    }
  }
  
  h3 {
    margin: 0 0 8px 0;
    font-size: 18px;
    font-weight: 600;
    color: #343a40;
  }
  
  p {
    margin: 0 0 16px 0;
    font-size: 14px;
    line-height: 1.5;
    max-width: 300px;
  }
  
  .btn {
    min-width: 120px;
  }
}

.error-state {
  .mdi-alert-circle {
    color: #dc3545;
  }
}

/* Button styles */
.btn {
  display: inline-flex;
  align-items: center;
  padding: 6px 12px;
  font-size: 12px;
  font-weight: 500;
  border: 1px solid transparent;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s ease;
  text-decoration: none;
  
  .mdi {
    margin-right: 4px;
    font-size: 14px;
  }
  
  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }
}

.btn-sm {
  padding: 4px 8px;
  font-size: 11px;
  
  .mdi {
    font-size: 12px;
  }
}

.btn-secondary {
  background-color: #6c757d;
  border-color: #6c757d;
  color: white;
  
  &:hover:not(:disabled) {
    background-color: #5a6268;
    border-color: #545b62;
  }
  
  &:active:not(:disabled) {
    background-color: #545b62;
    border-color: #4e555b;
  }
}

.btn-primary {
  background-color: #007bff;
  border-color: #007bff;
  color: white;
  
  &:hover:not(:disabled) {
    background-color: #0069d9;
    border-color: #0062cc;
  }
}

.btn-outline-primary {
  background-color: transparent;
  border-color: #007bff;
  color: #007bff;
  
  &:hover:not(:disabled) {
    background-color: #007bff;
    color: white;
  }
}

/* Custom scrollbar */
.file-tree-content::-webkit-scrollbar {
  width: 8px;
  height: 8px;
}

.file-tree-content::-webkit-scrollbar-track {
  background: #f1f1f1;
}

.file-tree-content::-webkit-scrollbar-thumb {
  background: #c1c1c1;
  border-radius: 4px;
}

.file-tree-content::-webkit-scrollbar-thumb:hover {
  background: #a1a1a1;
}

/* Animation for loading spinner */
@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

/* Responsive adjustments */
@media (max-width: 768px) {
  .tree-controls {
    flex-direction: column;
    gap: 8px;
    
    .controls-left,
    .controls-right {
      width: 100%;
      justify-content: center;
    }
  }
  
  .file-tree-content {
    max-height: 400px;
  }
}
</style>
