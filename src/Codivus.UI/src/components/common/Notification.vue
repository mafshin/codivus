<template>
  <Teleport to="body">
    <div 
      v-if="show"
      class="notification" 
      :class="[type]"
    >
      <div class="notification-icon">
        <span v-if="type === 'success'" class="mdi mdi-check-circle"></span>
        <span v-else-if="type === 'error'" class="mdi mdi-alert-circle"></span>
        <span v-else-if="type === 'warning'" class="mdi mdi-alert"></span>
        <span v-else class="mdi mdi-information"></span>
      </div>
      <div class="notification-content">
        <div v-if="title" class="notification-title">{{ title }}</div>
        <div class="notification-message">{{ message }}</div>
      </div>
      <button @click="close" class="close-btn">
        <span class="mdi mdi-close"></span>
      </button>
    </div>
  </Teleport>
</template>

<script setup>
import { ref, onMounted, watch } from 'vue'

const props = defineProps({
  title: {
    type: String,
    default: ''
  },
  message: {
    type: String,
    required: true
  },
  type: {
    type: String,
    default: 'info',
    validator: (value) => ['success', 'error', 'warning', 'info'].includes(value)
  },
  duration: {
    type: Number,
    default: 5000
  },
  autoClose: {
    type: Boolean,
    default: true
  }
})

const emit = defineEmits(['close'])

const show = ref(false)
let timeoutId = null

const close = () => {
  show.value = false
  emit('close')
  if (timeoutId) {
    clearTimeout(timeoutId)
    timeoutId = null
  }
}

const startTimer = () => {
  if (props.autoClose && props.duration > 0) {
    timeoutId = setTimeout(() => {
      close()
    }, props.duration)
  }
}

onMounted(() => {
  show.value = true
  startTimer()
})

watch(() => props.message, () => {
  // Reset timer when message changes
  if (timeoutId) {
    clearTimeout(timeoutId)
  }
  show.value = true
  startTimer()
})
</script>

<style scoped>
.notification {
  position: fixed;
  top: 20px;
  right: 20px;
  z-index: 9999;
  display: flex;
  align-items: center;
  min-width: 300px;
  max-width: 450px;
  padding: 16px;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  background-color: white;
  animation: slide-in 0.3s ease-out;
  overflow: hidden;
}

.notification-icon {
  flex-shrink: 0;
  margin-right: 12px;
  font-size: 24px;
}

.notification-content {
  flex-grow: 1;
}

.notification-title {
  font-weight: 600;
  margin-bottom: 4px;
}

.notification-message {
  font-size: 14px;
  line-height: 1.5;
}

.close-btn {
  background: none;
  border: none;
  cursor: pointer;
  font-size: 18px;
  margin-left: 12px;
  opacity: 0.6;
  transition: opacity 0.2s;
}

.close-btn:hover {
  opacity: 1;
}

/* Notification types */
.success {
  border-left: 4px solid var(--success-color);
}

.success .notification-icon {
  color: var(--success-color);
}

.error {
  border-left: 4px solid var(--danger-color);
}

.error .notification-icon {
  color: var(--danger-color);
}

.warning {
  border-left: 4px solid var(--warning-color);
}

.warning .notification-icon {
  color: var(--warning-color);
}

.info {
  border-left: 4px solid var(--primary-color);
}

.info .notification-icon {
  color: var(--primary-color);
}

@keyframes slide-in {
  from {
    transform: translateX(100%);
    opacity: 0;
  }
  to {
    transform: translateX(0);
    opacity: 1;
  }
}
</style>
