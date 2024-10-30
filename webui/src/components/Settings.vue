<template>
  <v-dialog v-model="show" max-width="600">
    <v-card>
      <v-card-title class="d-flex">
        Settings
        <v-spacer />
        <v-icon color="currentColor" :icon="mdiClose" @click="show = false" />
      </v-card-title>
      <v-container>
        <v-row align="center">
          <v-col>
            <div>Show FNet</div>
            <div class="text-caption text-grey">
              Must install FNet binaries manually
            </div>
          </v-col>
          <v-col>
            <v-switch
              :model-value="showFNet"
              class="d-flex"
              style="justify-content: right"
              @click.prevent="setFNet()"
            />
          </v-col>
        </v-row>
      </v-container>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { mdiClose } from "@mdi/js";
import { NetworkId, useWallet } from "@txnlab/use-wallet-vue";

const props = defineProps({ visible: { type: Boolean, required: true } });
const emit = defineEmits(["close"]);

const store = useAppStore();
const { activeNetwork, setActiveNetwork } = useWallet();

const show = computed({
  get() {
    return props.visible;
  },
  set(val) {
    if (!val) {
      emit("close");
    }
  },
});

const showFNet = computed(() => activeNetwork.value === "fnet");

async function setFNet() {
  setActiveNetwork((showFNet.value ? "mainnet" : "fnet") as NetworkId);
  store.refresh++;
}
</script>
