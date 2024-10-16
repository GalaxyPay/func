<template>
  <svg
    :width="width"
    :height="height"
    :fill="color"
    :stroke="color"
    :opacity="opacity"
    version="1.1"
    viewBox="0 0 190 190"
    xml:space="preserve"
    xmlns="http://www.w3.org/2000/svg"
  >
    <g transform="translate(-19.507 -49.564)">
      <g fill="none">
        <g stroke-width="4.8098">
          <path d="m185.71 104.06-0.0872 82.173" />
          <path d="m114.59 62.897 71.12 41.162" />
          <path d="m43.387 103.91 71.208-41.011-0.17453 164.35" />
          <path d="m43.3 186.08 0.08727-82.173" />
          <path d="m114.42 227.24-71.12-41.162" />
          <path d="m185.63 186.23-71.208 41.011" />
        </g>
        <path d="m43.3 186.08 142.42-82.022" stroke-width="5" />
        <path d="m43.387 103.91 142.24 82.324" stroke-width="5" />
      </g>
      <g stroke-width=".0030332">
        <circle cx="43.343" cy="103.91" r="12.506" />
        <circle cx="185.67" cy="104.06" r="12.506" />
        <circle cx="43.343" cy="186.08" r="12.506" />
        <circle cx="185.67" cy="186.24" r="12.506" />
        <circle cx="114.51" cy="225.23" r="12.506" />
        <circle cx="114.51" cy="63.9" r="12.506" />
      </g>
      <circle cx="114.51" cy="145.07" r="15.165" stroke-width=".003678" />
    </g>
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
