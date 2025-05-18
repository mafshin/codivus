import { defineStore } from 'pinia'
import api from '@/services/api'

export const useScanningStore = defineStore({
  id: 'scanning',
  
  state: () => ({
    scans: {},             // Map of scan ID to scan data
    currentScan: null,     // Current scan data
    scanIssues: {},        // Map of scan ID to array of issues
    configurations: {},    // Map of repository ID to array of configurations
    loadingScans: false,
    loadingIssues: false,
    loadingConfigurations: false,
    error: null,
    pollingIntervals: {},  // Track polling intervals for cleanup
    autoRefresh: true      // Enable/disable auto-refresh
  }),
  
  getters: {
    getScansByRepositoryId: (state) => (repositoryId) => {
      return Object.values(state.scans).filter(scan => scan.repositoryId === repositoryId)
    },
    
    getScanById: (state) => (scanId) => {
      return state.scans[scanId] || null
    },
    
    getIssuesByScanId: (state) => (scanId) => {
      return state.scanIssues[scanId] || []
    },
    
    getConfigurationsByRepositoryId: (state) => (repositoryId) => {
      return state.configurations[repositoryId] || []
    },

    getActiveScanCount: (state) => {
      return Object.values(state.scans).filter(scan => 
        scan.status === 'InProgress' || scan.status === 'Initializing'
      ).length
    }
  },
  
  actions: {
    // Start polling for scan updates
    startPolling(scanId, interval = 2000) {
      if (this.pollingIntervals[scanId]) {
        this.stopPolling(scanId)
      }

      this.pollingIntervals[scanId] = setInterval(async () => {
        try {
          const scan = await this.fetchScanProgress(scanId, false) // Skip polling to avoid recursion
          
          // Stop polling if scan is completed
          if (scan && (scan.status === 'Completed' || scan.status === 'Failed' || scan.status === 'Canceled')) {
            this.stopPolling(scanId)
            // Fetch final issues if scan completed successfully
            if (scan.status === 'Completed') {
              await this.fetchScanIssues(scanId)
            }
          }
        } catch (error) {
          console.warn(`Error polling scan ${scanId}:`, error)
          // Continue polling even on errors
        }
      }, interval)
    },

    // Stop polling for a specific scan
    stopPolling(scanId) {
      if (this.pollingIntervals[scanId]) {
        clearInterval(this.pollingIntervals[scanId])
        delete this.pollingIntervals[scanId]
      }
    },

    // Stop all polling
    stopAllPolling() {
      Object.keys(this.pollingIntervals).forEach(scanId => {
        this.stopPolling(scanId)
      })
    },

    // Auto-refresh active scans
    startAutoRefresh() {
      if (!this.autoRefresh) return

      // Refresh active scans every 5 seconds
      setInterval(() => {
        const activeScans = Object.values(this.scans).filter(scan => 
          scan.status === 'InProgress' || scan.status === 'Initializing'
        )
        
        activeScans.forEach(scan => {
          this.fetchScanProgress(scan.id, false)
        })
      }, 5000)
    },
    
    // Fetch scans for a repository
    async fetchScansByRepository(repositoryId) {
      this.loadingScans = true
      this.error = null
      
      try {
        const response = await api.getScansByRepository(repositoryId)
        const scansList = response.data || []
        
        // Validate and update scans map
        scansList.forEach(scan => {
          // Only add valid scan objects
          if (scan && typeof scan === 'object' && scan.id) {
            this.scans[scan.id] = scan
          } else {
            console.warn('Invalid scan object received:', scan)
          }
        })
        
        return scansList
      } catch (error) {
        this.error = error.message || `Failed to fetch scans for repository ${repositoryId}`
        console.error(`Error fetching scans for repository ${repositoryId}:`, error)
        throw error
      } finally {
        this.loadingScans = false
      }
    },
    
    // Fetch scan progress
    async fetchScanProgress(scanId, startPolling = true) {
      this.error = null
      
      try {
        const response = await api.getScanProgress(scanId)
        const scan = response.data
        
        // Validate scan data before storing
        if (scan && typeof scan === 'object' && scan.id) {
          this.scans[scanId] = scan
          this.currentScan = scan
          
          // Start polling for active scans (unless disabled)
          if (startPolling && this.autoRefresh && 
              (scan.status === 'InProgress' || scan.status === 'Initializing')) {
            this.startPolling(scanId)
          }
          
          return scan
        } else {
          console.warn('Invalid scan data received:', scan)
          throw new Error('Invalid scan data received from API')
        }
      } catch (error) {
        this.error = error.message || `Failed to fetch scan progress for ${scanId}`
        console.error(`Error fetching scan progress for ${scanId}:`, error)
        throw error
      }
    },
    
    // Start a new scan
    async startScan(repositoryId, configuration) {
      this.error = null
      
      try {
        console.log('Starting scan for repository:', repositoryId)
        console.log('With configuration:', JSON.stringify(configuration))
        
        const response = await api.startScan(repositoryId, configuration)
        const scan = response.data
        
        console.log('Scan started:', scan)
        
        this.scans[scan.id] = scan
        this.currentScan = scan
        
        // Start polling for real-time updates
        if (this.autoRefresh) {
          this.startPolling(scan.id)
        }
        
        return scan
      } catch (error) {
        this.error = error.message || `Failed to start scan for repository ${repositoryId}`
        console.error(`Error starting scan for repository ${repositoryId}:`, error)
        console.error('Error response:', error.response?.data)
        throw error
      }
    },
    
    // Pause a scan
    async pauseScan(scanId) {
      this.error = null
      
      try {
        const response = await api.pauseScan(scanId)
        
        // Stop polling when paused
        this.stopPolling(scanId)
        
        // Refresh scan data
        await this.fetchScanProgress(scanId, false)
        
        return response.data
      } catch (error) {
        this.error = error.message || `Failed to pause scan ${scanId}`
        console.error(`Error pausing scan ${scanId}:`, error)
        throw error
      }
    },
    
    // Resume a paused scan
    async resumeScan(scanId) {
      this.error = null
      
      try {
        const response = await api.resumeScan(scanId)
        
        // Restart polling when resumed
        if (this.autoRefresh) {
          this.startPolling(scanId)
        }
        
        // Refresh scan data
        await this.fetchScanProgress(scanId, false)
        
        return response.data
      } catch (error) {
        this.error = error.message || `Failed to resume scan ${scanId}`
        console.error(`Error resuming scan ${scanId}:`, error)
        throw error
      }
    },
    
    // Cancel a scan
    async cancelScan(scanId) {
      this.error = null
      
      try {
        const response = await api.cancelScan(scanId)
        
        // Stop polling when canceled
        this.stopPolling(scanId)
        
        // Refresh scan data
        await this.fetchScanProgress(scanId, false)
        
        return response.data
      } catch (error) {
        this.error = error.message || `Failed to cancel scan ${scanId}`
        console.error(`Error canceling scan ${scanId}:`, error)
        throw error
      }
    },
    
    // Fetch issues for a scan
    async fetchScanIssues(scanId) {
      this.loadingIssues = true
      this.error = null
      
      try {
        const response = await api.getScanIssues(scanId)
        this.scanIssues[scanId] = response.data
        return response.data
      } catch (error) {
        this.error = error.message || `Failed to fetch issues for scan ${scanId}`
        console.error(`Error fetching issues for scan ${scanId}:`, error)
        throw error
      } finally {
        this.loadingIssues = false
      }
    },
    
    // Fetch scan configurations for a repository
    async fetchScanConfigurations(repositoryId) {
      this.loadingConfigurations = true
      this.error = null
      
      try {
        const response = await api.getScanConfigurations(repositoryId)
        this.configurations[repositoryId] = response.data
        return response.data
      } catch (error) {
        this.error = error.message || `Failed to fetch scan configurations for repository ${repositoryId}`
        console.error(`Error fetching scan configurations for repository ${repositoryId}:`, error)
        throw error
      } finally {
        this.loadingConfigurations = false
      }
    },
    
    // Create a new scan configuration
    async createScanConfiguration(configuration) {
      this.error = null
      
      try {
        const response = await api.createScanConfiguration(configuration)
        const newConfig = response.data
        const repositoryId = newConfig.repositoryId
        
        if (!this.configurations[repositoryId]) {
          this.configurations[repositoryId] = []
        }
        
        this.configurations[repositoryId].push(newConfig)
        
        return newConfig
      } catch (error) {
        this.error = error.message || 'Failed to create scan configuration'
        console.error('Error creating scan configuration:', error)
        throw error
      }
    },
    
    // Update a scan configuration
    async updateScanConfiguration(configurationId, configuration) {
      this.error = null
      
      try {
        const response = await api.updateScanConfiguration(configurationId, configuration)
        const updatedConfig = response.data
        const repositoryId = updatedConfig.repositoryId
        
        if (this.configurations[repositoryId]) {
          const index = this.configurations[repositoryId].findIndex(c => c.id === configurationId)
          
          if (index !== -1) {
            this.configurations[repositoryId][index] = updatedConfig
          }
        }
        
        return updatedConfig
      } catch (error) {
        this.error = error.message || `Failed to update scan configuration ${configurationId}`
        console.error(`Error updating scan configuration ${configurationId}:`, error)
        throw error
      }
    },
    
    // Delete a scan configuration
    async deleteScanConfiguration(configurationId, repositoryId) {
      this.error = null
      
      try {
        await api.deleteScanConfiguration(configurationId)
        
        if (this.configurations[repositoryId]) {
          this.configurations[repositoryId] = this.configurations[repositoryId]
            .filter(c => c.id !== configurationId)
        }
        
        return true
      } catch (error) {
        this.error = error.message || `Failed to delete scan configuration ${configurationId}`
        console.error(`Error deleting scan configuration ${configurationId}:`, error)
        throw error
      }
    },

    // Toggle auto-refresh
    toggleAutoRefresh() {
      this.autoRefresh = !this.autoRefresh
      
      if (!this.autoRefresh) {
        this.stopAllPolling()
      } else {
        // Restart polling for active scans
        Object.values(this.scans).forEach(scan => {
          if (scan.status === 'InProgress' || scan.status === 'Initializing') {
            this.startPolling(scan.id)
          }
        })
      }
    },

    // Manual refresh for all data
    async refreshAll(repositoryId = null) {
      if (repositoryId) {
        await this.fetchScansByRepository(repositoryId)
        await this.fetchScanConfigurations(repositoryId)
      }

      // Refresh all scans
      const scanPromises = Object.keys(this.scans).map(scanId => 
        this.fetchScanProgress(scanId, false)
      )
      await Promise.allSettled(scanPromises)

      // Refresh all issues
      const issuePromises = Object.keys(this.scans).map(scanId => 
        this.fetchScanIssues(scanId)
      )
      await Promise.allSettled(issuePromises)
    }
  }
})
