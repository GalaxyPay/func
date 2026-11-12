// Loaded before every test file (see vite.config.mts).

// happy-dom has no visualViewport, which Vuetify's dialogs and menus listen to.
if (!("visualViewport" in globalThis)) {
  const viewport = Object.assign(new EventTarget(), {
    width: 1280,
    height: 800,
    offsetLeft: 0,
    offsetTop: 0,
    scale: 1,
  });
  Object.defineProperty(globalThis, "visualViewport", { value: viewport });
}
