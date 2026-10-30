<template>
  <v-dialog v-model="show" max-width="600">
    <v-card>
      <v-card-title class="d-flex">
        Settings
        <v-spacer />
        <v-icon :icon="mdiClose" @click="show = false" />
      </v-card-title>
      <div class="settings-body">
        <div class="group">
          <div class="set-row">
            <div class="set-title">Password</div>
            <div class="set-desc">Required to control this node</div>
            <div class="set-actions">
              <v-btn
                variant="outlined"
                size="small"
                @click="showPassword = true"
              >
                Change
              </v-btn>
              <v-btn variant="outlined" size="small" @click="store.signOut()">
                Sign Out
              </v-btn>
            </div>
          </div>
          <div class="set-row">
            <div class="set-title">FUNC Version</div>
            <div class="set-desc">
              <span class="font-mono">{{ appVersion }}</span>
              {{ !store.funcUpdateAvailable ? "(latest)" : "" }}
            </div>
            <div class="set-actions">
              <v-btn
                :color="
                  store.funcUpdateAvailable && !updatingFunc ? 'warning' : ''
                "
                :variant="
                  store.funcUpdateAvailable && !updatingFunc
                    ? 'flat'
                    : 'outlined'
                "
                size="small"
                :disabled="!store.funcUpdateAvailable || updatingFunc"
                :loading="updatingFunc"
                @click="updateFunc()"
              >
                Update
                <v-tooltip
                  activator="parent"
                  location="left"
                  :text="`Update to ${funcLatest}`"
                />
              </v-btn>
            </div>
          </div>
          <div class="set-row">
            <div class="set-title">Node Version</div>
            <div class="set-desc">
              <span class="font-mono">{{ store.goalVersion?.installed }}</span>
              {{ !store.nodeUpdateAvailable ? "(latest)" : "" }}
            </div>
            <div class="set-actions">
              <Releases
                @release="updateNode"
                v-if="store.showNodeVersions"
              />
              <v-btn
                :color="store.nodeUpdateAvailable ? 'warning' : ''"
                :variant="store.nodeUpdateAvailable ? 'flat' : 'outlined'"
                size="small"
                :disabled="!store.nodeUpdateAvailable || store.downloading"
                @click="updateNodeLatest()"
                v-else
              >
                Update
                <v-tooltip
                  activator="parent"
                  location="left"
                  :text="`Update to ${store.goalVersion?.latest}`"
                />
              </v-btn>
            </div>
          </div>
        </div>
        <div class="group">
          <div class="set-row">
            <div class="set-title">Manual Node Version Selection</div>
            <div class="set-desc">Also suppresses new version alerts</div>
            <div class="set-actions">
              <v-switch
                v-model="store.showNodeVersions"
                @click.prevent="setShowNodeVersions(!store.showNodeVersions)"
              />
            </div>
          </div>
          <div class="set-row">
            <div class="set-title">Show Machine Name</div>
            <div class="set-desc">In App Bar</div>
            <div class="set-actions">
              <v-switch
                v-model="store.showMachineName"
                @click.prevent="setShowMachineName(!store.showMachineName)"
              />
            </div>
          </div>
          <div class="set-row">
            <div class="set-title">Show Alternative Networks</div>
            <div class="set-actions">
              <v-switch
                v-model="store.showNetworks"
                @click.prevent="setShowNetworks(!store.showNetworks)"
              />
            </div>
          </div>
        </div>
      </div>
    </v-card>
    <v-dialog v-model="showPassword" max-width="480">
      <v-card>
        <v-form ref="passwordForm" @submit.prevent="changePassword()">
          <v-card-title>Change Password</v-card-title>
          <v-card-text>
            <v-text-field
              v-model="currentPassword"
              label="Current Password"
              type="password"
              autocomplete="current-password"
              :rules="[required]"
              :error-messages="passwordError"
              @update:model-value="passwordError = ''"
            />
            <v-text-field
              v-model="newPassword"
              label="New Password"
              type="password"
              autocomplete="new-password"
              :rules="[required, minLength]"
              hint="At least 8 characters"
              persistent-hint
              class="mb-2"
            />
            <v-text-field
              v-model="confirmPassword"
              label="Confirm New Password"
              type="password"
              autocomplete="new-password"
              :rules="[required, matches]"
            />
            <div class="footnote mt-2">
              Every other browser and device will be signed out.
            </div>
          </v-card-text>
          <v-card-actions>
            <v-btn
              text="Cancel"
              variant="outlined"
              @click="showPassword = false"
            />
            <v-btn
              type="submit"
              text="Change"
              color="primary"
              variant="flat"
              :loading="changingPassword"
            />
          </v-card-actions>
        </v-form>
      </v-card>
    </v-dialog>
  </v-dialog>
</template>

