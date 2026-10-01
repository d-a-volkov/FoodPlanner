<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useShoppingListsStore } from '../stores/shoppingLists'
import { useProductsStore } from '../stores/products'
import KuperCartModal from '../components/KuperCartModal.vue'

const store = useShoppingListsStore()
const productsStore = useProductsStore()
const showDetail = ref(null)
const showKuper = ref(false)

const sortedItems = computed(() => {
  const items = showDetail.value?.items || []
  return [...items].sort((a, b) => {
    if (a.isPurchased !== b.isPurchased) return a.isPurchased ? 1 : -1
    return a.productName.localeCompare(b.productName, 'ru', { sensitivity: 'base' })
  })
})

let syncTimer = null

onMounted(() => {
  store.fetchLists()
  productsStore.fetchCategories()
  initCategories()
  syncTimer = setInterval(syncLists, 10000)
})

onUnmounted(() => {
  clearInterval(syncTimer)
})

async function syncLists() {
  await store.silentFetchLists()
  if (showDetail.value) {
    const fresh = store.lists.find(l => l.id === showDetail.value.id)
    if (fresh) showDetail.value = fresh
    else showDetail.value = null
  }
}

function openDetail(list) {
  showDetail.value = list
}

async function togglePurchased(listId, itemId) {
  await store.togglePurchased(listId, itemId)
}

function purchasedCount(list) {
  return list.items?.filter(i => i.isPurchased).length || 0
}

function totalCount(list) {
  return list.items?.length || 0
}

function progressPercent(list) {
  if (!list.items?.length) return 0
  return Math.round(purchasedCount(list) / totalCount(list) * 100)
}

async function removeList(id) {
  if (confirm('Удалить список покупок?')) {
    await store.deleteList(id)
    if (showDetail.value?.id === id) showDetail.value = null
  }
}

const creatingFromStock = ref(false)
const includeLow = ref(localStorage.getItem('includeLowStock') === '1')
const selectedCategories = ref(new Set())
const categoriesCollapsed = ref(localStorage.getItem('collapsedCategoriesList') === '1')

// логические группы категорий, каждая сворачивается независимо
const CATEGORY_GROUPS = [
  { key: 'produce', label: 'Овощи, фрукты, зелень', ids: ['0', '1', '2'] },
  { key: 'protein', label: 'Мясо, птица, рыба, яйца', ids: ['3', '4', '5', '6', '7'] },
  { key: 'dairy', label: 'Молочное и масла', ids: ['8', '13'] },
  { key: 'grain', label: 'Крупы, выпечка, бобовые, орехи', ids: ['9', '10', '11', '12'] },
  { key: 'spices', label: 'Специи и сладости', ids: ['14', '17'] },
  { key: 'canned', label: 'Консервы и замороженное', ids: ['15', '16'] },
  { key: 'drinks', label: 'Напитки', ids: ['18'] },
  { key: 'other', label: 'Прочее', ids: ['19'] }
]

const collapsedGroups = ref(JSON.parse(localStorage.getItem('collapsedCategoryGroups') || '{}'))

const categoryGroups = computed(() => {
  const all = productsStore.CATEGORIES
  const used = new Set()
  const groups = []

  for (const g of CATEGORY_GROUPS) {
    const items = g.ids
      .filter(id => id in all)
      .map(id => { used.add(id); return { key: id, name: all[id] } })
    if (items.length) groups.push({ ...g, items })
  }

  const rest = Object.keys(all).filter(k => !used.has(k))
  if (rest.length) {
    groups.push({ key: 'rest', label: 'Другие категории', items: rest.map(k => ({ key: k, name: all[k] })) })
  }
  return groups
})

function isGroupCollapsed(key) {
  return !!collapsedGroups.value[key]
}

function toggleGroupCollapse(key) {
  collapsedGroups.value[key] = !collapsedGroups.value[key]
  localStorage.setItem('collapsedCategoryGroups', JSON.stringify(collapsedGroups.value))
}

function collapseAllGroups(collapse) {
  const next = {}
  if (collapse) for (const g of categoryGroups.value) next[g.key] = true
  collapsedGroups.value = next
  localStorage.setItem('collapsedCategoryGroups', JSON.stringify(next))
}

