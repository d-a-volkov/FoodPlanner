<script setup>
import { useRouter } from 'vue-router'

const router = useRouter()

const navItems = [
  { path: '/products', label: 'Продукты', icon: '🥬' },
  { path: '/harvard-plate', label: 'Гарвардская тарелка', icon: '🍽' },
  { path: '/recipes', label: 'Рецепты', icon: '📖' },
  { path: '/shopping-list', label: 'Списки покупок', icon: '🛒' }
]
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
    <main class="content">
      <router-view />
    </main>
  </div>
</template>

<style scoped>
#app {
  display: flex;
  min-height: 100vh;
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

.content {
  flex: 1;
  padding: 30px;
  background: #f5f5f5;
  overflow-y: auto;
}
</style>
