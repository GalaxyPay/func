<template>
  <v-dialog v-model="show" max-width="800" min-height="400">
    <v-card>
      <v-card-title class="d-flex align-center">
        Messages
        <v-spacer />
        <v-btn
          v-if="store.unreadCount > 0"
          class="mr-2"
          size="small"
          variant="tonal"
          @click="store.markAllRead()"
        >
          Mark all read
        </v-btn>
        <v-icon color="currentColor" :icon="mdiClose" @click="show = false" />
      </v-card-title>
      <v-data-table
        :headers="headers"
        :items="items"
        :items-per-page="-1"
        density="comfortable"
        hide-default-footer
        no-data-text="No messages"
      >
        <template #item.status="{ item }">
          <span>
            <v-icon
              :icon="item.read ? mdiEmailOpenOutline : mdiEmail"
              :color="item.read ? 'grey' : 'primary'"
              @click="
                item.read ? store.markUnread(item.id) : store.markRead(item.id)
              "
            />
            <v-tooltip
              :text="item.read ? 'Mark as unread' : 'Mark as read'"
              location="bottom"
              activator="parent"
            />
          </span>
        </template>
        <template #item.title="{ item }">
          <span :class="{ 'font-weight-bold': !item.read }">
            {{ item.title }}
          </span>
        </template>
        <template #item.body="{ item }">
          <span :class="{ 'text-grey': item.read }">{{ item.body }}</span>
        </template>
      </v-data-table>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { mdiClose, mdiEmail, mdiEmailOpenOutline } from "@mdi/js";

const props = defineProps({ visible: { type: Boolean, required: true } });
const emit = defineEmits(["close"]);

const store = useAppStore();

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

const headers = [
  { title: "", key: "status", sortable: false, width: 40 },
  { title: "Title", key: "title" },
  { title: "Message", key: "body", sortable: false },
] as const;

const items = computed(() =>
  store.messages.map((m) => ({ ...m, read: store.isRead(m.id) }))
);

watch(
  () => props.visible,
  (visible) => {
    if (visible) store.fetchMessages().catch((err) => console.error(err));
  }
);
</script>
