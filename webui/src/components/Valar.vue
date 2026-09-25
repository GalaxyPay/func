<template>
  <v-dialog v-model="show" max-width="800" persistent>
    <v-card :disabled="loading">
      <v-progress-linear indeterminate v-show="loading" class="mb-n1" />
      <v-card-title class="d-flex">
        Valar Daemon
        <v-spacer />
        <v-icon color="currentColor" :icon="mdiClose" @click="show = false" />
      </v-card-title>
      <v-form ref="form" @submit.prevent="startDaemon()">
        <v-card-text>
          You first need to create a Validator Ad on
          <a href="https://stake.valar.solutions" target="_blank">Valar</a>.
          There you will receive a Validator Ad ID and configure your Manager
          Address. FUNC installs a self-contained Python runtime and the
          <a
            href="https://github.com/ValarStaking/valar/tree/master/projects/valar-daemon"
            target="_blank"
            >Valar daemon</a
          >
          in its data directory, then runs the daemon as a service.
        </v-card-text>
        <v-container>
          <v-row>
            <v-col>
              <v-text-field
                v-model="adIds"
                label="Validator Ad ID(s)"
                hint="Comma-separated"
                persistent-hint
                :rules="[required, validAdIds]"
              />
            </v-col>
          </v-row>
          <v-row>
            <v-col>
              <v-textarea
                v-model="mnemonic"
                rows="2"
                label="Manager Mnemonic"
                :hint="mnemonicHint"
                persistent-hint
                :rules="[required, length25, validMnemonic]"
              />
            </v-col>
          </v-row>
        </v-container>
        <v-card-actions>
          <v-spacer />
          <v-btn
            type="submit"
            text="Start Valar Service"
            color="primary"
            variant="tonal"
          />
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { errorMessage } from "@/utils";
import { mdiClose } from "@mdi/js";
import algosdk from "algosdk";

const props = defineProps({
  visible: { type: Boolean, required: true },
  port: { type: Number, required: true },
  token: { type: String, required: true },
});
const emit = defineEmits(["close"]);

const store = useAppStore();
const required = (v: any) => !!v || "Required";
// The daemon eval()s this list, so accept nothing but integers.
const adIdPattern = /^\s*\d+(\s*,\s*\d+)*\s*$/;
const validAdIds = (v: string) =>
  adIdPattern.test(v || "") || "Comma-separated numbers only";
const length25 = (v: string) =>
  v.split(" ").length === 25 || "Must be 25 words";
const validMnemonic = () => !!mnemonicAcct.value?.addr || "Invalid Mnemonic";
const form = ref();
const adIds = ref<string>();
const mnemonic = ref();
const loading = ref(false);

const show = computed({
  get() {
    return props.visible;
  },
  set(val) {
    if (!val) {
      form.value?.reset();
      emit("close");
    }
  },
});

const mnemonicAcct = computed(() => {
  if (!mnemonic.value) return undefined;
  let val;
  try {
    val = algosdk.mnemonicToSecretKey(mnemonic.value);
  } catch {
    return undefined;
  }
  return val;
});

const mnemonicHint = computed(
  () =>
    mnemonicAcct.value?.addr.toString() ||
    "This is a hot wallet that performs validator functions such as key registrations and fee claims"
);

async function startDaemon() {
  try {
    const { valid } = await form.value.validate();
    if (!valid) return;
    loading.value = true;
    const ids = adIds.value!.split(",").map((s) => Number(s.trim()));
    const config = `[validator_config]
validator_ad_id_list = [${ids.join(", ")}]
validator_manager_mnemonic = ${mnemonic.value}

[algo_client_config]
algod_config_server = http://localhost:${props.port}
algod_config_token = ${props.token}

[logging_config]
max_log_file_size_B = 400*1024
num_of_log_files_per_level = 3

[runtime_config]
loop_period_s = 15
`;
    store.setSnackbar(
      "Installing Python runtime and Valar daemon. This can take a few minutes...",
      "info",
      -1
    );
    await store.api.post("valar", { config });
    await store.api.put("valar/start");
    store.setSnackbar("Valar Started", "success");
    store.refreshStatus++;
    show.value = false;
  } catch (err: any) {
    console.error(err);
    store.setSnackbar(errorMessage(err, "Add Valar service"), "error");
  }
  loading.value = false;
}
</script>
