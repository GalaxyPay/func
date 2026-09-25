<template>
  <v-dialog v-model="store.authRequired" max-width="520" persistent>
    <v-card>
      <v-form ref="form" @submit.prevent="submit()">
        <v-card-title>
          {{ setupMode ? "Choose a Password" : "Sign In" }}
        </v-card-title>
        <v-card-text>
          <p class="mb-4" v-if="setupMode">
            FUNC manages services on this computer, so it needs a password
            before anything can control it. You will use this password to sign
            in from this computer and from other devices on your network.
          </p>
          <p class="mb-4" v-else>Enter your FUNC password to continue.</p>
          <v-text-field
            v-model="password"
            label="Password"
            autocomplete="current-password"
            :type="reveal ? 'text' : 'password'"
            :append-inner-icon="reveal ? mdiEyeOff : mdiEye"
            @click:append-inner="reveal = !reveal"
            :rules="setupMode ? [required, minLength] : [required]"
            :error-messages="error"
            @update:model-value="error = ''"
            :hint="setupMode ? 'At least 8 characters' : ''"
            persistent-hint
            class="mb-2"
          />
          <v-text-field
            v-if="setupMode"
            v-model="confirm"
            label="Confirm Password"
            autocomplete="new-password"
            :type="reveal ? 'text' : 'password'"
            :rules="[required, matches]"
          />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn
            type="submit"
            :text="setupMode ? 'Set Password' : 'Sign In'"
            color="primary"
            variant="tonal"
            :loading="loading"
          />
        </v-card-actions>
      </v-form>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { mdiEye, mdiEyeOff } from "@mdi/js";

const store = useAppStore();
const form = ref();
const password = ref("");
const confirm = ref("");
const reveal = ref(false);
const loading = ref(false);
const error = ref("");

const setupMode = computed(() => store.auth?.hasPassword === false);

const required = (v: string) => !!v || "Required";
const minLength = (v: string) =>
  (v?.length ?? 0) >= 8 || "At least 8 characters";
const matches = (v: string) => v === password.value || "Passwords do not match";

// Find out whether this is first-run setup or a sign-in as soon as the app loads,
// and again whenever a 401 reopens the dialog.
onBeforeMount(refresh);
watch(
  () => store.authRequired,
  (val) => {
    if (val) refresh();
  }
);

async function refresh() {
  password.value = "";
  confirm.value = "";
  error.value = "";
  try {
    await store.checkAuth();
  } catch {
    // Service unreachable; the polling components report that.
  }
}

async function submit() {
  const { valid } = await form.value.validate();
  if (!valid) return;
  loading.value = true;
  try {
    const { data } = await store.api.post(
      setupMode.value ? "auth/setup" : "auth/login",
      { password: password.value }
    );
    store.setApiToken(data.token);
    // Components stopped polling on 401; a reload restarts them cleanly.
    location.reload();
  } catch (err: any) {
    const status = err?.response?.status;
    const detail =
      typeof err?.response?.data === "string" ? err.response.data : "";
    if (status === 401) error.value = "Incorrect password";
    else if (status === 429) error.value = detail || "Too many attempts";
    else if (status === 400 || status === 403)
      error.value = detail || "Request rejected";
    else error.value = "Could not reach the FUNC service";
  }
  loading.value = false;
}
</script>
