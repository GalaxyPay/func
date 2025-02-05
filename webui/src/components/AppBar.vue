<template>
  <v-app-bar app :order="0">
    <NodeIcon color="currentColor" :width="30" class="ml-3 text-primary" />
    <div class="ml-2 text-subtitle-1 text-blu">
      FUNC
      {{
        store.showMachineName && store.machineName
          ? `(${store.machineName})`
          : ""
      }}
      <div class="ml-1 app-version text-grey">{{ appVersion }}</div>
    </div>
    <v-spacer />
    <v-btn icon @click="showSettings = true">
      <v-icon
        :icon="mdiCog"
        :color="
          (store.nodeUpdateAvailable || store.funcUpdateAvailable) &&
          !store.downloading
            ? 'warning'
            : ''
        "
      />
      <v-tooltip text="Settings" activator="parent" location="bottom" />
    </v-btn>
    <v-btn
      color="primary"
      variant="tonal"
      :icon="store.showMachineName && xs"
      class="mr-3"
    >
      <template v-if="!store.showMachineName || !xs">
        {{ formatAddr(activeAccount?.address) || "Connect Wallet" }}
      </template>
      <template v-else>
        <v-icon :icon="mdiWallet" />
      </template>
      <v-menu activator="parent" v-model="store.connectMenu" scrim>
        <v-list>
          <v-container :min-width="350">
            <v-row v-if="activeAccount">
              <v-col class="text-caption" style="font-family: monospace">
                {{ activeAccount?.address }}
              </v-col>
              <v-col class="pt-2 text-right">
                <v-icon
                  size="x-small"
                  :icon="mdiContentCopy"
                  @click.stop="copyToClipboard()"
                />
              </v-col>
            </v-row>
            <v-row>
              <v-col>Balance:</v-col>
              <v-col class="text-right">
                <span
                  v-if="activeNetwork === 'voimain'"
                  class="font-weight-bold"
                  >V
                </span>
                <AlgoIcon
                  v-else
                  color="currentColor"
                  :width="13"
                  class="mr-1"
                />
                {{
                  account
                    ? (Number(account.amount) / 10 ** 6).toLocaleString(
                        undefined,
                        {
                          maximumFractionDigits: 6,
                        }
                      )
                    : "-"
                }}
              </v-col>
            </v-row>
          </v-container>
          <template v-for="wallet in wallets" :key="wallet.id">
            <v-divider />
            <v-list-item
              :prepend-avatar="wallet.metadata.icon"
              :height="wallet.isActive ? '70' : ''"
            >
              <div class="d-flex align-center">
                {{ wallet.metadata.name }}
                <v-select
                  v-if="wallet.isActive"
                  :items="items"
                  :model-value="{...activeAccount!, title: formatAddr(activeAccount!.address)}"
                  return-object
                  class="pl-2"
                  density="compact"
                  variant="solo-filled"
                  hide-details
                  @click.stop
                  @update:model-value="(acct) => switchAccount(wallet, acct)"
                />
              </div>
              <template #append>
                <v-btn
                  class="ml-3"
                  color="white no-uppercase"
                  size="small"
                  variant="tonal"
                  :min-width="140"
                  @click.stop="walletAction(wallet)"
                >
                  <v-icon
                    v-if="wallet.isActive"
                    :icon="mdiMinusCircleOutline"
                    color="error"
                  />
                  <v-icon
                    v-else-if="wallet.isConnected"
                    :icon="mdiLightningBoltOutline"
                    color="primary"
                  />
                  <v-icon v-else :icon="mdiPlusCircleOutline" color="success" />
                  <div class="ml-1">
                    {{
                      wallet.isActive
                        ? "Disconnect"
                        : wallet.isConnected
                        ? "Make Active"
                        : "Connect"
                    }}
                  </div>
                </v-btn>
              </template>
            </v-list-item>
          </template>
        </v-list>
      </v-menu>
    </v-btn>
    <Settings :visible="showSettings" @close="showSettings = false" />
  </v-app-bar>
</template>

<script lang="ts" setup>
import { formatAddr } from "@/utils";
import {
  mdiCog,
  mdiContentCopy,
  mdiLightningBoltOutline,
  mdiMinusCircleOutline,
  mdiPlusCircleOutline,
  mdiWallet,
} from "@mdi/js";
import { Wallet, WalletAccount, useWallet } from "@txnlab/use-wallet-vue";
import { modelsv2 } from "algosdk";
import { useDisplay } from "vuetify";

const store = useAppStore();
const { activeAccount, activeNetwork, activeWallet, algodClient, wallets } =
  useWallet();
const { xs } = useDisplay();

const appVersion = __APP_VERSION__;
const account = ref<modelsv2.Account>();
const showSettings = ref(false);

onBeforeMount(() => {
  store.refresh++;
});
async function walletAction(wallet: Wallet) {
  try {
    wallet.isActive
      ? await wallet.disconnect()
      : wallet.isConnected
      ? wallet.setActive()
      : await wallet.connect();
    store.refresh++;
    store.connectMenu = false;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

const items = computed(() => {
  const val = activeWallet.value?.accounts.map((a) => ({
    ...a,
    title: formatAddr(a.address),
  }));
  return val;
});

async function switchAccount(wallet: Wallet, acct: WalletAccount) {
  wallet.setActiveAccount(acct.address);
  store.refresh++;
  store.connectMenu = false;
}

function copyToClipboard() {
  if (!activeAccount.value) return;
  navigator.clipboard.writeText(activeAccount.value.address);
  store.setSnackbar("Address Copied", "info", 1000);
}

watch(
  () => store.refresh,
  async () => {
    if (activeAccount.value) {
      const info = await algodClient.value
        .accountInformation(activeAccount.value.address)
        .do();
      account.value = modelsv2.Account.from_obj_for_encoding(info);
    } else {
      account.value = undefined;
    }
  }
);

watch(
  () => [store.showMachineName, store.machineName],
  ([val1, val2]) => {
    if (val1 && val2) document.title = `FUNC (${val2})`;
    else document.title = "FUNC";
  },
  { immediate: true }
);
</script>

<style>
.app-version {
  font-size: 0.7rem;
  line-height: 0.8rem;
}
</style>
