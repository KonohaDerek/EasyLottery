import { createApp } from 'vue'
import App from './App.vue'
import { router } from './router'
import { pinia } from './stores/pinia'
import { vuetify } from './theme'
import './styles.css'

createApp(App).use(pinia).use(router).use(vuetify).mount('#app')
