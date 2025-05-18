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
    console.log('API: Calling deleteScan with scanId:', scanId)
    console.log('API: URL will be:', `/scanning/${scanId}`)
    
    // Try the main delete endpoint
    return apiClient.delete(`/scanning/${scanId}`)
      .then(response => {
        console.log('API: Delete scan response:', {
          status: response.status,
          statusText: response.statusText
        })
        return response
      })
      .catch(error => {
        console.error('API: Delete scan error (first attempt):', {
          message: error.message,
          status: error.response?.status,
          statusText: error.response?.statusText,
          url: error.config?.url,
          method: error.config?.method,
          data: error.response?.data
        })
        
        // If we get a 404, it might be that the endpoint is wrong
        // Let's log all the information we can
        if (error.response?.status === 404) {
          console.log('API: 404 error - endpoint might not exist')
          console.log('API: Full error details:', error.response)
          console.log('API: Base URL:', apiClient.defaults.baseURL)
          console.log('API: Full URL attempted:', error.config?.url)
        }
        
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
  }
}
