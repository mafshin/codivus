<template>
  <div class="page-header">
    <div class="page-title-container">
      <h1 class="page-title">
        Scan Details
        <span 
          class="scan-status-badge"
          :class="'status-' + (scan?.status ? String(scan.status).toLowerCase() : 'unknown')"
        >
          {{ scan?.status || 'Unknown' }}
        </span>
      </h1>
      <div class="scan-meta">
        <span class="repository-name">{{ repositoryName }}</span>
        <span class="scan-date">Started: {{ formatDateTime(scan.startedAt) }}</span>
        <span v-if="scan.completedAt" class="scan-date">Completed: {{ formatDateTime(scan.completedAt) }}</span>
      </div>
    </div>
    
    <div class="page-actions">
      <button 
        v-if="scan?.status === 'InProgress'"
        class="btn btn-warning" 
        @click="$emit('pause-scan')"
        :disabled="operationLoading.pausing"
      >
        <span class="mdi" :class="operationLoading.pausing ? 'mdi-loading mdi-spin' : 'mdi-pause'"></span> 
        {{ operationLoading.pausing ? 'Pausing...' : 'Pause Scan' }}
      </button>
      <button 
        v-if="scan?.status === 'Paused'"
        class="btn btn-primary" 
        @click="$emit('resume-scan')"
        :disabled="operationLoading.resuming"
      >
        <span class="mdi" :class="operationLoading.resuming ? 'mdi-loading mdi-spin' : 'mdi-play'"></span> 
        {{ operationLoading.resuming ? 'Resuming...' : 'Resume Scan' }}
      </button>
      <button 
        v-if="scan?.status === 'InProgress' || scan?.status === 'Paused'"
        class="btn btn-danger" 
        @click="$emit('cancel-scan')"
        :disabled="operationLoading.canceling"
      >
        <span class="mdi" :class="operationLoading.canceling ? 'mdi-loading mdi-spin' : 'mdi-stop'"></span> 
        {{ operationLoading.canceling ? 'Canceling...' : 'Cancel' }}
      </button>
      <button 
        v-if="scan?.status === 'Completed' || scan?.status === 'Failed' || scan?.status === 'Canceled'"
        class="btn btn-danger" 
        @click="$emit('delete-scan')"
        :disabled="operationLoading.deleting"
      >
        <span class="mdi" :class="operationLoading.deleting ? 'mdi-loading mdi-spin' : 'mdi-delete'"></span> 
        {{ operationLoading.deleting ? 'Deleting...' : 'Delete' }}
      </button>
      <button 
        class="btn btn-secondary"
        @click="$emit('refresh-data')"
        :disabled="operationLoading.refreshing"
      >
        <span class="mdi" :class="operationLoading.refreshing ? 'mdi-loading mdi-spin' : 'mdi-refresh'"></span> 
        {{ operationLoading.refreshing ? 'Refreshing...' : 'Refresh' }}
      </button>
      <button 
        class="btn btn-secondary"
        @click="$emit('go-back')"
      >
        <span class="mdi mdi-arrow-left"></span> Back
      </button>
    </div>
  </div>
</template>

<script setup>
defineProps({
  scan: {
    type: Object,
    default: null
  },
  repositoryName: {
    type: String,
    default: 'Unknown Repository'
  },
  operationLoading: {
    type: Object,
    default: () => ({
      pausing: false,
      resuming: false,
      canceling: false,
      deleting: false,
      refreshing: false
    })
  }
})

defineEmits([
  'pause-scan',
  'resume-scan', 
  'cancel-scan',
  'delete-scan',
  'refresh-data',
  'go-back'
])

function formatDateTime(dateTimeString) {
  if (!dateTimeString) return 'N/A'
  const date = new Date(dateTimeString)
  return date.toLocaleString()
}
</script>

<style lang="scss" scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1.5rem;
  
  .page-title-container {
    .page-title {
      margin: 0 0 0.5rem;
      display: flex;
      align-items: center;
      
      .scan-status-badge {
        font-size: 0.875rem;
        padding: 0.25rem 0.5rem;
        border-radius: var(--border-radius);
        margin-left: 0.75rem;
        
        &.status-inprogress {
          background: rgba(255, 193, 7, 0.1);
          color: var(--warning-color);
        }
        
        &.status-completed {
          background: rgba(40, 167, 69, 0.1);
          color: var(--success-color);
        }
        
        &.status-failed, &.status-canceled {
          background: rgba(220, 53, 69, 0.1);
          color: var(--danger-color);
        }
        
        &.status-paused {
          background: rgba(108, 117, 125, 0.1);
          color: var(--secondary-color);
        }
      }
    }
    
    .scan-meta {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      font-size: 0.875rem;
      color: var(--text-muted);
      
      .repository-name {
        font-weight: 500;
      }
    }
  }
  
  .page-actions {
    display: flex;
    gap: 0.75rem;
    
    .btn {
      &:disabled {
        opacity: 0.6;
        cursor: not-allowed;
        
        &:hover {
          transform: none;
        }
      }
    }
  }
}
</style>
