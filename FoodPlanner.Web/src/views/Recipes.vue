<script setup>
import { ref, onMounted } from 'vue'
import { useRecipesStore } from '../stores/recipes'
import { useProductsStore } from '../stores/products'
import { useShoppingListsStore } from '../stores/shoppingLists'

const recipesStore = useRecipesStore()
const productsStore = useProductsStore()
const shoppingStore = useShoppingListsStore()

const showAddModal = ref(false)
const editingRecipe = ref(null)
const searchQuery = ref('')

const form = ref({
  name: '',
  description: '',
  mealType: '',
  servings: 1,
  preparationTimeMinutes: 0,
  tags: [],
  ingredients: [],
  steps: []
})

const newIngredient = ref({ productId: '', amount: 0, unit: 0 })
const newStep = ref('')

onMounted(() => {
  recipesStore.fetchRecipes()
  productsStore.fetchProducts()
})

function filteredRecipes() {
  if (!searchQuery.value) return recipesStore.recipes
  const q = searchQuery.value.toLowerCase()
  return recipesStore.recipes.filter(r =>
    r.name.toLowerCase().includes(q) ||
    r.description?.toLowerCase().includes(q) ||
    r.tags?.some(t => t.toLowerCase().includes(q))
  )
}

function openAdd() {
  editingRecipe.value = null
  form.value = {
    name: '',
    description: '',
    mealType: '',
    servings: 1,
    preparationTimeMinutes: 0,
    tags: [],
    ingredients: [],
    steps: []
  }
  showAddModal.value = true
}

function openEdit(recipe) {
  editingRecipe.value = recipe
  form.value = JSON.parse(JSON.stringify(recipe))
  showAddModal.value = true
}

function addIngredient() {
  if (newIngredient.value.productId && newIngredient.value.amount > 0) {
    const product = productsStore.products.find(p => p.id === newIngredient.value.productId)
    form.value.ingredients.push({
      productId: newIngredient.value.productId,
      productName: product?.name || '',
      amount: newIngredient.value.amount,
      unit: newIngredient.value.unit
    })
    newIngredient.value = { productId: '', amount: 0, unit: 0 }
  }
}

function removeIngredient(index) {
  form.value.ingredients.splice(index, 1)
}

function addStep() {
  if (newStep.value.trim()) {
    form.value.steps.push(newStep.value.trim())
    newStep.value = ''
  }
}

function removeStep(index) {
  form.value.steps.splice(index, 1)
}

async function saveRecipe() {
  try {
    if (editingRecipe.value) {
      await recipesStore.updateRecipe(form.value)
    } else {
      await recipesStore.addRecipe(form.value)
    }
    showAddModal.value = false
  } catch (e) {
    alert('Ошибка: ' + e.message)
  }
}

async function removeRecipe(id) {
  if (confirm('Удалить рецепт?')) {
    await recipesStore.deleteRecipe(id)
  }
}

async function createShoppingList(recipe) {
  try {
    await shoppingStore.createFromRecipe(recipe.id)
    alert('Список покупок создан!')
  } catch (e) {
    alert('Ошибка: ' + e.message)
  }
}
</script>

<template>
  <div>
    <h1>Рецепты</h1>

    <div class="toolbar">
      <input
        v-model="searchQuery"
        type="text"
        placeholder="Поиск рецептов..."
        class="search-input"
      />
      <button class="btn btn-primary" @click="openAdd">+ Новый рецепт</button>
    </div>

    <div v-if="recipesStore.loading" class="loading">Загрузка...</div>

    <div v-else-if="filteredRecipes().length === 0" class="empty">
      Рецептов пока нет. Добавьте первый рецепт!
    </div>

    <div v-else class="recipes-grid">
      <div v-for="recipe in filteredRecipes()" :key="recipe.id" class="recipe-card">
        <div class="recipe-header">
          <h3>{{ recipe.name }}</h3>
          <div class="recipe-meta">
            <span v-if="recipe.mealType" class="meta-tag">{{ recipe.mealType }}</span>
            <span v-if="recipe.preparationTimeMinutes" class="meta-tag">⏱ {{ recipe.preparationTimeMinutes }} мин</span>
            <span v-if="recipe.servings" class="meta-tag">👥 {{ recipe.servings }}</span>
          </div>
        </div>
        <p v-if="recipe.description" class="recipe-desc">{{ recipe.description }}</p>
        <div v-if="recipe.tags?.length" class="recipe-tags">
          <span v-for="tag in recipe.tags" :key="tag" class="tag">{{ tag }}</span>
        </div>
        <div class="recipe-ingredients">
          <strong>Ингредиенты ({{ recipe.ingredients?.length || 0 }}):</strong>
          <span v-for="(ing, i) in (recipe.ingredients || []).slice(0, 5)" :key="i" class="ingredient-text">
            {{ ing.productName || 'ID:' + ing.productId }} — {{ ing.amount }} ед.
          </span>
          <span v-if="(recipe.ingredients?.length || 0) > 5" class="ingredient-text more">
            ...и ещё {{ recipe.ingredients.length - 5 }}
          </span>
        </div>
        <div class="recipe-actions">
          <button class="btn btn-small btn-primary" @click="createShoppingList(recipe)">🛒 В список покупок</button>
          <button class="btn btn-small" @click="openEdit(recipe)">✏️</button>
          <button class="btn btn-small btn-danger" @click="removeRecipe(recipe.id)">🗑</button>
        </div>
      </div>
    </div>

    <div v-if="showAddModal" class="modal-overlay" @click.self="showAddModal = false">
      <div class="modal modal-large">
        <h2>{{ editingRecipe ? 'Редактировать рецепт' : 'Новый рецепт' }}</h2>
        <form @submit.prevent="saveRecipe">
          <div class="form-group">
            <label>Название</label>
            <input v-model="form.name" type="text" required />
          </div>
          <div class="form-group">
            <label>Описание</label>
            <textarea v-model="form.description" rows="2"></textarea>
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>Тип блюда</label>
              <select v-model="form.mealType">
                <option value="">Не указано</option>
                <option>Завтрак</option>
                <option>Обед</option>
                <option>Ужин</option>
                <option>Перекус</option>
                <option>Десерт</option>
              </select>
            </div>
            <div class="form-group">
              <label>Порции</label>
              <input v-model.number="form.servings" type="number" min="1" />
            </div>
            <div class="form-group">
              <label>Время (мин)</label>
              <input v-model.number="form.preparationTimeMinutes" type="number" min="0" />
            </div>
          </div>
          <div class="form-group">
            <label>Через запятую: теги</label>
            <input
              :value="form.tags?.join(', ')"
              @input="form.tags = $event.target.value.split(',').map(t => t.trim()).filter(Boolean)"
              type="text"
            />
          </div>

          <div class="section-title">Ингредиенты</div>
          <div class="ingredient-list">
            <div v-for="(ing, i) in form.ingredients" :key="i" class="ingredient-item">
              <span>{{ ing.productName || ing.productId }} — {{ ing.amount }} ед.</span>
              <button type="button" class="btn-remove" @click="removeIngredient(i)">✕</button>
            </div>
          </div>
          <div class="add-ingredient-row">
            <select v-model="newIngredient.productId">
              <option value="">Выберите продукт</option>
              <option v-for="p in productsStore.products" :key="p.id" :value="p.id">{{ p.name }}</option>
            </select>
            <input v-model.number="newIngredient.amount" type="number" min="0" placeholder="Кол-во" />
            <button type="button" class="btn btn-small" @click="addIngredient">Добавить</button>
          </div>

          <div class="section-title">Шаги приготовления</div>
          <ol class="steps-list">
            <li v-for="(step, i) in form.steps" :key="i">
              {{ step }}
              <button type="button" class="btn-remove" @click="removeStep(i)">✕</button>
            </li>
          </ol>
          <div class="add-step-row">
            <input v-model="newStep" type="text" placeholder="Шаг приготовления..." @keyup.enter="addStep" />
            <button type="button" class="btn btn-small" @click="addStep">Добавить</button>
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

