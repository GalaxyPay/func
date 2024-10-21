/**
 * plugins/index.ts
 *
 * Automatically included in `./src/main.ts`
 */

// Plugins
import vuetify from "./vuetify";
import {
  NetworkId,
  WalletId,
  WalletManagerPlugin,
} from "@txnlab/use-wallet-vue";
import pinia from "../stores";

// Types
import type { App } from "vue";

export function registerPlugins(app: App) {
  app
    .use(vuetify)
    .use(pinia)
    .use(WalletManagerPlugin, {
      wallets: [
        {
          id: WalletId.LUTE,
          options: { siteName: "AvmWinNode" },
        },
        WalletId.DEFLY,
        WalletId.PERA,
        WalletId.KIBISIS,
      ],
      network: NetworkId.MAINNET,
    });
}
