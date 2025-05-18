<template>
  <div class="scan-summary-stats">
    <div class="stats-card">
      <div class="stats-icon">
        <span class="mdi mdi-file-document-multiple"></span>
      </div>
      <div class="stats-content">
        <div class="stats-value">{{ scan.scannedFiles }} / {{ scan.totalFiles }}</div>
        <div class="stats-label">Files Scanned</div>
      </div>
    </div>
    
    <div class="stats-card">
      <div class="stats-icon">
        <span class="mdi mdi-alert-circle"></span>
      </div>
      <div class="stats-content">
        <div class="stats-value">{{ scan.issuesFound || 0 }}</div>
        <div class="stats-label">Issues Found</div>
      </div>
    </div>
    
    <div class="stats-card">
      <div class="stats-icon">
        <span class="mdi mdi-timer-outline"></span>
      </div>
      <div class="stats-content">
        <div class="stats-value">{{ formatDuration(scan) }}</div>
        <div class="stats-label">Duration</div>
      </div>
    </div>
  </div>
</template>

<script setup>
defineProps({
  scan: {
    type: Object,
    required: true
  }
})

function formatDuration(scanData) {
  if (!scanData || !scanData.startedAt) return 'N/A'
  
  const start = new Date(scanData.startedAt)
  const end = scanData.completedAt ? new Date(scanData.completedAt) : new Date()
  
  const durationMs = end - start
  const seconds = Math.floor(durationMs / 1000)
  
  if (seconds < 60) {
    return `${seconds} second${seconds !== 1 ? 's' : ''}`
  } else if (seconds < 3600) {
    const minutes = Math.floor(seconds / 60)
    return `${minutes} minute${minutes !== 1 ? 's' : ''}`
  } else {
    const hours = Math.floor(seconds / 3600)
    const minutes = Math.floor((seconds % 3600) / 60)
    return `${hours} hour${hours !== 1 ? 's' : ''}${minutes > 0 ? ` ${minutes} minute${minutes !== 1 ? 's' : ''}` : ''}`
  }
}
</script>

<style lang="scss" scoped>
.scan-summary-stats {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  gap: 1.5rem;
  margin-bottom: 1.5rem;
  
  .stats-card {
    background: white;
    border-radius: var(--border-radius);
    box-shadow: var(--box-shadow);
    padding: 1.5rem;
    display: flex;
    align-items: center;
    
    .stats-icon {
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
    
    .stats-content {
      flex: 1;
      
      .stats-value {
        font-size: 1.5rem;
        font-weight: 700;
        margin-bottom: 0.25rem;
      }
      
      .stats-label {
        color: var(--text-muted);
        font-size: 0.875rem;
      }
    }
  }
}
</style>
