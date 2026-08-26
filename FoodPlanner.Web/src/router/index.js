import { createRouter, createWebHistory } from 'vue-router'

const routes = [
  {
    path: '/',
    redirect: '/products'
  },
  {
    path: '/products',
    name: 'Products',
    component: () => import('../views/Products.vue'),
    meta: { title: 'Продукты', icon: '🥬' }
  },
  {
    path: '/harvard-plate',
    name: 'HarvardPlate',
    component: () => import('../views/HarvardPlate.vue'),
    meta: { title: 'Гарвардская тарелка', icon: '🍽' }
  },
  {
    path: '/recipes',
    name: 'Recipes',
    component: () => import('../views/Recipes.vue'),
    meta: { title: 'Рецепты', icon: '📖' }
  },
  {
    path: '/shopping-list',
    name: 'ShoppingList',
    component: () => import('../views/ShoppingList.vue'),
    meta: { title: 'Списки покупок', icon: '🛒' }
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

export default router
