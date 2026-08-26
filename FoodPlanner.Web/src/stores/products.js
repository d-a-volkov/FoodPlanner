import { defineStore } from 'pinia'
import { ref } from 'vue'
import { productsApi } from '../api'

export const useProductsStore = defineStore('products', () => {
  const products = ref([])
  const loading = ref(false)
  const error = ref(null)

  const STORAGE_ZONES = {
    0: 'Холодильник',
    1: 'Овощи и фрукты',
    2: 'Молочные продукты',
    3: 'Консервы и закатки',
    4: 'Дверца',
    5: 'Морозилка',
    6: 'Выпечка',
    7: 'Крупы и макароны',
    8: 'Специи и приправы',
    9: 'Кофе и чай',
    10: 'Хоз. товары'
  }

  const STOCK_STATUS = {
    0: { label: 'В наличии', color: 'green', icon: '✓' },
    1: { label: 'Нет в наличии', color: 'red', icon: '✗' },
    2: { label: 'Мало/пусто', color: 'orange', icon: '○' }
  }

  const CATEGORIES = {
    0: 'Овощи',
    1: 'Фрукты',
    2: 'Цельнозерновые',
    3: 'Белок',
    4: 'Молочные',
    5: 'Полезные жиры',
    6: 'Бобовые',
    7: 'Орехи',
    8: 'Специи',
    9: 'Напитки',
    10: 'Другое'
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
    const newStatus = product.stockStatus === 0 ? 1 : 0
    return updateProduct({ ...product, stockStatus: newStatus })
  }

  function getZoneName(zone) {
    return STORAGE_ZONES[zone] ?? 'Неизвестно'
  }

  function getStatusInfo(status) {
    return STOCK_STATUS[status] ?? { label: 'Неизвестно', color: 'gray', icon: '?' }
  }

  function getCategoryName(category) {
    return CATEGORIES[category] ?? 'Другое'
  }

  function groupedByZone() {
    const groups = {}
    for (const p of products.value) {
      const zone = p.storageZone
      if (!groups[zone]) groups[zone] = []
      groups[zone].push(p)
    }
    return groups
  }

  return {
    products, loading, error,
    fetchProducts, addProduct, updateProduct, deleteProduct, toggleStock,
    getZoneName, getStatusInfo, getCategoryName, groupedByZone,
    STORAGE_ZONES, STOCK_STATUS, CATEGORIES
  }
})
