<script setup>
import { onMounted } from 'vue'
import { useHarvardPlateStore } from '../stores/harvardPlate'
import { useProductsStore } from '../stores/products'
import { useShoppingListsStore } from '../stores/shoppingLists'
import { Pie } from 'vue-chartjs'
import { Chart as ChartJS, ArcElement, Tooltip, Legend } from 'chart.js'

ChartJS.register(ArcElement, Tooltip, Legend)

const hpStore = useHarvardPlateStore()
const productsStore = useProductsStore()
const shoppingStore = useShoppingListsStore()

onMounted(async () => {
  await hpStore.fetchAnalysis()
  await hpStore.fetchMatchingRecipes()
})

function getChartData() {
  if (!hpStore.analysis) return { labels: [], datasets: [] }
  const colors = ['#4caf50', '#ff9800', '#2196f3', '#f44336']
  return {
    labels: hpStore.analysis.ratios.map(r => r.displayName),
    datasets: [{
      data: hpStore.analysis.ratios.map(r => r.currentPercentage),
      backgroundColor: colors,
      borderWidth: 2
    }]
  }
}

function getRecommendChartData() {
  if (!hpStore.analysis) return { labels: [], datasets: [] }
  const colors = ['#4caf50', '#ff9800', '#2196f3', '#f44336']
  return {
    labels: hpStore.analysis.ratios.map(r => r.displayName),
    datasets: [{
      data: hpStore.analysis.ratios.map(r => r.recommendedPercentage),
      backgroundColor: colors.map(c => c + '80'),
      borderWidth: 2,
      borderDash: [5, 5]
    }]
  }
}

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { position: 'bottom' }
  }
}

async function createShoppingFromMissing(recipe) {
  if (recipe.missingIngredients?.length > 0) {
    alert('Для этого рецепта не хватает продуктов. Сначала добавьте их в наличие.')
    return
  }
  try {
    await shoppingStore.createFromRecipe(recipe.recipe.id)
    alert('Список покупок создан!')
  } catch (e) {
    alert('Ошибка: ' + e.message)
  }
}
</script>

<template>
  <div>
    <h1>Гарвардская тарелка</h1>

    <div v-if="hpStore.loading" class="loading">Загрузка анализа...</div>
    <div v-else-if="hpStore.error" class="error">{{ hpStore.error }}</div>

    <template v-else-if="hpStore.analysis">
      <div class="score-section">
        <div class="score-card">
          <div class="score-value" :class="{ good: hpStore.analysis.overallScore >= 60, bad: hpStore.analysis.overallScore < 40 }">
            {{ hpStore.analysis.overallScore }}%
          </div>
          <div class="score-label">Баланс рациона</div>
        </div>
      </div>

      <div class="charts-row">
        <div class="chart-card">
          <h3>Текущее распределение</h3>
          <div class="chart-container">
            <Pie :data="getChartData()" :options="chartOptions" />
          </div>
        </div>
        <div class="chart-card">
          <h3>Рекомендуемое</h3>
          <div class="chart-container">
            <Pie :data="getRecommendChartData()" :options="chartOptions" />
          </div>
        </div>
      </div>

      <div class="ratios-section">
        <h2>Детали по категориям</h2>
        <div v-for="ratio in hpStore.analysis.ratios" :key="ratio.displayName" class="ratio-card">
          <div class="ratio-header">
            <span class="ratio-name">{{ ratio.displayName }}</span>
            <span class="ratio-values">
              {{ ratio.currentPercentage }}% / {{ ratio.recommendedPercentage }}%
            </span>
          </div>
          <div class="ratio-bar">
            <div
              class="ratio-fill"
              :style="{ width: Math.min(ratio.currentPercentage, 100) + '%' }"
              :class="{ over: ratio.currentPercentage > ratio.recommendedPercentage + 5, under: ratio.currentPercentage < ratio.recommendedPercentage - 5 }"
            ></div>
            <div
              class="ratio-target"
              :style="{ left: ratio.recommendedPercentage + '%' }"
            ></div>
          </div>
        </div>
      </div>

      <div v-if="hpStore.analysis.recommendations.length" class="recommendations">
        <h2>Рекомендации</h2>
        <ul>
          <li v-for="(rec, i) in hpStore.analysis.recommendations" :key="i" class="rec-item">
            {{ rec }}
          </li>
        </ul>
      </div>

      <div v-if="hpStore.analysis.suggestedProducts.length" class="suggestions">
        <h2>Рекомендуемые продукты</h2>
        <div class="suggestion-list">
          <span v-for="p in hpStore.analysis.suggestedProducts" :key="p.id" class="suggestion-tag">
            {{ p.name }}
          </span>
        </div>
      </div>

      <div v-if="hpStore.matchingRecipes.length" class="matching-recipes">
        <h2>Рецепты на основе имеющихся продуктов</h2>
        <div v-for="match in hpStore.matchingRecipes.slice(0, 10)" :key="match.recipe.id" class="recipe-match-card">
          <div class="recipe-match-header">
            <span class="recipe-name">{{ match.recipe.name }}</span>
            <span class="match-percentage" :class="{ high: match.availabilityPercentage >= 80, medium: match.availabilityPercentage >= 50 }">
              {{ match.availabilityPercentage }}%
            </span>
          </div>
          <div class="recipe-match-bar">
            <div class="recipe-match-fill" :style="{ width: match.availabilityPercentage + '%' }"></div>
          </div>
          <div v-if="match.missingIngredients.length" class="missing-label">
            Не хватает: {{ match.missingIngredients.map(i => i.productName || i.productId).join(', ') }}
          </div>
          <button
            v-if="match.availabilityPercentage === 100"
            class="btn btn-small btn-primary"
            @click="createShoppingFromMissing(match)"
          >
            Создать список покупок
          </button>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
