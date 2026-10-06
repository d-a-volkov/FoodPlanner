<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { useProductsStore } from '../stores/products'
import { productsApi } from '../api'

const store = useProductsStore()

const searchQuery = ref(localStorage.getItem('productsSearch') || '')
const filterCategory = ref(localStorage.getItem('productsFilterCategory') ?? '')
const filterStatus = ref(localStorage.getItem('productsFilterStatus') ?? '')
const showAddModal = ref(false)
const editingProduct = ref(null)

// Фильтры применяются только по явной команде (обновление страницы или
// кнопка). Применённые значения и список подходящих id фиксируются снимком:
// иначе смена статуса тут же скрыла бы строку и ошибочно изменённую запись
// нельзя было бы вернуть обратно.
const appliedFilters = ref({ query: '', category: '', status: '' })
const appliedIds = ref(null)

const form = ref({
  name: '',
  category: 0,
  stockStatus: 1,
  hasReserve: false,
  defaultUnit: 0,
  caloriesPer100g: 0,
  proteinPer100g: 0,
  fatPer100g: 0,
  carbsPer100g: 0,
  quantityInStock: 0
})

function matchesFilters(product) {
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    if (!product.name.toLowerCase().includes(q)) return false
  }
  if (filterCategory.value !== '' && product.category !== Number(filterCategory.value)) return false
  if (filterStatus.value !== '' && product.stockStatus !== Number(filterStatus.value)) return false
  return true
}

function applyFilters() {
  localStorage.setItem('productsSearch', searchQuery.value)
  localStorage.setItem('productsFilterCategory', String(filterCategory.value))
  localStorage.setItem('productsFilterStatus', String(filterStatus.value))
  const active = !!searchQuery.value || filterCategory.value !== '' || filterStatus.value !== ''
  appliedFilters.value = {
    query: searchQuery.value,
    category: filterCategory.value,
    status: filterStatus.value
  }
  appliedIds.value = active
    ? new Set(store.products.filter(matchesFilters).map(p => p.id))
    : null
}

function resetFilters() {
  searchQuery.value = ''
  filterCategory.value = ''
  filterStatus.value = ''
  applyFilters()
}

const filtersPending = computed(() => {
  const applied = appliedFilters.value
  return applied.query !== searchQuery.value
    || applied.category !== filterCategory.value
    || applied.status !== filterStatus.value
})

const filtersActive = computed(() => appliedIds.value !== null)

const filteredProducts = computed(() => {
  const ids = appliedIds.value
  if (!ids) return store.products
  return store.products.filter(p => ids.has(p.id))
})

const groupedProducts = computed(() => {
  const groups = {}
  for (const p of filteredProducts.value) {
    const category = p.category
    if (!groups[category]) groups[category] = []
    groups[category].push(p)
  }
  for (const list of Object.values(groups)) {
    list.sort((a, b) => store.rankOf(a) - store.rankOf(b))
  }
  return groups
})

const collapsedCategories = ref(JSON.parse(localStorage.getItem('collapsedCategories') || '{}'))

function isCollapsed(category) {
  return !!collapsedCategories.value[category]
}

function toggleCategory(category) {
  collapsedCategories.value[category] = !collapsedCategories.value[category]
  localStorage.setItem('collapsedCategories', JSON.stringify(collapsedCategories.value))
}

function categoryCounts(products) {
  const total = products.length
  const inStock = products.filter(p => p.stockStatus === 0).length
  return { inStock, total }
}

const stats = computed(() => ({
  total: store.products.length,
  inStock: store.products.filter(p => p.stockStatus === 0).length,
  outOfStock: store.products.filter(p => p.stockStatus === 1).length,
  lowStock: store.products.filter(p => p.stockStatus === 2).length,
  notUsed: store.products.filter(p => p.stockStatus === 3).length
}))

async function refreshAll() {
  await store.fetchProducts()
  applyFilters()
}

onMounted(async () => {
  await store.fetchCategories()
  await store.fetchProducts()
  applyFilters()
})

