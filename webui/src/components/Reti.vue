<template>
  <v-dialog v-model="show" max-width="800" persistent>
    <v-card :disabled="loading">
      <v-progress-linear indeterminate v-show="loading" class="mb-n1" />
      <v-card-title class="d-flex">
        Reti Validator {{ version.current }}
        <v-spacer />
        <v-icon color="currentColor" :icon="mdiClose" @click="show = false" />
      </v-card-title>
      <v-form ref="form" @submit.prevent="startValidator()">
        <v-card-text>
          You first need to register your validator and pool(s) with
          <a href="https://reti.vercel.app/" target="_blank">Reti</a>. There you
          will receive a Validator ID, and configure your Node Number and
          Manager Address.
        </v-card-text>
        <v-container>
          <v-row>
            <v-col>
              <v-text-field
                v-model.number="validatorId"
                type="number"
                label="Validator ID"
                :rules="[required]"
              />
            </v-col>
            <v-col>
              <v-text-field
                v-model.number="nodeNum"
                type="number"
                label="Node Number"
                :rules="[required]"
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
                :rules="[required, validMnemonic]"
              />
            </v-col>
          </v-row>
        </v-container>
        <v-card-actions>
          <v-btn
            v-show="version.current && version.current != version.latest"
            variant="tonal"
            color="warning"
          >
            Update
            <v-tooltip
              activator="parent"
              location="top"
              :text="`Update to ${version.latest}`"
            />
          </v-btn>
          <v-spacer />
          <v-btn type="submit" text="Start Reti Service" />
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { mdiClose } from "@mdi/js";
import algosdk from "algosdk";

const props = defineProps({
  visible: { type: Boolean, required: true },
  port: { type: Number, required: true },
  token: { type: String, required: true },
});
const emit = defineEmits(["close"]);

const required = (v: any) => !!v || "Required";
const validMnemonic = () => !!mnemonicAcct.value?.addr || "Invalid Mnemonic";
const form = ref();
const validatorId = ref();
const nodeNum = ref();
const mnemonic = ref();
const loading = ref(false);
const emptyVersion = { latest: undefined, current: undefined };
const version = ref(emptyVersion);

const show = computed({
  get() {
    return props.visible;
  },
  set(val) {
    if (!val) {
      version.value = emptyVersion;
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
    mnemonicAcct.value?.addr ||
    "This is a hot wallet that performs validator functions such as key registrations and rewards payouts"
);

async function startValidator() {
  const { valid } = await form.value.validate();
  if (!valid) return;
  const env = `ALGO_ALGOD_URL=http://localhost:${props.port}
ALGO_ALGOD_TOKEN=${props.token}
RETI_VALIDATORID=${validatorId.value}
RETI_NODENUM=${nodeNum.value}
MANAGER_MNEMONIC=${mnemonic.value}`;
  await axios({
    url: "http://localhost:3536/reti",
    method: "post",
    data: { env },
  });
}

watch(
  () => show.value,
  async (val) => {
    if (val) {
      loading.value = true;
      const releases = await axios({
        url: "https://api.github.com/repos/TxnLab/reti/releases",
      });
      version.value.latest = releases.data[0].tag_name;
      const resp = await axios({
        url:
          "http://localhost:3536/reti/version?latest=" + version.value.latest,
      });
      version.value.current = resp.data.slice(
        27,
        27 + resp.data.slice(27).indexOf(" ")
      );
      loading.value = false;
    }
  }
);
</script>
