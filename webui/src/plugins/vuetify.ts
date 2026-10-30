/**
 * plugins/vuetify.ts
 *
 * Framework documentation: https://vuetifyjs.com`
 */

// Styles
import "vuetify/styles";

// Composables
import { createVuetify, type ThemeDefinition } from "vuetify";
import { aliases, mdi } from "vuetify/iconsets/mdi-svg";

/*
 * Design tokens. oklch is the source of truth (in the comments); Vuetify
 * needs hex. Neutrals carry a faint cyan tint (hue 230) under the FUNC
 * primary. Besides Vuetify's own keys the theme has:
 * surface-selected, border-subtle, border, border-strong, border-input,
 * text-body, text-muted, text-dim, text-placeholder, text-disabled, icon,
 * primary-hover, primary-fill, primary-fill-hover, primary-text, error-text
 * and danger-fill. primary-fill is the background of flat primary buttons.
 */
const dark: ThemeDefinition = {
  dark: true,
  colors: {
    background: "#090c0d", // oklch(0.150 0.006 230)
    surface: "#0f1213", // oklch(0.178 0.006 230)
    "surface-bright": "#131618", // oklch(0.197 0.006 230)
    "surface-light": "#1c2022", // = surface-selected
    "surface-variant": "#16191b", // oklch(0.210 0.006 230)
    "surface-selected": "#1c2022", // oklch(0.240 0.006 230)
    "border-subtle": "#121617", // oklch(0.196 0.006 230)
    border: "#191d1e", // oklch(0.227 0.006 230)
    "border-strong": "#202426", // oklch(0.257 0.006 230)
    "border-input": "#5f6467", // oklch(0.500 0.008 230)
    "on-background": "#eaedef",
    "on-surface": "#eaedef", // oklch(0.944 0.004 230)
    "on-surface-variant": "#eaedef",
    "text-body": "#c0c6c9", // oklch(0.822 0.008 230)
    "text-muted": "#868d91", // oklch(0.640 0.010 230)
    "text-dim": "#858c90", // oklch(0.635 0.010 230)
    "text-placeholder": "#787e82", // oklch(0.590 0.010 230)
    "text-disabled": "#51575b", // oklch(0.453 0.010 230)
    icon: "#6a7175", // oklch(0.545 0.010 230)
    primary: "#1acbf7", // oklch(0.781 0.140 221.5)
    "primary-hover": "#00bbe6", // oklch(0.731 0.140 221.5)
    "primary-fill": "#1acbf7", // = primary
    "primary-fill-hover": "#00bbe6", // = primary-hover
    "on-primary": "#0b181c", // oklch(0.200 0.020 221.5)
    "primary-text": "#81e0ff", // oklch(0.860 0.100 221.5)
    error: "#f46a82", // oklch(0.700 0.170 12)
    "error-text": "#fa7f91", // oklch(0.740 0.150 12)
    "danger-fill": "#c72c53", // oklch(0.550 0.190 12)
    "on-danger-fill": "#fffdff",
    warning: "#e6b37b", // oklch(0.800 0.093 68)
    success: "#57ce70", // oklch(0.760 0.170 148), clear of the cyan primary
    info: "#7ed2ee", // oklch(0.820 0.090 221.5)
    "on-error": "#090c0d",
    "on-warning": "#090c0d",
    "on-success": "#090c0d",
    "on-info": "#090c0d",
  },
  variables: {
    "border-color": "#191d1e",
    "border-opacity": 1,
  },
};

// https://vuetifyjs.com/en/introduction/why-vuetify/#feature-guides
export default createVuetify({
  defaults: {
    global: {
      ripple: false,
    },
    VBtn: {
      variant: "text",
      color: "primary",
    },
    VSelect: {
      variant: "outlined",
    },
    VTextField: {
      variant: "outlined",
    },
    VTextarea: {
      variant: "outlined",
    },
    VFileInput: {
      variant: "outlined",
    },
    VCheckbox: {
      color: "primary",
    },
    VCheckboxBtn: {
      color: "primary",
    },
    VRadio: {
      color: "primary",
    },
    VSwitch: {
      color: "primary",
      inset: true,
      density: "compact",
    },
    VProgressLinear: {
      color: "primary",
      // The track defaults to --v-border-opacity, which the theme sets to 1:
      // a solid track hides the indeterminate bars moving over it.
      bgOpacity: 0.12,
    },
    VProgressCircular: {
      color: "primary",
    },
    VAlert: {
      variant: "tonal",
    },
    VMenu: {
      offset: 4,
    },
  },
  theme: {
    defaultTheme: "dark",
    themes: { dark },
  },
  icons: {
    defaultSet: "mdi",
    aliases,
    sets: { mdi },
  },
});