function openAdd() {
  editingProduct.value = null
  form.value = {
    name: '',
    category: 0,
    stockStatus: 1,
    hasReserve: false,
    defaultUnit: 0,
    caloriesPer100g: 0,
    proteinPer100g: 0,
    fatPer100g: 0,
    carbsPer100g: 0,
    quantityInStock: 0
  }
  categoryTouched.value = false
  detectedCategory.value = null
  showAddModal.value = true
}

function openEdit(product) {
  editingProduct.value = product
  form.value = { ...product }
  categoryTouched.value = true
  detectedCategory.value = product.category
  showAddModal.value = true
}

// Категорию предлагает серверный детектор (тот же, что и при авторазметке),
// но выбор всегда можно переопределить до сохранения.
let detectTimer = null
const detectedCategory = ref(null)
const categoryTouched = ref(false)

const categoryDetectedName = computed(() => {
  if (detectedCategory.value === null) return ''
  const names = store.CATEGORIES[detectedCategory.value]
  return names || ''
})

watch(() => form.value.name, (name) => {
  if (editingProduct.value || categoryTouched.value) return
  clearTimeout(detectTimer)
  const trimmed = (name || '').trim()
  if (trimmed.length < 2) {
    detectedCategory.value = null
    form.value.category = 0
    return
  }
  detectTimer = setTimeout(async () => {
    try {
      const { data } = await productsApi.detectCategory(trimmed)
      detectedCategory.value = data.category
      if (!categoryTouched.value) form.value.category = data.category
    } catch {
      detectedCategory.value = null
    }
  }, 350)
})

function onCategoryChange() {
  categoryTouched.value = true
}

// ---- управление категориями ----
const showCategoriesModal = ref(false)
const newCategoryName = ref('')
const editingCategoryId = ref(null)
const editingCategoryName = ref('')
const categoryError = ref('')
const categoryBusy = ref(false)

const customCategories = computed(() => store.categoryList.filter(c => c.isCustom))
const builtinCategories = computed(() => store.categoryList.filter(c => !c.isCustom))

function openCategories() {
  categoryError.value = ''
  newCategoryName.value = ''
  editingCategoryId.value = null
  editingCategoryName.value = ''
  showCategoriesModal.value = true
}

function startRenameCategory(cat) {
  editingCategoryId.value = cat.id
  editingCategoryName.value = cat.name
  categoryError.value = ''
}

function cancelRenameCategory() {
  editingCategoryId.value = null
  editingCategoryName.value = ''
}

function errorText(e) {
  return e?.response?.data?.error || e.message || 'Неизвестная ошибка'
}

async function addCategory() {
  const name = newCategoryName.value.trim()
  if (!name) return
  categoryBusy.value = true
  categoryError.value = ''
  try {
    await store.createCategory(name)
    newCategoryName.value = ''
  } catch (e) {
    categoryError.value = errorText(e)
  } finally {
    categoryBusy.value = false
  }
}

async function saveRenameCategory() {
  const name = editingCategoryName.value.trim()
  if (!name) return
  categoryBusy.value = true
  categoryError.value = ''
  try {
    await store.renameCategory(editingCategoryId.value, name)
    cancelRenameCategory()
  } catch (e) {
    categoryError.value = errorText(e)
  } finally {
    categoryBusy.value = false
  }
}

async function removeCategory(cat) {
  if (cat.productCount > 0) {
    categoryError.value = `В категории «${cat.name}» ещё ${cat.productCount} шт. Сначала перенесите их в другую категорию.`
    return
  }
  if (!confirm(`Удалить категорию «${cat.name}»?`)) return
  categoryBusy.value = true
  categoryError.value = ''
  try {
    await store.deleteCategory(cat.id)
  } catch (e) {
    categoryError.value = errorText(e)
  } finally {
    categoryBusy.value = false
  }
}

async function saveProduct() {
  try {
    if (editingProduct.value) {
      await store.updateProduct(form.value)
    } else {
      await store.addProduct(form.value)
    }
    showAddModal.value = false
  } catch (e) {
    alert('Ошибка сохранения: ' + e.message)
  }
}

async function removeProduct(id) {
  if (confirm('Удалить продукт?')) {
    await store.deleteProduct(id)
  }
}

