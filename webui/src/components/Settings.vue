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
              {{ goalVersion }}
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
                :text="`Update to ${latestRelease}`"
              />
            </v-btn>
            <Releases
              class="ml-2"
              :algowin="ALGOWIN"
              @release="updateRelease"
            />
          </v-col>
        </v-row>
        <v-row align="center">
          <v-col>
            <div>Show FNet</div>
            <div class="text-caption text-grey">Must install FNet binaries</div>
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
import AWN from "@/services/api";
import { delay } from "@/utils";
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

const ALGOWIN = "https://api.github.com/repos/GalaxyPay/algowin";

const goalVersion = ref();
const latestRelease = ref();

onBeforeMount(() => {
  getVersion();
});

async function getVersion() {
  try {
    const version = await AWN.api.get("goal/version");
    goalVersion.value = version.data.substring(
      version.data.indexOf("\n") + 1,
      version.data.indexOf("dev") - 1
    );

    if (goalVersion.value) store.ready = true;

    const latest = (await axios({ url: `${ALGOWIN}/releases/latest` })).data;
    latestRelease.value = latest.name.substring(1, latest.name.indexOf("-"));

    if (!goalVersion.value) updateLatest(true);

    store.updateAvailable = latestRelease.value !== goalVersion.value;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err.message, "error");
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
    await delay(500);
    store.ready = false;
    await AWN.api.post("goal/update", { name: release });
    await getVersion();
    store.stopNodeServices = false;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(err.message, "error");
  }
  store.downloading = false;
}

const showFNet = computed(() => activeNetwork.value === "fnet");

async function setFNet() {
  setActiveNetwork((showFNet.value ? "mainnet" : "fnet") as NetworkId);
  store.refresh++;
}
</script>
