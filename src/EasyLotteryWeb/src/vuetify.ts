import "vuetify/styles";
import {
  VAlert,
  VApp,
  VAppBar,
  VAppBarTitle,
  VBtn,
  VCard,
  VCardActions,
  VCardItem,
  VCardSubtitle,
  VCardText,
  VCardTitle,
  VChip,
  VCol,
  VContainer,
  VList,
  VListItem,
  VMain,
  VMenu,
  VNavigationDrawer,
  VProgressCircular,
  VRow,
  VSelect,
  VSpacer,
  VTextField
} from "vuetify/components";
import { createVuetify } from "vuetify";

export const vuetify = createVuetify({
  components: {
    VAlert,
    VApp,
    VAppBar,
    VAppBarTitle,
    VBtn,
    VCard,
    VCardActions,
    VCardItem,
    VCardSubtitle,
    VCardText,
    VCardTitle,
    VChip,
    VCol,
    VContainer,
    VList,
    VListItem,
    VMain,
    VMenu,
    VNavigationDrawer,
    VProgressCircular,
    VRow,
    VSelect,
    VSpacer,
    VTextField
  },
  defaults: {
    VBtn: { color: "primary" },
    VCard: { rounded: "lg" }
  }
});
