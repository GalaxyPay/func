<template>
  <v-app-bar
    app
    :order="0"
    flat
    color="background"
    height="56"
    class="app-bar"
  >
    <div class="brand">
      <NodeIcon color="currentColor" :width="26" class="text-primary" />
      <div class="brand-text">
        <span class="brand-name">
          FUNC
          <span
            v-if="store.showMachineName && store.machineName"
            class="brand-machine"
          >
            ({{ store.machineName }})
          </span>
        </span>
        <span class="brand-version">{{ appVersion }}</span>
      </div>
    </div>
    <v-spacer />
    <div class="bar-actions">
      <v-btn icon size="small" @click="openMessages">
        <v-badge
          :model-value="store.unreadCount > 0"
          location="top right"
          color="error"
          :content="store.unreadCount"
        >
          <v-icon :icon="mdiBell" size="20" />
        </v-badge>
        <v-tooltip text="Messages" activator="parent" location="bottom" />
      </v-btn>
      <v-btn icon size="small" @click="showSettings = true">
        <v-badge
          :model-value="
            ((store.nodeUpdateAvailable && !store.showNodeVersions) ||
              store.funcUpdateAvailable) &&
            !store.downloading
          "
          location="top right"
          color="warning"
          dot
        >
          <v-icon :icon="mdiCog" size="20" />
        </v-badge>
        <v-tooltip text="Settings" activator="parent" location="bottom" />
      </v-btn>
      <v-btn
        :variant="activeAccount ? 'outlined' : 'flat'"
        :icon="xs"
        :size="xs ? 'small' : undefined"
        class="wallet-btn"
      >
        <template v-if="!xs">
          <span :class="activeAccount && 'font-mono'">
            {{ formatAddr(activeAccount?.address) || "Connect Wallet" }}
          </span>
        </template>
        <template v-else>
          <v-icon :icon="mdiWallet" size="20" />
        </template>
        <v-menu activator="parent" v-model="store.connectMenu" scrim>
          <v-list class="wallet-menu" :min-width="xs ? 300 : 380">
            <div class="wallet-summary">
              <div v-if="activeAccount" class="wallet-addr">
                <span class="font-mono">{{ activeAccount?.address }}</span>
                <v-icon
                  size="16"
                  class="clickable"
                  :icon="mdiContentCopy"
                  @click.stop="copyToClipboard()"
                />
              </div>
              <div class="wallet-balance">
                <span class="text-muted">Balance</span>
                <span class="balance-amount">
                  <span
                    v-if="activeNetwork === 'voimain'"
                    class="font-weight-bold"
                    >V</span
                  >
                  <AlgoIcon v-else color="currentColor" :width="12" />
                  {{
                    store.account
                      ? (Number(store.account.amount) / 10 ** 6).toLocaleString(
                          undefined,
                          {
                            maximumFractionDigits: 6,
                          }
                        )
                      : "-"
                  }}
                </span>
              </div>
            </div>
            <template v-for="wallet in wallets" :key="wallet.id">
              <v-divider />
              <v-list-item
                class="wallet-row"
                :prepend-avatar="wallet.metadata.icon"
              >
                <div class="d-flex align-center ga-2">
                  <span class="wallet-name">{{ wallet.metadata.name }}</span>
                  <v-select
                    v-if="wallet.isActive"
                    :items="items"
                    :model-value="{
                      ...activeAccount!,
                      title: formatAddr(activeAccount!.address),
                    }"
                    return-object
                    class="wallet-account font-mono"
                    hide-details
                    @click.stop
                    @update:model-value="(acct) => switchAccount(wallet, acct)"
                  />
                </div>
                <template #append>
                  <!-- Icon-only on xs so the account select keeps its room -->
                  <v-btn
                    class="ml-3 wallet-action"
                    size="small"
                    variant="outlined"
                    :icon="xs"
                    :min-width="xs ? undefined : 128"
                    :aria-label="walletActionLabel(wallet)"
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
                    <v-icon
                      v-else
                      :icon="mdiPlusCircleOutline"
                      color="success"
                    />
                    <span v-if="!xs" class="ml-2">
                      {{ walletActionLabel(wallet) }}
                    </span>
                  </v-btn>
                </template>
              </v-list-item>
            </template>
          </v-list>
        </v-menu>
      </v-btn>
      <v-btn icon size="small" href="https://func.algo.xyz" target="_blank">
        <v-icon :icon="mdiHelpCircleOutline" size="20" />
        <v-tooltip text="Help" activator="parent" location="bottom" />
      </v-btn>
    </div>
    <Settings :visible="showSettings" @close="showSettings = false" />
    <Messages :visible="showMessages" @close="showMessages = false" />
    <Admin :visible="showAdmin" @close="showAdmin = false" />
  </v-app-bar>
