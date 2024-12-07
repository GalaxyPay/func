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
              {{ goalVersion?.installed }}
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
                :text="`Update to ${goalVersion?.latest}`"
              />
            </v-btn>
            <Releases class="ml-2" @release="updateRelease" />
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
import FUNC from "@/services/api";
import { GoalVersion } from "@/types";
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

const goalVersion = ref<GoalVersion>();
let init = false;

onBeforeMount(() => {
  getVersion();
});

async function getVersion() {
  try {
    const version = await FUNC.api.get("goal/version");
    goalVersion.value = version.data;
    if (goalVersion.value?.installed) store.ready = true;
    else {
      if (!init) {
        init = true;
        updateLatest(true);
      } else {
        throw Error("Download Failed");
      }
    }

    store.updateAvailable =
      goalVersion.value?.latest !== goalVersion.value?.installed;
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
    await FUNC.api.post("goal/update", { name: release });
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
