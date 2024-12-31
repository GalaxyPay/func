<template>
  <v-container
    v-show="store.ready && !store.downloading"
    fluid
    :max-width="1200"
  >
    <v-card>
      <v-tabs
        :model-value="tab"
        color="primary"
        @update:model-value="setNetwork"
      >
        <v-tab
          v-for="n in networks"
          :key="n.id"
          :text="n.title"
          :value="n.title"
        />
      </v-tabs>
      <v-window disabled :model-value="tab">
        <v-window-item v-for="n in networks" :key="n.id" :value="n.title">
          <Node v-if="tab === n.title" :name="n.title" />
        </v-window-item>
      </v-window>
    </v-card>
  </v-container>
</template>

<script setup lang="ts">
import { NetworkId, useWallet } from "@txnlab/use-wallet-vue";

const store = useAppStore();
const { activeNetwork, setActiveNetwork } = useWallet();

const networks = [
  { title: "Algorand", id: "mainnet" },
  { title: "Voi", id: "voimain" },
];

const tab = computed(() => {
  return networks.find((n) => n.id === activeNetwork.value)?.title;
});

function setNetwork(val: any) {
  const nid = networks.find((n) => n.title === val)?.id as NetworkId;
  setActiveNetwork(nid);
  store.refresh++;
}
</script>
