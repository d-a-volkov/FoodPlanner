import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { productsApi, categoriesApi } from '../api'

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

// Порядок вывода: сначала по наличию, затем по алфавиту
export const STATUS_ORDER = { 0: 0, 2: 1, 1: 2, 3: 3 }

export function compareByStockThenName(a, b) {
  const rankA = STATUS_ORDER[a.stockStatus] ?? 99
  const rankB = STATUS_ORDER[b.stockStatus] ?? 99
  if (rankA !== rankB) return rankA - rankB
  return a.name.localeCompare(b.name, 'ru', { sensitivity: 'base' })
}

export const useProductsStore = defineStore('products', () => {
  const products = ref([])
  const loading = ref(false)
  const error = ref(null)

  // Порядок фиксируется один раз на загрузку окна и больше не меняется,
  // чтобы смена статуса не переставляла строки. Обновляется только после F5.
  const orderIds = ref([])
  let orderCaptured = false

  const CATEGORIES = ref({ ...DEFAULT_CATEGORIES })
  // Полный список для экрана управления: с флагом пользовательской и числом продуктов
  const categoryList = ref([])

  async function fetchCategories() {
    try {
      const { data } = await categoriesApi.getAll()
      if (Array.isArray(data) && data.length) {
        const map = {}
        for (const c of data) map[c.id] = c.name
        CATEGORIES.value = { ...map }
        categoryList.value = data
      }
    } catch (e) {
      // keep local fallback
    }
  }

  async function createCategory(name) {
    const { data } = await categoriesApi.create(name)
    await fetchCategories()
    return data
  }

  async function renameCategory(id, name) {
    const { data } = await categoriesApi.rename(id, name)
    await fetchCategories()
    return data
  }

  async function deleteCategory(id) {
    await categoriesApi.remove(id)
    await fetchCategories()
  }

  const STOCK_STATUS = {
    0: { label: 'В наличии', color: 'green', icon: '✓' },
    1: { label: 'Нет в наличии', color: 'red', icon: '✗' },
    2: { label: 'Мало/пусто', color: 'orange', icon: '○' },
    3: { label: 'Не используется', color: 'gray', icon: '⊘' }
  }

  async function fetchProducts() {
    loading.value = true
    error.value = null
    try {
      const { data } = await productsApi.getAll()
      products.value = data
      captureOrder(data)
    } catch (e) {
      error.value = e.message
    } finally {
      loading.value = false
    }
  }

  function captureOrder(list) {
    if (orderCaptured) return
    orderCaptured = true
    orderIds.value = [...list].sort(compareByStockThenName).map(p => p.id)
  }

  const orderIndex = computed(() => {
    const map = new Map()
    orderIds.value.forEach((id, i) => map.set(id, i))
    return map
  })

  // Продукты, добавленные после фиксации порядка, уходят в конец своей категории
  function rankOf(product) {
    const rank = orderIndex.value.get(product.id)
    if (rank !== undefined) return rank
    return orderIds.value.length + (STATUS_ORDER[product.stockStatus] ?? 99) * 1000
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
    products, loading, error, CATEGORIES, categoryList, STOCK_STATUS, orderIds,
    fetchCategories, fetchProducts, addProduct, updateProduct, deleteProduct, toggleStock,
    createCategory, renameCategory, deleteCategory,
    getStatusInfo, getCategoryName, rankOf
  }
})