<template>
  <div class="file-tree-item" v-if="item">
    <div 
      class="file-tree-item-header" 
      @click="toggleExpanded"
      :style="{ paddingLeft: `${level * 20 + 8}px` }"
    >
      <span 
        class="toggle-icon" 
        v-if="isDirectory && hasChildren"
      >
        <span v-if="expanded" class="mdi mdi-chevron-down"></span>
        <span v-else class="mdi mdi-chevron-right"></span>
      </span>
      <span 
        class="spacer" 
        v-else-if="isDirectory"
      ></span>
      <span 
        class="mdi item-icon" 
        :class="isDirectory ? 'mdi-folder' : getFileIcon(item.name || '')"
        :style="isDirectory ? { color: expanded ? '#ffc107' : '#6c757d' } : {}"
      ></span>
      <span class="item-name">{{ item.name || 'Unknown' }}</span>
      <span class="item-info" v-if="!isDirectory && item.sizeInBytes">
        {{ formatFileSize(item.sizeInBytes) }}
      </span>
    </div>
    
    <transition name="slide">
      <div v-if="isDirectory && expanded && hasChildren" class="file-tree-children">
        <FileTreeItem 
          v-for="child in sortedChildren" 
          :key="child.id || child.name || Math.random()"
          :item="child"
          :level="level + 1"
          :default-expanded="defaultExpanded"
        />
      </div>
    </transition>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'

// Define component name for recursive use
const FileTreeItem = { name: 'FileTreeItem' }

const props = defineProps({
  item: {
    type: Object,
    required: true,
    validator: (value) => {
      return value && (value.name || value.path)
    }
  },
  level: {
    type: Number,
    default: 0
  },
  defaultExpanded: {
    type: Boolean,
    default: false
  }
})

const expanded = ref(props.defaultExpanded)

watch(() => props.defaultExpanded, (newVal) => {
  expanded.value = newVal
})

const isDirectory = computed(() => {
  return props.item?.isDirectory === true
})

const hasChildren = computed(() => {
  return props.item?.children && Array.isArray(props.item.children) && props.item.children.length > 0
})

const sortedChildren = computed(() => {
  if (!hasChildren.value) return []
  
  const children = [...props.item.children]
  return children.sort((a, b) => {
    // Directories first, then files
    if (a.isDirectory && !b.isDirectory) return -1
    if (!a.isDirectory && b.isDirectory) return 1
    
    // Then sort alphabetically by name
    const aName = a.name || ''
    const bName = b.name || ''
    return aName.localeCompare(bName, undefined, { numeric: true, sensitivity: 'base' })
  })
})

const toggleExpanded = () => {
  if (isDirectory.value && hasChildren.value) {
    expanded.value = !expanded.value
  }
}

