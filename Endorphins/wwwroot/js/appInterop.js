// Small app-wide JS interop helpers.

/** Smoothly scrolls a scroll container to its end (used by the ink story runner). */
window.scrollStoryToEnd = (element) => {
    if (!element) return;
    requestAnimationFrame(() => {
        element.scrollTo({ top: element.scrollHeight, behavior: "smooth" });
    });
};
