<template>
  <div class="not-found">
    <div class="not-found-content">
      <span class="not-found-code">404</span>
      <h1 class="not-found-title">Page Not Found</h1>
      <p class="not-found-message">
        The page you are looking for doesn't exist or has been moved.
      </p>
      <router-link to="/" class="btn btn-primary">
        <span class="mdi mdi-home"></span> Go to Dashboard
      </router-link>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.not-found {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: calc(100vh - var(--header-height) - var(--footer-height));
  padding: 2rem;
  text-align: center;
  
  .not-found-content {
    max-width: 500px;
  }
  
  .not-found-code {
    display: block;
    font-size: 8rem;
    font-weight: 700;
    color: var(--primary-color);
    line-height: 1;
    margin-bottom: 1rem;
    opacity: 0.5;
  }
  
  .not-found-title {
    font-size: 2rem;
    margin-bottom: 1rem;
  }
  
  .not-found-message {
    color: var(--text-muted);
    margin-bottom: 2rem;
  }
}
</style>
