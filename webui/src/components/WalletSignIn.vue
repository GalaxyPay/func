<template>
  <div>
    <p v-if="!secure" class="footnote">
      Wallet sign-in needs HTTPS.
      <a :href="httpsUrl">Open {{ httpsUrl }}</a>
      and accept the self-signed certificate warning.
    </p>
    <template v-else>
      <div class="d-flex flex-wrap ga-2 justify-center">
        <v-btn
          v-for="wallet in signers"
          :key="wallet.id"
          variant="outlined"
          class="text-none"
          :loading="busy === wallet.id"
          :disabled="!!busy && busy !== wallet.id"
          @click="choose(wallet)"
        >
          <template #prepend>
            <v-avatar size="20" rounded="0" :image="wallet.metadata.icon" />
          </template>
          {{ label }} {{ wallet.metadata.name }}
        </v-btn>
      </div>
      <div v-if="picking" class="d-flex align-center ga-2 mt-3">
        <v-select
          v-model="address"
          :items="picking.accounts.map((a) => a.address)"
          label="Account"
          density="compact"
          hide-details
          class="font-mono"
        />
        <v-btn
          color="primary"
          variant="flat"
          :loading="busy === picking.id"
          @click="sign(picking, address)"
          text="Sign"
        />
      </div>
      <p v-if="error" class="text-error text-body-2 mt-2">{{ error }}</p>
    </template>
  </div>
</template>

<script lang="ts" setup>
// Sign-in with Algorand: has a wallet sign a single-use FUNC challenge (ARC-60
// signData, SIWA payload) and hands the result to `submit`. Only wallets that
// support signData are offered, and browsers only allow it over HTTPS.
// Algorand only: FUNC checks rekeys (the signer) on Algorand mainnet.
import type { WalletProof } from "@/types";
import { insecureContextHint, walletNeedsSecureContext } from "@/utils";
import { ScopeType, useWallet, type Wallet } from "@txnlab/use-wallet-vue";

const props = defineProps<{
  label: string;
  submit: (proof: WalletProof) => Promise<void>;
  // Checked before asking the wallet, e.g. that the rest of a form is valid.
  ready?: () => Promise<boolean>;
}>();

const store = useAppStore();
const { wallets, signData } = useWallet();

const secure = window.isSecureContext;
const httpsUrl = `https://${location.hostname}:3537`;
const signers = computed(() => wallets.value.filter((w) => w.canSignData));
const busy = ref<string>();
const error = ref("");
const picking = ref<Wallet>();
const address = ref("");

async function choose(wallet: Wallet) {
  error.value = "";
  picking.value = undefined;
  if (props.ready && !(await props.ready())) return;
  if (!wallet.isConnected && walletNeedsSecureContext(wallet.id)) {
    error.value = `${wallet.metadata.name} can't connect: ${insecureContextHint()}`;
    return;
  }
  busy.value = wallet.id;
  try {
    const accounts = wallet.isConnected
      ? wallet.accounts
      : await wallet.connect();
    wallet.setActive();
    busy.value = undefined;
    if (accounts.length === 1) return await sign(wallet, accounts[0].address);
    // Several accounts: let the user pick the one with participation keys here.
    picking.value = wallet;
    address.value = wallet.activeAccount?.address ?? accounts[0]?.address;
  } catch (err) {
    error.value = describe(err);
  }
  busy.value = undefined;
}

async function sign(wallet: Wallet, account: string) {
  error.value = "";
  if (props.ready && !(await props.ready())) return;
  busy.value = wallet.id;
  try {
    wallet.setActive();
    wallet.setActiveAccount(account);
    const { data: challenge } = await store.api.get("auth/challenge");
    const issued = new Date();
    const siwa = {
      domain: location.host,
      account_address: account,
      uri: location.origin,
      version: "1",
      statement: "Sign in to FUNC",
      nonce: challenge.nonce,
      "issued-at": issued.toISOString(),
      "expiration-time": new Date(issued.getTime() + 5 * 60_000).toISOString(),
      chain_id: "283",
      type: "ed25519",
    };
    const data = btoa(JSON.stringify(siwa));
    const signed = await signData(data, {
      scope: ScopeType.AUTH,
      encoding: "base64",
    });
    await props.submit({
      address: account,
      data,
      signer: toBase64(signed.signer),
      domain: signed.domain,
      authenticatorData: toBase64(signed.authenticatorData),
      signature: toBase64(signed.signature),
    });
    picking.value = undefined;
  } catch (err) {
    error.value = describe(err);
  }
  busy.value = undefined;
}

function toBase64(bytes: Uint8Array): string {
  let binary = "";
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary);
}

function describe(err: any): string {
  const status = err?.response?.status;
  const detail =
    typeof err?.response?.data === "string" ? err.response.data : "";
  if (status === 429) return detail || "Too many attempts";
  if (status) return detail || "Wallet sign-in was rejected";
  if (err?.code === 4100 || err?.code === 4001) return "Cancelled in the wallet";
  return err?.message || "Wallet sign-in failed";
}
</script>