<script lang="ts" setup>
import { DEFAULT_NETWORK } from "@/data";
import { errorMessage } from "@/utils";
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

const showPassword = ref(false);
const passwordForm = ref();
const currentPassword = ref("");
const newPassword = ref("");
const confirmPassword = ref("");
const passwordError = ref("");
const changingPassword = ref(false);
const required = (v: string) => !!v || "Required";
const minLength = (v: string) =>
  (v?.length ?? 0) >= 8 || "At least 8 characters";
const matches = (v: string) =>
  v === newPassword.value || "Passwords do not match";

async function changePassword() {
  const { valid } = await passwordForm.value.validate();
  if (!valid) return;
  changingPassword.value = true;
  try {
    const { data } = await store.api.put("auth/password", {
      currentPassword: currentPassword.value,
      newPassword: newPassword.value,
    });
    // The change signs out every session; keep this one on the new token.
    store.setApiToken(data.token);
    showPassword.value = false;
    currentPassword.value = newPassword.value = confirmPassword.value = "";
    store.setSnackbar("Password changed", "success");
  } catch (err: any) {
    if (err?.response?.status === 401) {
      passwordError.value = "Current password is incorrect";
    } else {
      store.setSnackbar(errorMessage(err, "Change password"), "error");
    }
  }
  changingPassword.value = false;
}
const updatingFunc = ref(false);
let init = false;

onBeforeMount(async () => {
  if (activeNetwork.value !== DEFAULT_NETWORK) setShowNetworks(true);
  await getVersion();
});

const githubClient = axios.create({
  baseURL: "https://api.github.com/repos/GalaxyPay/func/releases",
});

async function getVersion() {
  try {
    const { data } = await githubClient.get("latest");
    if (data?.name) {
      funcLatest.value = data.name.slice(1);
      store.funcUpdateAvailable = funcLatest.value !== appVersion;
    }
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(errorMessage(err, "Check for FUNC updates"), "error");
  }

  try {
    const goalVersion = await store.api.get("goal/version");
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
    store.ready = true;
    console.error(err);
    store.setSnackbar(errorMessage(err, "Get installed node version"), "error");
  }
}

async function updateFunc() {
  if (!confirm("Are you sure you want to update FUNC to the latest version?"))
    return;
  try {
    updatingFunc.value = true;
    const started = (await store.api.get("func/started")).data;
    await store.api.post("func/update");
    store.setSnackbar(
      "Updating FUNC - this page will reload when complete",
      "info",
      -1
    );
    // The installer restarts the service; poll until its process start
    // time changes (restarts too fast to be caught as downtime), then reload.
    const start = Date.now();
    const interval = setInterval(async () => {
      if (Date.now() - start > 2 * 60 * 1000) {
        clearInterval(interval);
        updatingFunc.value = false;
        store.setSnackbar("FUNC update timed out", "error");
        return;
      }
      try {
        const { data } = await store.api.get("func/started");
        if (data !== started) {
          clearInterval(interval);
          location.reload();
        }
      } catch {
        // service is down mid-update; keep waiting
      }
    }, 1000);
  } catch (err: any) {
    updatingFunc.value = false;
    console.error(err);
    store.setSnackbar(errorMessage(err, "Update FUNC"), "error");
  }
}

async function updateNodeLatest(bypass = false) {
  if (
    !bypass &&
    !confirm("Are you sure you want to update your node to the latest version?")
  )
    return;
  await updateNode("latest", true);
}

async function updateNode(release: string, bypass = false) {
  if (
    !bypass &&
    !confirm(`Are you sure you want to update your node to ${release}?`)
  )
    return;
  try {
    store.downloading = true;
    await store.api.post("goal/update", { name: release });
    await getVersion();
    store.refreshStatus++;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(errorMessage(err, `Update node to ${release}`), "error");
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

async function setShowNodeVersions(val: boolean) {
  store.showNodeVersions = val;
  localStorage.setItem("showNodeVersions", val.toString());
}
</script>

<style scoped>
.settings-body {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 10px 24px 24px;
}
.settings-body .group {
  background: rgb(var(--v-theme-background));
}
.set-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  grid-template-areas:
    "title actions"
    "desc actions";
  align-items: center;
  column-gap: 16px;
  min-height: 60px;
  padding: 12px 16px;
}
.set-row:not(:has(.set-desc)) {
  grid-template-areas: "title actions";
}
.set-row + .set-row {
  border-top: 1px solid rgb(var(--v-theme-border));
}
.set-title {
  grid-area: title;
  font-size: 14px;
}
.set-desc {
  grid-area: desc;
  margin-top: 2px;
  font-size: 12px;
  line-height: 1.45;
  color: rgb(var(--v-theme-text-dim));
}
.set-actions {
  grid-area: actions;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 6px;
}
.set-actions :deep(.v-switch) {
  flex: none;
}
</style>
