import { createPinia } from "pinia";
import { createApp } from "vue";
import App from "./App.vue";
import { vuetify } from "./vuetify";
import { bootstrapObsSessionToken } from "./obs/session-token";
import "./styles.css";

bootstrapObsSessionToken();

void import("./router").then(({ router }) => {
  createApp(App)
    .use(createPinia())
    .use(router)
    .use(vuetify)
    .mount("#app");
});
