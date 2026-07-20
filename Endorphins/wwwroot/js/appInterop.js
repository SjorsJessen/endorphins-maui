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

// Sound bites played by the story's playAudio() function. Each call gets its own Audio
// element so overlapping effects layer rather than cutting each other off — a line's sound
// should not be truncated because the next line fired one. They are tracked only so a
// restart can silence whatever is still ringing.
const storySounds = new Set();

/** Plays a sound bite from a loopback-server URL, with no visible player.
    Returns a status string so the caller can log why nothing was heard. */
window.playStoryAudio = async (url) => {
    if (!url) return "no url";

    const audio = new Audio(url);
    storySounds.add(audio);
    const forget = () => storySounds.delete(audio);
    audio.addEventListener("ended", forget, { once: true });
    audio.addEventListener("error", forget, { once: true });

    try {
        await audio.play();
        return "ok";
    } catch (e) {
        // Most likely the autoplay policy: WebKit blocks playback that isn't traceable to a
        // user gesture, so a sound fired before the reader has clicked anything stays silent.
        forget();
        return `blocked: ${e?.name ?? e}`;
    }
};

/** Silences every sound bite still playing (used when the story restarts). */
window.stopStoryAudio = () => {
    for (const audio of storySounds) {
        audio.pause();
        audio.currentTime = 0;
    }
    storySounds.clear();
};

/** Clears the video source so its bytes can be garbage-collected. */
window.releaseVideoStream = (videoEl) => {
    if (videoEl) {
        videoEl.removeAttribute("src");
        videoEl.load();
    }
};
