import { defineStore } from "pinia";
import { ref, watch } from "vue";

/** Palettes from #51. "default" is the Log Ascent look in tokens.css. */
export const themes = [
  { id: "default", label: "Log Ascent" },
  { id: "night", label: "Blokowisko night" },
] as const;

export type ThemeId = (typeof themes)[number]["id"];

const STORAGE_KEY = "gymlogs:theme";

/** Page background per palette, for the browser UI (`theme-color`). */
const themeColor: Record<ThemeId, string> = {
  default: "#141416",
  night: "#191819",
};

function isThemeId(value: unknown): value is ThemeId {
  return themes.some((t) => t.id === value);
}

function readStored(): ThemeId {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return isThemeId(value) ? value : "default";
  } catch {
    return "default";
  }
}

function apply(id: ThemeId) {
  const root = document.documentElement;
  if (id === "default") delete root.dataset.theme;
  else root.dataset.theme = id;
  document
    .querySelector('meta[name="theme-color"]')
    ?.setAttribute("content", themeColor[id]);
}

/**
 * The selected palette. Sets `data-theme` on <html> (themes.css
 * overrides the tokens from there) and remembers the choice in
 * localStorage. Create it before mounting so the first paint is
 * already in the saved palette.
 */
export const useThemeStore = defineStore("theme", () => {
  const theme = ref<ThemeId>(readStored());

  watch(
    theme,
    (id) => {
      apply(id);
      try {
        if (id === "default") localStorage.removeItem(STORAGE_KEY);
        else localStorage.setItem(STORAGE_KEY, id);
      } catch {
        // storage blocked (private mode): the choice lasts this visit only
      }
    },
    { immediate: true },
  );

  function setTheme(id: string) {
    if (isThemeId(id)) theme.value = id;
  }

  return { theme, setTheme };
});
