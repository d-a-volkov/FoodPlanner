<script setup>
import { ref, watch, computed } from 'vue'
import { useKuperStore } from '../stores/kuper'
import { KUPER_CITIES, DEFAULT_CITY } from '../constants/kuperCities'

const props = defineProps({
  show: { type: Boolean, default: false },
  list: { type: Object, default: null }
})
const emit = defineEmits(['close'])

const store = useKuperStore()

const step = ref('connect') // connect | store | resolve | result
const sessionCookie = ref('')
const spscCookie = ref('')
const cityName = ref(store.selectedCity || DEFAULT_CITY.name)
const citySearch = ref('')
const storeFilter = ref('')
const wideSearch = ref(true)
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

const cityMatches = computed(() => {
  const q = citySearch.value.trim().toLowerCase()
  if (!q) return KUPER_CITIES
  return KUPER_CITIES.filter(c => c.name.toLowerCase().includes(q))
})

const filteredStores = computed(() => {
  const q = storeFilter.value.trim().toLowerCase()
  const list = q
    ? store.stores.filter(s => [s.name, s.retailer_name, s.address, s.city]
        .some(v => (v || '').toLowerCase().includes(q)))
    : store.stores
  return [...list].sort((a, b) => {
    if (a.store_id === store.selectedStoreId) return -1
    if (b.store_id === store.selectedStoreId) return 1
    return (a.name || '').localeCompare(b.name || '', 'ru')
  })
})

const activeCityName = computed(() => {
  const sel = store.coordinates
  if (sel?.lat && sel?.lon) {
    const exact = KUPER_CITIES.find(c => Math.abs(c.lat - sel.lat) < 0.0001 && Math.abs(c.lon - sel.lon) < 0.0001)
    return exact?.name || 'Произвольная точка'
  }
  return cityName.value
})

watch(() => props.show, async (v) => {
  if (!v) return
  error.value = ''
  const saved = loadRows()
  rows.value = saved?.rows || []
  result.value = null
  sessionCookie.value = ''
  spscCookie.value = ''
  try {
    await store.fetchStatus()
    await store.fetchSession()
  } catch (e) {
    error.value = friendly(e)
  }
  // Подбор привязан к магазину: если магазин сменился — старые строки не годятся.
  if (saved && store.selectedStoreId && saved.storeId !== store.selectedStoreId) {
    clearRows()
    rows.value = []
  }
  if (!connected.value) step.value = 'connect'
  // Сохранённые строки подбора уже восстановлены выше — показываем их сразу.
  else if (storeSelected.value) step.value = 'resolve'
  else step.value = 'store'
})

function friendly(e) {
  const status = e?.response?.status
  const data = e?.response?.data
  let msg = data?.error || data?.detail || data?.message
  if (typeof data === 'string' && data.trim()) msg = data.trim()
  // Ошибки валидации ASP.NET: { errors: { поле: [текст, ...] } }
  if (data?.errors) {
    const parts = Object.entries(data.errors)
      .flatMap(([field, list]) => (Array.isArray(list) ? list : [list]).map(t => `${field}: ${t}`))
      .filter(Boolean)
    if (parts.length) msg = parts.join('; ')
  }
  if (!msg && status >= 500) msg = data?.title || 'Сервер вернул ошибку без описания'
  if (!msg) msg = e?.message
  if (!msg) msg = 'Пустой ответ сервера'
  const prefix = status ? `Ошибка ${status}. ` : ''
  return prefix + String(msg)
}

// Результаты подбора сохраняем, чтобы закрытие формы не сбрасывало работу.
const STORAGE_PREFIX = 'kuperResolve'

function storageKey(listId) {
  return `${STORAGE_PREFIX}:${listId}`
}

function saveRows() {
  if (!props.list) return
  try {
    localStorage.setItem(storageKey(props.list.id), JSON.stringify({
      at: Date.now(),
      storeId: store.selectedStoreId,
      rows: rows.value
    }))
  } catch { /* приватный режим / переполнение */ }
}

function loadRows() {
  if (!props.list) return null
  try {
    const raw = localStorage.getItem(storageKey(props.list.id))
    if (!raw) return null
    const parsed = JSON.parse(raw)
    if (!parsed?.rows?.length) return null
    return parsed
  } catch {
    return null
  }
}

function clearRows() {
  if (!props.list) return
  try { localStorage.removeItem(storageKey(props.list.id)) } catch { /* игнорируем */ }
}

watch(rows, () => saveRows(), { deep: true })
watch(() => props.show, v => { if (v) saveRows() })

