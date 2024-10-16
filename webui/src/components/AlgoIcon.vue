<template>
  <svg
    :width="width"
    :height="height"
    :fill="color"
    :opacity="opacity"
    xmlns="http://www.w3.org/2000/svg"
    viewBox="0 0 113 113.4"
    @click="$emit('click')"
  >
    <polygon
      points="19.6 113.4 36 85 52.4 56.7 68.7 28.3 71.4 23.8 72.6 28.3 77.6 47 72 56.7 55.6 85 39.3 113.4 58.9 113.4 75.3 85 83.8 70.3 87.8 85 95.4 113.4 113 113.4 105.4 85 97.8 56.7 95.8 49.4 108 28.3 90.2 28.3 89.6 26.2 83.4 3 82.6 0 65.5 0 65.1 0.6 49.1 28.3 32.7 56.7 16.4 85 0 113.4 19.6 113.4"
    />
  </svg>
</template>

<script>
export default {
  props: {
    width: { type: Number, default: 30 },
    height: { type: Number, default: null },
    color: { type: String, default: null },
    opacity: { type: Number, default: 1 },
  },
  emits: ["click"],
};
</script>