h1 { margin-top: 0; color: #333; }
h2 { color: #444; }
h3 { color: #555; text-align: center; margin-bottom: 10px; }

.score-section { text-align: center; margin-bottom: 30px; }
.score-card {
  display: inline-block;
  background: #fff;
  padding: 20px 40px;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}
.score-value { font-size: 3rem; font-weight: bold; }
.score-value.good { color: #4caf50; }
.score-value.bad { color: #f44336; }
.score-label { color: #666; font-size: 0.9rem; margin-top: 5px; }

.charts-row {
  display: flex;
  gap: 20px;
  margin-bottom: 30px;
}
.chart-card {
  flex: 1;
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.08);
}
.chart-container {
  height: 250px;
}

.ratios-section { margin-bottom: 30px; }
.ratio-card {
  background: #fff;
  border-radius: 8px;
  padding: 15px;
  margin-bottom: 10px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.06);
}
.ratio-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 8px;
}
.ratio-name { font-weight: 600; }
.ratio-values { color: #666; }
.ratio-bar {
  position: relative;
  height: 12px;
  background: #eee;
  border-radius: 6px;
  overflow: visible;
}
.ratio-fill {
  height: 100%;
  border-radius: 6px;
  background: #4caf50;
  transition: width 0.5s;
}
.ratio-fill.over { background: #ff9800; }
.ratio-fill.under { background: #f44336; }
.ratio-target {
  position: absolute;
  top: -3px;
  width: 2px;
  height: 18px;
  background: #333;
}

.recommendations, .suggestions, .matching-recipes {
  background: #fff;
  border-radius: 12px;
  padding: 20px;
  margin-bottom: 20px;
  box-shadow: 0 1px 4px rgba(0,0,0,0.08);
}

.recommendations ul { padding-left: 20px; }
.rec-item {
  margin-bottom: 8px;
  line-height: 1.5;
  color: #555;
}

.suggestion-list { display: flex; flex-wrap: wrap; gap: 8px; }
.suggestion-tag {
  background: #e8f5e9;
  color: #2e7d32;
  padding: 6px 12px;
  border-radius: 20px;
  font-size: 0.85rem;
}

.recipe-match-card {
  border-bottom: 1px solid #eee;
  padding: 12px 0;
}
.recipe-match-card:last-child { border-bottom: none; }
.recipe-match-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 6px;
}
.recipe-name { font-weight: 500; }
.match-percentage {
  padding: 3px 10px;
  border-radius: 12px;
  font-size: 0.85rem;
  font-weight: bold;
}
.match-percentage.high { background: #e8f5e9; color: #2e7d32; }
.match-percentage.medium { background: #fff3e0; color: #e65100; }
.recipe-match-bar {
  height: 6px;
  background: #eee;
  border-radius: 3px;
  margin-bottom: 4px;
}
.recipe-match-fill {
  height: 100%;
  background: #4caf50;
  border-radius: 3px;
}
.missing-label { font-size: 0.8rem; color: #999; margin-bottom: 6px; }

.btn {
  padding: 6px 14px;
  border: 1px solid #ddd;
  border-radius: 6px;
  background: #fff;
  cursor: pointer;
  font-size: 13px;
}
.btn-primary { background: #1976d2; color: #fff; border-color: #1976d2; }
.btn-small { padding: 4px 10px; font-size: 12px; }

.loading, .error { text-align: center; padding: 40px; color: #666; }
.error { color: #f44336; }

@media (max-width: 767px) {
  h1 { font-size: 1.4rem; }

  .charts-row {
    flex-direction: column;
    gap: 14px;
  }
  .chart-card { padding: 14px; }
  .chart-container { height: 220px; }

  .score-card { width: 100%; padding: 20px; }
  .score-value { font-size: 2.4rem; }

  .recommendations, .suggestions, .matching-recipes { padding: 14px; }
  .recipe-match-header { align-items: flex-start; gap: 8px; }
  .btn { padding: 10px 14px; font-size: 14px; }
}
</style>
