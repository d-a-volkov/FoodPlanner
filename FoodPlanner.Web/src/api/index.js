import axios from 'axios'

const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' }
})

export const productsApi = {
  getAll: () => api.get('/products'),
  getById: (id) => api.get(`/products/${id}`),
  getAllCategories: () => api.get('/products/categories'),
  getByCategory: (category) => api.get(`/products/category/${category}`),
  getByStatus: (status) => api.get(`/products/status/${status}`),
  search: (q) => api.get('/products/search', { params: { q } }),
  detectCategory: (name) => api.get('/products/detect-category', { params: { name } }),
  create: (product) => api.post('/products', product),
  update: (id, product) => api.put(`/products/${id}`, product),
  delete: (id) => api.delete(`/products/${id}`)
}

export const categoriesApi = {
  getAll: () => api.get('/products/categories'),
  create: (name) => api.post('/categories', { name }),
  rename: (id, name) => api.put(`/categories/${id}`, { name }),
  remove: (id) => api.delete(`/categories/${id}`)
}

export const recipesApi = {
  getAll: () => api.get('/recipes'),
  getById: (id) => api.get(`/recipes/${id}`),
  create: (recipe) => api.post('/recipes', recipe),
  update: (id, recipe) => api.put(`/recipes/${id}`, recipe),
  delete: (id) => api.delete(`/recipes/${id}`),
  match: (availableProducts) => api.post('/recipes/match', availableProducts)
}

export const harvardPlateApi = {
  getAnalysis: () => api.get('/harvardplate/analysis'),
  getMatchingRecipes: () => api.get('/harvardplate/recipes')
}

export const externalRecipesApi = {
  search: (query, source, maxResults) => api.get('/externalrecipes/search', { params: { query, source, maxResults } }),
  searchByAvailable: (source, maxResults, minAvailability) => api.get('/externalrecipes/search-by-available', { params: { source, maxResults, minAvailability } }),
  getDetails: (source, url) => api.get('/externalrecipes/detail', { params: { source, url } }),
  importRecipe: (result) => api.post('/externalrecipes/import', result)
}

export const shoppingListsApi = {
  getAll: () => api.get('/shoppinglists'),
  getById: (id) => api.get(`/shoppinglists/${id}`),
  createFromRecipe: (recipeId) => api.post(`/shoppinglists/from-recipe/${recipeId}`),
  createFromOutOfStock: (includeLowStock = false, categories = []) => {
    const params = { includeLowStock }
    if (categories.length) params.categories = categories.join(',')
    return api.post('/shoppinglists/from-out-of-stock', null, { params })
  },
  merge: (list1, list2) => api.post(`/shoppinglists/merge?list1=${list1}&list2=${list2}`),
  togglePurchased: (listId, itemId) => api.put(`/shoppinglists/${listId}/items/${itemId}/toggle`),
  delete: (id) => api.delete(`/shoppinglists/${id}`),
  deleteItem: (listId, itemId) => api.delete(`/shoppinglists/${listId}/items/${itemId}`)
}

export const kuperApi = {
  status: () => api.get('/kuper/status'),
  getSession: () => api.get('/kuper/session'),
  connectSession: (cookie, lat = 55.7558, lon = 37.6173) => api.post('/kuper/session', { cookie, lat, lon }),
  selectStore: (storeId) => api.post('/kuper/session/store', { storeId }),
  refreshHistory: () => api.post('/kuper/history/refresh'),
  resolve: (listId) => api.post('/kuper/resolve', { listId }),
  addToCart: (items) => api.post('/kuper/cart', { items }),
  getCart: () => api.get('/kuper/cart'),
  disconnect: () => api.delete('/kuper/session')
}

export default api
