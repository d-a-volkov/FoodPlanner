<script setup>
import { onMounted, computed } from 'vue'
import { useKuperPreferencesStore } from '../stores/kuperPreferences'

const prefs = useKuperPreferencesStore()

const sortedActive = computed(() =>
  [...prefs.items].sort((a, b) => (b.times_bought || 0) - (a.times_bought || 0))
)

function formatDate(value) {
  if (!value) return ''
  const d = new Date(value)
  return isNaN(d.getTime()) ? '' : d.toLocaleDateString('ru-RU')
}

function hideItem(item) {
  if (confirm(`Скрыть «${item.name}» из предпочтений?`)) {
    prefs.remove(item.product_id)
  }
}

function restoreItem(item) {
  prefs.restore(item.product_id)
}

onMounted(async () => {
  try {
    await prefs.fetch()
    await prefs.ensureFresh()
  } catch (e) {
    prefs.error = e?.response?.data?.error || e.message
  }
})
</script>

<template>
  <div>
    <h1>Предпочтения из истории покупок</h1>

    <div class="toolbar">
      <button class="btn btn-primary" :disabled="prefs.loading" @click="prefs.sync()">
        {{ prefs.loading ? 'Обновление...' : '🔄 Обновить из истории Купера' }}
      </button>
      <span v-if="prefs.syncedAt" class="prefs-synced">
        Обновлено: {{ formatDate(prefs.syncedAt) }}
      </span>
      <span v-else class="prefs-synced prefs-never">
        Ещё не синхронизировано
      </span>
    </div>

    <p class="prefs-hint">
      Здесь собираются товары, которые вы чаще всего покупаете в Купере. При подборе корзины
      предпочтения получают приоритет, обновление истории происходит автоматически раз в сутки.
    </p>

    <p v-if="prefs.error" class="prefs-error">{{ prefs.error }}</p>

    <template v-if="sortedActive.length">
      <h2>Активные ({{ sortedActive.length }})</h2>
      <div class="prefs-list">
        <div v-for="item in sortedActive" :key="item.product_id" class="pref-row">
          <div class="pref-info">
            <div class="pref-name">
              {{ item.name }}
              <span v-if="item.human_volume" class="pref-volume">{{ item.human_volume }}</span>
            </div>
            <div class="pref-meta">
              <span v-if="item.times_bought">покупок: {{ item.times_bought }}</span>
              <span v-if="item.last_price">по {{ item.last_price }} ₽</span>
              <span v-if="item.last_bought_at">последняя покупка: {{ formatDate(item.last_bought_at) }}</span>
            </div>
          </div>
          <button class="btn btn-small btn-danger" title="Скрыть из предпочтений" @click="hideItem(item)">Скрыть</button>
        </div>
      </div>
    </template>
    <div v-else-if="!prefs.loading" class="empty">
      Пока нет данных. Нажмите «Обновить из истории Купера».
    </div>

    <div v-if="prefs.hiddenItems.length" class="hidden-block">
      <h2>Скрытые ({{ prefs.hiddenItems.length }})</h2>
      <div class="prefs-list">
        <div v-for="item in prefs.hiddenItems" :key="item.product_id" class="pref-row muted">
          <div class="pref-info">
            <div class="pref-name">{{ item.name }}</div>
            <div class="pref-meta">
              <span v-if="item.times_bought">покупок: {{ item.times_bought }}</span>
            </div>
          </div>
          <button class="btn btn-small" title="Вернуть в предпочтения" @click="restoreItem(item)">Вернуть</button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
h1 { margin-top: 0; color: #333; }
h2 { margin: 20px 0 10px; font-size: 1.05rem; color: #444; }

.toolbar {
  display: flex;
  gap: 12px;
  margin-bottom: 12px;
  align-items: center;
  flex-wrap: wrap;
}

.prefs-synced { font-size: 0.85rem; color: #4caf50; }
.prefs-never { color: #999; }

.prefs-hint {
  font-size: 0.9rem;
  color: #666;
  margin: 0 0 12px;
  max-width: 720px;
}

.prefs-error {
  background: #fdecea;
  color: #c62828;
  padding: 10px 12px;
  border-radius: 8px;
  font-size: 0.9rem;
}

.prefs-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.pref-row {
  display: flex;
  align-items: center;
  gap: 12px;
  background: #fff;
  border-radius: 10px;
  padding: 12px 14px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.06);
}
.pref-row.muted { opacity: 0.55; }

.pref-info { flex: 1; min-width: 0; }
.pref-name { font-weight: 500; overflow-wrap: anywhere; }
.pref-volume { color: #888; font-size: 0.85rem; margin-left: 6px; }
.pref-meta {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
  font-size: 0.8rem;
  color: #888;
  margin-top: 2px;
}

.hidden-block { margin-top: 20px; }

.btn {
  padding: 8px 14px;
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
.btn-danger { color: #c62828; }
.btn-danger:hover { background: #fdecea; }
.btn-small { padding: 4px 10px; font-size: 12px; }
.btn:disabled { opacity: 0.6; cursor: not-allowed; }

.empty { text-align: center; padding: 30px; color: #666; }
</style>