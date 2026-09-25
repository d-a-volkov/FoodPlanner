<script setup>
import { ref, computed, onMounted } from 'vue'
import { useProductsStore } from '../stores/products'

const store = useProductsStore()

const searchQuery = ref('')
const filterZone = ref('')
const filterStatus = ref('')
const showAddModal = ref(false)
const editingProduct = ref(null)

const form = ref({
  name: '',
  storageZone: 0,
  category: 10,
  stockStatus: 1,
  hasReserve: false,
  defaultUnit: 0,
  caloriesPer100g: 0,
  proteinPer100g: 0,
  fatPer100g: 0,
  carbsPer100g: 0,
  quantityInStock: 0
})

const filteredProducts = computed(() => {
  let list = store.products
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    list = list.filter(p => p.name.toLowerCase().includes(q))
  }
  if (filterZone.value !== '') {
    list = list.filter(p => p.storageZone === Number(filterZone.value))
  }
  if (filterStatus.value !== '') {
    list = list.filter(p => p.stockStatus === Number(filterStatus.value))
  }
  return list
})

const groupedProducts = computed(() => {
  const groups = {}
  for (const p of filteredProducts.value) {
    const zone = p.storageZone
    if (!groups[zone]) groups[zone] = []
    groups[zone].push(p)
  }
  return groups
})

const stats = computed(() => ({
  total: store.products.length,
  inStock: store.products.filter(p => p.stockStatus === 0).length,
  outOfStock: store.products.filter(p => p.stockStatus === 1).length,
  lowStock: store.products.filter(p => p.stockStatus === 2).length,
  notUsed: store.products.filter(p => p.stockStatus === 3).length
}))

onMounted(() => {
  store.fetchProducts()
})

function openAdd() {
  editingProduct.value = null
  form.value = {
    name: '',
    storageZone: 0,
    category: 10,
    stockStatus: 1,
    hasReserve: false,
    defaultUnit: 0,
    caloriesPer100g: 0,
    proteinPer100g: 0,
    fatPer100g: 0,
    carbsPer100g: 0,
    quantityInStock: 0
  }
  showAddModal.value = true
}

function openEdit(product) {
  editingProduct.value = product
  form.value = { ...product }
  showAddModal.value = true
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
      />
      <select v-model="filterZone" class="filter-select">
        <option value="">Все зоны</option>
        <option v-for="(name, key) in store.STORAGE_ZONES" :key="key" :value="key">{{ name }}</option>
      </select>
      <select v-model="filterStatus" class="filter-select">
        <option value="">Все статусы</option>
        <option v-for="(info, key) in store.STOCK_STATUS" :key="key" :value="key">{{ info.label }}</option>
      </select>
      <button class="btn btn-primary" @click="openAdd">+ Добавить продукт</button>
    </div>

    <div v-if="store.loading" class="loading">Загрузка...</div>
    <div v-else-if="store.error" class="error">{{ store.error }}</div>

    <div v-for="(products, zone) in groupedProducts" :key="zone" class="zone-group">
      <h2 class="zone-title">{{ store.getZoneName(Number(zone)) }}</h2>
      <table class="products-table">
        <thead>
          <tr>
            <th>Статус</th>
            <th>Название</th>
            <th>Категория</th>
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
            <td>{{ store.getCategoryName(product.category) }}</td>
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
          <div class="form-row">
            <div class="form-group">
              <label>Зона хранения</label>
              <select v-model="form.storageZone">
                <option v-for="(name, key) in store.STORAGE_ZONES" :key="key" :value="Number(key)">{{ name }}</option>
              </select>
            </div>
            <div class="form-group">
              <label>Категория</label>
              <select v-model="form.category">
                <option v-for="(name, key) in store.CATEGORIES" :key="key" :value="Number(key)">{{ name }}</option>
              </select>
            </div>
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

.zone-group {
  margin-bottom: 25px;
}

.zone-title {
  font-size: 1.1rem;
  color: #555;
  margin-bottom: 8px;
  padding-bottom: 5px;
  border-bottom: 2px solid #e0e0e0;
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
  z-index: 100;
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
  .products-table td:nth-child(3),
  .products-table td:nth-child(4) { display: none; }
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
}
</style>
