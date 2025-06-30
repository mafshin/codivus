<template>
  <div class="graph-view">
    <div class="graph-header">
      <div class="header-content">
        <h1>Code Graph Analysis</h1>
        <p class="header-description">
          Visualize and analyze code dependencies, relationships, and architecture
        </p>
      </div>
      
      <div class="header-actions">
        <button
          @click="showSettings = !showSettings"
          class="btn btn-outline"
          :class="{ active: showSettings }"
        >
          <span class="icon">⚙️</span>
          Settings
        </button>
        
        <button
          v-if="selectedRepository"
          @click="startGraphScan"
          class="btn btn-primary"
          :disabled="isScanning || !graphStore.isGraphEnabled"
        >
          <span v-if="isScanning" class="icon">🔄</span>
          <span v-else class="icon">🚀</span>
          {{ isScanning ? 'Scanning...' : 'Start Graph Scan' }}
        </button>
      </div>
    </div>

    <!-- Settings Panel -->
    <div v-if="showSettings" class="settings-panel">
      <GraphSettings @close="showSettings = false" />
    </div>

    <!-- Repository Selection -->
    <div v-if="!selectedRepository" class="repository-selection">
      <div class="selection-card">
        <h2>Select Repository</h2>
        <p>Choose a repository to analyze its code graph and dependencies.</p>
        
        <div v-if="repositories.length === 0" class="empty-state">
          <div class="empty-icon">📁</div>
          <p>No repositories found</p>
          <router-link to="/repositories/add" class="btn btn-primary">
            Add Repository
          </router-link>
        </div>
        
        <div v-else class="repository-list">
          <div
            v-for="repo in repositories"
            :key="repo.id"
            @click="selectRepository(repo)"
            class="repository-item"
          >
            <div class="repo-info">
              <h3>{{ repo.name }}</h3>
              <p>{{ repo.description || 'No description' }}</p>
              <div class="repo-meta">
                <span class="meta-item">
                  <span class="icon">📂</span>
                  {{ repo.type }}
                </span>
                <span class="meta-item">
                  <span class="icon">📍</span>
                  {{ repo.location }}
                </span>
              </div>
            </div>
            <div class="repo-action">
              <span class="icon">→</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Graph Content -->
    <div v-else class="graph-content">
      <!-- Repository Info Bar -->
      <div class="repo-info-bar">
        <div class="repo-details">
          <h2>{{ selectedRepository.name }}</h2>
          <span class="repo-path">{{ selectedRepository.location }}</span>
        </div>
        
        <div class="repo-actions">
          <button @click="refreshGraph" class="btn btn-outline btn-sm" :disabled="loadingData">
            <span class="icon">🔄</span>
            Refresh
          </button>
          <button @click="selectedRepository = null" class="btn btn-outline btn-sm">
            <span class="icon">⬅️</span>
            Back
          </button>
        </div>
      </div>

      <!-- Active Scans -->
      <div v-if="activeScans.length > 0" class="active-scans">
        <h3>Active Scans</h3>
        <div class="scan-list">
          <div
            v-for="scan in activeScans"
            :key="scan.id"
            class="scan-item"
          >
            <div class="scan-info">
              <span class="scan-status" :class="scan.status?.toLowerCase() || 'unknown'">
                {{ scan.status }}
              </span>
              <span class="scan-progress">{{ Math.round(scan.progress || 0) }}%</span>
            </div>
            <div class="scan-actions">
              <button
                v-if="scan.status === 'Running'"
                @click="pauseScan(scan.id)"
                class="btn btn-sm btn-outline"
              >
                Pause
              </button>
              <button
                v-if="scan.status === 'Paused'"
                @click="resumeScan(scan.id)"
                class="btn btn-sm btn-primary"
              >
                Resume
              </button>
              <button
                @click="cancelScan(scan.id)"
                class="btn btn-sm btn-danger"
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Graph Tabs -->
      <div class="graph-tabs">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          @click="activeTab = tab.id"
          class="tab-button"
          :class="{ active: activeTab === tab.id }"
        >
          <span class="tab-icon">{{ tab.icon }}</span>
          {{ tab.name }}
        </button>
      </div>

      <!-- Tab Content -->
      <div class="tab-content">
        <!-- Visualization Tab -->
        <div v-if="activeTab === 'visualization'" class="visualization-tab">
          <div v-if="loadingVisualization" class="loading-state">
            <div class="spinner"></div>
            <p>Loading visualization...</p>
          </div>
          
          <div v-else-if="visualizationData" class="visualization-container">
            <div class="visualization-controls">
              <select v-model="visualizationType" class="control-select">
                <option value="dependencies">Dependencies</option>
                <option value="hierarchy">Type Hierarchy</option>
                <option value="calls">Call Graph</option>
              </select>
              
              <div class="layout-controls">
                <label>
                  <input type="checkbox" v-model="showLabels" />
                  Show Labels
                </label>
                <label>
                  <input type="checkbox" v-model="showMetrics" />
                  Show Metrics
                </label>
              </div>
            </div>
            
            <div ref="visualizationContainer" class="visualization-graph">
              <!-- D3.js visualization will be rendered here -->
            </div>
          </div>
          
          <div v-else class="empty-visualization">
            <div class="empty-icon">📊</div>
            <p>No visualization data available</p>
            <button @click="loadVisualizationData" class="btn btn-primary">
              Generate Visualization
            </button>
          </div>
        </div>

        <!-- Metrics Tab -->
        <div v-if="activeTab === 'metrics'" class="metrics-tab">
          <div v-if="loadingMetrics" class="loading-state">
            <div class="spinner"></div>
            <p>Loading metrics...</p>
          </div>
          
          <div v-else-if="metrics" class="metrics-grid">
            <div class="metric-card">
              <h3>Total Nodes</h3>
              <div class="metric-value">{{ metrics.totalNodes || 0 }}</div>
            </div>
            
            <div class="metric-card">
              <h3>Total Edges</h3>
              <div class="metric-value">{{ metrics.totalEdges || 0 }}</div>
            </div>
            
            <div class="metric-card">
              <h3>Complexity Score</h3>
              <div class="metric-value">{{ metrics.complexityScore || 0 }}</div>
            </div>
            
            <div class="metric-card">
              <h3>Coupling Level</h3>
              <div class="metric-value">{{ metrics.couplingLevel || 'Low' }}</div>
            </div>
          </div>
          
          <div v-else class="empty-metrics">
            <div class="empty-icon">📈</div>
            <p>No metrics data available</p>
            <button @click="loadMetrics" class="btn btn-primary">
              Calculate Metrics
            </button>
          </div>
        </div>

        <!-- Nodes Tab -->
        <div v-if="activeTab === 'nodes'" class="nodes-tab">
          <div class="nodes-filters">
            <input
              v-model="nodeFilter"
              type="text"
              placeholder="Filter nodes..."
              class="filter-input"
            />
            
            <select v-model="nodeTypeFilter" class="filter-select">
              <option value="">All Types</option>
              <option value="Class">Classes</option>
              <option value="Method">Methods</option>
              <option value="Property">Properties</option>
              <option value="Interface">Interfaces</option>
            </select>
          </div>
          
          <div v-if="loadingNodes" class="loading-state">
            <div class="spinner"></div>
            <p>Loading nodes...</p>
          </div>
          
          <div v-else-if="filteredNodes.length > 0" class="nodes-list">
            <div
              v-for="node in filteredNodes"
              :key="node.id"
              @click="selectNode(node)"
              class="node-item"
              :class="{ selected: selectedNode?.id === node.id }"
            >
              <div class="node-info">
                <div class="node-header">
                  <span class="node-type">{{ node.type }}</span>
                  <span class="node-name">{{ node.name }}</span>
                </div>
                <div class="node-meta">
                  <span class="meta-item">{{ node.namespace }}</span>
                  <span class="meta-item">{{ node.filePath }}</span>
                </div>
              </div>
              
              <div class="node-stats">
                <span class="stat">
                  <span class="stat-label">Dependencies:</span>
                  <span class="stat-value">{{ node.dependencyCount || 0 }}</span>
                </span>
                <span class="stat">
                  <span class="stat-label">Dependents:</span>
                  <span class="stat-value">{{ node.dependentCount || 0 }}</span>
                </span>
              </div>
            </div>
          </div>
          
          <div v-else class="empty-nodes">
            <div class="empty-icon">🔍</div>
            <p>No nodes found</p>
          </div>
        </div>
      </div>
    </div>

    <!-- Error Message -->
    <div v-if="error" class="error-message">
      <div class="error-content">
        <span class="error-icon">⚠️</span>
        <span class="error-text">{{ error }}</span>
        <button @click="clearError" class="error-close">×</button>
      </div>
    </div>
  </div>
