<template>
  <div v-if="show" class="error-message">
    <span class="mdi mdi-alert-circle"></span>
    <span class="error-text">{{ message }}</span>
    <button @click="$emit('close')" class="error-close">
      <span class="mdi mdi-close"></span>
    </button>
  </div>
</template>

<script setup>
defineProps({
  show: {
    type: Boolean,
    default: false
  },
  message: {
    type: String,
    default: ''
  }
})

defineEmits(['close'])
</script>

<style lang="scss" scoped>
.error-message {
  background: rgba(220, 53, 69, 0.1);
  border: 1px solid rgba(220, 53, 69, 0.3);
  border-radius: var(--border-radius);
  padding: 1rem;
  margin-bottom: 1rem;
  display: flex;
  align-items: center;
  gap: 0.75rem;
  
  .mdi-alert-circle {
    color: var(--danger-color);
    font-size: 1.25rem;
  }
  
  .error-text {
    flex: 1;
    color: var(--danger-color);
    font-weight: 500;
  }
  
  .error-close {
    background: none;
    border: none;
    padding: 0.25rem;
    cursor: pointer;
    color: var(--danger-color);
    border-radius: 4px;
    transition: background var(--transition-speed);
    
    &:hover {
      background: rgba(220, 53, 69, 0.1);
    }
  }
}
</style>
