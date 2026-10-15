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
  theme: {
    defaultTheme: "creatorStudio",
    themes: {
      creatorStudio: {
        dark: false,
        colors: {
          primary: "#5541b8",
          secondary: "#a43173",
          accent: "#137a80",
          error: "#b3263e",
          info: "#2861a8",
          success: "#176b48",
          warning: "#805300",
          background: "#f7f5fc",
          surface: "#ffffff",
          "on-primary": "#ffffff",
          "on-secondary": "#ffffff",
          "on-accent": "#ffffff",
          "on-error": "#ffffff",
          "on-info": "#ffffff",
          "on-success": "#ffffff",
          "on-warning": "#ffffff"
        }
      }
    }
  },
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
