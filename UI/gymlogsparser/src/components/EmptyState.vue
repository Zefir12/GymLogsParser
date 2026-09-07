<template>
  <div class="empty-state">
    <div class="empty-icon">✦</div>

    <h3>No workout parsed yet</h3>

    <p>
      Paste your gym log on the left and let the parser build the editable
      structure here.
    </p>
  </div>
</template>

<style scoped>
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;

  min-height: 500px;

  padding: 40px;

  text-align: center;
}

.empty-icon {
  margin-bottom: 12px;

  color: var(--accent);

  font-size: 20px;

  text-shadow: 0 0 18px rgba(255, 122, 48, 0.45);
}

.empty-state h3 {
  margin: 0;

  color: var(--text-secondary);

  font-size: 12px;
}

.empty-state p {
  max-width: 280px;

  margin: 7px 0 0;

  color: var(--text-tertiary);

  font-size: 9px;
  line-height: 1.6;
}
</style>
