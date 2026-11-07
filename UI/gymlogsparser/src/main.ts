import { createApp } from "vue";
import "./styles/brand.css";
import "./styles/tokens.css";
import "./style.css";
import "./styles/theme.css";
import "./styles/themes.css";
import App from "./App.vue";
import { createPinia } from "pinia";
import { router } from "@/router";
import { useAuthStore } from "./stores/auth.ts";
import { useThemeStore } from "./stores/theme.ts";
import { setUnauthorizedHandler } from "./api/api.ts";

const app = createApp(App);
const pinia = createPinia();
app.use(pinia).use(router);

// apply the saved palette before the first paint
useThemeStore(pinia);

const auth = useAuthStore(pinia);
setUnauthorizedHandler(() => {
  // session died mid-use
  if (auth.isAuthenticated) {
    auth.expire();
    auth.login();
  }
});

app.mount("#app");
