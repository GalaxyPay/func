<template>
  <v-container>
    <v-card>
      <v-progress-linear indeterminate v-show="loading" class="mb-n1" />
      <v-card-title class="d-flex">
        Node Version <v-spacer />
        <v-btn
          color="warning"
          variant="tonal"
          v-show="updateAvailable && !loading"
          @click="update()"
        >
          Update
          <v-tooltip
            activator="parent"
            location="bottom"
            :text="`Update to ${latestRelease?.name}`"
          />
        </v-btn>
      </v-card-title>
      <v-card-text>
        <div>{{ goalVersion }}</div>
        <div class="text-center font-italic" v-show="loading">
          Downloading node software, Please wait
        </div>
      </v-card-text>
    </v-card>
  </v-container>
</template>

<script setup lang="ts">
import { delay } from "@/utils";

const store = useAppStore();
const goalVersion = ref();
const latestRelease = ref();
const updateAvailable = ref(false);
const loading = ref(false);

onBeforeMount(() => {
  getVersion();
});

async function getVersion() {
  const version = await axios({ url: "http://localhost:3536/goal/version" });
  goalVersion.value = version.data.substring(
    version.data.indexOf("\n") + 1,
    version.data.indexOf("dev") - 1
  );

  if (goalVersion.value) store.ready = true;

  const releases = await axios({
    url: "https://api.github.com/repos/GalaxyPay/algowin/releases",
  });
  latestRelease.value = releases.data[0];
  const name = latestRelease.value.name;

  if (!goalVersion.value) update(true);

  updateAvailable.value =
    name.substring(1, name.indexOf("-")) !== goalVersion.value;
}

async function update(bypass = false) {
  if (
    !bypass &&
    !confirm("Are you sure you want to update your node to the latest version?")
  )
    return;
  loading.value = true;
  store.stopNodeServices = true;
  await delay(500);
  store.ready = false;
  await axios({ url: "http://localhost:3536/goal/update", method: "post" });
  await getVersion();
  store.stopNodeServices = false;
  loading.value = false;
}
</script>