const getFileIcon = (fileName) => {
  if (!fileName) return 'mdi-file-document-outline'
  
  const extension = fileName.split('.').pop()?.toLowerCase() || ''
  
  const iconMap = {
    // JavaScript/TypeScript
    'js': 'mdi-language-javascript',
    'jsx': 'mdi-react',
    'ts': 'mdi-language-typescript',
    'tsx': 'mdi-react',
    
    // Web
    'html': 'mdi-language-html5',
    'htm': 'mdi-language-html5',
    'css': 'mdi-language-css3',
    'scss': 'mdi-sass',
    'sass': 'mdi-sass',
    'less': 'mdi-language-css3',
    
    // Frameworks
    'vue': 'mdi-vuejs',
    'react': 'mdi-react',
    'angular': 'mdi-angular',
    
    // Data
    'json': 'mdi-code-json',
    'xml': 'mdi-xml',
    'yaml': 'mdi-code-braces',
    'yml': 'mdi-code-braces',
    'toml': 'mdi-code-braces',
    'ini': 'mdi-cog',
    'env': 'mdi-cog',
    
    // Documentation
    'md': 'mdi-language-markdown',
    'markdown': 'mdi-language-markdown',
    'txt': 'mdi-text-box-outline',
    'readme': 'mdi-text-box-multiple',
    
    // Programming Languages
    'py': 'mdi-language-python',
    'cs': 'mdi-language-csharp',
    'java': 'mdi-language-java',
    'php': 'mdi-language-php',
    'go': 'mdi-language-go',
    'rb': 'mdi-language-ruby',
    'c': 'mdi-language-c',
    'cpp': 'mdi-language-cpp',
    'cc': 'mdi-language-cpp',
    'h': 'mdi-language-c',
    'hpp': 'mdi-language-cpp',
    'rs': 'mdi-language-rust',
    'swift': 'mdi-language-swift',
    'kt': 'mdi-language-kotlin',
    'scala': 'mdi-language-scala',
    'clj': 'mdi-clojure',
    'dart': 'mdi-dart',
    'elixir': 'mdi-language-elixir',
    'ex': 'mdi-language-elixir',
    'elm': 'mdi-elm',
    'erl': 'mdi-language-erlang',
    'fs': 'mdi-language-f-sharp',
    'fsx': 'mdi-language-f-sharp',
    'haskell': 'mdi-language-haskell',
    'hs': 'mdi-language-haskell',
    'lua': 'mdi-language-lua',
    'perl': 'mdi-language-perl',
    'r': 'mdi-language-r',
    'sql': 'mdi-database',
    
    // Shell/Scripts
    'sh': 'mdi-console',
    'bash': 'mdi-console',
    'zsh': 'mdi-console',
    'fish': 'mdi-console',
    'ps1': 'mdi-powershell',
    'bat': 'mdi-console',
    'cmd': 'mdi-console',
    
    // Images
    'png': 'mdi-file-image',
    'jpg': 'mdi-file-image',
    'jpeg': 'mdi-file-image',
    'gif': 'mdi-file-image',
    'svg': 'mdi-svg',
    'ico': 'mdi-image',
    'webp': 'mdi-file-image',
    'bmp': 'mdi-file-image',
    'tiff': 'mdi-file-image',
    
    // Documents
    'pdf': 'mdi-file-pdf-box',
    'doc': 'mdi-file-word-box',
    'docx': 'mdi-file-word-box',
    'xls': 'mdi-file-excel-box',
    'xlsx': 'mdi-file-excel-box',
    'ppt': 'mdi-file-powerpoint-box',
    'pptx': 'mdi-file-powerpoint-box',
    'rtf': 'mdi-file-document',
    
    // Archives
    'zip': 'mdi-zip-box',
    'rar': 'mdi-zip-box',
    'tar': 'mdi-archive',
    'gz': 'mdi-archive',
    '7z': 'mdi-zip-box',
    'bz2': 'mdi-archive',
    'xz': 'mdi-archive',
    
    // Media
    'mp3': 'mdi-music',
    'wav': 'mdi-music',
    'ogg': 'mdi-music',
    'flac': 'mdi-music',
    'aac': 'mdi-music',
    'mp4': 'mdi-video',
    'avi': 'mdi-video',
    'mov': 'mdi-video',
    'wmv': 'mdi-video',
    'flv': 'mdi-video',
    'webm': 'mdi-video',
    'mkv': 'mdi-video',
    
    // Git
    'gitignore': 'mdi-git',
    'gitmodules': 'mdi-git',
    'gitattributes': 'mdi-git',
    
    // Configs
    'dockerfile': 'mdi-docker',
    'docker-compose': 'mdi-docker',
    'makefile': 'mdi-wrench',
    'cmake': 'mdi-wrench',
    'gradle': 'mdi-gradle',
    'gulpfile': 'mdi-gulp',
    'webpack': 'mdi-webpack',
    'vite': 'mdi-lightning-bolt',
    'rollup': 'mdi-package-variant',
    'package': 'mdi-package',
    'composer': 'mdi-package',
    'requirements': 'mdi-package',
    'gemfile': 'mdi-language-ruby',
    'podfile': 'mdi-package',
    'license': 'mdi-certificate',
    'changelog': 'mdi-format-list-bulleted',
    'editorconfig': 'mdi-cog'
  }
  
  // Check for exact match first (for files like package.json)
  const lowerName = fileName.toLowerCase()
  for (const [pattern, icon] of Object.entries(iconMap)) {
    if (lowerName.includes(pattern)) {
      return icon
    }
  }
  
  // Then check extension
  return iconMap[extension] || 'mdi-file-document-outline'
}

