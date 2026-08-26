import { defineStore } from 'pinia'
import { ref } from 'vue'
import { harvardPlateApi } from '../api'

export const useHarvardPlateStore = defineStore('harvardPlate', () => {
  const analysis = ref(null)
  const matchingRecipes = ref([])
  const loading = ref(false)
  const error = ref(null)

  async function fetchAnalysis() {
    loading.value = true
    error.value = null
    try {
      const { data } = await harvardPlateApi.getAnalysis()
      analysis.value = data
    } catch (e) {
      error.value = e.message
    } finally {
      loading.value = false
    }
  }

  async function fetchMatchingRecipes() {
    loading.value = true
    error.value = null
    try {
      const { data } = await harvardPlateApi.getMatchingRecipes()
      matchingRecipes.value = data
    } catch (e) {
      error.value = e.message
    } finally {
      loading.value = false
    }
  }

  return { analysis, matchingRecipes, loading, error, fetchAnalysis, fetchMatchingRecipes }
})
