<template>
  <div v-if="!isOnline" class="offline-indicator">
    <span class="mdi mdi-wifi-off"></span>
    <span>You appear to be offline. Some features may not work properly.</span>
  </div>
</template>

<script setup>
defineProps({
  isOnline: {
    type: Boolean,
    default: true
  }
})
</script>

<style lang="scss" scoped>
.offline-indicator {
  background: rgba(255, 193, 7, 0.1);
  border: 1px solid rgba(255, 193, 7, 0.3);
  border-radius: var(--border-radius);
  padding: 1rem;
  margin-bottom: 1rem;
  display: flex;
  align-items: center;
  gap: 0.75rem;
  color: var(--warning-color);
  
  .mdi-wifi-off {
    font-size: 1.25rem;
  }
}
</style>
