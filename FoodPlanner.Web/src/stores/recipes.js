import { defineStore } from 'pinia'
import { ref } from 'vue'
import { recipesApi } from '../api'

export const useRecipesStore = defineStore('recipes', () => {
  const recipes = ref([])
  const loading = ref(false)
  const error = ref(null)

  async function fetchRecipes() {
    loading.value = true
    error.value = null
    try {
      const { data } = await recipesApi.getAll()
      recipes.value = data
    } catch (e) {
      error.value = e.message
    } finally {
      loading.value = false
    }
  }

  async function addRecipe(recipe) {
    const { data } = await recipesApi.create(recipe)
    recipes.value.push(data)
    return data
  }

  async function updateRecipe(recipe) {
    const { data } = await recipesApi.update(recipe.id, recipe)
    const index = recipes.value.findIndex(r => r.id === data.id)
    if (index !== -1) recipes.value[index] = data
    return data
  }

  async function deleteRecipe(id) {
    await recipesApi.delete(id)
    recipes.value = recipes.value.filter(r => r.id !== id)
  }

  return { recipes, loading, error, fetchRecipes, addRecipe, updateRecipe, deleteRecipe }
})
