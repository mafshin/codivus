<template>
  <div class="file-tree-node">
    <div 
      class="node-item"
      :class="{ 
        'is-file': node.type === 'file',
        'is-directory': node.type === 'directory',
        'has-issues': node.type === 'file' && filesWithIssues.has(node.path),
        'is-selected': node.type === 'file' && selectedFile === node.path,
        'is-expanded': node.type === 'directory' && expandedFolders.has(node.path)
      }"
      @click="handleClick"
    >
      <span class="node-icon">
        <span 
          v-if="node.type === 'directory'"
          class="mdi"
          :class="expandedFolders.has(node.path) ? 'mdi-folder-open' : 'mdi-folder'"
        ></span>
        <span 
          v-else
          class="mdi mdi-file-document-outline"
        ></span>
      </span>
      
      <span class="node-name">{{ node.name }}</span>
      
      <span v-if="node.type === 'file' && filesWithIssues.has(node.path)" class="issue-indicator">
        <span class="mdi mdi-alert-circle"></span>
      </span>
    </div>
    
    <div 
      v-if="node.type === 'directory' && node.children && expandedFolders.has(node.path)"
      class="node-children"
    >
      <FileTreeNode
        v-for="child in node.children"
        :key="child.path"
        :node="child"
        :selected-file="selectedFile"
        :files-with-issues="filesWithIssues"
        :expanded-folders="expandedFolders"
        @select-file="$emit('select-file', $event)"
        @toggle-folder="$emit('toggle-folder', $event)"
      />
    </div>
  </div>
</template>

<script setup>
const props = defineProps({
  node: {
    type: Object,
    required: true
  },
  selectedFile: {
    type: String,
    default: null
  },
  filesWithIssues: {
    type: Set,
    required: true
  },
  expandedFolders: {
    type: Set,
    required: true
  }
})

const emit = defineEmits(['select-file', 'toggle-folder'])

function handleClick() {
  if (props.node.type === 'file') {
    emit('select-file', props.node.path)
  } else if (props.node.type === 'directory') {
    emit('toggle-folder', props.node.path)
  }
}
</script>

<style lang="scss" scoped>
.file-tree-node {
  .node-item {
    display: flex;
    align-items: center;
    padding: 0.25rem 0.5rem;
    cursor: pointer;
    border-radius: 4px;
    transition: background var(--transition-speed);
    user-select: none;
    
    &:hover {
      background: rgba(74, 108, 247, 0.05);
    }
    
    &.is-selected {
      background: rgba(74, 108, 247, 0.1);
      
      .node-name {
        font-weight: 600;
        color: var(--primary-color);
      }
    }
    
    &.has-issues .node-name {
      font-weight: 500;
    }
    
    .node-icon {
      width: 20px;
      display: flex;
      align-items: center;
      justify-content: center;
      margin-right: 0.5rem;
      
      .mdi {
        font-size: 16px;
      }
      
      .mdi-folder,
      .mdi-folder-open {
        color: #ffa726;
      }
      
      .mdi-file-document-outline {
        color: #66bb6a;
      }
    }
    
    .node-name {
      flex: 1;
      font-size: 0.875rem;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    
    .issue-indicator {
      margin-left: 0.5rem;
      
      .mdi {
        font-size: 14px;
        color: var(--warning-color);
      }
    }
  }
  
  .node-children {
    margin-left: 1rem;
    border-left: 1px solid #e0e0e0;
    padding-left: 0.5rem;
  }
}
</style>
