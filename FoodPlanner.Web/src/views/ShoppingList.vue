<script setup>
import { ref, onMounted, computed } from 'vue'
import { useShoppingListsStore } from '../stores/shoppingLists'

const store = useShoppingListsStore()
const showDetail = ref(null)

onMounted(() => {
  store.fetchLists()
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
</script>

<template>
  <div>
    <h1>Списки покупок</h1>

    <div v-if="store.loading" class="loading">Загрузка...</div>

    <div v-else-if="store.lists.length === 0" class="empty">
      Списков покупок пока нет. Создайте из рецепта на странице "Рецепты" или "Гарвардская тарелка".
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
        <h2>{{ showDetail.name }}</h2>
        <div class="detail-date">Создан: {{ new Date(showDetail.createdDate).toLocaleDateString('ru-RU') }}</div>

        <div class="detail-progress">
          <div class="progress-bar large">
            <div class="progress-fill" :style="{ width: progressPercent(showDetail) + '%' }"></div>
          </div>
          <span>{{ purchasedCount(showDetail) }} из {{ totalCount(showDetail) }} куплено</span>
        </div>

        <div class="items-list">
          <div
            v-for="item in showDetail.items"
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
</style>
