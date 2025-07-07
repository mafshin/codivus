import { defineStore } from 'pinia'
import api from '@/services/api'

export const useGraphStore = defineStore({
  id: 'graph',
  
  state: () => ({
    // Graph settings
    settings: {
      enabled: true,
      neo4j: {
        uri: 'bolt://localhost:7687',
        username: 'neo4j',
        password: 'pass12345678',
        database: 'neo4j',
        maxConnectionPoolSize: 50,
        connectionTimeout: 30,
        enableEncryption: false,
        trustStrategy: 'TrustAllCertificates'
      },
      processing: {
        maxConcurrentFiles: 50,
        batchSize: 1000,
        timeoutMinutes: 30,
        retryAttempts: 3
      },
      analysis: {
        includeTests: false,
        maxFileSize: 1048576,
        supportedExtensions: ['.cs', '.vb'],
        excludedDirectories: ['bin', 'obj', 'packages', '.git', '.vs']
      }
    },
    
    // Scan management
    activeScans: new Map(),
    scanHistory: [],
    
    // Graph data
    nodes: [],
    metrics: null,
    visualizationData: null,
    
    // Loading states
    loadingSettings: false,
    loadingScans: false,
    loadingNodes: false,
    loadingMetrics: false,
    loadingVisualization: false,
    
    // Error states
    error: null,
    scanError: null,
    
    // Polling state
    pollingIntervals: new Map(),
    pollingEnabled: true
  }),
  
  getters: {
    isGraphEnabled: (state) => state.settings.enabled,
    
    activeScanCount: (state) => state.activeScans.size,
    
    getActiveScan: (state) => (scanId) => state.activeScans.get(scanId),
    
    getNodeById: (state) => (nodeId) => 
      state.nodes.find(node => node.id === nodeId),
    
    getNodesByType: (state) => (nodeType) =>
      state.nodes.filter(node => node.type === nodeType),
    
    hasGraphData: (state) => state.nodes.length > 0,
    
    connectionString: (state) => {
      const { uri } = state.settings.neo4j
      return uri
    }
  },
  
  actions: {
    // Settings management
    async loadSettings() {
      this.loadingSettings = true
      this.error = null
      
      try {
        // Note: This would require a backend endpoint to get current graph settings
        // For now, we'll use default settings
        console.log('Graph settings loaded from defaults')
      } catch (error) {
        this.error = error.message || 'Failed to load graph settings'
        console.error('Error loading graph settings:', error)
      } finally {
        this.loadingSettings = false
      }
    },
    
    async updateSettings(newSettings) {
      this.loadingSettings = true
      this.error = null
      
      try {
        // Note: This would require a backend endpoint to update graph settings
        this.settings = { ...this.settings, ...newSettings }
        console.log('Graph settings updated:', newSettings)
        return this.settings
      } catch (error) {
        this.error = error.message || 'Failed to update graph settings'
        console.error('Error updating graph settings:', error)
        throw error
      } finally {
        this.loadingSettings = false
      }
    },
    
    // Scan management
    async startGraphScan(repositoryId, configuration = {}) {
      this.loadingScans = true
      this.scanError = null
      
      try {
        const response = await api.startGraphScan(repositoryId, configuration)
        const scan = {
          ...response.data,
          repositoryId,
          status: 'Running',
          progress: 0,
          createdAt: new Date().toISOString(),
          totalTasks: 0,
          completedTasks: 0,
          failedTasks: 0
        }
        
        this.activeScans.set(scan.scanId || scan.id, scan)
        this.scanHistory.unshift(scan)
        
        // Start polling for this scan
        this.startPolling(scan.scanId || scan.id)
        
        console.log('Graph scan started:', scan.scanId || scan.id)
        return scan
      } catch (error) {
        this.scanError = error.message || 'Failed to start graph scan'
        console.error('Error starting graph scan:', error)
        throw error
      } finally {
        this.loadingScans = false
      }
    },
    
    async getScanStatus(scanId) {
      try {
        const response = await api.getGraphScanStatus(scanId)
        const scanStatus = response.data
        
        // Calculate progress percentage
        if (scanStatus.totalTasks > 0) {
          scanStatus.progress = Math.round((scanStatus.completedTasks / scanStatus.totalTasks) * 100)
        } else {
          scanStatus.progress = 0
        }
        
        if (this.activeScans.has(scanId)) {
          const existingScan = this.activeScans.get(scanId)
          const updatedScan = { ...existingScan, ...scanStatus }
          this.activeScans.set(scanId, updatedScan)
          
          // Check if scan is complete
          if (scanStatus.status === 'Completed' || scanStatus.status === 'Failed') {
            this.stopPolling(scanId)
            
            // If completed successfully, refresh graph data
            if (scanStatus.status === 'Completed' && existingScan.repositoryId) {
              this.refreshGraphData(existingScan.repositoryId)
            }
          }
        }
        
        return scanStatus
      } catch (error) {
        console.error('Error getting scan status:', error)
        throw error
      }
    },
    
    async pauseScan(scanId) {
      try {
        await api.pauseGraphScan(scanId)
        const scan = this.activeScans.get(scanId)
        if (scan) {
          scan.status = 'Paused'
          this.activeScans.set(scanId, scan)
        }
        console.log('Graph scan paused:', scanId)
      } catch (error) {
        console.error('Error pausing graph scan:', error)
        throw error
      }
    },
    
    async resumeScan(scanId) {
      try {
        await api.resumeGraphScan(scanId)
        const scan = this.activeScans.get(scanId)
        if (scan) {
          scan.status = 'Running'
          this.activeScans.set(scanId, scan)
        }
        console.log('Graph scan resumed:', scanId)
      } catch (error) {
        console.error('Error resuming graph scan:', error)
        throw error
      }
    },
    
    async cancelScan(scanId) {
      try {
        await api.cancelGraphScan(scanId)
        this.activeScans.delete(scanId)
        console.log('Graph scan cancelled:', scanId)
      } catch (error) {
        console.error('Error cancelling graph scan:', error)
        throw error
      }
    },
    
    // Node operations
    async loadNodes(repositoryId, filters = {}) {
      this.loadingNodes = true
      this.error = null
      
      try {
        const response = await api.getGraphNodes(repositoryId, filters)
        this.nodes = response.data
        return this.nodes
      } catch (error) {
        this.error = error.message || 'Failed to load graph nodes'
        console.error('Error loading graph nodes:', error)
        throw error
      } finally {
        this.loadingNodes = false
      }
    },
    
    async getNodeDetails(nodeId) {
      try {
        const response = await api.getGraphNode(nodeId)
        return response.data
      } catch (error) {
        console.error('Error getting node details:', error)
        throw error
      }
    },
    
    async getNodeDependencies(nodeId) {
      try {
        const response = await api.getNodeDependencies(nodeId)
        return response.data
      } catch (error) {
        console.error('Error getting node dependencies:', error)
        throw error
      }
    },
    
    async getNodeDependents(nodeId) {
      try {
        const response = await api.getNodeDependents(nodeId)
        return response.data
      } catch (error) {
        console.error('Error getting node dependents:', error)
        throw error
      }
    },
    
    // Metrics and visualization
    async loadGraphMetrics(repositoryId) {
      this.loadingMetrics = true
      this.error = null
      
      try {
        const response = await api.getGraphMetrics(repositoryId)
        this.metrics = response.data
        return this.metrics
      } catch (error) {
        this.error = error.message || 'Failed to load graph metrics'
        console.error('Error loading graph metrics:', error)
        throw error
      } finally {
        this.loadingMetrics = false
      }
    },
    
    async loadVisualizationData(repositoryId) {
      console.log('Store: loadVisualizationData called with repositoryId:', repositoryId)
      this.loadingVisualization = true
      this.error = null
      
      try {
        console.log('Store: Making API call to getGraphVisualization')
        const response = await api.getGraphVisualization(repositoryId)
        console.log('Store: API response received:', response)
        this.visualizationData = response.data
        console.log('Store: Visualization data set:', this.visualizationData)
        return this.visualizationData
      } catch (error) {
        this.error = error.message || 'Failed to load visualization data'
        console.error('Store: Error loading visualization data:', error)
        throw error
      } finally {
        this.loadingVisualization = false
      }
    },
    
    // Analysis operations
    async performImpactAnalysis(nodeId, analysisType) {
      try {
        const response = await api.performImpactAnalysis(nodeId, analysisType)
        return response.data
      } catch (error) {
        console.error('Error performing impact analysis:', error)
        throw error
      }
    },
    
    async getCouplingAnalysis(repositoryId) {
      try {
        const response = await api.getCouplingAnalysis(repositoryId)
        return response.data
      } catch (error) {
        console.error('Error getting coupling analysis:', error)
        throw error
      }
    },
    
    // Utility actions
    clearError() {
      this.error = null
      this.scanError = null
    },
    
    clearGraphData() {
      this.nodes = []
      this.metrics = null
      this.visualizationData = null
    },
    
    updateScanProgress(scanId, progress) {
      const scan = this.activeScans.get(scanId)
      if (scan) {
        scan.progress = progress
        this.activeScans.set(scanId, scan)
      }
    },
    
    // Polling methods
    startPolling(scanId, interval = 2000) {
      if (!this.pollingEnabled || this.pollingIntervals.has(scanId)) {
        return
      }
      
      const intervalId = setInterval(async () => {
        try {
          await this.getScanStatus(scanId)
        } catch (error) {
          console.error('Error during polling:', error)
          this.stopPolling(scanId)
        }
      }, interval)
      
      this.pollingIntervals.set(scanId, intervalId)
      console.log('Started polling for scan:', scanId)
    },
    
    stopPolling(scanId) {
      const intervalId = this.pollingIntervals.get(scanId)
      if (intervalId) {
        clearInterval(intervalId)
        this.pollingIntervals.delete(scanId)
        console.log('Stopped polling for scan:', scanId)
      }
    },
    
    stopAllPolling() {
      this.pollingIntervals.forEach((intervalId, scanId) => {
        clearInterval(intervalId)
        console.log('Stopped polling for scan:', scanId)
      })
      this.pollingIntervals.clear()
    },
    
    // Data refresh after scan completion
    async refreshGraphData(repositoryId) {
      try {
        console.log('Refreshing graph data for repository:', repositoryId)
        
        // Refresh all graph data
        await Promise.all([
          this.loadNodes(repositoryId),
          this.loadGraphMetrics(repositoryId),
          this.loadVisualizationData(repositoryId)
        ])
        
        console.log('Graph data refreshed successfully')
      } catch (error) {
        console.error('Error refreshing graph data:', error)
      }
    }
  }
})