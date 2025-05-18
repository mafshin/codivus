<template>
  <div class="app-container">
    <header class="app-header">
      <div class="app-logo">
        <img src="@/assets/images/logo.svg" alt="Codivus Logo" class="logo-image">
        <span class="logo-text">Codivus</span>
      </div>
      <nav class="app-nav">
        <router-link to="/" class="nav-link">Dashboard</router-link>
        <router-link to="/repositories" class="nav-link">Repositories</router-link>
        <router-link to="/scans" class="nav-link">Scans</router-link>
        <router-link to="/settings" class="nav-link">Settings</router-link>
      </nav>
    </header>
    <main class="app-content">
      <router-view v-slot="{ Component }">
        <transition name="fade" mode="out-in">
          <component :is="Component" />
        </transition>
      </router-view>
    </main>
    <footer class="app-footer">
      <p>© {{ currentYear }} Codivus - AI-Enabled Code Scanning Solution</p>
    </footer>
  </div>
</template>

<script setup>
import { computed } from 'vue'

const currentYear = computed(() => new Date().getFullYear())
</script>

<style lang="scss">
:root {
  --primary-color: #4a6cf7;
  --primary-color-dark: #3a5cd7; /* Pre-darkened color for primary */
  --secondary-color: #6c757d;
  --secondary-color-dark: #5a6268; /* Pre-darkened color for secondary */
  --success-color: #28a745;
  --success-color-dark: #218838; /* Pre-darkened color for success */
  --danger-color: #dc3545;
  --danger-color-dark: #c82333; /* Pre-darkened color for danger */
  --warning-color: #ffc107;
  --warning-color-dark: #e0a800; /* Pre-darkened color for warning */
  --info-color: #17a2b8;
  --info-color-dark: #138496; /* Pre-darkened color for info */
  --light-color: #f8f9fa;
  --dark-color: #343a40;
  --text-color: #333;
  --bg-color: #f5f7fb;
  --header-height: 60px;
  --footer-height: 50px;
  --sidebar-width: 250px;
  --border-radius: 6px;
  --box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  --transition-speed: 0.3s;
}

* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

body {
  font-family: 'Roboto', sans-serif;
  color: var(--text-color);
  background: var(--bg-color);
  line-height: 1.6;
  min-height: 100vh;
}

.app-container {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}

.app-header {
  height: var(--header-height);
  background: white;
  box-shadow: var(--box-shadow);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 1.5rem;
  position: sticky;
  top: 0;
  z-index: 1000;
}

.app-logo {
  display: flex;
  align-items: center;
  
  .logo-image {
    height: 32px;
    margin-right: 10px;
  }
  
  .logo-text {
    font-size: 20px;
    font-weight: 700;
    color: var(--primary-color);
  }
}

.app-nav {
  display: flex;
  gap: 1.5rem;
  
  .nav-link {
    color: var(--secondary-color);
    text-decoration: none;
    font-weight: 500;
    padding: 0.5rem;
    border-radius: var(--border-radius);
    transition: color var(--transition-speed), background-color var(--transition-speed);
    
    &:hover {
      color: var(--primary-color);
    }
    
    &.router-link-active {
      color: var(--primary-color);
      background-color: rgba(74, 108, 247, 0.1);
    }
  }
}

.app-content {
  flex-grow: 1;
  padding: 1.5rem;
}

.app-footer {
  height: var(--footer-height);
  display: flex;
  align-items: center;
  justify-content: center;
  background: white;
  border-top: 1px solid #eaeaea;
  
  p {
    font-size: 14px;
    color: var(--secondary-color);
  }
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity var(--transition-speed);
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

/* Common UI Components */
.btn {
  padding: 0.5rem 1rem;
  border-radius: var(--border-radius);
  border: none;
  font-weight: 500;
  cursor: pointer;
  transition: all var(--transition-speed);
  
  &.btn-primary {
    background: var(--primary-color);
    color: white;
    
    &:hover {
      background: var(--primary-color-dark, #3a5cd7);
      /* Using predefined darker shade instead of Sass darken() */
    }
  }
  
  &.btn-secondary {
    background: var(--secondary-color);
    color: white;
    
    &:hover {
      background: var(--secondary-color-dark, #5a6268);
      /* Using predefined darker shade instead of Sass darken() */
    }
  }
  
  &.btn-success {
    background: var(--success-color);
    color: white;
    
    &:hover {
      background: var(--success-color-dark, #218838);
      /* Using predefined darker shade instead of Sass darken() */
    }
  }
  
  &.btn-danger {
    background: var(--danger-color);
    color: white;
    
    &:hover {
      background: var(--danger-color-dark, #c82333);
      /* Using predefined darker shade instead of Sass darken() */
    }
  }
}

.card {
  background: white;
  border-radius: var(--border-radius);
  box-shadow: var(--box-shadow);
  padding: 1.5rem;
  margin-bottom: 1.5rem;
  
  .card-title {
    font-size: 1.25rem;
    font-weight: 700;
    margin-bottom: 1rem;
  }
}

.form-group {
  margin-bottom: 1rem;
  
  label {
    display: block;
    margin-bottom: 0.5rem;
    font-weight: 500;
  }
  
  input, select, textarea {
    width: 100%;
    padding: 0.5rem;
    border: 1px solid #ddd;
    border-radius: var(--border-radius);
    font-family: inherit;
    font-size: inherit;
    
    &:focus {
      outline: none;
      border-color: var(--primary-color);
    }
  }
}
</style>
