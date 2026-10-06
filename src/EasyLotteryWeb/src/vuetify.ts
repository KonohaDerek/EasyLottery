import "vuetify/styles";
import { createVuetify } from "vuetify";

export const vuetify = createVuetify({
  defaults: {
    VBtn: { color: "primary" },
    VCard: { rounded: "lg" }
  }
});
