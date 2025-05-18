<template>
  <div class="not-found">
    <span class="mdi mdi-alert-circle"></span>
    <h2>Scan Not Found</h2>
    <p>The scan you're looking for doesn't exist or has been deleted.</p>
    <router-link to="/scans" class="btn btn-primary">
      View All Scans
    </router-link>
  </div>
</template>

<style lang="scss" scoped>
.not-found {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 4rem 0;
  text-align: center;
  
  .mdi {
    font-size: 4rem;
    color: var(--danger-color);
    margin-bottom: 1rem;
  }
  
  h2 {
    margin-bottom: 0.5rem;
  }
  
  p {
    color: var(--text-muted);
    margin-bottom: 1.5rem;
  }
}
</style>