const formatFileSize = (bytes) => {
  if (!bytes || bytes < 0) return '0 B'
  
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const base = 1024
  const unitIndex = Math.floor(Math.log(bytes) / Math.log(base))
  const size = (bytes / Math.pow(base, unitIndex)).toFixed(unitIndex === 0 ? 0 : 1)
  
  return `${size} ${units[unitIndex] || 'B'}`
}

onMounted(() => {
  expanded.value = props.defaultExpanded
})
</script>

<style scoped>
.file-tree-item {
  user-select: none;
}

.file-tree-item-header {
  display: flex;
  align-items: center;
  cursor: pointer;
  transition: background-color 0.2s ease;
  white-space: nowrap;
  min-height: 32px;
  
  &:hover {
    background-color: #f8f9fa;
  }
  
  .toggle-icon {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 20px;
    height: 20px;
    margin-right: 4px;
    font-size: 14px;
    transition: transform 0.2s ease;
    flex-shrink: 0;
  }
  
  .spacer {
    width: 24px;
    flex-shrink: 0;
  }
  
  .item-icon {
    font-size: 18px;
    margin-right: 8px;
    flex-shrink: 0;
    
    &.mdi-folder {
      color: #6c757d;
    }
    
    &.mdi-language-javascript { color: #f7df1e; }
    &.mdi-language-typescript { color: #3178c6; }
    &.mdi-react { color: #61dafb; }
    &.mdi-vuejs { color: #4fc08d; }
    &.mdi-language-html5 { color: #e34f26; }
    &.mdi-language-css3 { color: #1572b6; }
    &.mdi-sass { color: #cf649a; }
    &.mdi-language-python { color: #3776ab; }
    &.mdi-language-java { color: #ed8b00; }
    &.mdi-language-csharp { color: #239120; }
    &.mdi-language-php { color: #777bb4; }
    &.mdi-language-ruby { color: #cc342d; }
    &.mdi-language-go { color: #00add8; }
    &.mdi-git { color: #f05032; }
    &.mdi-docker { color: #2496ed; }
  }
  
  .item-name {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    font-size: 14px;
    line-height: 1.5;
  }
  
  .item-info {
    margin-left: 8px;
    font-size: 12px;
    color: #6c757d;
    flex-shrink: 0;
  }
}

.file-tree-children {
  overflow: hidden;
}

/* Slide transition for expanding/collapsing directories */
.slide-enter-active, .slide-leave-active {
  transition: all 0.3s ease;
  max-height: 1000px;
}

.slide-enter-from, .slide-leave-to {
  max-height: 0;
  opacity: 0;
}

/* Make the tree more compact */
.file-tree-item:not(:first-child) {
  border-top: 1px solid #f0f0f0;
}

.file-tree-item:last-child {
  border-bottom: 1px solid #f0f0f0;
}

/* Add some visual feedback */
.file-tree-item-header:active {
  background-color: #e9ecef;
}

/* Accessibility improvements */
.file-tree-item-header:focus-visible {
  outline: 2px solid var(--primary-color, #007bff);
  outline-offset: -2px;
  border-radius: 2px;
}

/* Handle very long file names */
@media (max-width: 768px) {
  .file-tree-item-header {
    .item-name {
      font-size: 13px;
    }
    
    .item-info {
      font-size: 11px;
    }
  }
}
</style>
