import { defineStore } from 'pinia'
import { ref } from 'vue'
import { kuperApi } from '../api'

const SYNC_INTERVAL_MS = 24 * 60 * 60 * 1000

export const useKuperPreferencesStore = defineStore('kuperPreferences', () => {
  const items = ref([])
  const hiddenItems = ref([])
  const syncedAt = ref(null)
  const loading = ref(false)
  const error = ref('')

  function applyDoc(doc) {
    const all = doc?.items || []
    items.value = all.filter(i => !i.excluded)
    hiddenItems.value = all.filter(i => i.excluded)
    syncedAt.value = doc?.syncedAt || null
  }

  async function fetch() {
    error.value = ''
    const { data } = await kuperApi.getPreferences(true)
    applyDoc(data)
    return data
  }

  async function sync() {
    error.value = ''
    loading.value = true
    try {
      const { data } = await kuperApi.syncPreferences()
      applyDoc(data)
      return data
    } finally {
      loading.value = false
    }
  }

  async function remove(productId) {
    error.value = ''
    const { data } = await kuperApi.removePreference(productId)
    applyDoc(data)
  }

  async function restore(productId) {
    error.value = ''
    const { data } = await kuperApi.restorePreference(productId)
    applyDoc(data)
  }

  function isStale() {
    if (!syncedAt.value) return true
    const age = Date.now() - new Date(syncedAt.value).getTime()
    return age > SYNC_INTERVAL_MS
  }

  async function ensureFresh() {
    if (!isStale()) return false
    try {
      await sync()
      return true
    } catch (e) {
      error.value = e?.response?.data?.error || e.message
      return false
    }
  }

  return {
    items, hiddenItems, syncedAt, loading, error,
    fetch, sync, remove, restore, ensureFresh, isStale
  }
})