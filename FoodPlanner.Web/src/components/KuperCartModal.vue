<script setup>
import { ref, watch, computed } from 'vue'
import { useKuperStore } from '../stores/kuper'

const props = defineProps({
  show: { type: Boolean, default: false },
  list: { type: Object, default: null }
})
const emit = defineEmits(['close'])

const store = useKuperStore()

const step = ref('connect') // connect | store | resolve | result
const cookie = ref('')
const storesLoading = ref(false)
const resolving = ref(false)
const adding = ref(false)
const historySynced = ref(false)
const error = ref('')
const rows = ref([])
const result = ref(null)

const connected = computed(() => !!store.status?.session?.has_cookie || !!store.profile)
const storeSelected = computed(() => !!store.selectedStoreId)
const pendingRows = computed(() => rows.value.filter(r => r.included && r.chosen))
const addedCount = computed(() => result.value?.added?.length || 0)
const failedCount = computed(() => result.value?.failed?.length || 0)

watch(() => props.show, async (v) => {
  if (!v) return
  error.value = ''
  rows.value = []
  result.value = null
  try {
    await store.fetchStatus()
    await store.fetchSession()
  } catch (e) {
    error.value = friendly(e)
  }
  if (!connected.value) step.value = 'connect'
  else step.value = storeSelected.value ? 'resolve' : 'store'
})

function friendly(e) {
  const msg = e?.response?.data?.error || e?.response?.data || e?.message
  if (typeof msg === 'string') return msg
  if (msg && typeof msg === 'object' && msg.detail) return msg.detail
  if (typeof msg === 'string') return msg
  return 'Неизвестная ошибка'
}

async function connect() {
  error.value = ''
  if (!cookie.value.trim()) {
    error.value = 'Вставьте cookie из авторизованного браузера Купера'
    return
  }
  storesLoading.value = true
  try {
    await store.connectSession(cookie.value.trim())
    cookie.value = ''
    step.value = storeSelected.value ? 'resolve' : 'store'
  } catch (e) {
    error.value = friendly(e)
  } finally {
    storesLoading.value = false
  }
}

async function selectStoreAndResolve() {
  error.value = ''
  if (!store.stores.length) {
    error.value = 'Магазины недоступны. Переподключите аккаунт.'
    step.value = 'connect'
    return
  }
  if (!store.selectedStoreId) {
    error.value = 'Выберите магазин'
    return
  }
  try {
    await store.selectStore(store.selectedStoreId)
    await resolveItems()
  } catch (e) {
    error.value = friendly(e)
  }
}

async function resolveItems() {
  if (!props.list) return
  error.value = ''
  resolving.value = true
  try {
    if (!historySynced.value) {
      await store.refreshHistory()
      historySynced.value = true
    }
  } catch (e) { /* предпочтения из истории — опционально */ }

  try {
    const decisions = await store.resolve(props.list.id)
    if (!decisions.length) {
      error.value = 'Не удалось подобрать ни одного товара'
      return
    }
    rows.value = decisions.map((d, i) => ({
      key: `${props.list.id}_${i}`,
      sourceName: d.source?.name || `Пункт ${i + 1}`,
      amount: d.source?.amount,
      quantity: d.source?.quantity || 1,
      quantityNote: d.source?.quantity_note || '',
      chosen: d.product,
      isPreviousBuy: !!d.is_previous_buy,
      isReplacement: !!d.is_replacement,
      reason: d.reason,
      alternatives: d.alternatives || [],
      included: !!d.product
    }))
    step.value = 'resolve'
  } catch (e) {
    error.value = friendly(e)
    if (e?.response?.status === 401) step.value = 'connect'
  } finally {
    resolving.value = false
  }
}

function onAlternative(row, index) {
  const alt = row.alternatives[Number(index)]
  if (!alt) return
  row.chosen = alt.product || null
  row.isReplacement = true
  row.reason = 'выбрано вручную'
}

function rowTotal(row) {
  const price = row.chosen?.price || 0
  return (price * (row.quantity || 0)).toFixed(0)
}

