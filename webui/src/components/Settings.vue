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
            <div>Node Version</div>
            <div class="text-caption text-grey">
              {{ store.goalVersion?.installed }}
            </div>
          </v-col>
          <v-col class="text-right">
            <v-btn
              :color="store.updateAvailable ? 'warning' : ''"
              variant="tonal"
              :disabled="!store.updateAvailable || store.downloading"
              @click="updateLatest()"
            >
              Update
              <v-tooltip
                activator="parent"
                location="left"
                :text="`Update to ${store.goalVersion?.latest}`"
              />
            </v-btn>
            <Releases class="ml-2" @release="updateRelease" />
          </v-col>
        </v-row>
        <v-row align="center">
          <v-col>
            <div>Show Alternative Networks</div>
          </v-col>
          <v-col>
            <v-switch
              v-model="store.showNetworks"
              class="d-flex"
              style="justify-content: right"
              color="primary"
              @click.prevent="setShowNetworks(!store.showNetworks)"
            />
          </v-col>
        </v-row>
      </v-container>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import FUNC from "@/services/api";
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

let init = false;

onBeforeMount(() => {
  if (activeNetwork.value !== "mainnet") setShowNetworks(true);
  getVersion();
});

async function getVersion() {
  try {
    const version = await FUNC.api.get("goal/version");
    store.goalVersion = version.data;
    if (store.goalVersion?.installed) store.ready = true;
    else {
      if (!init) {
        init = true;
        updateLatest(true);
      } else {
        throw Error("Download Failed");
      }
    }

    store.updateAvailable =
      !!store.goalVersion?.latest &&
      store.goalVersion?.latest !== store.goalVersion?.installed;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function updateLatest(bypass = false) {
  if (
    !bypass &&
    !confirm("Are you sure you want to update your node to the latest version?")
  )
    return;
  await updateRelease("latest");
}

async function updateRelease(release: string) {
  try {
    store.downloading = true;
    store.stopNodeServices = true;
    await FUNC.api.post("goal/update", { name: release });
    await getVersion();
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  store.stopNodeServices = false;
  store.downloading = false;
}

async function setShowNetworks(val: boolean) {
  store.showNetworks = val;
  localStorage.setItem("showNetworks", val.toString());
  if (!val) setActiveNetwork("mainnet" as NetworkId);
}
</script>
