<template>
  <div class="issues-filter">
    <div class="search-box">
      <span class="mdi mdi-magnify"></span>
      <input 
        type="text" 
        :value="searchQuery"
        @input="$emit('update:searchQuery', $event.target.value)"
        placeholder="Search issues..." 
        class="search-input"
      />
    </div>
    
    <div class="filter-dropdown">
      <select 
        :value="severityFilter" 
        @change="$emit('update:severityFilter', $event.target.value)"
        class="filter-select"
      >
        <option value="all">All Severities</option>
        <option value="Critical">Critical</option>
        <option value="High">High</option>
        <option value="Medium">Medium</option>
        <option value="Low">Low</option>
        <option value="Info">Info</option>
      </select>
    </div>
    
    <div class="filter-dropdown">
      <select 
        :value="categoryFilter"
        @change="$emit('update:categoryFilter', $event.target.value)"
        class="filter-select"
      >
        <option value="all">All Categories</option>
        <option value="Security">Security</option>
        <option value="Performance">Performance</option>
        <option value="Quality">Quality</option>
        <option value="Architecture">Architecture</option>
        <option value="Dependency">Dependency</option>
        <option value="Testing">Testing</option>
        <option value="Documentation">Documentation</option>
        <option value="Accessibility">Accessibility</option>
        <option value="Other">Other</option>
      </select>
    </div>
    
    <!-- Filter Active Indicator and Clear Button -->
    <div v-if="filtersActive" class="filter-active">
      <span class="filter-count">{{ filteredCount }} of {{ totalCount }} issues</span>
      <button class="btn btn-sm btn-secondary clear-filters-btn" @click="$emit('clear-filters')">
        <span class="mdi mdi-filter-off"></span>
        Clear Filters
      </button>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  searchQuery: {
    type: String,
    default: ''
  },
  severityFilter: {
    type: String,
    default: 'all'
  },
  categoryFilter: {
    type: String,
    default: 'all'
  },
  filteredCount: {
    type: Number,
    default: 0
  },
  totalCount: {
    type: Number,
    default: 0
  }
})

defineEmits([
  'update:searchQuery',
  'update:severityFilter',
  'update:categoryFilter',
  'clear-filters'
])

const filtersActive = computed(() => {
  return props.searchQuery !== '' || 
         props.severityFilter !== 'all' || 
         props.categoryFilter !== 'all'
})
</script>

<style lang="scss" scoped>
.issues-filter {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  margin-bottom: 1.5rem;
  align-items: center;
  
  .filter-active {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    
    .filter-count {
      font-size: 0.875rem;
      color: var(--text-muted);
      font-weight: 500;
    }
    
    .clear-filters-btn {
      font-size: 0.75rem;
      padding: 0.375rem 0.75rem;
    }
  }
  
  .search-box {
    flex: 1;
    min-width: 250px;
    position: relative;
    
    .mdi-magnify {
      position: absolute;
      left: 12px;
      top: 50%;
      transform: translateY(-50%);
      color: var(--text-muted);
    }
    
    .search-input {
      width: 100%;
      padding: 0.625rem 2.5rem 0.625rem 2.5rem;
      border-radius: var(--border-radius);
      border: 1px solid var(--border-color);
      
      &:focus {
        outline: none;
        border-color: var(--primary-color);
      }
    }
  }
  
  .filter-dropdown {
    width: 150px;
    
    .filter-select {
      width: 100%;
      padding: 0.625rem;
      border-radius: var(--border-radius);
      border: 1px solid var(--border-color);
      appearance: none;
      background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24'%3E%3Cpath fill='%236c757d' d='M7 10l5 5 5-5z'/%3E%3C/svg%3E");
      background-repeat: no-repeat;
      background-position: right 8px center;
    }
  }
}
</style>
