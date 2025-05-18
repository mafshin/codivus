import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    }
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'https://localhost:5001',
        changeOrigin: true,
        secure: false,
        rewrite: (path) => {
          console.log('Proxying API request:', path)
          return path
        }
      },
      '/hubs': {
        target: 'https://localhost:5001',
        ws: true,
        changeOrigin: true,
        secure: false
      }
    }
  }
})