</template>

<script>
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { useRepositoryStore } from '@/store/repository'
import { useGraphStore } from '@/store/graph'
import GraphSettings from '@/components/graph/GraphSettings.vue'

export default {
  name: 'GraphView',
  
  components: {
    GraphSettings
  },
  
  setup() {
    const router = useRouter()
    const repositoryStore = useRepositoryStore()
    const graphStore = useGraphStore()
    
    // Reactive state
    const showSettings = ref(false)
    const selectedRepository = ref(null)
    const activeTab = ref('visualization')
    const loadingData = ref(false)
    const isScanning = ref(false)
    
    // Filters
    const nodeFilter = ref('')
    const nodeTypeFilter = ref('')
    
    // Visualization settings
    const visualizationType = ref('dependencies')
    const showLabels = ref(true)
    const showMetrics = ref(false)
    const visualizationContainer = ref(null)
    
    // Selected node
    const selectedNode = ref(null)
    
    // Tab configuration
    const tabs = [
      { id: 'visualization', name: 'Visualization', icon: '📊' },
      { id: 'metrics', name: 'Metrics', icon: '📈' },
      { id: 'nodes', name: 'Nodes', icon: '🔗' }
    ]
    
    // Computed properties
    const repositories = computed(() => repositoryStore.repositories)
    const activeScans = computed(() => Array.from(graphStore.activeScans.values()))
    const metrics = computed(() => graphStore.metrics)
    const visualizationData = computed(() => graphStore.visualizationData)
    const loadingVisualization = computed(() => graphStore.loadingVisualization)
    const loadingMetrics = computed(() => graphStore.loadingMetrics)
    const loadingNodes = computed(() => graphStore.loadingNodes)
    const error = computed(() => graphStore.error || graphStore.scanError)
    
    const filteredNodes = computed(() => {
      let nodes = graphStore.nodes
      
      if (nodeFilter.value) {
        const filter = nodeFilter.value.toLowerCase()
        nodes = nodes.filter(node => 
          node.name?.toLowerCase().includes(filter) ||
          node.namespace?.toLowerCase().includes(filter)
        )
      }
      
      if (nodeTypeFilter.value) {
        nodes = nodes.filter(node => node.type === nodeTypeFilter.value)
      }
      
      return nodes
    })
    
    // Methods
    const selectRepository = async (repository) => {
      selectedRepository.value = repository
      await loadGraphData()
    }
    
    const loadGraphData = async () => {
      if (!selectedRepository.value) return
      
      loadingData.value = true
      try {
        await Promise.all([
          graphStore.loadNodes(selectedRepository.value.id),
          graphStore.loadGraphMetrics(selectedRepository.value.id)
        ])
      } catch (error) {
        console.error('Error loading graph data:', error)
      } finally {
        loadingData.value = false
      }
    }
    
    const refreshGraph = async () => {
      await loadGraphData()
    }
    
    const startGraphScan = async () => {
      if (!selectedRepository.value) return
      
      isScanning.value = true
      try {
        await graphStore.startGraphScan(selectedRepository.value.id)
      } catch (error) {
        console.error('Error starting graph scan:', error)
      } finally {
        isScanning.value = false
      }
    }
    
    const pauseScan = async (scanId) => {
      try {
        await graphStore.pauseScan(scanId)
      } catch (error) {
        console.error('Error pausing scan:', error)
      }
    }
    
    const resumeScan = async (scanId) => {
      try {
        await graphStore.resumeScan(scanId)
      } catch (error) {
        console.error('Error resuming scan:', error)
      }
    }
    
    const cancelScan = async (scanId) => {
      try {
        await graphStore.cancelScan(scanId)
      } catch (error) {
        console.error('Error cancelling scan:', error)
      }
    }
    
    const loadVisualizationData = async () => {
      if (!selectedRepository.value) return
      
      try {
        await graphStore.loadVisualizationData(selectedRepository.value.id)
        await nextTick()
        renderVisualization()
      } catch (error) {
        console.error('Error loading visualization data:', error)
      }
    }
    
    const loadMetrics = async () => {
      if (!selectedRepository.value) return
      
      try {
        await graphStore.loadGraphMetrics(selectedRepository.value.id)
      } catch (error) {
        console.error('Error loading metrics:', error)
      }
    }
    
    const selectNode = (node) => {
      selectedNode.value = selectedNode.value?.id === node.id ? null : node
    }
    
    const clearError = () => {
      graphStore.clearError()
    }
    
    const renderVisualization = () => {
      // This would integrate with D3.js for actual visualization
      console.log('Rendering visualization with data:', visualizationData.value)
    }
    
    // Lifecycle
    onMounted(async () => {
      await repositoryStore.fetchRepositories()
      
      // Auto-select repository if passed via query params
      const repoId = router.currentRoute.value.query.repository
      if (repoId) {
        const repo = repositories.value.find(r => r.id === repoId)
        if (repo) {
          await selectRepository(repo)
        }
      }
    })
    
    // Watchers
    watch(visualizationType, () => {
      if (visualizationData.value) {
        renderVisualization()
      }
    })
    
    watch([showLabels, showMetrics], () => {
      if (visualizationData.value) {
        renderVisualization()
      }
    })
    
    return {
      // State
      showSettings,
      selectedRepository,
      activeTab,
      loadingData,
      isScanning,
      nodeFilter,
      nodeTypeFilter,
      visualizationType,
      showLabels,
      showMetrics,
      visualizationContainer,
      selectedNode,
      
      // Data
      tabs,
      repositories,
      activeScans,
      metrics,
      visualizationData,
      filteredNodes,
      
      // Loading states
      loadingVisualization,
      loadingMetrics,
      loadingNodes,
      error,
      
      // Stores
      graphStore,
      
      // Methods
      selectRepository,
      refreshGraph,
      startGraphScan,
      pauseScan,
      resumeScan,
      cancelScan,
      loadVisualizationData,
      loadMetrics,
      selectNode,
      clearError
    }
  }
}
</script>

