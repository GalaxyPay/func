/**
 * plugins/index.ts
 *
 * Automatically included in `./src/main.ts`
 */

// Plugins
import vuetify from "./vuetify";
import { lute } from "@galaxypay/use-wallet-lute";
import { pera } from "@txnlab/use-wallet-pera";
import {
  NetworkConfigBuilder,
  NetworkId,
  WalletManagerPlugin,
} from "@txnlab/use-wallet-vue";
import pinia from "../stores";

// Types
import type { App } from "vue";
import { DEFAULT_NETWORK } from "@/data";

const networks = new NetworkConfigBuilder()
  .addNetwork("voimain", {
    algod: {
      baseServer: "https://mainnet-api.voi.nodely.dev",
      token: "",
    },
  })
  .build();

export function registerPlugins(app: App) {
  app
    .use(vuetify)
    .use(pinia)
    .use(WalletManagerPlugin, {
      wallets: [lute(), pera()],
      defaultNetwork: DEFAULT_NETWORK as NetworkId,
      networks,
    });
}