function groupSelectedCount(group) {
  return group.items.filter(i => selectedCategories.value.has(i.key)).length
}

function isGroupFullySelected(group) {
  return group.items.length > 0 && groupSelectedCount(group) === group.items.length
}

function toggleGroup(group) {
  if (isGroupFullySelected(group)) {
    for (const i of group.items) selectedCategories.value.delete(i.key)
  } else {
    for (const i of group.items) selectedCategories.value.add(i.key)
  }
  persistSelectedCategories()
}

function persistSelectedCategories() {
  localStorage.setItem('selectedCategories', JSON.stringify([...selectedCategories.value]))
}

function initCategories() {
  const categoryKeys = Object.keys(productsStore.CATEGORIES)
  const stored = localStorage.getItem('selectedCategories')
  if (stored) {
    try {
      selectedCategories.value = new Set(JSON.parse(stored).filter(k => categoryKeys.includes(k)))
      return
    } catch (e) {
      // fall through to default
    }
  }
  selectedCategories.value = new Set(categoryKeys)
}

function toggleCategory(key) {
  const has = selectedCategories.value.has(key)
  if (has) selectedCategories.value.delete(key)
  else selectedCategories.value.add(key)
  persistSelectedCategories()
}

function selectAllCategories() {
  selectedCategories.value = new Set(Object.keys(productsStore.CATEGORIES))
  persistSelectedCategories()
}

function clearCategories() {
  selectedCategories.value = new Set()
  persistSelectedCategories()
}

function toggleCategoriesCard() {
  categoriesCollapsed.value = !categoriesCollapsed.value
  localStorage.setItem('collapsedCategoriesList', categoriesCollapsed.value ? '1' : '0')
}

async function createFromOutOfStock() {
  if (selectedCategories.value.size === 0) {
    alert('Выберите хотя бы одну категорию для формирования списка')
    return
  }
  creatingFromStock.value = true
  try {
    localStorage.setItem('includeLowStock', includeLow.value ? '1' : '0')
    persistSelectedCategories()
    const allKeys = Object.keys(productsStore.CATEGORIES)
    const categories = selectedCategories.value.size === allKeys.length ? [] : [...selectedCategories.value]
    const list = await store.createFromOutOfStock(includeLow.value, categories)
    showDetail.value = list
  } catch (e) {
    alert(e.response?.data || e.message)
  } finally {
    creatingFromStock.value = false
  }
}

async function removeItemFromList(itemId) {
  const item = showDetail.value?.items.find(i => i.id === itemId)
  if (!item) return
  if (!confirm(`Удалить «${item.productName}» из списка?`)) return
  await store.removeItem(showDetail.value.id, itemId)
}

