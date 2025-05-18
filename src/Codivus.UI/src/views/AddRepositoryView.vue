<template>
  <div class="add-repository-view">
    <div class="page-header">
      <h1 class="page-title">Add Repository</h1>
      <button class="btn btn-secondary" @click="goBack">
        <span class="mdi mdi-arrow-left"></span> Back
      </button>
    </div>
    
    <div class="form-container">
      <div class="repository-type-tabs">
        <button 
          class="tab-button" 
          :class="{ active: repositoryType === 'Local' }"
          @click="repositoryType = 'Local'"
        >
          <span class="mdi mdi-folder"></span>
          Local Repository
        </button>
        <button 
          class="tab-button" 
          :class="{ active: repositoryType === 'GitHub' }"
          @click="repositoryType = 'GitHub'"
        >
          <span class="mdi mdi-github"></span>
          GitHub Repository
        </button>
      </div>
      
      <div class="form-panel">
        <form @submit.prevent="submitForm">
          <div class="form-group">
            <label for="name">Repository Name *</label>
            <input 
              type="text" 
              id="name" 
              v-model="repository.name" 
              required
              placeholder="e.g., My Project"
              :disabled="validating || submitting"
            />
          </div>
          
          <div class="form-group">
            <label for="location">
              {{ repositoryType === 'Local' ? 'Repository Path *' : 'GitHub URL *' }}
            </label>
            <div class="location-input">
              <input 
                type="text" 
                id="location" 
                v-model="repository.location" 
                required
                :placeholder="locationPlaceholder"
                :disabled="validating || submitting"
              />
              <button 
                type="button" 
                class="validate-btn" 
                @click="validateRepository"
                :disabled="!repository.location || validating || submitting"
              >
                <span v-if="validating" class="mdi mdi-loading mdi-spin"></span>
                <span v-else class="mdi mdi-check"></span>
                Validate
              </button>
            </div>
            <div v-if="validationMessage" :class="['validation-message', validationStatus]">
              <span :class="'mdi ' + validationIcon"></span>
              {{ validationMessage }}
            </div>
          </div>
          
          <div v-if="repositoryType === 'GitHub'" class="form-group">
            <label for="owner">Repository Owner (Optional)</label>
            <input 
              type="text" 
              id="owner" 
              v-model="repository.owner" 
              placeholder="e.g., username or organization"
              :disabled="validating || submitting"
            />
          </div>
          
          <div class="form-group">
            <label for="description">Description (Optional)</label>
            <textarea 
              id="description" 
              v-model="repository.description" 
              rows="3" 
              placeholder="Brief description of the repository"
              :disabled="validating || submitting"
            ></textarea>
          </div>
          
          <div class="form-group">
            <label for="branch">Default Branch (Optional)</label>
            <input 
              type="text" 
              id="branch" 
              v-model="repository.defaultBranch" 
              placeholder="e.g., main, master"
              :disabled="validating || submitting"
            />
          </div>
          
          <div class="form-actions">
            <button 
              type="button" 
              class="btn btn-secondary" 
              @click="goBack"
              :disabled="submitting"
            >
              Cancel
            </button>
            <button 
              type="submit" 
              class="btn btn-primary" 
              :disabled="!isFormValid || submitting"
            >
              <span v-if="submitting" class="mdi mdi-loading mdi-spin"></span>
              Add Repository
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
  
  <!-- Notification component -->
  <Notification
    v-if="notification"
    :type="notification.type"
    :title="notification.title"
    :message="notification.message"
    :duration="notification.duration"
    @close="notification = null"
  />
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useRepositoryStore } from '@/store/repository'
import Notification from '@/components/common/Notification.vue'

const router = useRouter()
const repositoryStore = useRepositoryStore()

// Reactive state
const repositoryType = ref('Local')
const repository = ref({
  name: '',
  location: '',
  type: 0, // 0 for Local, 1 for GitHub
  description: '',
  defaultBranch: '',
  owner: ''
})
const validating = ref(false)
const validationResult = ref(null)
const validationMessage = ref('')
const notification = ref(null)
const submitting = ref(false)

// Computed properties
const locationPlaceholder = computed(() => {
  return repositoryType.value === 'Local' 
    ? 'e.g., /path/to/repository' 
    : 'e.g., https://github.com/username/repo'
})

const validationStatus = computed(() => {
  return validationResult.value === true ? 'success' : 'error'
})

const validationIcon = computed(() => {
  return validationResult.value === true ? 'mdi-check-circle' : 'mdi-alert-circle'
})

const isFormValid = computed(() => {
  return (
    repository.value.name.trim() !== '' &&
    repository.value.location.trim() !== '' &&
    validationResult.value === true
  )
})

// Watchers
watch(repositoryType, (newValue) => {
  // Convert string type to numeric enum value (0 for Local, 1 for GitHub)
  repository.value.type = newValue === 'Local' ? 0 : 1
  repository.value.location = ''
  validationResult.value = null
  validationMessage.value = ''
})

