import { defineStore } from 'pinia'
import api from '@/services/api'

export const useGraphStore = defineStore({
  id: 'graph',
  
  state: () => ({
    // Graph settings
    settings: {
      enabled: true,
      janusGraph: {
        host: 'localhost',
        port: 8182,
        username: '',
        password: '',
        connectionPoolSize: 10,
        connectionTimeout: 30000,
        enableSsl: false,
        graphName: 'codivus'
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
    scanError: null
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
      const { host, port, enableSsl } = state.settings.janusGraph
      const protocol = enableSsl ? 'wss' : 'ws'
      return `${protocol}://${host}:${port}/gremlin`
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
        const scan = response.data
        
        this.activeScans.set(scan.id, scan)
        this.scanHistory.unshift(scan)
        
        console.log('Graph scan started:', scan.id)
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
        
        if (this.activeScans.has(scanId)) {
          this.activeScans.set(scanId, { ...this.activeScans.get(scanId), ...scanStatus })
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
      this.loadingVisualization = true
      this.error = null
      
      try {
        const response = await api.getGraphVisualization(repositoryId)
        this.visualizationData = response.data
        return this.visualizationData
      } catch (error) {
        this.error = error.message || 'Failed to load visualization data'
        console.error('Error loading visualization data:', error)
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
    }
  }
})