async function toggleStock(product) {
  await store.toggleStock(product)
}
</script>

<template>
  <div>
    <h1>Учет продуктов</h1>

    <div class="stats">
      <div class="stat total">Всего: <strong>{{ stats.total }}</strong></div>
      <div class="stat in-stock">В наличии: <strong>{{ stats.inStock }}</strong></div>
      <div class="stat out-of-stock">Нет: <strong>{{ stats.outOfStock }}</strong></div>
      <div class="stat low-stock">Мало: <strong>{{ stats.lowStock }}</strong></div>
      <div class="stat not-used">Не используется: <strong>{{ stats.notUsed }}</strong></div>
    </div>

    <div class="toolbar">
      <input
        v-model="searchQuery"
        type="text"
        placeholder="Поиск продукта..."
        class="search-input"
        @keyup.enter="applyFilters"
      />
      <select v-model="filterCategory" class="filter-select">
        <option value="">Все категории</option>
        <option v-for="(name, key) in store.CATEGORIES" :key="key" :value="key">{{ name }}</option>
      </select>
      <select v-model="filterStatus" class="filter-select">
        <option value="">Все статусы</option>
        <option v-for="(info, key) in store.STOCK_STATUS" :key="key" :value="key">{{ info.label }}</option>
      </select>
      <button class="btn btn-primary" :disabled="!filtersPending" @click="applyFilters">
        Применить фильтры
      </button>
      <button class="btn" :disabled="!filtersPending && !filtersActive" @click="resetFilters">Сбросить</button>
      <button class="btn" @click="refreshAll">Обновить</button>
      <button class="btn" @click="openCategories">Категории</button>
      <button class="btn btn-primary" @click="openAdd">+ Добавить продукт</button>
    </div>

    <p v-if="filtersPending" class="filters-hint pending">
      Фильтры изменены, но пока не применены — список прежний. Нажмите «Применить фильтры».
    </p>
    <p v-else-if="filtersActive" class="filters-hint">
      Фильтры применены при загрузке: {{ filteredProducts.length }} из {{ store.products.length }}.
      Изменённый статус не скрывает запись — чтобы отфильтровать заново, нажмите «Применить фильтры» или «Обновить».
    </p>

    <div v-if="store.loading" class="loading">Загрузка...</div>
    <div v-else-if="store.error" class="error">{{ store.error }}</div>

    <div v-for="(products, category) in groupedProducts" :key="category" class="category-group">
      <button type="button" class="category-header" @click="toggleCategory(category)">
        <span class="category-caret">{{ isCollapsed(category) ? '▸' : '▾' }}</span>
        <span class="category-name">{{ store.getCategoryName(Number(category)) }}</span>
        <span class="category-counts">{{ categoryCounts(products).inStock }} в наличии / {{ categoryCounts(products).total }} всего</span>
      </button>
      <table v-if="!isCollapsed(category)" class="products-table">
        <thead>
          <tr>
            <th>Статус</th>
            <th>Название</th>
            <th>Запас</th>
            <th>Действия</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="product in products" :key="product.id">
            <td>
              <span
                class="status-badge"
                :class="{
                  'in-stock': product.stockStatus === 0,
                  'out-of-stock': product.stockStatus === 1,
                  'low-stock': product.stockStatus === 2,
                  'not-used': product.stockStatus === 3
                }"
                @click="toggleStock(product)"
                title="Нажмите, чтобы изменить статус"
              >
                {{ store.getStatusInfo(product.stockStatus).icon }}
              </span>
            </td>
            <td class="product-name">{{ product.name }}</td>
            <td>
              <span v-if="product.hasReserve" class="reserve-badge">+</span>
            </td>
            <td>
              <button class="btn btn-small" @click="openEdit(product)">✏️</button>
              <button class="btn btn-small btn-danger" @click="removeProduct(product.id)">🗑</button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-if="Object.keys(groupedProducts).length === 0 && !store.loading" class="empty">
      Продуктов не найдено
    </div>

    <div v-if="showAddModal" class="modal-overlay" @click.self="showAddModal = false">
      <div class="modal">
        <h2>{{ editingProduct ? 'Редактировать продукт' : 'Добавить продукт' }}</h2>
        <form @submit.prevent="saveProduct">
          <div class="form-group">
            <label>Название</label>
            <input v-model="form.name" type="text" required />
          </div>
          <div class="form-group">
            <label>Категория</label>
            <select v-model="form.category" @change="onCategoryChange">
              <option v-for="(name, key) in store.CATEGORIES" :key="key" :value="Number(key)">{{ name }}</option>
            </select>
            <p v-if="!categoryTouched && categoryDetectedName" class="form-hint auto-category">
              Определено автоматически: <strong>{{ categoryDetectedName }}</strong>. Подтвердите или выберите другую категорию.
            </p>
            <p v-else-if="editingProduct" class="form-hint">Категория сохранится как выбрана.</p>
            <p v-else class="form-hint">Категория подставляется по названию. Если предложена неверно — выберите нужную.</p>
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>Статус</label>
              <select v-model="form.stockStatus">
                <option v-for="(info, key) in store.STOCK_STATUS" :key="key" :value="Number(key)">{{ info.label }}</option>
              </select>
            </div>
            <div class="form-group">
              <label><input type="checkbox" v-model="form.hasReserve" /> Есть запас</label>
            </div>
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>Калории (на 100г)</label>
              <input v-model.number="form.caloriesPer100g" type="number" />
            </div>
            <div class="form-group">
              <label>Белки (на 100г)</label>
              <input v-model.number="form.proteinPer100g" type="number" />
            </div>
            <div class="form-group">
              <label>Жиры (на 100г)</label>
              <input v-model.number="form.fatPer100g" type="number" />
            </div>
            <div class="form-group">
              <label>Углеводы (на 100г)</label>
              <input v-model.number="form.carbsPer100g" type="number" />
            </div>
          </div>
          <div class="modal-actions">
            <button type="button" class="btn" @click="showAddModal = false">Отмена</button>
            <button type="submit" class="btn btn-primary">Сохранить</button>
          </div>
        </form>
      </div>
    </div>

    <div v-if="showCategoriesModal" class="modal-overlay" @click.self="showCategoriesModal = false">
      <div class="modal modal-categories">
        <h2>Категории продуктов</h2>

        <p class="form-hint">
          Встроенные категории (овощи, мясо, молочное и т.д.) удалить нельзя — на них завязаны
          автоопределение и фильтры. Свои категории можно добавлять, переименовывать и удалять,
          пока в них пусто.
        </p>

        <div class="category-add-row">
          <input
            v-model="newCategoryName"
            type="text"
            maxlength="60"
            placeholder="Название новой категории"
            @keyup.enter="addCategory"
          />
          <button type="button" class="btn btn-primary" :disabled="categoryBusy || !newCategoryName.trim()" @click="addCategory">
            Добавить
          </button>
        </div>

        <p v-if="categoryError" class="category-error">{{ categoryError }}</p>

        <h3 class="cat-section-title">Свои категории ({{ customCategories.length }})</h3>
        <p v-if="!customCategories.length" class="form-hint">Пока нет ни одной своей категории.</p>
        <ul class="cat-list">
          <li v-for="cat in customCategories" :key="cat.id" class="cat-item">
            <template v-if="editingCategoryId === cat.id">
              <input v-model="editingCategoryName" type="text" maxlength="60" @keyup.enter="saveRenameCategory" @keyup.esc="cancelRenameCategory" />
              <span class="cat-count">{{ cat.productCount }} шт.</span>
              <button type="button" class="btn btn-primary" :disabled="categoryBusy" @click="saveRenameCategory">ОК</button>
              <button type="button" class="btn" @click="cancelRenameCategory">Отмена</button>
            </template>
            <template v-else>
              <span class="cat-name">{{ cat.name }}</span>
              <span class="cat-count">{{ cat.productCount }} шт.</span>
              <button type="button" class="btn" @click="startRenameCategory(cat)">Переименовать</button>
              <button type="button" class="btn btn-danger" :disabled="categoryBusy" @click="removeCategory(cat)">Удалить</button>
            </template>
          </li>
        </ul>

        <h3 class="cat-section-title">Встроенные ({{ builtinCategories.length }})</h3>
        <ul class="cat-list cat-list-builtin">
          <li v-for="cat in builtinCategories" :key="cat.id" class="cat-item">
            <span class="cat-name">{{ cat.name }}</span>
            <span class="cat-count">{{ cat.productCount }} шт.</span>
          </li>
        </ul>

        <div class="modal-actions">
          <button type="button" class="btn" @click="showCategoriesModal = false">Закрыть</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
