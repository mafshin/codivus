<template>
  <div class="loading-container">
    <span class="mdi mdi-loading mdi-spin"></span>
    <span v-if="!isOnline">You appear to be offline. Retrying...</span>
    <span v-else>{{ message }}</span>
  </div>
</template>

<script setup>
defineProps({
  message: {
    type: String,
    default: 'Loading...'
  },
  isOnline: {
    type: Boolean,
    default: true
  }
})
</script>

<style lang="scss" scoped>
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

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
