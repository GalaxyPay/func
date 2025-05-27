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
            <div>FUNC Version</div>
            <div class="text-caption text-grey">
              {{ appVersion }}
              {{ !store.funcUpdateAvailable ? "(latest)" : "" }}
            </div>
          </v-col>
          <v-col class="text-right">
            <v-btn
              :color="url ? 'warning' : ''"
              variant="tonal"
              :disabled="!url"
              :href="url"
            >
              Download
              <v-tooltip
                activator="parent"
                location="left"
                :text="`Download ${funcLatest}`"
              />
            </v-btn>
          </v-col>
        </v-row>
        <v-row align="center">
          <v-col>
            <div>Node Version</div>
            <div class="text-caption text-grey">
              {{ store.goalVersion?.installed }}
              {{ !store.nodeUpdateAvailable ? "(latest)" : "" }}
            </div>
          </v-col>
          <v-col class="text-right">
            <v-btn
              :color="store.nodeUpdateAvailable ? 'warning' : ''"
              variant="tonal"
              :disabled="!store.nodeUpdateAvailable || store.downloading"
              @click="updateNodeLatest()"
            >
              Update
              <v-tooltip
                activator="parent"
                location="left"
                :text="`Update to ${store.goalVersion?.latest}`"
              />
            </v-btn>
          </v-col>
        </v-row>
        <v-row align="center">
          <v-col>
            <div>Show Machine Name</div>
            <div class="text-caption text-grey">In App Bar</div>
          </v-col>
          <v-col>
            <v-switch
              v-model="store.showMachineName"
              class="d-flex"
              style="justify-content: right"
              color="primary"
              @click.prevent="setShowMachineName(!store.showMachineName)"
            />
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
import { DEFAULT_NETWORK } from "@/data";
import FUNC from "@/services/api";
import { mdiClose } from "@mdi/js";
import { NetworkId, useNetwork } from "@txnlab/use-wallet-vue";

const props = defineProps({ visible: { type: Boolean, required: true } });
const emit = defineEmits(["close"]);

const store = useAppStore();
const { activeNetwork, setActiveNetwork } = useNetwork();

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

const appVersion = __APP_VERSION__;
const funcLatest = ref();
const url = ref();
let init = false;

onBeforeMount(async () => {
  if (activeNetwork.value !== DEFAULT_NETWORK) setShowNetworks(true);
  await getVersion();
  if (store.funcUpdateAvailable) {
    url.value = (await FUNC.api.get("func/latest")).data;
  }
});

const githubClient = axios.create({
  baseURL: "https://api.github.com/repos/GalaxyPay/func/releases",
});

async function getVersion() {
  try {
    const { data } = await githubClient.get("latest");
    funcLatest.value = data?.name.slice(1);
    store.funcUpdateAvailable = funcLatest.value !== appVersion;
    const goalVersion = await FUNC.api.get("goal/version");
    store.goalVersion = goalVersion.data;
    if (store.goalVersion?.installed) store.ready = true;
    else {
      if (!init) {
        init = true;
        updateNodeLatest(true);
      }
    }

    store.nodeUpdateAvailable =
      !!store.goalVersion?.latest &&
      store.goalVersion?.latest !== store.goalVersion?.installed;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
}

async function updateNodeLatest(bypass = false) {
  if (
    !bypass &&
    !confirm("Are you sure you want to update your node to the latest version?")
  )
    return;
  await updateNode("latest");
}

async function updateNode(release: string) {
  try {
    store.downloading = true;
    await FUNC.api.post("goal/update", { name: release });
    await getVersion();
    store.refreshStatus++;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err?.response?.data || err.message, "error");
  }
  store.downloading = false;
}

async function setShowNetworks(val: boolean) {
  store.showNetworks = val;
  localStorage.setItem("showNetworks", val.toString());
  if (!val) setActiveNetwork(DEFAULT_NETWORK as NetworkId);
}

async function setShowMachineName(val: boolean) {
  store.showMachineName = val;
  localStorage.setItem("showMachineName", val.toString());
}
</script>