function exportToTxt(list) {
  const lines = []
  lines.push(`Список покупок: ${list.name}`)
  lines.push(`Создан: ${new Date(list.createdDate).toLocaleDateString('ru-RU')}`)
  lines.push('')
  ;(list.items || []).forEach((item, i) => {
    const mark = item.isPurchased ? '✓' : '☐'
    lines.push(
      `${i + 1}. ${mark} ${item.productName} — ${item.amount} ед.` +
      (item.sourceRecipeName ? ` (из "${item.sourceRecipeName}")` : '')
    )
  })
  lines.push('')
  lines.push(`Куплено: ${purchasedCount(list)} из ${totalCount(list)}`)

  const content = lines.join('\r\n')
  const blob = new Blob([content], { type: 'text/plain;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = (list.name.replace(/[\\/:*?"<>|]/g, '_').trim() || 'spisok-pokupok') + '.txt'
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div>
    <h1>Списки покупок</h1>

    <div class="toolbar">
      <button class="btn btn-primary" :disabled="creatingFromStock" @click="createFromOutOfStock">
        {{ creatingFromStock ? 'Создание...' : '🛒 Сформировать из отсутствующих' }}
      </button>
      <label class="toolbar-toggle">
        <input v-model="includeLow" type="checkbox" />
        Включать продукты со статусом «Мало»
      </label>
    </div>

    <div class="categories-card">
      <button type="button" class="categories-top" @click="toggleCategoriesCard">
        <span class="categories-caret">{{ categoriesCollapsed ? '▸' : '▾' }}</span>
        <span class="categories-title">
          Категории для включения в список
          <span v-if="categoriesCollapsed" class="categories-summary">
            ({{ selectedCategories.size }})
          </span>
        </span>
        <span class="categories-actions" @click.stop>
          <button class="btn btn-small" @click="selectAllCategories">Все</button>
          <button class="btn btn-small" @click="clearCategories">Ничего</button>
          <button class="btn btn-small" @click="collapseAllGroups(true)">Свернуть</button>
          <button class="btn btn-small" @click="collapseAllGroups(false)">Развернуть</button>
        </span>
      </button>
      <div v-if="!categoriesCollapsed" class="categories-groups">
        <div v-for="group in categoryGroups" :key="group.key" class="category-group-block">
          <div class="category-group-head">
            <button
              type="button"
              class="group-caret-btn"
              :aria-expanded="!isGroupCollapsed(group.key)"
              @click="toggleGroupCollapse(group.key)"
            >
              <span class="categories-caret">{{ isGroupCollapsed(group.key) ? '▸' : '▾' }}</span>
              <span class="group-label">{{ group.label }}</span>
            </button>
            <label class="group-toggle" :class="{ active: isGroupFullySelected(group) }">
              <input
                type="checkbox"
                :checked="isGroupFullySelected(group)"
                :indeterminate="groupSelectedCount(group) > 0 && !isGroupFullySelected(group)"
                @change="toggleGroup(group)"
              />
              <span class="group-count">{{ groupSelectedCount(group) }}/{{ group.items.length }}</span>
            </label>
          </div>
          <div v-if="!isGroupCollapsed(group.key)" class="categories-grid">
            <label
              v-for="item in group.items"
              :key="item.key"
              class="category-chip"
              :class="{ active: selectedCategories.has(item.key) }"
            >
              <input type="checkbox" :checked="selectedCategories.has(item.key)" @change="toggleCategory(item.key)" />
              <span>{{ item.name }}</span>
            </label>
          </div>
        </div>
      </div>
    </div>

    <div v-if="store.loading" class="loading">Загрузка...</div>

    <div v-else-if="store.lists.length === 0" class="empty">
      Списков покупок пока нет. Создайте из рецепта на странице "Рецепты", "Гарвардская тарелка" или нажмите кнопку выше.
    </div>

    <div v-else class="lists-layout">
      <div class="lists-sidebar">
        <div
          v-for="list in store.lists"
          :key="list.id"
          class="list-card"
          :class="{ active: showDetail?.id === list.id }"
          @click="openDetail(list)"
        >
          <div class="list-card-header">
            <span class="list-name">{{ list.name }}</span>
            <button class="btn-remove" @click.stop="removeList(list.id)">✕</button>
          </div>
          <div class="list-date">{{ new Date(list.createdDate).toLocaleDateString('ru-RU') }}</div>
          <div class="list-progress">
            <div class="progress-bar">
              <div class="progress-fill" :style="{ width: progressPercent(list) + '%' }"></div>
            </div>
            <span class="progress-text">{{ purchasedCount(list) }}/{{ totalCount(list) }}</span>
          </div>
        </div>
      </div>

      <div v-if="showDetail" class="list-detail">
        <div class="detail-top">
          <h2>{{ showDetail.name }}</h2>
          <div class="detail-top-actions">
            <button class="btn btn-small" @click="exportToTxt(showDetail)">⬇ TXT</button>
            <button
              class="btn btn-small btn-primary"
              :disabled="!showDetail.items?.length"
              title="Собрать корзину в интернет-магазине Купер"
              @click="showKuper = true"
            >
              🛒 Купер
            </button>
          </div>
        </div>
        <div class="detail-date">Создан: {{ new Date(showDetail.createdDate).toLocaleDateString('ru-RU') }}</div>

        <div class="detail-progress">
          <div class="progress-bar large">
            <div class="progress-fill" :style="{ width: progressPercent(showDetail) + '%' }"></div>
          </div>
          <span>{{ purchasedCount(showDetail) }} из {{ totalCount(showDetail) }} куплено</span>
        </div>

        <div class="items-list">
          <div
            v-for="item in sortedItems"
            :key="item.id"
            class="item-row"
            :class="{ purchased: item.isPurchased }"
          >
            <label class="checkbox-label">
              <input
                type="checkbox"
                :checked="item.isPurchased"
                @change="togglePurchased(showDetail.id, item.id)"
              />
              <span class="item-checkmark"></span>
            </label>
            <div class="item-info">
              <span class="item-name">{{ item.productName }}</span>
              <span class="item-amount">{{ item.amount }} ед.</span>
              <span v-if="item.sourceRecipeName" class="item-source">из "{{ item.sourceRecipeName }}"</span>
            </div>
            <button type="button" class="item-delete" title="Удалить из списка" @click="removeItemFromList(item.id)">✕</button>
          </div>
        </div>
      </div>

      <div v-else class="list-detail empty-detail">
        <p>Выберите список покупок слева</p>
      </div>
    </div>

    <KuperCartModal :show="showKuper" :list="showDetail || { id: '' }" @close="showKuper = false" />
  </div>
</template>

<style scoped>
h1 { margin-top: 0; color: #333; }
h2 { margin-top: 0; }

.toolbar {
  display: flex;
  gap: 10px;
  margin-bottom: 20px;
  flex-wrap: wrap;
  align-items: center;
}

.toolbar-toggle {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 0.85rem;
  color: #555;
  cursor: pointer;
  user-select: none;
  white-space: nowrap;
}
.toolbar-toggle input {
  width: 16px;
  height: 16px;
  cursor: pointer;
}

.categories-card {
  background: #fff;
  border-radius: 12px;
  padding: 14px 16px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.08);
  margin-bottom: 20px;
}
.categories-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  margin-bottom: 12px;
  width: 100%;
  background: none;
  border: none;
  cursor: pointer;
  padding: 0;
  text-align: left;
  font-family: inherit;
  -webkit-tap-highlight-color: transparent;
}
.categories-caret {
  font-size: 0.75rem;
  color: #999;
  flex-shrink: 0;
}
.categories-title { font-weight: 600; font-size: 0.9rem; color: #444; }
.categories-summary { font-weight: 400; color: #888; }
.categories-actions { display: flex; gap: 6px; flex-wrap: wrap; }
.categories-groups { display: flex; flex-direction: column; gap: 10px; }
.category-group-block {
  border: 1px solid #e8e8e8;
  border-radius: 8px;
  padding: 8px 10px;
  background: #fafafa;
}
.category-group-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}
.group-caret-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  min-width: 0;
  background: none;
  border: none;
  padding: 2px 0;
  cursor: pointer;
  text-align: left;
  font-family: inherit;
  -webkit-tap-highlight-color: transparent;
}
.group-label { font-weight: 600; font-size: 0.85rem; color: #444; }
.group-toggle {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 8px;
  border: 1px solid #e0e0e0;
  border-radius: 6px;
  background: #fff;
  cursor: pointer;
  flex-shrink: 0;
}
.group-toggle.active { background: #e3f2fd; border-color: #1976d2; }
.group-toggle input { margin: 0; cursor: pointer; }
.group-count { font-size: 0.75rem; color: #888; font-variant-numeric: tabular-nums; }
.group-toggle.active .group-count { color: #1976d2; }
.categories-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 8px;
}
.category-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 12px;
  background: #f5f5f5;
  border-radius: 8px;
  border: 1px solid #e0e0e0;
  cursor: pointer;
  font-size: 0.85rem;
  user-select: none;
  color: #555;
}
.category-chip.active {
  background: #e3f2fd;
  border-color: #1976d2;
  color: #1976d2;
  font-weight: 500;
}
.category-chip input { margin: 0; cursor: pointer; }

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
.btn:disabled { opacity: 0.6; cursor: not-allowed; }
.btn-small { padding: 4px 10px; font-size: 12px; }

.lists-layout {
  display: flex;
  gap: 20px;
  min-height: 400px;
}

.lists-sidebar {
  width: 300px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.list-card {
  background: #fff;
  border-radius: 10px;
  padding: 14px;
  cursor: pointer;
  box-shadow: 0 1px 3px rgba(0,0,0,0.06);
  transition: all 0.2s;
  border: 2px solid transparent;
}
.list-card:hover { border-color: #90caf9; }
.list-card.active { border-color: #1976d2; background: #e3f2fd; }

.list-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.list-name { font-weight: 600; font-size: 0.95rem; }
.list-date { font-size: 0.8rem; color: #999; margin: 4px 0 8px; }
.list-progress { display: flex; align-items: center; gap: 8px; }
.progress-text { font-size: 0.8rem; color: #666; }

.progress-bar {
  flex: 1;
  height: 6px;
  background: #eee;
  border-radius: 3px;
  overflow: hidden;
}
.progress-bar.large { height: 10px; }
.progress-fill {
  height: 100%;
  background: #4caf50;
  border-radius: 3px;
  transition: width 0.3s;
}

.list-detail {
  flex: 1;
  background: #fff;
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.08);
}

.detail-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
}
.detail-top h2 { margin: 0; overflow-wrap: anywhere; }
.detail-top-actions { display: flex; gap: 8px; flex-shrink: 0; flex-wrap: wrap; }

.detail-date { font-size: 0.85rem; color: #999; margin-bottom: 15px; }

.detail-progress {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 20px;
}
.detail-progress span { font-size: 0.9rem; color: #555; }

.items-list { display: flex; flex-direction: column; gap: 4px; }

.item-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 12px;
  border-radius: 8px;
  transition: background 0.15s;
}
.item-row:hover { background: #f5f5f5; }
.item-row.purchased { opacity: 0.5; }
.item-row.purchased .item-name { text-decoration: line-through; }

.checkbox-label {
  position: relative;
  cursor: pointer;
}
.checkbox-label input { display: none; }
.item-checkmark {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border: 2px solid #ccc;
  border-radius: 6px;
  font-size: 14px;
  transition: all 0.15s;
}
.checkbox-label input:checked + .item-checkmark {
  background: #4caf50;
  border-color: #4caf50;
  color: #fff;
}
.checkbox-label input:checked + .item-checkmark::after { content: '✓'; }

.item-info { display: flex; align-items: center; gap: 10px; flex: 1; }
.item-name { font-weight: 500; }
.item-amount { color: #888; font-size: 0.85rem; }
.item-source { color: #aaa; font-size: 0.8rem; font-style: italic; }

.item-delete {
  background: none;
  border: none;
  color: #ccc;
  cursor: pointer;
  font-size: 15px;
  padding: 6px 8px;
  flex-shrink: 0;
  border-radius: 6px;
}
.item-row:hover .item-delete { color: #f44336; }
.item-delete:active { background: #ffebee; color: #f44336; }

.btn-remove {
  background: none;
  border: none;
  color: #ccc;
  cursor: pointer;
  font-size: 14px;
  padding: 2px 6px;
}
.btn-remove:hover { color: #f44336; }

.empty-detail {
  display: flex;
  align-items: center;
  justify-content: center;
  color: #999;
}

.loading, .empty {
  text-align: center;
  padding: 40px;
  color: #666;
}

@media (max-width: 767px) {
  h1 { font-size: 1.4rem; }

  .toolbar .btn { flex: 1; padding: 12px; font-size: 14px; }
  .toolbar-toggle {
    flex-basis: 100%;
    font-size: 0.9rem;
    padding: 8px 4px;
  }
  .toolbar-toggle input { width: 20px; height: 20px; }

  .categories-card { padding: 12px; margin-bottom: 16px; }
  .categories-title { font-size: 0.95rem; }
  .categories-grid { gap: 8px; }
  .category-chip {
    flex: 1 1 calc(50% - 4px);
    min-width: 0;
    padding: 12px 10px;
    font-size: 0.9rem;
  }
  .category-chip input {
    width: 20px;
    height: 20px;
  }

  .lists-layout {
    flex-direction: column;
    gap: 14px;
    min-height: 0;
  }

  .lists-sidebar {
    width: 100%;
    flex-direction: row;
    overflow-x: auto;
    padding-bottom: 4px;
  }
  .list-card {
    min-width: 230px;
    flex-shrink: 0;
  }

  .list-detail { padding: 16px; }
  .detail-top { align-items: flex-start; }
  .detail-top .btn { padding: 8px 12px; font-size: 13px; flex-shrink: 0; }
  .item-info { flex-wrap: wrap; row-gap: 2px; }
  .item-source { flex-basis: 100%; }
  .item-row { padding: 12px; }
  .item-checkmark { width: 28px; height: 28px; }
}
</style>