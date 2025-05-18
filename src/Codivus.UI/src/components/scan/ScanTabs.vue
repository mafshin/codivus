<template>
  <div class="scan-details-tabs">
    <button 
      class="tab-button" 
      :class="{ active: activeTab === 'issues' }"
      @click="$emit('update:activeTab', 'issues')"
    >
      <span class="mdi mdi-alert-circle"></span> Issues
    </button>
    <button 
      class="tab-button" 
      :class="{ active: activeTab === 'config' }"
      @click="$emit('update:activeTab', 'config')"
    >
      <span class="mdi mdi-cog"></span> Configuration
    </button>
  </div>
</template>

<script setup>
defineProps({
  activeTab: {
    type: String,
    required: true
  }
})

defineEmits(['update:activeTab'])
</script>

<style lang="scss" scoped>
.scan-details-tabs {
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
</style>
