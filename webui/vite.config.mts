// Plugins
import AutoImport from "unplugin-auto-import/vite";
import Components from "unplugin-vue-components/vite";
import vue from "@vitejs/plugin-vue";
import vuetify from "vite-plugin-vuetify";
import { vuetifyStylesFallback } from "./scripts/vuetifyStylesFallback.mts";
import { nodePolyfills } from "vite-plugin-node-polyfills";

// Utilities
import { defineConfig } from "vite";
import { fileURLToPath, URL } from "node:url";

const vuetifySettings = fileURLToPath(
  new URL("./src/styles/settings.scss", import.meta.url)
);

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vuetify({ styles: { configFile: vuetifySettings } }),
    vuetifyStylesFallback(vuetifySettings),
    AutoImport({
      imports: [
        "vue",
        { axios: [["default", "axios"]] },
        { "@/stores/app": ["useAppStore"] },
      ],
      dts: "src/auto-imports.d.ts",
      eslintrc: {
        enabled: true,
      },
      vueTemplate: true,
    }),
    Components({
      dts: "src/components.d.ts",
    }),
    nodePolyfills(),
  ],
  define: {
    "process.env": {},
    __APP_VERSION__: JSON.stringify(process.env.npm_package_version),
  },
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
    extensions: [".js", ".json", ".jsx", ".mjs", ".ts", ".tsx", ".vue"],
  },
  server: {
    port: 3000,
  },
  build: {
    outDir: "../publish/wwwroot",
    emptyOutDir: true,
    chunkSizeWarningLimit: 1024,
    rollupOptions: {
      output: {
        codeSplitting: {
          groups: [
            {
              test: /node_modules\/algosdk/,
              name: "algosdk",
            },
          ],
        },
      },
    },
  },
});
