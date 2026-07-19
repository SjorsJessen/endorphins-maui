// Small app-wide JS interop helpers.

/** Smoothly scrolls a scroll container to its end (used by the ink story runner). */
window.scrollStoryToEnd = (element) => {
    if (!element) return;
    requestAnimationFrame(() => {
        element.scrollTo({ top: element.scrollHeight, behavior: "smooth" });
    });
};

/** Plays a video from a .NET byte stream (project files live outside wwwroot,
    so the WebView can't address them directly).

    Note: WKWebView (MacCatalyst/iOS) can't play a blob: URL on a <video> — its
    media process has no access to the blob store, so decoding silently fails.
    A data: URL embeds the bytes inline and plays reliably instead. We build it
    with FileReader so the file is still streamed over interop (no base64
    inflation on the wire) rather than serialized as a byte[].
    Returns a status string so the caller can log whether decoding worked. */
window.playVideoStream = async (videoEl, streamRef, mimeType) => {
    if (!videoEl) return "no video element";
    const buffer = await streamRef.arrayBuffer();
    const blob = new Blob([buffer], { type: mimeType });

    const url = await new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(blob);
    });
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
