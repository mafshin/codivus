import { defineStore } from 'pinia'
import api from '@/services/api'

export const useRepositoryStore = defineStore({
  id: 'repository',
  
  state: () => ({
    repositories: [],
    currentRepository: null,
    repositoryStructure: null,
    loadingRepositories: false,
    loadingRepository: false,
    loadingStructure: false,
    error: null
  }),
  
  getters: {
    getRepositoryById: (state) => (id) => {
      return state.repositories.find(repo => repo.id === id)
    }
  },
  
  actions: {
    async fetchRepositories() {
      this.loadingRepositories = true
      this.error = null
      
      try {
        const response = await api.getRepositories()
        this.repositories = response.data
      } catch (error) {
        this.error = error.message || 'Failed to fetch repositories'
        console.error('Error fetching repositories:', error)
      } finally {
        this.loadingRepositories = false
      }
    },
    
    async fetchRepository(id) {
      console.log('Store: fetchRepository called with ID:', id)
      this.loadingRepository = true
      this.error = null
      
      try {
        console.log('Store: Making API call for repository', id)
        const response = await api.getRepository(id)
        console.log('Store: API response received:', response)
        
        if (response && response.data) {
          this.currentRepository = response.data
          console.log('Store: Repository set in store:', this.currentRepository)
        } else {
          throw new Error('No repository data received from API')
        }
      } catch (error) {
        this.error = error.message || `Failed to fetch repository ${id}`
        console.error(`Store: Error fetching repository ${id}:`, error)
        console.error('Store: Error details:', {
          message: error.message,
          response: error.response?.data,
          status: error.response?.status
        })
        throw error
      } finally {
        this.loadingRepository = false
        console.log('Store: fetchRepository completed for ID:', id)
      }
    },
    
    async fetchRepositoryStructure(id) {
      this.loadingStructure = true
      this.error = null
      
      try {
        console.log('API: Fetching repository structure for ID:', id)
        const response = await api.getRepositoryStructure(id)
        console.log('API: Repository structure response:', response)
        
        if (response && response.data) {
          this.repositoryStructure = response.data
          console.log('Repository structure loaded successfully:', this.repositoryStructure)
        } else {
          throw new Error('No repository structure data received')
        }
      } catch (error) {
        this.error = error.message || `Failed to fetch repository structure for ${id}`
        console.error(`Error fetching repository structure for ${id}:`, error)
        console.error('Error details:', {
          message: error.message,
          response: error.response?.data,
          status: error.response?.status
        })
        throw error
      } finally {
        this.loadingStructure = false
      }
    },
    
    async createRepository(repository) {
      this.error = null
      
      try {
        console.log('Creating repository:', JSON.stringify(repository))
        const response = await api.createRepository(repository)
        const createdRepo = response.data
        this.repositories.push(createdRepo)
        console.log('Repository created successfully:', createdRepo)
        
        // Automatically fetch the repository structure after creation
        if (createdRepo.id) {
          try {
            await this.fetchRepositoryStructure(createdRepo.id)
            console.log('Repository structure fetched for new repository')
          } catch (structureErr) {
            console.warn('Could not fetch repository structure:', structureErr)
            // Don't fail the whole operation if this secondary request fails
          }
          
          // Set as current repository
          this.currentRepository = createdRepo
        }
        
        return createdRepo
      } catch (error) {
        this.error = error.message || 'Failed to create repository'
        console.error('Error creating repository:', error)
        console.error('Error response data:', error.response?.data)
        throw error
      }
    },
    
    async updateRepository(id, repository) {
      this.error = null
      
      try {
        const response = await api.updateRepository(id, repository)
        
        // Update in list
        const index = this.repositories.findIndex(r => r.id === id)
        if (index !== -1) {
          this.repositories[index] = response.data
        }
        
        // Update current if needed
        if (this.currentRepository && this.currentRepository.id === id) {
          this.currentRepository = response.data
        }
        
        return response.data
      } catch (error) {
        this.error = error.message || `Failed to update repository ${id}`
        console.error(`Error updating repository ${id}:`, error)
        throw error
      }
    },
    
    async deleteRepository(id) {
      this.error = null
      
      try {
        await api.deleteRepository(id)
        
        // Remove from list
        this.repositories = this.repositories.filter(r => r.id !== id)
        
        // Clear current if needed
        if (this.currentRepository && this.currentRepository.id === id) {
          this.currentRepository = null
        }
        
        return true
      } catch (error) {
        this.error = error.message || `Failed to delete repository ${id}`
        console.error(`Error deleting repository ${id}:`, error)
        throw error
      }
    },
    
    async validateRepository(location, type) {
      this.error = null
      
      try {
        console.log('Validating repository:', { location, type, typeType: typeof type })
        const response = await api.validateRepository(location, type)
        console.log('Validation response:', response.data)
        return response.data
      } catch (error) {
        this.error = error.message || 'Failed to validate repository'
        console.error('Error validating repository:', error)
        console.error('Error response data:', error.response?.data)
        throw error
      }
    },
    
    async getFileContent(id, filePath) {
      this.error = null
      
      try {
        const response = await api.getFileContent(id, filePath)
        return response.data
      } catch (error) {
        this.error = error.message || `Failed to get file content for ${filePath}`
        console.error(`Error getting file content for ${filePath}:`, error)
        throw error
      }
    }
  }
})
