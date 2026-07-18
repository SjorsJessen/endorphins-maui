// Small app-wide JS interop helpers.

/** Smoothly scrolls a scroll container to its end (used by the ink story runner). */
window.scrollStoryToEnd = (element) => {
    if (!element) return;
    requestAnimationFrame(() => {
        element.scrollTo({ top: element.scrollHeight, behavior: "smooth" });
    });
};

/** Plays a video from a .NET byte stream via a blob URL (project files live
    outside wwwroot, so the WebView can't address them directly).
    Returns a status string so the caller can log whether decoding worked. */
window.playVideoStream = async (videoEl, streamRef, mimeType) => {
    if (!videoEl) return "no video element";
    const buffer = await streamRef.arrayBuffer();
    if (videoEl.dataset.blobUrl) URL.revokeObjectURL(videoEl.dataset.blobUrl);
    const url = URL.createObjectURL(new Blob([buffer], { type: mimeType }));
    videoEl.dataset.blobUrl = url;
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

/** Releases the blob URL created by playVideoStream. */
window.releaseVideoStream = (videoEl) => {
    if (videoEl?.dataset.blobUrl) {
        URL.revokeObjectURL(videoEl.dataset.blobUrl);
        delete videoEl.dataset.blobUrl;
    }
};
