import { defineStore } from 'pinia'
import { ref } from 'vue'
import { externalRecipesApi } from '../api'

export const useExternalRecipesStore = defineStore('externalRecipes', () => {
  const results = ref([])
  const loading = ref(false)
  const error = ref(null)
  const mode = ref('query')
  const query = ref('')
  const source = ref('')

  async function search() {
    loading.value = true
    error.value = null
    const sourceParam = source.value === '' ? null : Number(source.value)
    try {
      let data
      if (mode.value === 'query') {
        const res = await externalRecipesApi.search(query.value.trim(), sourceParam, 10)
        data = res.data
      } else {
        const res = await externalRecipesApi.searchByAvailable(sourceParam, 10, 0)
        data = res.data
      }
      results.value = data
    } catch (e) {
      error.value = e.response?.data || e.message
      results.value = []
    } finally {
      loading.value = false
    }
  }

  async function importRecipe(result) {
    const { data } = await externalRecipesApi.importRecipe(result)
    results.value = results.value.filter(r => r !== result)
    return data
  }

  function reset() {
    results.value = []
    error.value = null
  }

  return {
    results, loading, error, mode, query, source,
    search, importRecipe, reset
  }
})