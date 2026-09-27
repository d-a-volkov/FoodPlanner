import { defineStore } from 'pinia'
import { ref } from 'vue'
import { productsApi } from '../api'

const DEFAULT_CATEGORIES = {
  0: 'Овощи',
  1: 'Фрукты и ягоды',
  2: 'Зелень и салаты',
  3: 'Мясо',
  4: 'Птица',
  5: 'Рыба',
  6: 'Морепродукты',
  7: 'Яйца',
  8: 'Молочные продукты',
  9: 'Крупы и макароны',
  10: 'Хлеб и выпечка',
  11: 'Бобовые',
  12: 'Орехи и семена',
  13: 'Масла и жиры',
  14: 'Специи и приправы',
  15: 'Консервы и заготовки',
  16: 'Замороженные продукты',
  17: 'Сладости',
  18: 'Напитки',
  19: 'Прочее'
}

export const useProductsStore = defineStore('products', () => {
  const products = ref([])
  const loading = ref(false)
  const error = ref(null)

  const CATEGORIES = ref({ ...DEFAULT_CATEGORIES })

  const STOCK_STATUS = {
    0: { label: 'В наличии', color: 'green', icon: '✓' },
    1: { label: 'Нет в наличии', color: 'red', icon: '✗' },
    2: { label: 'Мало/пусто', color: 'orange', icon: '○' },
    3: { label: 'Не используется', color: 'gray', icon: '⊘' }
  }

  async function fetchCategories() {
    try {
      const { data } = await productsApi.getAllCategories()
      if (Array.isArray(data) && data.length) {
        const map = {}
        for (const c of data) map[c.id] = c.name
        CATEGORIES.value = { ...map }
      }
    } catch (e) {
      // keep local fallback
    }
  }

  async function fetchProducts() {
    loading.value = true
    error.value = null
    try {
      const { data } = await productsApi.getAll()
      products.value = data
    } catch (e) {
      error.value = e.message
    } finally {
      loading.value = false
    }
  }

  async function addProduct(product) {
    const { data } = await productsApi.create(product)
    products.value.push(data)
    return data
  }

  async function updateProduct(product) {
    const { data } = await productsApi.update(product.id, product)
    const index = products.value.findIndex(p => p.id === data.id)
    if (index !== -1) products.value[index] = data
    return data
  }

  async function deleteProduct(id) {
    await productsApi.delete(id)
    products.value = products.value.filter(p => p.id !== id)
  }

  async function toggleStock(product) {
    const newStatus = (product.stockStatus + 1) % 4
    return updateProduct({ ...product, stockStatus: newStatus })
  }

  function getStatusInfo(status) {
    return STOCK_STATUS[status] ?? { label: 'Неизвестно', color: 'gray', icon: '?' }
  }

  function getCategoryName(category) {
    return CATEGORIES.value[category] ?? 'Прочее'
  }

  return {
    products, loading, error, CATEGORIES, STOCK_STATUS,
    fetchCategories, fetchProducts, addProduct, updateProduct, deleteProduct, toggleStock,
    getStatusInfo, getCategoryName
  }
})