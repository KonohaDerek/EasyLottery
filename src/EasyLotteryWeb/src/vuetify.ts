import "vuetify/styles";
import {
  VApp,
  VAppBar,
  VAppBarTitle,
  VBtn,
  VCard,
  VCardItem,
  VCardSubtitle,
  VCardText,
  VCardTitle,
  VCol,
  VContainer,
  VList,
  VListItem,
  VMain,
  VMenu,
  VNavigationDrawer,
  VProgressCircular,
  VRow,
  VSpacer
} from "vuetify/components";
import { createVuetify } from "vuetify";

export const vuetify = createVuetify({
  components: {
    VApp,
    VAppBar,
    VAppBarTitle,
    VBtn,
    VCard,
    VCardItem,
    VCardSubtitle,
    VCardText,
    VCardTitle,
    VCol,
    VContainer,
    VList,
    VListItem,
    VMain,
    VMenu,
    VNavigationDrawer,
    VProgressCircular,
    VRow,
    VSpacer
  },
  defaults: {
    VBtn: { color: "primary" },
    VCard: { rounded: "lg" }
  }
});
