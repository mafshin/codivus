<template>
  <div v-if="scan?.status === 'InProgress'" class="scan-progress-container">
    <div class="scan-progress-header">
      <div class="progress-text">
        <div class="scanning-file">
          <span class="label">Scanning:</span>
          <span class="value">{{ scan.currentFile || 'Initializing...' }}</span>
        </div>
        <div class="progress-status">
          <span class="scanned-files">{{ scan.scannedFiles }} / {{ scan.totalFiles }} files</span>
          <span class="percentage">{{ calculateProgress(scan) }}% Complete</span>
        </div>
      </div>
      <div class="estimated-time">
        <span class="mdi mdi-clock-outline"></span>
        {{ formatRemainingTime(scan.estimatedRemainingSeconds) }}
      </div>
    </div>
    <div class="progress-bar">
      <div class="progress-value" :style="{ width: calculateProgress(scan) + '%' }"></div>
    </div>
  </div>
</template>

<script setup>
defineProps({
  scan: {
    type: Object,
    default: null
  }
})

function calculateProgress(scanData) {
  if (!scanData || scanData.totalFiles === 0) return 0
  return Math.floor((scanData.scannedFiles / scanData.totalFiles) * 100)
}

function formatRemainingTime(remainingSeconds) {
  if (!remainingSeconds) return 'Calculating...'
  
  if (remainingSeconds < 60) {
    return `About ${Math.ceil(remainingSeconds)} seconds remaining`
  } else if (remainingSeconds < 3600) {
    const minutes = Math.ceil(remainingSeconds / 60)
    return `About ${minutes} minute${minutes > 1 ? 's' : ''} remaining`
  } else {
    const hours = Math.floor(remainingSeconds / 3600)
    const minutes = Math.ceil((remainingSeconds % 3600) / 60)
    return `About ${hours} hour${hours > 1 ? 's' : ''} ${minutes > 0 ? `and ${minutes} minute${minutes > 1 ? 's' : ''}` : ''} remaining`
  }
}
</script>

<style lang="scss" scoped>
.scan-progress-container {
  background: white;
  border-radius: var(--border-radius);
  box-shadow: var(--box-shadow);
  padding: 1.5rem;
  margin-bottom: 1.5rem;
  
  .scan-progress-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    margin-bottom: 1rem;
    
    .progress-text {
      .scanning-file {
        margin-bottom: 0.5rem;
        
        .label {
          font-weight: 500;
          margin-right: 0.5rem;
        }
        
        .value {
          color: var(--text-muted);
          font-family: 'JetBrains Mono', monospace;
          font-size: 0.875rem;
        }
      }
      
      .progress-status {
        display: flex;
        gap: 1rem;
        font-size: 0.875rem;
        
        .scanned-files {
          font-weight: 500;
        }
      }
    }
    
    .estimated-time {
      font-size: 0.875rem;
      color: var(--text-muted);
      
      .mdi {
        margin-right: 0.25rem;
      }
    }
  }
  
  .progress-bar {
    height: 8px;
    background: #e9ecef;
    border-radius: 4px;
    overflow: hidden;
    
    .progress-value {
      height: 100%;
      background: var(--primary-color);
      border-radius: 4px;
      transition: width 0.5s ease;
    }
  }
}
</style>