.toolbar {
  display: flex;
  gap: 10px;
  margin-bottom: 20px;
}
.search-input {
  flex: 1;
  padding: 8px 12px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
}

.recipes-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 16px;
}

.recipe-card {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.08);
  transition: box-shadow 0.2s;
}
.recipe-card:hover { box-shadow: 0 3px 12px rgba(0,0,0,0.12); }

.recipe-header h3 { margin: 0 0 8px 0; color: #333; }
.recipe-meta { display: flex; gap: 6px; flex-wrap: wrap; margin-bottom: 8px; }
.meta-tag {
  background: #f5f5f5;
  padding: 3px 8px;
  border-radius: 10px;
  font-size: 0.8rem;
  color: #555;
}
.recipe-desc { color: #666; font-size: 0.9rem; line-height: 1.4; margin: 8px 0; }
.recipe-tags { margin-bottom: 10px; }
.tag {
  background: #e3f2fd;
  color: #1565c0;
  padding: 2px 8px;
  border-radius: 10px;
  font-size: 0.75rem;
  margin-right: 4px;
}
.recipe-ingredients {
  font-size: 0.85rem;
  color: #555;
  margin-bottom: 12px;
}
.ingredient-text { display: inline; margin-left: 6px; }
.ingredient-text.more { color: #999; }
.recipe-actions { display: flex; gap: 6px; }

.btn {
  padding: 8px 16px;
  border: 1px solid #ddd;
  border-radius: 6px;
  background: #fff;
  cursor: pointer;
  font-size: 14px;
}
.btn:hover { background: #f5f5f5; }
.btn-primary { background: #1976d2; color: #fff; border-color: #1976d2; }
.btn-primary:hover { background: #1565c0; }
.btn-danger { color: #f44336; border-color: #f44336; }
.btn-small { padding: 4px 10px; font-size: 12px; }

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
  max-height: 90vh;
  overflow-y: auto;
}
.modal-large { max-width: 700px; }
.modal h2 { margin-top: 0; }
.form-group { margin-bottom: 12px; }
.form-group label { display: block; font-size: 0.85rem; color: #555; margin-bottom: 4px; }
.form-group input, .form-group select, .form-group textarea {
  width: 100%;
  padding: 8px 10px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
  box-sizing: border-box;
  font-family: inherit;
}
.form-row { display: flex; gap: 12px; }
.form-row .form-group { flex: 1; }

.section-title { font-weight: 600; margin: 15px 0 8px; color: #444; }
.ingredient-list { margin-bottom: 8px; }
.ingredient-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 5px 8px;
  background: #f9f9f9;
  border-radius: 4px;
  margin-bottom: 4px;
  font-size: 0.9rem;
}
.add-ingredient-row, .add-step-row {
  display: flex;
  gap: 8px;
  margin-bottom: 10px;
}
.add-ingredient-row select, .add-ingredient-row input, .add-step-row input {
  flex: 1;
  padding: 6px 8px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 13px;
}

.steps-list { padding-left: 20px; font-size: 0.9rem; }
.steps-list li { margin-bottom: 6px; display: flex; justify-content: space-between; align-items: center; }

.btn-remove {
  background: none;
  border: none;
  color: #f44336;
  cursor: pointer;
  font-size: 14px;
  padding: 2px 6px;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
}

.loading, .empty {
  text-align: center;
  padding: 40px;
  color: #666;
}
</style>