<style scoped>
.graph-view {
  padding: 24px;
  min-height: 100vh;
  background: #f9fafb;
}

.graph-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 32px;
  padding: 24px;
  background: white;
  border-radius: 12px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.header-content h1 {
  color: #1f2937;
  font-size: 28px;
  font-weight: 700;
  margin: 0 0 8px 0;
}

.header-description {
  color: #6b7280;
  font-size: 16px;
  margin: 0;
}

.header-actions {
  display: flex;
  gap: 12px;
}

.btn {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 16px;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  border: 1px solid transparent;
  text-decoration: none;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-primary {
  background: #3b82f6;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #2563eb;
}

.btn-outline {
  background: transparent;
  color: #3b82f6;
  border-color: #3b82f6;
}

.btn-outline:hover:not(:disabled) {
  background: #3b82f6;
  color: white;
}

.btn-outline.active {
  background: #3b82f6;
  color: white;
}

.btn-danger {
  background: #ef4444;
  color: white;
}

.btn-danger:hover:not(:disabled) {
  background: #dc2626;
}

.btn-sm {
  padding: 6px 12px;
  font-size: 12px;
}

.icon {
  font-size: 16px;
}

.settings-panel {
  margin-bottom: 24px;
  background: white;
  border-radius: 12px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.repository-selection {
  display: flex;
  justify-content: center;
  padding: 48px 0;
}

.selection-card {
  background: white;
  padding: 48px;
  border-radius: 12px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  max-width: 600px;
  width: 100%;
  text-align: center;
}

.selection-card h2 {
  color: #1f2937;
  font-size: 24px;
  font-weight: 600;
  margin: 0 0 12px 0;
}

.selection-card p {
  color: #6b7280;
  font-size: 16px;
  margin: 0 0 32px 0;
}

.empty-state {
  padding: 48px 0;
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 16px;
}

.repository-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.repository-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.repository-item:hover {
  border-color: #3b82f6;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.1);
}

.repo-info h3 {
  color: #1f2937;
  font-size: 18px;
  font-weight: 600;
  margin: 0 0 4px 0;
}

.repo-info p {
  color: #6b7280;
  font-size: 14px;
  margin: 0 0 8px 0;
}

.repo-meta {
  display: flex;
  gap: 16px;
}

.meta-item {
  display: flex;
  align-items: center;
  gap: 4px;
  color: #9ca3af;
  font-size: 12px;
}

.repo-action {
  color: #3b82f6;
  font-size: 20px;
}

.graph-content {
  background: white;
  border-radius: 12px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  overflow: hidden;
}

.repo-info-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  border-bottom: 1px solid #e5e7eb;
  background: #f8fafc;
}

.repo-details h2 {
  color: #1f2937;
  font-size: 20px;
  font-weight: 600;
  margin: 0 0 4px 0;
}

.repo-path {
  color: #6b7280;
  font-size: 14px;
}

.repo-actions {
  display: flex;
  gap: 8px;
}

.active-scans {
  padding: 20px 24px;
  border-bottom: 1px solid #e5e7eb;
  background: #fef3c7;
}

.active-scans h3 {
  color: #92400e;
  font-size: 16px;
  font-weight: 600;
  margin: 0 0 12px 0;
}

.scan-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.scan-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  background: white;
  border-radius: 6px;
  border: 1px solid #fbbf24;
}

