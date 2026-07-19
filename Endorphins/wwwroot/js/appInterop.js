// Small app-wide JS interop helpers.

/** Smoothly scrolls a scroll container to its end (used by the ink story runner). */
window.scrollStoryToEnd = (element) => {
    if (!element) return;
    requestAnimationFrame(() => {
        element.scrollTo({ top: element.scrollHeight, behavior: "smooth" });
    });
};

/** Points a <video> at a URL served by the in-app loopback file server and
    starts playback. The WebView streams the file natively (progressive + seek),
    so nothing is copied across the interop bridge.
    Returns a status string so the caller can log whether decoding worked. */
window.playVideoUrl = async (videoEl, url) => {
    if (!videoEl) return "no video element";
    videoEl.src = url;

    // Wait for metadata (decode success) or an error, so failures are visible.
    const status = await new Promise((resolve) => {
        const timer = setTimeout(() => resolve("timeout: no metadata after 8s"), 8000);
        videoEl.addEventListener("loadedmetadata", () => {
            clearTimeout(timer);
            resolve(`ok: duration=${videoEl.duration.toFixed(2)}s ${videoEl.videoWidth}x${videoEl.videoHeight}`);
        }, { once: true });
        videoEl.addEventListener("error", () => {
            clearTimeout(timer);
            resolve(`media error: code=${videoEl.error?.code} ${videoEl.error?.message ?? ""}`);
        }, { once: true });
    });

    try { await videoEl.play(); } catch { /* autoplay policy — user can press play */ }
    return status;
};

/** Clears the video source so its bytes can be garbage-collected. */
window.releaseVideoStream = (videoEl) => {
    if (videoEl) {
        videoEl.removeAttribute("src");
        videoEl.load();
    }
};