async function connectByCookie() {
  error.value = ''
  const sess = sessionCookie.value.trim()
  if (!sess) {
    error.value = 'Вставьте значение _Instamart_session'
    return
  }
  const spsc = spscCookie.value.trim()
  const cookie = spsc ? `_Instamart_session=${sess}; spsc=${spsc}` : `_Instamart_session=${sess}`
  storesLoading.value = true
  try {
    const city = store.setCity(cityName.value)
    await store.connectByCookie(cookie, city?.lat, city?.lon)
    sessionCookie.value = ''
    spscCookie.value = ''
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

async function loadStores() {
  error.value = ''
  storesLoading.value = true
  try {
    const city = store.setCity(cityName.value) || DEFAULT_CITY
    await store.refreshStores(city.lat, city.lon, wideSearch.value)
    if (!store.stores.length) {
      error.value = `Купер не нашёл магазинов в городе ${city.name}. Выберите другой город.`
    }
  } catch (e) {
    error.value = friendly(e)
  } finally {
    storesLoading.value = false
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

function closeAndForget() {
  clearRows()
  emit('close')
}

function restartResolve() {
  clearRows()
  rows.value = []
  result.value = null
  step.value = 'resolve'
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
      cartUrl: data.cart_url || store.cartUrl || 'https://web.kuper.ru/cart',
      byProduct: new Map(rows.value.map(r => [r.chosen?.product_id, r.sourceName]))
    }
    step.value = 'result'
    // Пользователь просил сразу видеть товары в корзине Купера.
    if (result.value.added.length) {
      window.open(result.value.cartUrl, '_blank', 'noopener')
    }
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
            Авторизуйтесь на
            <a href="https://web.kuper.ru" target="_blank" rel="noopener">web.kuper.ru</a>
            в своём обычном браузере. Затем F12 → Application → Cookies →
            <code>https://web.kuper.ru</code> и скопируйте <b>значение</b> (не имя) двух строк
            в поля ниже. Cookie хранится только на вашем сервере.
          </p>

          <label class="kup-select-label">
            _Instamart_session <span class="kup-req">обязательно</span>
            <textarea
              v-model="sessionCookie"
              class="kup-cookie"
              rows="3"
              placeholder="значение cookie _Instamart_session"
              spellcheck="false"
              autocomplete="off"
            ></textarea>
          </label>

          <label class="kup-select-label">
            spsc <span class="kup-req kup-req-optional">желательно</span>
            <textarea
              v-model="spscCookie"
              class="kup-cookie"
              rows="2"
              placeholder="значение cookie spsc"
              spellcheck="false"
              autocomplete="off"
            ></textarea>
          </label>

          <button
            class="kup-btn kup-primary"
            :disabled="storesLoading || !sessionCookie.trim()"
            @click="connectByCookie"
          >
            {{ storesLoading ? 'Проверяем…' : 'Проверить и подключиться' }}
          </button>

          <div v-if="store.status?.session?.has_cookie" class="kup-current">
            <span>Уже подключены</span>
            <button class="kup-btn kup-small" @click="store.fetchSession()">Обновить статус</button>
            <button class="kup-btn kup-small kup-danger" @click="resetSession">Отключить</button>
          </div>
        </div>

        <!-- Шаг: магазин -->
        <div v-else-if="step === 'store'" class="kup-body">
          <p class="kup-hint">
            Магазины ищутся рядом с выбранным городом. Смените город и обновите список,
            если нужного магазина нет.
          </p>

          <label class="kup-select-label">
            Город
            <input v-model="citySearch" class="kup-input" placeholder="Поиск города" />
          </label>

          <select v-model="cityName" class="kup-select" size="1">
            <option v-for="c in cityMatches" :key="c.name" :value="c.name">{{ c.name }}</option>
          </select>

          <label class="kup-check">
            <input v-model="wideSearch" type="checkbox" />
            Искать в окрестностях (находит магазины вне зоны доставки центра, ~10 сек)
          </label>

          <button class="kup-btn kup-small" :disabled="storesLoading" @click="loadStores">
            {{ storesLoading ? 'Ищем магазины…' : `🔄 Магазины: ${activeCityName}` }}
          </button>

          <label class="kup-select-label" v-if="store.stores.length">
            Фильтр магазинов
            <input v-model="storeFilter" class="kup-input" placeholder="Название, сеть или адрес" />
          </label>

          <p class="kup-hint" v-if="store.stores.length">
            Найдено магазинов: {{ filteredStores.length }} из {{ store.stores.length }}
          </p>

          <div class="kup-stores">
            <label
              v-for="s in filteredStores"
              :key="s.store_id"
              class="kup-store"
              :class="{ active: s.store_id === store.selectedStoreId }"
            >
              <input type="radio" :value="s.store_id" v-model="store.selectedStoreId" name="kuper-store" />
              <span class="kup-store-body">
                <span class="kup-store-name">{{ s.name }}</span>
                <span class="kup-store-meta" v-if="s.retailer_name">{{ s.retailer_name }}</span>
                <span class="kup-store-meta" v-if="s.address || s.city">{{ [s.city, s.address].filter(Boolean).join(', ') }}</span>
                <span class="kup-store-meta" v-if="s.delivery_min">
                  доставка {{ s.delivery_min }}{{ s.delivery_max ? '–' + s.delivery_max : '' }} мин
                </span>
              </span>
            </label>
            <p class="kup-hint" v-if="!filteredStores.length">
              Ничего не найдено. Измените фильтр или город.
            </p>
          </div>

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

              <div v-if="rows.length" class="kup-actions">
                <button class="kup-btn kup-small" @click="restartResolve">Сбросить подбор</button>
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
                <details class="kup-alts-details">
                  <summary>Другие варианты ({{ row.alternatives.length }})</summary>
                  <button
                    v-for="(a, ai) in row.alternatives"
                    :key="a.product.product_id"
                    type="button"
                    class="kup-alt-item"
                    @click="onAlternative(row, ai)"
                  >
                    <img v-if="a.product.image_url" :src="a.product.image_url" alt="" class="kup-alt-img" />
                    <span class="kup-alt-name">{{ a.product.name }}</span>
                    <span class="kup-alt-price">{{ a.product.price }} ₽</span>
                  </button>
                </details>
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
                {{ result.byProduct?.get(f.product_id) || 'товар' }} (#{{ f.product_id }}): {{ f.error }}
              </span>
            </p>
          </div>
          <div class="kup-actions">
            <a class="kup-btn kup-primary kup-link" :href="result?.cartUrl || 'https://web.kuper.ru/cart'" target="_blank" rel="noopener">
              🛒 Открыть корзину на web.kuper.ru
            </a>
            <button class="kup-btn" @click="step = 'resolve'">Вернуться к подбору</button>
            <button class="kup-btn" @click="closeAndForget">Закрыть</button>
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
.kup-input {
    width: 100%;
    border: 1px solid #ddd;
    border-radius: 8px;
    padding: 9px 10px;
    font-size: 0.88rem;
    font-family: inherit;
    margin-top: 4px;
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
.kup-code-block {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.kup-manual {
  border-top: 1px dashed #ddd;
  padding-top: 10px;
  font-size: 0.8rem;
  color: #777;
}
.kup-manual summary { cursor: pointer; color: #1976d2; }
.kup-manual code {
  background: #f2f2f2; padding: 1px 5px; border-radius: 4px; font-size: 0.75rem;
  word-break: break-all;
}
.kup-select-label { display: block; margin-bottom: 12px; }
    .kup-check {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 0.82rem;
    color: #555;
    margin-bottom: 10px;
    font-weight: 400;
  }
  .kup-stores {
      max-height: 300px;
      overflow-y: auto;
      border: 1px solid #e4e4e4;
      border-radius: 8px;
      margin-bottom: 12px;
    }
    .kup-store {
      display: flex;
      gap: 10px;
      align-items: flex-start;
      padding: 9px 11px;
      border-bottom: 1px solid #f0f0f0;
      cursor: pointer;
      margin: 0;
      font-weight: 400;
    }
    .kup-store:last-child { border-bottom: none; }
    .kup-store:hover { background: #fafafa; }
    .kup-store.active { background: #eef7ff; box-shadow: inset 3px 0 0 #1976d2; }
    .kup-store-body { display: flex; flex-direction: column; gap: 1px; min-width: 0; }
    .kup-store-name { font-size: 0.88rem; font-weight: 600; color: #222; }
    .kup-store-meta { font-size: 0.76rem; color: #767676; }
    .kup-req {
      font-size: 0.72rem;
      font-weight: 600;
      color: #b3382c;
      background: #fdecea;
      border: 1px solid #f5c6c0;
      border-radius: 4px;
      padding: 1px 6px;
      margin-left: 6px;
      vertical-align: middle;
      text-transform: none;
      letter-spacing: 0;
    }
    .kup-req-optional {
      color: #8a6d1f;
      background: #fdf6e3;
      border-color: #f0e0b0;
    }
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
.kup-alts-details summary { cursor: pointer; color: #1976d2; }
.kup-alt-item {
  display: flex; align-items: center; gap: 8px; width: 100%; text-align: left;
  padding: 6px; margin-top: 4px; border: 1px solid #e0e0e0; border-radius: 6px;
  background: #fff; cursor: pointer; font: inherit;
}
.kup-alt-item:hover { border-color: #1976d2; background: #f5faff; }
.kup-alt-img { width: 32px; height: 32px; object-fit: contain; border-radius: 4px; }
.kup-alt-name { flex: 1; }
.kup-alt-price { white-space: nowrap; font-weight: 600; }
.kup-result-title { font-weight: 700; font-size: 1.05rem; margin: 0; }
.kup-result-fail { color: #d32f2f; margin: 4px 0 0; }
.kup-result-list { display: flex; flex-direction: column; gap: 4px; color: #b71c1c; font-size: 0.8rem; margin: 6px 0 0; }
.kup-fail-item { display: block; }
.kup-link { text-decoration: none; display: inline-block; }
</style>