.scan-info {
  display: flex;
  gap: 12px;
  align-items: center;
}

.scan-status {
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
  text-transform: uppercase;
}

.scan-status.running {
  background: #dbeafe;
  color: #1e40af;
}

.scan-status.paused {
  background: #fef3c7;
  color: #92400e;
}

.scan-progress {
  font-weight: 600;
  color: #374151;
}

.scan-actions {
  display: flex;
  gap: 8px;
}

.graph-tabs {
  display: flex;
  border-bottom: 1px solid #e5e7eb;
}

.tab-button {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 16px 24px;
  border: none;
  background: none;
  color: #6b7280;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  border-bottom: 2px solid transparent;
  transition: all 0.2s;
}

.tab-button:hover {
  color: #374151;
  background: #f9fafb;
}

.tab-button.active {
  color: #3b82f6;
  border-bottom-color: #3b82f6;
}

.tab-icon {
  font-size: 16px;
}

.tab-content {
  padding: 24px;
  min-height: 400px;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 48px;
}

.spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #e5e7eb;
  border-top: 3px solid #3b82f6;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.visualization-container {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.visualization-controls {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  background: #f8fafc;
  border-radius: 8px;
}

.control-select {
  padding: 8px 12px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font-size: 14px;
}

.layout-controls {
  display: flex;
  gap: 16px;
}

