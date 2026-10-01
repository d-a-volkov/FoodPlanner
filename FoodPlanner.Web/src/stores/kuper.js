import { defineStore } from 'pinia'
import { ref } from 'vue'
import { kuperApi } from '../api'

export const useKuperStore = defineStore('kuper', () => {
  const status = ref(null)
  const profile = ref(null)
  const stores = ref([])
  const selectedStoreId = ref(null)
  const storeName = ref(localStorage.getItem('kuperStoreName') || '')
  const cartUrl = ref('')
  const loading = ref(false)
  const error = ref('')

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
      const store = stores.value.find(s => s.store_id === selectedStoreId.value)
      if (store) storeName.value = store.name
    } else {
      profile.value = null
      stores.value = []
      selectedStoreId.value = null
    }
    return data
  }

  async function sendCode(phone) {
    error.value = ''
    loading.value = true
    try {
      const { data } = await kuperApi.sendCode(phone)
      return data
    } finally {
      loading.value = false
    }
  }

  async function confirmCode(phone, code) {
    error.value = ''
    loading.value = true
    try {
      const { data } = await kuperApi.confirmCode(phone, code)
      profile.value = data.profile
      stores.value = data.stores || []
      status.value = { ok: true, session: { has_cookie: true, profile: true, store_selected: false } }
      return data
    } finally {
      loading.value = false
    }
  }

  async function confirmCodeByCookie(cookie) {
    error.value = ''
    loading.value = true
    try {
      const { data } = await kuperApi.connectCookie(cookie)
      profile.value = data.profile
      stores.value = data.stores || []
      status.value = { ok: true, session: { has_cookie: true, profile: true, store_selected: false } }
      return data
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
    fetchStatus, fetchSession, sendCode, confirmCode, confirmCodeByCookie, selectStore, refreshHistory, resolve, addToCart, disconnect
  }
})