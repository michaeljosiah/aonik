/// <reference types="vite/client" />

declare module "*?raw" {
  const content: string;
  export default content;
}

declare global {
  interface Window {
    $: typeof import("jquery");
    jQuery: typeof import("jquery");
  }
}