.layout-controls label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  cursor: pointer;
}

.visualization-graph {
  min-height: 500px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: white;
}

.empty-visualization,
.empty-metrics,
.empty-nodes {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 48px;
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 16px;
}

.metrics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 20px;
}

.metric-card {
  padding: 24px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: #f8fafc;
  text-align: center;
}

.metric-card h3 {
  color: #374151;
  font-size: 14px;
  font-weight: 500;
  margin: 0 0 8px 0;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.metric-value {
  color: #1f2937;
  font-size: 32px;
  font-weight: 700;
}

.nodes-filters {
  display: flex;
  gap: 12px;
  margin-bottom: 20px;
}

.filter-input,
.filter-select {
  padding: 8px 12px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font-size: 14px;
}

.filter-input {
  flex: 1;
}

.nodes-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.node-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.node-item:hover {
  border-color: #3b82f6;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.1);
}

.node-item.selected {
  border-color: #3b82f6;
  background: #eff6ff;
}

.node-header {
  display: flex;
  gap: 12px;
  align-items: center;
  margin-bottom: 4px;
}

.node-type {
  padding: 2px 8px;
  background: #e0e7ff;
  color: #3730a3;
  font-size: 12px;
  font-weight: 500;
  border-radius: 4px;
}

.node-name {
  color: #1f2937;
  font-size: 16px;
  font-weight: 600;
}

.node-meta {
  display: flex;
  gap: 16px;
}

.node-meta .meta-item {
  color: #6b7280;
  font-size: 12px;
}

.node-stats {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.stat {
  display: flex;
  gap: 8px;
  font-size: 12px;
}

.stat-label {
  color: #6b7280;
}

.stat-value {
  color: #1f2937;
  font-weight: 600;
}

.error-message {
  position: fixed;
  top: 20px;
  right: 20px;
  z-index: 1000;
}

.error-content {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  background: #fee2e2;
  color: #991b1b;
  border: 1px solid #fca5a5;
  border-radius: 8px;
  font-size: 14px;
  max-width: 400px;
}

.error-icon {
  font-size: 16px;
}

.error-close {
  background: none;
  border: none;
  color: #991b1b;
  font-size: 18px;
  cursor: pointer;
  padding: 0;
  margin-left: auto;
}

@media (max-width: 768px) {
  .graph-header {
    flex-direction: column;
    gap: 16px;
  }
  
  .header-actions {
    width: 100%;
    justify-content: stretch;
  }
  
  .repo-info-bar {
    flex-direction: column;
    gap: 12px;
  }
  
  .metrics-grid {
    grid-template-columns: 1fr;
  }
  
  .nodes-filters {
    flex-direction: column;
  }
}
</style>