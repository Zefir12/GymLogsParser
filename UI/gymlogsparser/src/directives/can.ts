import { useAuthStore } from "@/stores/auth";

// directives/can.ts:  <button v-can="'admin'">Delete</button>
export const vCan = {
  mounted(el: HTMLElement, { value }: { value: string | string[] }) {
    const auth = useAuthStore();
    const ok = Array.isArray(value)
      ? auth.hasAnyRole(value)
      : auth.hasRole(value);
    if (!ok) el.remove();
  },
};