h1 { margin-top: 0; color: #333; }

.stats {
  display: flex;
  gap: 15px;
  margin-bottom: 20px;
}
.stat {
  padding: 10px 18px;
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 1px 3px rgba(0,0,0,0.1);
}
.stat.total { border-left: 4px solid #666; }
.stat.in-stock { border-left: 4px solid #4caf50; }
.stat.out-of-stock { border-left: 4px solid #f44336; }
.stat.low-stock { border-left: 4px solid #ff9800; }
.stat.not-used { border-left: 4px solid #9e9e9e; }

.toolbar {
  display: flex;
  gap: 10px;
  margin-bottom: 20px;
  flex-wrap: wrap;
}

.search-input {
  flex: 1;
  min-width: 200px;
  padding: 8px 12px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
}

.filter-select {
  padding: 8px 12px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
}

.category-group {
  margin-bottom: 25px;
}

.category-header {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  background: none;
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1.1rem;
  color: #555;
  margin-bottom: 8px;
  padding: 2px 0 5px;
  border-bottom: 2px solid #e0e0e0;
  font-family: inherit;
  -webkit-tap-highlight-color: transparent;
}

.category-caret {
  font-size: 0.75rem;
  color: #999;
  width: 14px;
  flex-shrink: 0;
  transition: transform 0.15s;
}

.category-name { font-weight: 600; color: #444; margin-right: auto; }

.category-counts {
  font-size: 0.8rem;
  color: #666;
  font-weight: 400;
  white-space: nowrap;
  background: #fff;
  border-radius: 12px;
  padding: 3px 10px;
  box-shadow: 0 1px 2px rgba(0,0,0,0.08);
}

.products-table {
  width: 100%;
  background: #fff;
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 1px 3px rgba(0,0,0,0.08);
  border-collapse: collapse;
}

.products-table th {
  background: #f8f9fa;
  padding: 10px 12px;
  text-align: left;
  font-size: 0.85rem;
  color: #666;
  font-weight: 600;
}

.products-table td {
  padding: 10px 12px;
  border-top: 1px solid #eee;
  font-size: 0.9rem;
}

.status-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  cursor: pointer;
  font-weight: bold;
  font-size: 1rem;
  transition: transform 0.15s;
}
.status-badge:hover { transform: scale(1.2); }
.status-badge.in-stock { background: #e8f5e9; color: #4caf50; }
.status-badge.out-of-stock { background: #ffebee; color: #f44336; }
.status-badge.low-stock { background: #fff3e0; color: #ff9800; }
.status-badge.not-used { background: #eceff1; color: #90a4ae; }

.product-name { font-weight: 500; }

.reserve-badge {
  display: inline-block;
  background: #e3f2fd;
  color: #1976d2;
  border-radius: 4px;
  padding: 2px 6px;
  font-weight: bold;
}

.form-hint {
  font-size: 0.8rem;
  color: #888;
  margin: 0 0 12px;
}
.form-hint.auto-category {
  color: #1976d2;
  background: #e3f2fd;
  border: 1px solid #bbdefb;
  border-radius: 6px;
  padding: 6px 8px;
  margin: 6px 0 0;
}

.btn {
  padding: 8px 16px;
  border: 1px solid #ddd;
  border-radius: 6px;
  background: #fff;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.2s;
}
.btn:hover { background: #f5f5f5; }
.btn-primary { background: #1976d2; color: #fff; border-color: #1976d2; }
.btn-primary:hover { background: #1565c0; }
.btn-danger { color: #f44336; border-color: #f44336; }
.btn-danger:hover { background: #ffebee; }
.btn-small { padding: 4px 8px; font-size: 12px; margin-right: 4px; }
.btn:disabled { opacity: 0.45; cursor: not-allowed; }
.btn:disabled:hover { background: #fff; }
.btn-primary:disabled:hover { background: #1976d2; }
.filters-hint {
  margin: -6px 0 4px;
  font-size: 12px;
  color: #666;
}
.filters-hint.pending { color: #e65100; font-weight: 500; }

.loading, .error, .empty {
  text-align: center;
  padding: 40px;
  color: #666;
  font-size: 1.1rem;
}
.error { color: #f44336; }

.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 300;
}
.modal {
  background: #fff;
  border-radius: 12px;
  padding: 30px;
  width: 90%;
  max-width: 600px;
  max-height: 90vh;
  overflow-y: auto;
}
.modal h2 { margin-top: 0; }
.modal-categories { max-width: 560px; }
.category-add-row { display: flex; gap: 8px; margin-bottom: 10px; }
.category-add-row input {
  flex: 1;
  padding: 8px 10px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 0.9rem;
  font-family: inherit;
}
.category-add-row input:focus { outline: none; border-color: #1976d2; }
.category-error {
  color: #c62828;
  background: #ffebee;
  border: 1px solid #ffcdd2;
  border-radius: 6px;
  padding: 8px 10px;
  font-size: 0.85rem;
  margin: 0 0 10px;
}
.cat-section-title { font-size: 0.9rem; color: #555; margin: 14px 0 6px; }
.cat-list { list-style: none; margin: 0 0 4px; padding: 0; }
.cat-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 0;
  border-bottom: 1px solid #f0f0f0;
}
.cat-name { flex: 1; min-width: 0; font-size: 0.9rem; color: #333; }
.cat-item input {
  flex: 1;
  min-width: 0;
  padding: 6px 8px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 0.9rem;
  font-family: inherit;
}
.cat-item input:focus { outline: none; border-color: #1976d2; }
.cat-count { font-size: 0.8rem; color: #888; white-space: nowrap; }
.cat-list-builtin .cat-name { color: #777; }
.form-group { margin-bottom: 12px; }
.form-group label { display: block; font-size: 0.85rem; color: #555; margin-bottom: 4px; }
.form-group input, .form-group select {
  width: 100%;
  padding: 8px 10px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
  box-sizing: border-box;
}
.form-row {
  display: flex;
  gap: 12px;
}
.form-row .form-group { flex: 1; }
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
}

@media (max-width: 767px) {
  h1 { font-size: 1.4rem; }

  .stats {
    flex-wrap: wrap;
    gap: 8px;
  }
  .stat {
    flex: 1 1 40%;
    min-width: 120px;
    padding: 10px 12px;
    font-size: 0.85rem;
  }

  .toolbar { gap: 8px; }
  .search-input { min-width: 100%; order: -1; }
  .filter-select { flex: 1; }
  .toolbar .btn { flex: 1; }

  .products-table,
  .products-table tbody {
    display: block;
    width: 100%;
    background: transparent;
    box-shadow: none;
  }
  .products-table thead { display: none; }
  .products-table tr {
    display: grid;
    grid-template-columns: 34px 1fr auto;
    gap: 8px;
    align-items: center;
    background: #fff;
    border-radius: 10px;
    padding: 10px 12px;
    margin-bottom: 8px;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
  }
  .products-table td {
    border-top: none;
    padding: 0;
  }
  .products-table td:nth-child(3) { display: none; }
  .status-badge { width: 32px; height: 32px; }
  .btn-small { padding: 6px 10px; }

  .modal-overlay {
    align-items: flex-end;
  }
  .modal {
    width: 100%;
    max-width: none;
    max-height: 92vh;
    border-radius: 16px 16px 0 0;
    padding: 20px 18px calc(20px + env(safe-area-inset-bottom));
  }
  .form-row {
    flex-direction: column;
    gap: 12px;
  }
  .modal-actions .btn {
    flex: 1;
    padding: 12px;
    font-size: 15px;
  }

  .category-header {
    padding: 12px 4px;
    font-size: 1rem;
  }
  .category-counts {
    font-size: 0.72rem;
    padding: 3px 8px;
  }
}
</style>