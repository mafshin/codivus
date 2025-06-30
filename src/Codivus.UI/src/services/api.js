import axios from 'axios'

// Create axios instance
const apiClient = axios.create({
  baseURL: '/api',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json'
  }
})

// Response interceptor for API calls
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const { response } = error
    // Handle error responses
    if (response && response.status) {
      // You can customize error handling based on status codes
      console.error(`API Error: ${response.status} - ${response.statusText}`)
    } else {
      console.error('Network Error:', error.message)
    }
    return Promise.reject(error)
  }
)

export default {
  // Repository API
  getRepositories() {
    return apiClient.get('/repositories')
  },
  
  getRepository(id) {
    console.log('API: Requesting repository with ID:', id)
    return apiClient.get(`/repositories/${id}`)
      .then(response => {
        console.log('API: Repository response received:', {
          status: response.status,
          data: response.data
        })
        return response
      })
      .catch(error => {
        console.error('API: Error fetching repository:', {
          id: id,
          message: error.message,
          status: error.response?.status,
          statusText: error.response?.statusText,
          data: error.response?.data
        })
        throw error
      })
  },
  
  createRepository(repository) {
    console.log('API: Sending create repository request:', JSON.stringify(repository))
    return apiClient.post('/repositories', repository)
      .then(response => {
        console.log('API: Create repository response:', response.data)
        return response
      })
      .catch(error => {
        console.error('API: Error creating repository:', error.message)
        console.error('API: Error response:', error.response?.data)
        throw error
      })
  },
  
  updateRepository(id, repository) {
    return apiClient.put(`/repositories/${id}`, repository)
  },
  
  deleteRepository(id) {
    return apiClient.delete(`/repositories/${id}`)
  },
  
  validateRepository(location, type) {
    console.log('API: Validating repository:', { location, type, typeType: typeof type })
    return apiClient.get('/repositories/validate', { params: { location, type } })
      .then(response => {
        console.log('API: Repository validation response:', response.data)
        return response
      })
  },
  
  getRepositoryStructure(id) {
    console.log('API: Requesting repository structure for ID:', id)
    return apiClient.get(`/repositories/${id}/structure`)
      .then(response => {
        console.log('API: Repository structure response received:', response.status)
        return response
      })
      .catch(error => {
        console.error('API: Error fetching repository structure:', {
          message: error.message,
          status: error.response?.status,
          statusText: error.response?.statusText,
          data: error.response?.data
        })
        throw error
      })
  },
  
  getFileContent(id, filePath) {
    return apiClient.get(`/repositories/${id}/files`, { params: { filePath } })
  },
  
  // LLM Provider API
  getLlmModels(providerType) {
    return apiClient.get(`/llmprovider/models`, { params: { providerType } })
  },
  
  // Scanning API
  startScan(repositoryId, configuration) {
    console.log('API: Sending startScan request', {
      repositoryId,
      configData: JSON.stringify(configuration)
    })
    return apiClient.post('/scanning/start', configuration, { params: { repositoryId } })
      .then(response => {
        console.log('API: Start scan response:', response.data)
        return response
      })
      .catch(error => {
        console.error('API: Start scan error:', error.message)
        if (error.response) {
          console.error('API: Error response:', error.response.data)
          console.error('API: Error status:', error.response.status)
        }
        throw error
      })
  },
  
  getScanProgress(scanId) {
    // We need to handle both the /progress endpoint and direct scan access
    return apiClient.get(`/scanning/${scanId}/progress`)
      .catch(error => {
        // If the progress endpoint fails, try to get the scan directly
        console.warn('Error fetching scan progress, trying alternative endpoint:', error.message)
        return apiClient.get(`/scanning/scan/${scanId}`)
      })
  },
  
  getScansByRepository(repositoryId) {
    return apiClient.get(`/scanning/repository/${repositoryId}`)
  },
  
  pauseScan(scanId) {
    return apiClient.post(`/scanning/${scanId}/pause`)
  },
  
  resumeScan(scanId) {
    return apiClient.post(`/scanning/${scanId}/resume`)
  },
  
  cancelScan(scanId) {
    return apiClient.post(`/scanning/${scanId}/cancel`)
  },
  
  deleteScan(scanId) {
    console.log('=== DELETE SCAN API CALL START ===')
    console.log('API: Calling deleteScan with scanId:', scanId)
    console.log('API: scanId type:', typeof scanId)
    console.log('API: scanId length:', scanId?.length)
    console.log('API: URL will be:', `/scanning/${scanId}`)
    console.log('API: Full URL will be:', `${apiClient.defaults.baseURL}/scanning/${scanId}`)
    console.log('API: Base URL:', apiClient.defaults.baseURL)
    console.log('API: Timeout:', apiClient.defaults.timeout)
    console.log('API: Headers:', apiClient.defaults.headers)
    
    // Validate scanId before making the request
    if (!scanId) {
      const error = new Error('scanId is required for delete operation')
      console.error('API: Delete scan validation error:', error.message)
      return Promise.reject(error)
    }
    
    if (typeof scanId !== 'string' || scanId.length < 10) {
      const error = new Error(`Invalid scanId format: ${scanId}`)
      console.error('API: Delete scan validation error:', error.message)
      return Promise.reject(error)
    }
    
    // Make the delete request with comprehensive logging
    const startTime = Date.now()
    console.log('API: Starting DELETE request at:', new Date().toISOString())
    
    return apiClient.delete(`/scanning/${scanId}`)
      .then(response => {
        const duration = Date.now() - startTime
        console.log('=== DELETE SCAN API CALL SUCCESS ===')
        console.log('API: Delete scan SUCCESS response:', {
          status: response.status,
          statusText: response.statusText,
          duration: `${duration}ms`,
          headers: response.headers,
          data: response.data
        })
        console.log('API: Response received at:', new Date().toISOString())
        console.log('=== DELETE SCAN API CALL END ===')
        return response
      })
      .catch(error => {
        const duration = Date.now() - startTime
        console.log('=== DELETE SCAN API CALL ERROR ===')
        console.error('API: Delete scan ERROR response:', {
          message: error.message,
          status: error.response?.status,
          statusText: error.response?.statusText,
          duration: `${duration}ms`,
          url: error.config?.url,
          method: error.config?.method,
          requestData: error.config?.data,
          responseData: error.response?.data,
          requestHeaders: error.config?.headers,
          responseHeaders: error.response?.headers
        })
        
        // Detailed error analysis
        if (error.response?.status === 404) {
          console.error('API: 404 NOT FOUND - The scan might not exist or endpoint is wrong')
          console.error('API: Attempted URL:', error.config?.url)
          console.error('API: Base URL:', apiClient.defaults.baseURL)
          console.error('API: Full URL attempted:', error.config?.url)
          console.error('API: Backend might not be running or endpoint not implemented')
        } else if (error.response?.status === 400) {
          console.error('API: 400 BAD REQUEST - Invalid request or scan cannot be deleted')
          console.error('API: Server response:', error.response?.data)
        } else if (error.response?.status >= 500) {
          console.error('API: 5xx SERVER ERROR - Backend server error')
          console.error('API: Server response:', error.response?.data)
        } else if (!error.response) {
          console.error('API: NETWORK ERROR - No response received')
          console.error('API: This might be a CORS issue or server is down')
          console.error('API: Error code:', error.code)
        }
        
        console.log('API: Error occurred at:', new Date().toISOString())
        console.log('=== DELETE SCAN API CALL END ===')
        throw error
      })
  },
  
  getScanIssues(scanId) {
    return apiClient.get(`/scanning/${scanId}/issues`)
  },
  
  // Scan Configuration API
  getScanConfigurations(repositoryId) {
    return apiClient.get(`/scanning/configurations/${repositoryId}`)
  },
  
  getScanConfiguration(configurationId) {
    return apiClient.get(`/scanning/configuration/${configurationId}`)
  },
  
  createScanConfiguration(configuration) {
    return apiClient.post('/scanning/configuration', configuration)
  },
  
  updateScanConfiguration(configurationId, configuration) {
    return apiClient.put(`/scanning/configuration/${configurationId}`, configuration)
  },
  
  deleteScanConfiguration(configurationId) {
    return apiClient.delete(`/scanning/configuration/${configurationId}`)
  },

  // Graph API
  startGraphScan(repositoryId, configuration) {
    console.log('API: Starting graph scan for repository:', repositoryId)
    return apiClient.post(`/graph/scan/${repositoryId}`, configuration)
      .then(response => {
        console.log('API: Graph scan started:', response.data)
        return response
      })
      .catch(error => {
        console.error('API: Error starting graph scan:', error.message)
        throw error
      })
  },

  getGraphScanStatus(scanId) {
    return apiClient.get(`/graph/scan/${scanId}/status`)
  },

  pauseGraphScan(scanId) {
    return apiClient.post(`/graph/scan/${scanId}/pause`)
  },

  resumeGraphScan(scanId) {
    return apiClient.post(`/graph/scan/${scanId}/resume`)
  },

  cancelGraphScan(scanId) {
    return apiClient.post(`/graph/scan/${scanId}/cancel`)
  },

  // Graph nodes
  getGraphNodes(repositoryId, filters = {}) {
    const params = { repositoryId, ...filters }
    return apiClient.get('/graph/nodes', { params })
  },

  getGraphNode(nodeId) {
    return apiClient.get(`/graph/nodes/${nodeId}`)
  },

  getNodeDependencies(nodeId) {
    return apiClient.get(`/graph/nodes/${nodeId}/dependencies`)
  },

  getNodeDependents(nodeId) {
    return apiClient.get(`/graph/nodes/${nodeId}/dependents`)
  },

  getCallHierarchy(nodeId) {
    return apiClient.get(`/graph/nodes/${nodeId}/call-hierarchy`)
  },

  getTypeHierarchy(nodeId) {
    return apiClient.get(`/graph/nodes/${nodeId}/type-hierarchy`)
  },

  // Graph analysis
  performImpactAnalysis(nodeId, analysisType) {
    return apiClient.post(`/graph/nodes/${nodeId}/impact-analysis`, { analysisType })
  },

  getCouplingAnalysis(repositoryId) {
    return apiClient.get(`/graph/coupling-analysis/${repositoryId}`)
  },

  getSubgraph(nodeId, options = {}) {
    return apiClient.post(`/graph/nodes/${nodeId}/subgraph`, options)
  },

  // Graph metrics and visualization
  getGraphMetrics(repositoryId) {
    return apiClient.get(`/graph/metrics/${repositoryId}`)
  },

  getGraphVisualization(repositoryId) {
    return apiClient.get(`/graph/visualization/${repositoryId}`)
  },

  // Custom graph queries
  executeGraphQuery(query) {
    return apiClient.post('/graph/query', { query })
  },

  // Enhanced scanning with graph context
  scanFileWithGraphContext(fileData, options = {}) {
    return apiClient.post('/enhancedscanning/scan-file', { ...fileData, ...options })
  },

  scanFilesWithGraphContext(filesData, options = {}) {
    return apiClient.post('/enhancedscanning/scan-files', { files: filesData, ...options })
  },

  analyzeDependenciesWithGraph(repositoryId, analysisOptions = {}) {
    return apiClient.post('/enhancedscanning/analyze-dependencies', { repositoryId, ...analysisOptions })
  }
}
