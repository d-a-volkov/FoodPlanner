import { defineStore } from 'pinia'
import { ref } from 'vue'
import { shoppingListsApi } from '../api'

export const useShoppingListsStore = defineStore('shoppingLists', () => {
  const lists = ref([])
  const currentList = ref(null)
  const loading = ref(false)
  const error = ref(null)

  async function fetchLists() {
    loading.value = true
    error.value = null
    try {
      const { data } = await shoppingListsApi.getAll()
      lists.value = data
    } catch (e) {
      error.value = e.message
    } finally {
      loading.value = false
    }
  }

  async function fetchList(id) {
    loading.value = true
    error.value = null
    try {
      const { data } = await shoppingListsApi.getById(id)
      currentList.value = data
    } catch (e) {
      error.value = e.message
    } finally {
      loading.value = false
    }
  }

  async function createFromRecipe(recipeId) {
    const { data } = await shoppingListsApi.createFromRecipe(recipeId)
    lists.value.push(data)
    return data
  }

  async function createFromOutOfStock() {
    const { data } = await shoppingListsApi.createFromOutOfStock()
    lists.value.push(data)
    return data
  }

  async function togglePurchased(listId, itemId) {
    const { data } = await shoppingListsApi.togglePurchased(listId, itemId)
    if (currentList.value && currentList.value.id === listId) {
      const item = currentList.value.items.find(i => i.id === itemId)
      if (item) item.isPurchased = data.isPurchased
    }
    return data
  }

  async function deleteList(id) {
    await shoppingListsApi.delete(id)
    lists.value = lists.value.filter(l => l.id !== id)
    if (currentList.value?.id === id) currentList.value = null
  }

  return {
    lists, currentList, loading, error,
    fetchLists, fetchList, createFromRecipe, createFromOutOfStock, togglePurchased, deleteList
  }
})