async function addToCart() {
  const items = pendingRows.value.map(r => ({ productId: r.chosen.product_id, quantity: r.quantity }))
  if (!items.length) {
    error.value = 'Не выбрано ни одного товара'
    return
  }
  error.value = ''
  adding.value = true
  try {
    const data = await store.addToCart(items)
    result.value = {
      orderNumber: data.order_number,
      added: data.added || [],
      failed: data.failed || [],
      cartUrl: store.cartUrl || 'https://web.kuper.ru'
    }
    step.value = 'result'
  } catch (e) {
    error.value = friendly(e)
    if (e?.response?.status === 401) step.value = 'connect'
  } finally {
    adding.value = false
  }
}

async function resetSession() {
  try {
    await store.disconnect()
  } catch (e) {
    error.value = friendly(e)
  }
  step.value = 'connect'
}
</script>

<template>
  <Teleport to="body">
    <div v-if="show" class="kup-overlay" @click.self="emit('close')">
      <div class="kup-modal">
        <div class="kup-head">
          <h3>🛒 Корзина Купера</h3>
          <button class="kup-close" @click="emit('close')">✕</button>
        </div>

        <div v-if="error" class="kup-error">{{ error }}</div>

        <!-- Шаг: подключение -->
        <div v-if="step === 'connect'" class="kup-body">
          <p class="kup-hint">
            Скопируйте cookie из авторизованного браузера
            <a href="https://web.kuper.ru" target="_blank" rel="noopener">web.kuper.ru</a>
            (DevTools → Network/Application → строка <code>_Instamart_session=...; spsc=...</code>)
            и вставьте ниже. Cookie хранится только на вашем сервере.
          </p>
          <textarea
            v-model="cookie"
            class="kup-cookie"
            rows="4"
            placeholder="_Instamart_session=...; spsc=..."
          ></textarea>
          <button class="kup-btn kup-primary" :disabled="storesLoading" @click="connect">
            {{ storesLoading ? 'Проверяем...' : 'Проверить и подключиться' }}
          </button>

          <div v-if="store.status?.session?.has_cookie" class="kup-current">
            <span>Уже подключены</span>
            <button class="kup-btn kup-small" @click="store.fetchSession()">Обновить статус</button>
            <button class="kup-btn kup-small kup-danger" @click="resetSession">Отключить</button>
          </div>
        </div>

        <!-- Шаг: магазин -->
        <div v-else-if="step === 'store'" class="kup-body">
          <p class="kup-hint">Выберите магазин доставки для подбора товаров:</p>
          <label class="kup-select-label">
            <select v-model="store.selectedStoreId" class="kup-select">
              <option disabled value="">— выберите магазин —</option>
              <option v-for="s in store.stores" :key="s.store_id" :value="s.store_id">
                {{ s.name }}{{ s.retailer_name ? ' — ' + s.retailer_name : '' }}
              </option>
            </select>
          </label>
          <button class="kup-btn kup-primary" :disabled="!store.selectedStoreId" @click="selectStoreAndResolve">
            Выбрать магазин и подобрать товары
          </button>
          <button class="kup-btn kup-small kup-danger" @click="resetSession">Отключить аккаунт</button>
        </div>

        <!-- Шаг: подбор -->
        <div v-else-if="step === 'resolve'" class="kup-body">
          <div class="kup-summary" v-if="store.storeName || store.profile">
            <span v-if="store.storeName">Магазин: <b>{{ store.storeName }}</b></span>
            <span v-if="store.profile?.fullname">· {{ store.profile.fullname }}</span>
            <button class="kup-btn kup-small kup-danger" @click="resetSession">Сменить аккаунт</button>
          </div>

          <div class="kup-actions">
            <button class="kup-btn kup-primary" :disabled="resolving" @click="resolveItems">
              {{ resolving ? 'Подбираем...' : '🎯 Подобрать товары по списку' }}
            </button>
          </div>

          <div v-if="rows.length" class="kup-rows">
            <div v-for="row in rows" :key="row.key" class="kup-row" :class="{ excluded: !row.included }">
              <header class="kup-row-head">
                <label class="kup-include">
                  <input v-model="row.included" type="checkbox" />
                  <span class="kup-source-name">{{ row.sourceName }}</span>
                  <span class="kup-amount">{{ row.amount }} ед.</span>
                </label>
              </header>

              <div v-if="row.chosen" class="kup-product">
                <img v-if="row.chosen.image_url" :src="row.chosen.image_url" alt="" class="kup-img" />
                <div class="kup-prod-info">
                  <div class="kup-prod-name">
                    {{ row.chosen.name }}
                    <span v-if="row.chosen.human_volume" class="kup-volume">{{ row.chosen.human_volume }}</span>
                  </div>
                  <div class="kup-prod-meta">
                    <span class="kup-badge" :class="row.isPreviousBuy ? 'prev' : (row.isReplacement ? 'repl' : 'match')">
                      {{ row.isPreviousBuy ? '✓ покупали ранее' : (row.isReplacement ? '🔁 замена' : '✓ по названию') }}
                    </span>
                    <span class="kup-price">{{ row.chosen.price }} ₽</span>
                    <span v-if="row.quantityNote" class="kup-note">{{ row.quantityNote }}</span>
                  </div>
                  <div class="kup-qty">
                    <label>Кол-во:
                      <input v-model.number="row.quantity" type="number" min="1" max="99" class="kup-qty-input" />
                    </label>
                    <span class="kup-total">≈ {{ rowTotal(row) }} ₽</span>
                  </div>
                </div>
              </div>

              <div v-if="!row.chosen" class="kup-notfound">
                Товар не найден — пропущен.
              </div>

              <div v-if="row.alternatives.length" class="kup-alts">
                <label>
                  Заменить на:
                  <select
                    class="kup-select"
                    @change="onAlternative(row, $event.target.value)"
                  >
                    <option value="" disabled :selected="true">— альтернативы —</option>
                    <option
                      v-for="(a, ai) in row.alternatives"
                      :key="a.product.product_id"
                      :value="ai"
                    >
                      {{ a.product.name }} · {{ a.product.price }} ₽{{ a.product.human_volume ? ' · ' + a.product.human_volume : '' }}
                    </option>
                  </select>
                </label>
              </div>
            </div>
          </div>

          <div class="kup-actions" v-if="pendingRows.length">
            <button class="kup-btn kup-primary" :disabled="adding" @click="addToCart">
              {{ adding ? 'Добавляем...' : `➕ Добавить в корзину (${pendingRows.length})` }}
            </button>
          </div>
        </div>

        <!-- Шаг: результат -->
        <div v-else-if="step === 'result'" class="kup-body">
          <div class="kup-result">
            <p class="kup-result-title">Добавлено в корзину: {{ addedCount }}</p>
            <p v-if="failedCount" class="kup-result-fail">Не удалось добавить: {{ failedCount }}</p>
            <p v-if="result?.failed?.length" class="kup-result-list">
              <span v-for="f in result.failed" :key="f.product_id" class="kup-fail-item">
                товар #{{ f.product_id }}: {{ f.error }}
              </span>
            </p>
          </div>
          <div class="kup-actions">
            <a class="kup-btn kup-primary kup-link" :href="result?.cartUrl || 'https://web.kuper.ru'" target="_blank" rel="noopener">
              Открыть корзину на web.kuper.ru
            </a>
            <button class="kup-btn" @click="emit('close')">Готово</button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.kup-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 16px;
}
.kup-modal {
  background: #fff;
  border-radius: 14px;
  width: 560px;
  max-width: 100%;
  max-height: 90vh;
  display: flex;
  flex-direction: column;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.25);
}
.kup-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 18px;
  border-bottom: 1px solid #eee;
}
.kup-head h3 { margin: 0; }
.kup-close {
  background: none;
  border: none;
  font-size: 18px;
  cursor: pointer;
  color: #999;
}
.kup-body {
  padding: 18px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 14px;
}
.kup-hint { font-size: 0.85rem; color: #666; line-height: 1.5; margin: 0; }
.kup-hint code {
  background: #f2f2f2; padding: 1px 5px; border-radius: 4px; font-size: 0.8rem;
  word-break: break-all;
}
.kup-cookie {
  width: 100%;
  border: 1px solid #ddd;
  border-radius: 8px;
  padding: 10px;
  font-size: 0.85rem;
  resize: vertical;
  font-family: monospace;
}
.kup-btn {
  padding: 9px 16px;
  border: 1px solid #ddd;
  border-radius: 8px;
  background: #fff;
  cursor: pointer;
  font-size: 14px;
  text-align: center;
}
.kup-btn:disabled { opacity: 0.6; cursor: not-allowed; }
.kup-primary { background: #1976d2; color: #fff; border-color: #1976d2; }
.kup-primary:hover:not(:disabled) { background: #1565c0; }
.kup-small { padding: 5px 10px; font-size: 12px; }
.kup-danger { color: #d32f2f; border-color: #d32f2f; background: #fff; }
.kup-error {
  margin: 12px 18px 0;
  padding: 10px 12px;
  background: #ffebee;
  color: #b71c1c;
  border-radius: 8px;
  font-size: 0.85rem;
}
.kup-current {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 0.85rem;
  color: #388e3c;
}
.kup-select-label { display: block; }
.kup-select {
  width: 100%;
  padding: 9px;
  border: 1px solid #ddd;
  border-radius: 8px;
  font-size: 0.9rem;
}
.kup-summary {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 0.85rem;
  color: #555;
  flex-wrap: wrap;
}
.kup-actions { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; }
.kup-rows { display: flex; flex-direction: column; gap: 10px; }
.kup-row {
  border: 1px solid #e8e8e8;
  border-radius: 10px;
  padding: 10px 12px;
  background: #fafafa;
}
.kup-row.excluded { opacity: 0.5; }
.kup-row-head { display: flex; align-items: center; gap: 8px; }
.kup-include {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  flex-wrap: wrap;
  font-size: 0.95rem;
  font-weight: 600;
}
.kup-amount { font-weight: 400; color: #888; font-size: 0.8rem; }
.kup-product {
  display: flex;
  gap: 10px;
  margin-top: 8px;
  align-items: flex-start;
}
.kup-img {
  width: 56px;
  height: 56px;
  border-radius: 8px;
  object-fit: cover;
  background: #f0f0f0;
  flex-shrink: 0;
}
.kup-prod-info { flex: 1; min-width: 0; }
.kup-prod-name { font-weight: 600; font-size: 0.9rem; }
.kup-volume { color: #888; font-size: 0.8rem; font-weight: 400; }
.kup-prod-meta { display: flex; align-items: center; gap: 10px; margin-top: 4px; flex-wrap: wrap; }
.kup-badge {
  font-size: 0.75rem;
  padding: 2px 8px;
  border-radius: 20px;
  font-weight: 600;
}
.kup-badge.prev { background: #e8f5e9; color: #2e7d32; }
.kup-badge.repl { background: #fff3e0; color: #e65100; }
.kup-badge.match { background: #e3f2fd; color: #1565c0; }
.kup-price { font-weight: 600; }
.kup-note { color: #e65100; font-size: 0.8rem; }
.kup-qty { display: flex; align-items: center; gap: 12px; margin-top: 8px; font-size: 0.85rem; }
.kup-qty-input {
  width: 56px;
  padding: 5px;
  border: 1px solid #ddd;
  border-radius: 6px;
}
.kup-total { color: #555; }
.kup-notfound { color: #999; font-size: 0.85rem; margin-top: 6px; }
.kup-alts { margin-top: 8px; font-size: 0.85rem; display: flex; flex-direction: column; gap: 6px; }
.kup-result-title { font-weight: 700; font-size: 1.05rem; margin: 0; }
.kup-result-fail { color: #d32f2f; margin: 4px 0 0; }
.kup-result-list { display: flex; flex-direction: column; gap: 4px; color: #b71c1c; font-size: 0.8rem; margin: 6px 0 0; }
.kup-fail-item { display: block; }
.kup-link { text-decoration: none; display: inline-block; }
</style>