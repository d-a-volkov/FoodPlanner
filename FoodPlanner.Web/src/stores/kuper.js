import { defineStore } from 'pinia'
import { ref } from 'vue'
import { kuperApi } from '../api'
import { DEFAULT_CITY, findCity } from '../constants/kuperCities'

export const useKuperStore = defineStore('kuper', () => {
  const status = ref(null)
  const profile = ref(null)
  const stores = ref([])
  const selectedStoreId = ref(null)
  const storeName = ref(localStorage.getItem('kuperStoreName') || '')
  const cartUrl = ref('')
  const loading = ref(false)
  const error = ref('')
  const selectedCity = ref(localStorage.getItem('kuperCity') || DEFAULT_CITY.name)
  const coordinates = ref({
    lat: findCity(selectedCity.value)?.lat ?? DEFAULT_CITY.lat,
    lon: findCity(selectedCity.value)?.lon ?? DEFAULT_CITY.lon
  })

  async function fetchStatus() {
    error.value = ''
    try {
      const { data } = await kuperApi.status()
      status.value = data
      selectedStoreId.value = data?.session?.store_id || null
      cartUrl.value = data?.session?.cart_url || ''
    } catch (e) {
      status.value = null
      throw e
    }
  }

  async function fetchSession() {
    error.value = ''
    const { data } = await kuperApi.getSession()
    if (data.connected) {
      profile.value = data.profile
      stores.value = data.stores || []
      selectedStoreId.value = data.store_id || null
      if (typeof data.lat === 'number') {
        coordinates.value = { lat: data.lat, lon: data.lon }
      }
      const store = stores.value.find(s => s.store_id === selectedStoreId.value)
      if (store) storeName.value = store.name
    } else {
      profile.value = null
      stores.value = []
      selectedStoreId.value = null
    }
    return data
  }

  async function connectByCookie(cookie, lat, lon) {
    error.value = ''
    loading.value = true
    try {
      const coords = lat != null && lon != null ? { lat, lon } : coordinates.value
      const { data } = await kuperApi.connectCookie(cookie, coords.lat, coords.lon)
      profile.value = data.profile
      stores.value = data.stores || []
      selectedStoreId.value = null
      coordinates.value = { lat: coords.lat, lon: coords.lon }
      status.value = { ok: true, session: { has_cookie: true, profile: true, store_selected: false } }
      return data
    } finally {
      loading.value = false
    }
  }

  function setCity(name) {
    const city = findCity(name)
    if (!city) return null
    selectedCity.value = city.name
    coordinates.value = { lat: city.lat, lon: city.lon }
    localStorage.setItem('kuperCity', city.name)
    return city
  }

  async function refreshStores(lat, lon, wide = true) {
    error.value = ''
    loading.value = true
    try {
      const coords = lat != null && lon != null ? { lat, lon } : coordinates.value
      const { data } = await kuperApi.refreshStores(coords.lat, coords.lon, wide)
      stores.value = data.stores || []
      selectedStoreId.value = data.store_id || null
      coordinates.value = { lat: data.lat ?? coords.lat, lon: data.lon ?? coords.lon }
      const store = stores.value.find(s => s.store_id === selectedStoreId.value)
      storeName.value = store?.name || ''
      return stores.value
    } finally {
      loading.value = false
    }
  }

  async function selectStore(storeId) {
    error.value = ''
    const store = stores.value.find(s => s.store_id === storeId)
    await kuperApi.selectStore(storeId)
    selectedStoreId.value = storeId
    storeName.value = store?.name || ''
    localStorage.setItem('kuperStoreName', storeName.value)
    return store
  }

  async function refreshHistory() {
    error.value = ''
    return await kuperApi.refreshHistory()
  }

  async function resolve(listId) {
    error.value = ''
    loading.value = true
    try {
      const { data } = await kuperApi.resolve(listId)
      return data.items || []
    } finally {
      loading.value = false
    }
  }

  async function search(query, storeId) {
    error.value = ''
    loading.value = true
    try {
      const { data } = await kuperApi.search(query, storeId ?? selectedStoreId.value ?? undefined)
      return data
    } finally {
      loading.value = false
    }
  }

  async function addToCart(items) {
    error.value = ''
    const { data } = await kuperApi.addToCart(items)
    if (data.cart_url) cartUrl.value = data.cart_url
    return data
  }

  async function disconnect() {
    error.value = ''
    await kuperApi.disconnect()
    status.value = null
    profile.value = null
    stores.value = []
    selectedStoreId.value = null
    storeName.value = ''
    localStorage.removeItem('kuperStoreName')
  }

  return {
    status, profile, stores, selectedStoreId, storeName, cartUrl, loading, error,
    selectedCity, coordinates,
    fetchStatus, fetchSession, connectByCookie, setCity, refreshStores,
    selectStore, refreshHistory, resolve, search, addToCart, disconnect
  }
})