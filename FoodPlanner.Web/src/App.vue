<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

const navItems = [
  { path: '/products', label: 'Продукты', shortLabel: 'Продукты', icon: '🥬' },
  { path: '/harvard-plate', label: 'Гарвардская тарелка', shortLabel: 'Тарелка', icon: '🍽' },
  { path: '/recipes', label: 'Рецепты', shortLabel: 'Рецепты', icon: '📖' },
  { path: '/shopping-list', label: 'Списки покупок', shortLabel: 'Покупки', icon: '🛒' }
]

const offline = ref(false)

function updateOnline() {
  offline.value = !navigator.onLine
}

onMounted(() => {
  updateOnline()
  window.addEventListener('online', updateOnline)
  window.addEventListener('offline', updateOnline)
})

onUnmounted(() => {
  window.removeEventListener('online', updateOnline)
  window.removeEventListener('offline', updateOnline)
})
</script>

<template>
  <div id="app">
    <nav class="sidebar">
      <div class="logo">
        <h2>🍽 FoodPlanner</h2>
      </div>
      <ul>
        <li v-for="item in navItems" :key="item.path">
          <router-link :to="item.path" class="nav-link">
            <span class="icon">{{ item.icon }}</span>
            <span class="label">{{ item.label }}</span>
          </router-link>
        </li>
      </ul>
    </nav>

    <div class="viewport">
      <header class="mobile-header">
        <h2>🍽 FoodPlanner</h2>
      </header>

      <main class="content">
        <router-view />
      </main>

      <nav class="bottom-nav">
        <router-link
          v-for="item in navItems"
          :key="item.path"
          :to="item.path"
          class="bottom-link"
        >
          <span class="bottom-icon">{{ item.icon }}</span>
          <span class="bottom-label">{{ item.shortLabel }}</span>
        </router-link>
      </nav>

      <Transition name="banner">
        <div v-if="offline" class="offline-banner">
          Нет соединения — данные API недоступны
        </div>
      </Transition>
    </div>
  </div>
</template>

<style scoped>
#app {
  display: flex;
  min-height: 100vh;
  min-height: 100dvh;
}

.sidebar {
  width: 240px;
  background: #1a1a2e;
  color: #eee;
  padding: 20px 0;
  flex-shrink: 0;
}

.logo {
  padding: 0 20px 20px;
  border-bottom: 1px solid #333;
}

.logo h2 {
  margin: 0;
  font-size: 1.2rem;
}

.sidebar ul {
  list-style: none;
  padding: 10px 0;
  margin: 0;
}

.sidebar li {
  margin: 2px 0;
}

.nav-link {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 20px;
  color: #ccc;
  text-decoration: none;
  transition: all 0.2s;
}

.nav-link:hover {
  background: #16213e;
  color: #fff;
}

.nav-link.router-link-active {
  background: #0f3460;
  color: #fff;
  border-left: 3px solid #e94560;
}

.icon {
  font-size: 1.2rem;
}

.viewport {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.content {
  flex: 1;
  padding: 30px;
  background: #f5f5f5;
  overflow-y: auto;
}

.mobile-header {
  display: none;
}

.bottom-nav {
  display: none;
}

.offline-banner {
  position: fixed;
  bottom: calc(84px + env(safe-area-inset-bottom));
  left: 50%;
  transform: translateX(-50%);
  background: #d32f2f;
  color: #fff;
  padding: 8px 16px;
  border-radius: 20px;
  font-size: 0.8rem;
  z-index: 1000;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.3);
}

.banner-enter-active,
.banner-leave-active {
  transition: opacity 0.2s, transform 0.2s;
}

.banner-enter-from,
.banner-leave-to {
  opacity: 0;
  transform: translateX(-50%) translateY(10px);
}

@media (min-width: 768px) {
  .sidebar {
    display: block;
  }

  .mobile-header {
    display: none;
  }

  .bottom-nav {
    display: none;
  }
}

@media (max-width: 767px) {
  #app {
    flex-direction: column;
  }

  .sidebar {
    display: none;
  }

  .viewport {
    min-height: 100vh;
    min-height: 100dvh;
  }

  .mobile-header {
    display: flex;
    align-items: center;
    position: sticky;
    top: 0;
    z-index: 100;
    background: #1a1a2e;
    color: #eee;
    padding: 12px 16px;
    height: 52px;
  }

  .mobile-header h2 {
    margin: 0;
    font-size: 1.05rem;
  }

  .content {
    padding: 14px;
    padding-bottom: calc(92px + env(safe-area-inset-bottom));
  }

  .bottom-nav {
    display: flex;
    position: fixed;
    bottom: 0;
    left: 0;
    right: 0;
    background: #1a1a2e;
    z-index: 200;
    padding-bottom: env(safe-area-inset-bottom);
    border-top: 1px solid #0f3460;
  }

  .bottom-link {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 2px;
    padding: 8px 4px;
    color: #aaa;
    text-decoration: none;
    font-size: 0.66rem;
    -webkit-tap-highlight-color: transparent;
  }

  .bottom-link.router-link-active {
    color: #fff;
  }

  .bottom-link.router-link-active .bottom-icon {
    background: #e94560;
  }

  .bottom-icon {
    font-size: 1.35rem;
    line-height: 1;
    border-radius: 12px;
    width: 48px;
    height: 26px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: transparent;
  }

  .bottom-label {
    white-space: nowrap;
  }

  .offline-banner {
    bottom: calc(96px + env(safe-area-inset-bottom));
  }
}
</style>