// Methods
const validateRepository = async () => {
  if (!repository.value.location) return
  
  validating.value = true
  validationMessage.value = ''
  validationResult.value = null
  
  try {
    const result = await repositoryStore.validateRepository(
      repository.value.location, 
      repositoryType.value === 'Local' ? 0 : 1 // Convert string to numeric enum value
    )
    
    validationResult.value = result
    
    if (result) {
      validationMessage.value = 'Repository is valid and accessible.'
    } else {
      validationMessage.value = `Invalid ${repositoryType.value.toLowerCase()} repository. Please check the path/URL and try again.`
    }
  } catch (error) {
    validationResult.value = false
    validationMessage.value = `Error validating repository: ${error.message}`
  } finally {
    validating.value = false
  }
}

const submitForm = async () => {
  if (!isFormValid.value) return
  
  submitting.value = true
  validationMessage.value = ''
  
  try {
    const newRepository = { ...repository.value }
    
    // Ensure type is numeric enum value
    newRepository.type = repositoryType.value === 'Local' ? 0 : 1
    
    console.log('Submitting repository:', JSON.stringify(newRepository))
    const createdRepo = await repositoryStore.createRepository(newRepository)
    
    // Show success message
    validationResult.value = true
    validationMessage.value = `Repository "${createdRepo.name}" added successfully!`
    
    // Show success notification
    notification.value = {
      type: 'success',
      title: 'Repository Added',
      message: `Repository "${createdRepo.name}" was added successfully!`,
      duration: 3000
    }
    
    // Short delay to show the success message before navigating
    setTimeout(() => {
      // Navigate to repository details instead of the list
      router.push({ 
        name: 'RepositoryDetails', 
        params: { id: createdRepo.id } 
      })
    }, 1500)
  } catch (error) {
    console.error('Error adding repository:', error)
    // Show error notification
    validationResult.value = false
    validationMessage.value = `Error adding repository: ${error.message || 'Unknown error'}`
    
    notification.value = {
      type: 'error',
      title: 'Error',
      message: `Failed to add repository: ${error.message || 'Unknown error'}`,
      duration: 5000
    }
  } finally {
    submitting.value = false
  }
}

const goBack = () => {
  router.back()
}
</script>

<style lang="scss" scoped>
.add-repository-view {
  .page-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 1.5rem;
    
    .page-title {
      margin: 0;
    }
  }
  
  .form-container {
    max-width: 800px;
    margin: 0 auto;
  }
  
  .repository-type-tabs {
    display: flex;
    gap: 1rem;
    margin-bottom: 1.5rem;
    
    .tab-button {
      flex: 1;
      padding: 1rem;
      background: white;
      border: 1px solid var(--border-color);
      border-radius: var(--border-radius);
      cursor: pointer;
      font-weight: 500;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all var(--transition-speed);
      
      .mdi {
        margin-right: 0.5rem;
        font-size: 1.25rem;
      }
      
      &:hover {
        border-color: var(--primary-color);
      }
      
      &.active {
        border-color: var(--primary-color);
        background: rgba(74, 108, 247, 0.1);
        color: var(--primary-color);
      }
    }
  }
  
  .form-panel {
    background: white;
    border-radius: var(--border-radius);
    box-shadow: var(--box-shadow);
    padding: 2rem;
    
    .form-group {
      margin-bottom: 1.5rem;
      
      label {
        display: block;
        margin-bottom: 0.5rem;
        font-weight: 500;
      }
      
      input, textarea {
        width: 100%;
        padding: 0.75rem;
        border: 1px solid var(--border-color);
        border-radius: var(--border-radius);
        font-family: inherit;
        font-size: inherit;
        
        &:focus {
          outline: none;
          border-color: var(--primary-color);
        }
        
        &:disabled {
          background-color: #f9f9f9;
          cursor: not-allowed;
        }
      }
      
      .location-input {
        display: flex;
        gap: 0.5rem;
        
        input {
          flex: 1;
        }
        
        .validate-btn {
          white-space: nowrap;
          padding: 0 1rem;
          background: var(--light-color);
          border: 1px solid var(--border-color);
          border-radius: var(--border-radius);
          font-weight: 500;
          cursor: pointer;
          display: flex;
          align-items: center;
          
          .mdi {
            margin-right: 0.5rem;
          }
          
          &:disabled {
            opacity: 0.6;
            cursor: not-allowed;
          }
          
          &:not(:disabled):hover {
            background: #e9ecef;
          }
        }
      }
      
      .validation-message {
        margin-top: 0.5rem;
        font-size: 0.875rem;
        display: flex;
        align-items: center;
        
        .mdi {
          margin-right: 0.25rem;
        }
        
        &.success {
          color: var(--success-color);
        }
        
        &.error {
          color: var(--danger-color);
        }
      }
    }
    
    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: 1rem;
      margin-top: 2rem;
      
      .btn {
        min-width: 120px;
        
        .mdi-loading {
          margin-right: 0.5rem;
          animation: spin 1s linear infinite;
        }
      }
    }
  }
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
