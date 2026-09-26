<script setup>
import { ref, onMounted, computed } from 'vue'
import { useShoppingListsStore } from '../stores/shoppingLists'
import { useProductsStore } from '../stores/products'

const store = useShoppingListsStore()
const productsStore = useProductsStore()
const showDetail = ref(null)

const sortedItems = computed(() => {
  const items = showDetail.value?.items || []
  return [...items].sort((a, b) => {
    if (a.isPurchased !== b.isPurchased) return a.isPurchased ? 1 : -1
    return a.productName.localeCompare(b.productName, 'ru', { sensitivity: 'base' })
  })
})

onMounted(() => {
  store.fetchLists()
  initZones()
})

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
const selectedZones = ref(new Set())

function persistSelectedZones() {
  localStorage.setItem('selectedZones', JSON.stringify([...selectedZones.value]))
}

function initZones() {
  const zoneKeys = Object.keys(productsStore.zones)
  const stored = localStorage.getItem('selectedZones')
  if (stored) {
    try {
      selectedZones.value = new Set(JSON.parse(stored).filter(k => zoneKeys.includes(k)))
      return
    } catch (e) {
      // fall through to default
    }
  }
  selectedZones.value = new Set(zoneKeys)
}

function toggleZone(key) {
  const has = selectedZones.value.has(key)
  if (has) selectedZones.value.delete(key)
  else selectedZones.value.add(key)
  persistSelectedZones()
}

function selectAllZones() {
  selectedZones.value = new Set(Object.keys(productsStore.zones))
  persistSelectedZones()
}

function clearZones() {
  selectedZones.value = new Set()
  persistSelectedZones()
}

async function createFromOutOfStock() {
  if (selectedZones.value.size === 0) {
    alert('Выберите хотя бы одну зону для формирования списка')
    return
  }
  creatingFromStock.value = true
  try {
    localStorage.setItem('includeLowStock', includeLow.value ? '1' : '0')
    persistSelectedZones()
    const allKeys = Object.keys(productsStore.zones)
    const zones = selectedZones.value.size === allKeys.length ? [] : [...selectedZones.value]
    const list = await store.createFromOutOfStock(includeLow.value, zones)
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

    <div class="zones-card">
      <div class="zones-top">
        <span class="zones-title">Зоны для включения в список</span>
        <div class="zones-actions">
          <button class="btn btn-small" @click="selectAllZones">Все</button>
          <button class="btn btn-small" @click="clearZones">Ничего</button>
        </div>
      </div>
      <div class="zones-grid">
        <label
          v-for="(name, key) in productsStore.zones"
          :key="key"
          class="zone-chip"
          :class="{ active: selectedZones.has(key) }"
        >
          <input type="checkbox" :checked="selectedZones.has(key)" @change="toggleZone(key)" />
          <span>{{ name }}</span>
        </label>
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
          <button class="btn btn-small" @click="exportToTxt(showDetail)">⬇ TXT</button>
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

.zones-card {
  background: #fff;
  border-radius: 12px;
  padding: 14px 16px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.08);
  margin-bottom: 20px;
}
.zones-top {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  margin-bottom: 12px;
}
.zones-title { font-weight: 600; font-size: 0.9rem; color: #444; }
.zones-actions { display: flex; gap: 6px; }
.zones-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.zone-chip {
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
.zone-chip.active {
  background: #e3f2fd;
  border-color: #1976d2;
  color: #1976d2;
  font-weight: 500;
}
.zone-chip input { margin: 0; cursor: pointer; }

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

  .zones-card { padding: 12px; margin-bottom: 16px; }
  .zones-title { font-size: 0.95rem; }
  .zones-grid { gap: 8px; }
  .zone-chip {
    flex: 1 1 calc(50% - 4px);
    min-width: 0;
    padding: 12px 10px;
    font-size: 0.9rem;
  }
  .zone-chip input {
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
