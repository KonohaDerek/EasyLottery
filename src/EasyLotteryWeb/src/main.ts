import { createPinia } from "pinia";
import { createApp } from "vue";
import App from "./App.vue";
import { router } from "./router";
import { vuetify } from "./vuetify";
import "./styles.css";

createApp(App)
  .use(createPinia())
  .use(router)
  .use(vuetify)
  .mount("#app");
