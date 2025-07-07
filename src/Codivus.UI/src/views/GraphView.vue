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
          <div class="graph-status">
            <span v-if="hasGraphData" class="status-indicator success">
              <span class="icon">✅</span>
              Graph data available
            </span>
            <span v-else-if="hasActiveScans" class="status-indicator scanning">
              <span class="icon">🔄</span>
              Scanning in progress...
            </span>
            <span v-else class="status-indicator empty">
              <span class="icon">⚠️</span>
              No graph data - run a graph scan first
            </span>
          </div>
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
          <!-- Debug info -->
          <div v-if="false" style="background: #f0f0f0; padding: 10px; margin: 10px; font-size: 12px;">
            <strong>Debug Info:</strong><br>
            loadingVisualization: {{ loadingVisualization }}<br>
            visualizationData: {{ visualizationData }}<br>
            hasGraphData: {{ hasGraphData }}<br>
            nodes count: {{ graphStore.nodes.length }}
          </div>
          
          <div v-if="loadingVisualization" class="loading-state">
            <div class="spinner"></div>
            <p>Loading visualization...</p>
          </div>
          
          <div v-else-if="visualizationData && visualizationData.nodes && visualizationData.nodes.length > 0" class="visualization-container">
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
            <h3>No Visualization Data</h3>
            <p v-if="hasGraphData">
              Graph data exists, but visualization needs to be generated.
            </p>
            <p v-else>
              Run a graph scan first to analyze your code structure and generate visualization data.
            </p>
            <div class="visualization-actions">
              <button 
                @click="loadVisualizationData" 
                class="btn btn-primary"
              >
                {{ hasGraphData ? 'Generate Visualization' : 'Show Demo Visualization' }}
              </button>
              <button 
                v-if="!hasGraphData"
                @click="startGraphScan" 
                class="btn btn-outline"
                :disabled="isScanning"
              >
                {{ isScanning ? 'Scanning...' : 'Start Graph Scan' }}
              </button>
            </div>
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
              <div class="metric-value">{{ metrics.vertexCount || 0 }}</div>
            </div>
            
            <div class="metric-card">
              <h3>Total Edges</h3>
              <div class="metric-value">{{ metrics.edgeCount || 0 }}</div>
            </div>
            
            <div class="metric-card">
              <h3>Average Coupling</h3>
              <div class="metric-value">{{ (metrics.averageCoupling || 0).toFixed(2) }}</div>
            </div>
            
            <div class="metric-card">
              <h3>Total Types</h3>
              <div class="metric-value">{{ metrics.totalTypes || 0 }}</div>
            </div>
            
            <div class="metric-card">
              <h3>Total Methods</h3>
              <div class="metric-value">{{ metrics.totalMethods || 0 }}</div>
            </div>
            
            <div class="metric-card">
              <h3>Total Files</h3>
              <div class="metric-value">{{ metrics.totalFiles || 0 }}</div>
            </div>
          </div>
          
          <div v-else class="empty-metrics">
            <div class="empty-icon">📈</div>
            <h3>No Metrics Data</h3>
            <p v-if="hasGraphData">
              Graph data exists, but metrics need to be calculated.
            </p>
            <p v-else>
              Run a graph scan first to analyze your code and calculate complexity metrics.
            </p>
            <button 
              v-if="hasGraphData"
              @click="loadMetrics" 
              class="btn btn-primary"
            >
              Calculate Metrics
            </button>
            <button 
              v-else
              @click="startGraphScan" 
              class="btn btn-primary"
              :disabled="isScanning"
            >
              {{ isScanning ? 'Scanning...' : 'Start Graph Scan' }}
            </button>
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
import { ref, computed, onMounted, watch, nextTick, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useRepositoryStore } from '@/store/repository'
import { useGraphStore } from '@/store/graph'
import GraphSettings from '@/components/graph/GraphSettings.vue'
import * as d3 from 'd3'

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
    
    // Visualization settings
    const visualizationType = ref('dependencies')
    const showLabels = ref(true)
    const showMetrics = ref(false)
    const visualizationContainer = ref(null)
    
    // Tab configuration
    const tabs = [
      { id: 'visualization', name: 'Visualization', icon: '📊' },
      { id: 'metrics', name: 'Metrics', icon: '📈' }
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
    
    // Status indicators
    const hasGraphData = computed(() => graphStore.nodes.length > 0)
    const hasActiveScans = computed(() => activeScans.value.length > 0)
    const isScanning = computed(() => 
      activeScans.value.some(scan => 
        scan.status === 'Running' || scan.status === 'Initializing' || scan.status === 'Clearing'
      )
    )
    
    
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
      
      try {
        await graphStore.startGraphScan(selectedRepository.value.id)
      } catch (error) {
        console.error('Error starting graph scan:', error)
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
      console.log('loadVisualizationData called')
      if (!selectedRepository.value) {
        console.log('No selected repository')
        return
      }
      
      console.log('Selected repository:', selectedRepository.value.id)
      console.log('Current graph nodes count:', graphStore.nodes.length)
      
      try {
        // Try to load from API first
        console.log('Attempting to load visualization data from API...')
        await graphStore.loadVisualizationData(selectedRepository.value.id)
        
        console.log('API call completed, visualizationData:', graphStore.visualizationData)
        
        // Check if we got proper visualization data from API
        if (graphStore.visualizationData && 
            graphStore.visualizationData.nodes && 
            graphStore.visualizationData.nodes.length > 0) {
          console.log(`Using API visualization data with ${graphStore.visualizationData.nodes.length} nodes`)
        } else {
          console.log('No API data, falling back to mock data for demonstration')
          const mockData = generateMockVisualizationData()
          graphStore.visualizationData = mockData
        }
        
        await nextTick()
        renderVisualization()
      } catch (error) {
        console.error('Error loading visualization data:', error)
        
        // Fallback to mock data if API fails
        console.log('Error occurred, using mock data')
        const mockData = generateMockVisualizationData()
        graphStore.visualizationData = mockData
        await nextTick()
        renderVisualization()
      }
    }
    
    const createVisualizationFromNodes_OLD = (nodes) => {
      // Group nodes by namespace to create hierarchical relationships
      const visualizationNodes = nodes.map(node => ({
        id: node.id,
        name: node.name,
        type: node.type,
        namespace: node.namespace,
        ...node
      }))
      
      const relationships = []
      const nodeMap = new Map(visualizationNodes.map(n => [n.id, n]))
      
      // Create namespace containment relationships
      const namespaceGroups = new Map()
      const namespaceNodes = new Set()
      
      visualizationNodes.forEach(node => {
        if (node.namespace) {
          // Create hierarchical namespace structure
          const namespaceParts = node.namespace.split('.')
          let currentNamespace = ''
          
          for (let i = 0; i < namespaceParts.length; i++) {
            const parentNamespace = currentNamespace
            currentNamespace = i === 0 ? namespaceParts[0] : currentNamespace + '.' + namespaceParts[i]
            const namespaceId = `ns_${currentNamespace.replace(/[^a-zA-Z0-9]/g, '_')}`
            
            // Add namespace node if not already exists
            if (!namespaceNodes.has(namespaceId)) {
              namespaceNodes.add(namespaceId)
              visualizationNodes.push({
                id: namespaceId,
                name: currentNamespace,
                type: 'Namespace'
              })
              
              // Link to parent namespace
              if (parentNamespace) {
                const parentId = `ns_${parentNamespace.replace(/[^a-zA-Z0-9]/g, '_')}`
                relationships.push({
                  source: parentId,
                  target: namespaceId,
                  type: 'Contains'
                })
              }
            }
          }
          
          // Link node to its immediate namespace
          const namespaceId = `ns_${node.namespace.replace(/[^a-zA-Z0-9]/g, '_')}`
          relationships.push({
            source: namespaceId,
            target: node.id,
            type: 'Contains'
          })
        }
      })
      
      // Create type-based relationships
      const nodesByType = new Map()
      visualizationNodes.forEach(node => {
        if (!nodesByType.has(node.type)) {
          nodesByType.set(node.type, [])
        }
        nodesByType.get(node.type).push(node)
      })
      
      const classes = nodesByType.get('Class') || []
      const interfaces = nodesByType.get('Interface') || []
      const methods = nodesByType.get('Method') || []
      const properties = nodesByType.get('Property') || []
      
      // Create some sample relationships between classes
      // Connect classes in the same namespace
      const classesByNamespace = new Map()
      classes.forEach(cls => {
        if (!classesByNamespace.has(cls.namespace)) {
          classesByNamespace.set(cls.namespace, [])
        }
        classesByNamespace.get(cls.namespace).push(cls)
      })
      
      // Add some uses relationships between classes in same namespace
      classesByNamespace.forEach(classesInNs => {
        if (classesInNs.length > 1) {
          for (let i = 0; i < Math.min(classesInNs.length - 1, 3); i++) {
            relationships.push({
              source: classesInNs[i].id,
              target: classesInNs[i + 1].id,
              type: 'Uses'
            })
          }
        }
      })
      
      // Connect some interfaces to classes (mock implementation)
      interfaces.forEach((iface, index) => {
        const potentialImplementers = classes.filter(c => 
          c.namespace === iface.namespace || 
          c.namespace?.startsWith(iface.namespace || '')
        )
        
        if (potentialImplementers.length > 0) {
          relationships.push({
            source: potentialImplementers[0].id,
            target: iface.id,
            type: 'Implements'
          })
        }
      })
      
      console.log(`Created visualization with ${visualizationNodes.length} nodes and ${relationships.length} relationships`)
      
      return {
        nodes: visualizationNodes,
        relationships: relationships
      }
    }
    
    const generateMockVisualizationData = () => {
      return {
        nodes: [
          { id: 'ns_MyApp', name: 'MyApp', type: 'namespace', fullName: 'MyApp' },
          { id: 'ns_MyApp_Controllers', name: 'Controllers', type: 'namespace', fullName: 'MyApp.Controllers' },
          { id: 'ns_MyApp_Services', name: 'Services', type: 'namespace', fullName: 'MyApp.Services' },
          { id: 'c1', name: 'HomeController', type: 'class', namespace: 'MyApp.Controllers' },
          { id: 'c2', name: 'UserService', type: 'class', namespace: 'MyApp.Services' },
          { id: 'c3', name: 'IUserService', type: 'interface', namespace: 'MyApp.Services' },
          { id: 'c4', name: 'ApiController', type: 'class', namespace: 'MyApp.Controllers' },
          { id: 'c5', name: 'DataService', type: 'class', namespace: 'MyApp.Services' }
        ],
        relationships: [
          { source: 'ns_MyApp', target: 'ns_MyApp_Controllers', type: 'contains' },
          { source: 'ns_MyApp', target: 'ns_MyApp_Services', type: 'contains' },
          { source: 'ns_MyApp_Controllers', target: 'c1', type: 'contains' },
          { source: 'ns_MyApp_Controllers', target: 'c4', type: 'contains' },
          { source: 'ns_MyApp_Services', target: 'c2', type: 'contains' },
          { source: 'ns_MyApp_Services', target: 'c3', type: 'contains' },
          { source: 'ns_MyApp_Services', target: 'c5', type: 'contains' },
          { source: 'c2', target: 'c3', type: 'implements' },
          { source: 'c1', target: 'c2', type: 'uses' },
          { source: 'c4', target: 'c5', type: 'uses' },
          { source: 'c2', target: 'c5', type: 'references' }
        ]
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
    
    
    const clearError = () => {
      graphStore.clearError()
    }
    
    const renderVisualization = () => {
      if (!visualizationData.value || !visualizationContainer.value) {
        console.log('No visualization data or container available')
        return
      }
      
      console.log('Rendering visualization with data:', visualizationData.value)
      
      // Clear previous visualization
      d3.select(visualizationContainer.value).selectAll('*').remove()
      
      const { nodes, relationships } = visualizationData.value
      
      if (!nodes || nodes.length === 0) {
        // Show message for empty data
        const svg = d3.select(visualizationContainer.value)
          .append('svg')
          .attr('width', '100%')
          .attr('height', '100%')
          .style('background', '#f8f9fa')
        
        svg.append('text')
          .attr('x', '50%')
          .attr('y', '50%')
          .attr('text-anchor', 'middle')
          .attr('dominant-baseline', 'middle')
          .style('font-size', '16px')
          .style('fill', '#6b7280')
          .text('No graph data available')
        
        return
      }
      
      // Set up dimensions
      const container = visualizationContainer.value
      const width = container.clientWidth || 800
      const height = container.clientHeight || 500
      
      // Create SVG
      const svg = d3.select(container)
        .append('svg')
        .attr('width', width)
        .attr('height', height)
        .style('background', '#fff')
        .style('border', '1px solid #e5e7eb')
        .style('border-radius', '8px')
      
      // Create zoom behavior
      const zoom = d3.zoom()
        .scaleExtent([0.1, 4])
        .on('zoom', function(event) {
          g.attr('transform', event.transform)
        })
      
      svg.call(zoom)
      
      // Create main group for all elements
      const g = svg.append('g')
      
      // Prepare data
      const nodeData = nodes.map(node => ({
        id: node.id,
        name: node.name || node.id,
        type: node.type || 'Unknown',
        group: getNodeGroup(node.type),
        ...node
      }))
      
      const linkData = relationships ? relationships.map(rel => ({
        source: rel.source || rel.sourceNodeId,
        target: rel.target || rel.targetNodeId,
        type: rel.type,
        ...rel
      })) : []
      
      // Create force simulation with improved layout for namespace containers
      const simulation = d3.forceSimulation(nodeData)
        .force('link', d3.forceLink(linkData).id(d => d.id).distance(d => {
          // Longer links for cross-namespace connections
          if (d.type === 'uses' || d.type === 'references') return 200
          if (d.type === 'contains') return 80
          return 120
        }).strength(0.6))
        .force('charge', d3.forceManyBody().strength(d => {
          // Stronger repulsion for namespace nodes
          return d.type.toLowerCase() === 'namespace' ? -1200 : -600
        }))
        .force('center', d3.forceCenter(width / 2, height / 2))
        .force('collision', d3.forceCollide().radius(d => getNodeRadius(d.type) + 15))
        .force('x', d3.forceX(width / 2).strength(0.03))
        .force('y', d3.forceY(height / 2).strength(0.03))
      
      // Create arrow marker for directed edges
      svg.append('defs').selectAll('marker')
        .data(['arrow'])
        .enter().append('marker')
        .attr('id', d => d)
        .attr('viewBox', '0 -5 10 10')
        .attr('refX', 20)
        .attr('refY', 0)
        .attr('markerWidth', 6)
        .attr('markerHeight', 6)
        .attr('orient', 'auto')
        .append('path')
        .attr('d', 'M0,-5L10,0L0,5')
        .attr('fill', '#999')
      
      // Create links
      const link = g.append('g')
        .attr('class', 'links')
        .selectAll('line')
        .data(linkData)
        .enter().append('line')
        .attr('stroke', d => getRelationshipColor(d.type))
        .attr('stroke-opacity', 0.8)
        .attr('stroke-width', d => getRelationshipWidth(d.type))
        .attr('marker-end', 'url(#arrow)')
      
      // Create nodes
      const node = g.append('g')
        .attr('class', 'nodes')
        .selectAll('g')
        .data(nodeData)
        .enter().append('g')
        .attr('class', 'node')
        .call(d3.drag()
          .on('start', dragstarted)
          .on('drag', dragged)
          .on('end', dragended))
      
      // Add circles to nodes
      node.append('circle')
        .attr('r', d => getNodeRadius(d.type))
        .attr('fill', d => getNodeColor(d.type))
        .attr('stroke', d => getNodeStrokeColor(d.type))
        .attr('stroke-width', d => d.type.toLowerCase() === 'namespace' ? 3 : 2)
      
      // Add labels if enabled
      if (showLabels.value) {
        node.append('text')
          .attr('dy', '.35em')
          .attr('text-anchor', 'middle')
          .style('font-size', '16px')
          .style('font-weight', '500')
          .style('fill', '#2c3e50')
          .style('pointer-events', 'none')
          .text(d => d.name.length > 20 ? d.name.substring(0, 20) + '...' : d.name)
      }
      
      // Add tooltips
      node.append('title')
        .text(d => `${d.name}\nType: ${d.type}${d.namespace ? '\nNamespace: ' + d.namespace : ''}`)
      
      // Update positions on simulation tick
      simulation.on('tick', () => {
        link
          .attr('x1', d => d.source.x)
          .attr('y1', d => d.source.y)
          .attr('x2', d => d.target.x)
          .attr('y2', d => d.target.y)
        
        node
          .attr('transform', d => `translate(${d.x},${d.y})`)
      })
      
      // Drag functions
      function dragstarted(event, d) {
        if (!event.active) simulation.alphaTarget(0.3).restart()
        d.fx = d.x
        d.fy = d.y
      }
      
      function dragged(event, d) {
        d.fx = event.x
        d.fy = event.y
      }
      
      function dragended(event, d) {
        if (!event.active) simulation.alphaTarget(0)
        d.fx = null
        d.fy = null
      }
      
      // Helper functions
      function getRelationshipWidth(type) {
        switch (type) {
          case 'Contains': return 3
          case 'Uses': return 4
          case 'Implements': return 3
          case 'Inherits': return 4
          case 'Calls': return 2
          default: return 3
        }
      }
      
      function getRelationshipColor(type) {
        switch (type.toLowerCase()) {
          case 'contains': return '#bdc3c7'
          case 'uses': return '#3498db'
          case 'references': return '#e67e22'
          case 'implements': return '#e74c3c'
          case 'inherits': return '#9b59b6'
          case 'calls': return '#2ecc71'
          default: return '#95a5a6'
        }
      }
      
      function getNodeGroup(type) {
        switch (type) {
          case 'Class': return 1
          case 'Interface': return 2
          case 'Method': return 3
          case 'Property': return 4
          case 'Namespace': return 5
          default: return 0
        }
      }
      
      function getNodeRadius(type) {
        switch (type.toLowerCase()) {
          case 'namespace': return 40
          case 'class': return 20
          case 'interface': return 18
          case 'struct': return 18
          default: return 15
        }
      }
      
      function getNodeColor(type) {
        switch (type.toLowerCase()) {
          case 'namespace': return '#e8f4f8'
          case 'class': return '#4ecdc4'
          case 'interface': return '#45b7d1'
          case 'struct': return '#96ceb4'
          default: return '#95a5a6'
        }
      }
      
      function getNodeStrokeColor(type) {
        switch (type.toLowerCase()) {
          case 'namespace': return '#2c3e50'
          case 'class': return '#16a085'
          case 'interface': return '#2980b9'
          case 'struct': return '#27ae60'
          default: return '#7f8c8d'
        }
      }
      
      // Store simulation reference for cleanup
      visualizationContainer.value._simulation = simulation
    }
    
    // Handle window resize
    const handleResize = () => {
      if (visualizationData.value && visualizationContainer.value) {
        renderVisualization()
      }
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
      
      // Add resize listener
      window.addEventListener('resize', handleResize)
    })
    
    onUnmounted(() => {
      // Clean up D3 simulation
      if (visualizationContainer.value && visualizationContainer.value._simulation) {
        visualizationContainer.value._simulation.stop()
      }
      
      // Remove resize listener
      window.removeEventListener('resize', handleResize)
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
      visualizationType,
      showLabels,
      showMetrics,
      visualizationContainer,
      
      // Data
      tabs,
      repositories,
      activeScans,
      metrics,
      visualizationData,
      hasGraphData,
      
      // Loading states
      loadingVisualization,
      loadingMetrics,
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

.graph-status {
  margin-top: 8px;
}

.status-indicator {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  font-weight: 500;
  padding: 4px 8px;
  border-radius: 6px;
}

.status-indicator.success {
  color: #065f46;
  background: #d1fae5;
}

.status-indicator.scanning {
  color: #92400e;
  background: #fef3c7;
}

.status-indicator.empty {
  color: #b45309;
  background: #fed7aa;
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
  height: 600px;
  width: 100%;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: white;
  overflow: hidden;
  position: relative;
}

.visualization-actions {
  display: flex;
  gap: 12px;
  justify-content: center;
  margin-top: 16px;
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