import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  {
    path: '/',
    name: 'Dashboard',
    component: () => import('@/views/DashboardView.vue')
  },
  {
    path: '/repositories',
    name: 'Repositories',
    component: () => import('@/views/RepositoriesView.vue')
  },
  {
    path: '/repositories/add',
    name: 'AddRepository',
    component: () => import('@/views/AddRepositoryView.vue')
  },
  {
    path: '/repositories/:id',
    name: 'RepositoryDetails',
    component: () => import('@/views/RepositoryDetailsView.vue'),
    props: true
  },
  {
    path: '/scans',
    name: 'Scans',
    component: () => import('@/views/ScansView.vue')
  },
  {
    path: '/scans/:id',
    name: 'ScanDetails',
    component: () => import('@/views/ScanDetailsView.vue'),
    props: true
  },
  {
    path: '/settings',
    name: 'Settings',
    component: () => import('@/views/SettingsView.vue')
  },
  {
    path: '/debug/file-tree',
    name: 'DebugFileTree',
    component: () => import('@/components/debug/DebugFileTree.vue')
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'NotFound',
    component: () => import('@/views/NotFoundView.vue')
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes,
  linkActiveClass: 'router-link-active'
})

export default router
