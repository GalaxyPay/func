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
              :model-value="store.showFNet"
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
const {
  activeNetwork,

  setActiveNetwork,
} = useWallet();

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

async function setFNet() {
  store.showFNet = !store.showFNet;
  localStorage.setItem("showFNet", store.showFNet.toString());
  if (activeNetwork.value !== "voimain")
    setActiveNetwork((store.showFNet ? "fnet" : "mainnet") as NetworkId);
}
</script>
