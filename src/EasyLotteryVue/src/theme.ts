import '@fontsource/roboto/400.css'
import '@fontsource/roboto/500.css'
import '@fontsource/roboto/700.css'
import '@mdi/font/css/materialdesignicons.css'
import 'vuetify/styles'

import { createVuetify } from 'vuetify'
import { aliases, mdi } from 'vuetify/iconsets/mdi'
import { en, zhHant } from 'vuetify/locale'

export const vuetify = createVuetify({
  defaults: {
    VBtn: { rounded: 'lg', class: 'text-none' },
    VCard: { rounded: 'xl' },
  },
  icons: {
    defaultSet: 'mdi',
    aliases,
    sets: { mdi },
  },
  locale: {
    locale: 'zhHant',
    fallback: 'en',
    messages: { zhHant, en },
  },
  theme: {
    defaultTheme: 'easyLotteryLight',
    themes: {
      easyLotteryLight: {
        dark: false,
        colors: {
          primary: '#145c4b',
          secondary: '#628b7d',
          background: '#f4f7f5',
          surface: '#ffffff',
          error: '#b3261e',
          info: '#2b6575',
          success: '#28734b',
          warning: '#8a5a16',
        },
      },
    },
  },
})