</template>

<script lang="ts" setup>
import { errorMessage, formatAddr } from "@/utils";
import {
  mdiBell,
  mdiCog,
  mdiContentCopy,
  mdiHelpCircleOutline,
  mdiLightningBoltOutline,
  mdiMinusCircleOutline,
  mdiPlusCircleOutline,
  mdiWallet,
} from "@mdi/js";
import {
  Wallet,
  WalletAccount,
  useNetwork,
  useWallet,
} from "@txnlab/use-wallet-vue";
import { useDisplay } from "vuetify";

const store = useAppStore();
const { activeAccount, activeWallet, algodClient, wallets } = useWallet();
const { activeNetwork } = useNetwork();
const { xs } = useDisplay();

const appVersion = __APP_VERSION__;
const showSettings = ref(false);
const showMessages = ref(false);
const showAdmin = ref(false);

// Shift-click the bell to open the (hidden) contract admin dialog.
function openMessages(e: MouseEvent) {
  if (e.shiftKey) showAdmin.value = true;
  else showMessages.value = true;
}

onBeforeMount(() => {
  store.refreshPart++;
  store
    .fetchMessages()
    .catch((err) => console.error(errorMessage(err, "Load messages"), err));
});
function walletActionLabel(wallet: Wallet) {
  return wallet.isActive
    ? "Disconnect"
    : wallet.isConnected
      ? "Make Active"
      : "Connect";
}

async function walletAction(wallet: Wallet) {
  try {
    wallet.isActive
      ? await wallet.disconnect()
      : wallet.isConnected
        ? wallet.setActive()
        : await wallet.connect();
    store.refreshPart++;
    store.connectMenu = false;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(
      errorMessage(err, `${wallet.metadata.name} wallet request`, "the wallet"),
      "error"
    );
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
  store.refreshPart++;
  store.connectMenu = false;
}

function copyToClipboard() {
  if (!activeAccount.value) return;
  navigator.clipboard.writeText(activeAccount.value.address);
  store.setSnackbar("Address Copied", "info", 1000);
}

watch(
  () => store.refreshPart,
  async () => {
    if (activeAccount.value) {
      store.account = await algodClient.value
        .accountInformation(activeAccount.value.address)
        .do();
    } else {
      store.account = undefined;
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

<style scoped>
.app-bar {
  border-bottom: 1px solid rgb(var(--v-theme-border)) !important;
}
.app-bar :deep(.v-toolbar__content) {
  padding-inline: 16px 10px;
}
.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}
.brand-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}
.brand-name {
  font-size: 15px;
  font-weight: 600;
  letter-spacing: 0.02em;
  line-height: 1.25;
  white-space: nowrap;
}
.brand-machine {
  font-weight: 400;
  letter-spacing: normal;
  color: rgb(var(--v-theme-text-muted));
}
.brand-version {
  font: 400 11px/1.3 var(--font-mono);
  color: rgb(var(--v-theme-text-dim));
}
.bar-actions {
  display: flex;
  align-items: center;
  gap: 4px;
}
.wallet-btn:not(.v-btn--icon) {
  margin-inline: 6px;
}
.wallet-menu {
  padding: 0 !important;
}
.wallet-summary {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 14px 16px;
}
.wallet-addr {
  display: flex;
  align-items: flex-start;
  gap: 10px;
  font-size: 12px;
  line-height: 1.5;
  color: rgb(var(--v-theme-text-body));
  word-break: break-all;
}
.wallet-balance {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 13px;
}
.balance-amount {
  display: inline-flex;
  align-items: baseline;
  gap: 0.45em;
  font: 500 14px var(--font-mono);
}
.wallet-menu :deep(.v-divider) {
  margin: 0 !important;
}
.wallet-row {
  min-height: 60px;
  padding: 10px 12px 10px 16px !important;
  border-radius: 0 !important;
}
.wallet-row:hover {
  background: transparent !important;
}
.wallet-name {
  font-size: 14px;
  white-space: nowrap;
}
.wallet-account {
  min-width: 150px;
}
.wallet-action :deep(.v-icon) {
  font-size: 16px;
}
.wallet-action.v-btn--icon {
  --v-btn-height: 32px;
  width: 32px;
  height: 32px;
  border-radius: 8px;
}
</style>
