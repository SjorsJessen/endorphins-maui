export function getRect(el) {
    const r = el.getBoundingClientRect();
    return [r.left, r.top, r.width, r.height];
}

// Render the board items onto an offscreen canvas and return base64 PNG.
// Items arrive camelCased from .NET: { imageUrl, x, y, width, height, zIndex, rotation }.
export async function exportBoardPng(items, background) {
    if (!items || !items.length) return null;

    const loaded = (await Promise.all(items.map(it => new Promise(res => {
        const img = new Image();
        img.onload = () => res({ it, img });
        img.onerror = () => res(null);
        img.src = it.imageUrl;
    })))).filter(Boolean);
    if (!loaded.length) return null;

    // Bounding box over all (rotated) item corners
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const { it } of loaded) {
        const cx = it.x + it.width / 2, cy = it.y + it.height / 2;
        const rad = (it.rotation || 0) * Math.PI / 180;
        const cos = Math.cos(rad), sin = Math.sin(rad);
        for (const [dx, dy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) {
            const px = dx * it.width / 2, py = dy * it.height / 2;
            const rx = cx + px * cos - py * sin;
            const ry = cy + px * sin + py * cos;
            minX = Math.min(minX, rx); maxX = Math.max(maxX, rx);
            minY = Math.min(minY, ry); maxY = Math.max(maxY, ry);
        }
    }

    const pad = 24;
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.ceil(maxX - minX + pad * 2));
    canvas.height = Math.max(1, Math.ceil(maxY - minY + pad * 2));
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = background || '#111214';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Paint in z-order, honouring rotation and object-fit: cover
    loaded.sort((a, b) => (a.it.zIndex || 0) - (b.it.zIndex || 0));
    for (const { it, img } of loaded) {
        const w = it.width, h = it.height;
        const cx = it.x + w / 2 - minX + pad;
        const cy = it.y + h / 2 - minY + pad;
        ctx.save();
        ctx.translate(cx, cy);
        ctx.rotate((it.rotation || 0) * Math.PI / 180);
        const scale = Math.max(w / img.width, h / img.height);
        const sw = w / scale, sh = h / scale;
        const sx = (img.width - sw) / 2, sy = (img.height - sh) / 2;
        ctx.drawImage(img, sx, sy, sw, sh, -w / 2, -h / 2, w, h);
        ctx.restore();
    }

    return canvas.toDataURL('image/png').split(',')[1];
}

// Wire paste + drop once, routing results back to .NET
export function initImport(el, dotnet) {
    // Downscale on import: full-resolution photos become multi-MB data URLs
    // that bloat the .moodboard file and make every Blazor re-render crawl.
    const MAX_DIM = 1600;

    async function fileToUrl(f) {
        try {
            const bitmap = await createImageBitmap(f);
            const scale = Math.min(1, MAX_DIM / Math.max(bitmap.width, bitmap.height));
            const canvas = document.createElement("canvas");
            canvas.width = Math.max(1, Math.round(bitmap.width * scale));
            canvas.height = Math.max(1, Math.round(bitmap.height * scale));
            canvas.getContext("2d").drawImage(bitmap, 0, 0, canvas.width, canvas.height);
            bitmap.close?.();
            // PNG keeps transparency; everything else compresses well as JPEG
            return f.type === "image/png"
                ? canvas.toDataURL("image/png")
                : canvas.toDataURL("image/jpeg", 0.85);
        } catch {
            // Fallback: raw data URL if decode fails
            return await new Promise(res => {
                const r = new FileReader();
                r.onload = () => res(r.result);
                r.readAsDataURL(f);
            });
        }
    }

    async function filesToUrls(files) {
        const out = [];
        for (const f of files) {
            if (!f.type.startsWith("image/")) continue;
            out.push(await fileToUrl(f));
        }
        return out;
    }

    el.addEventListener("dragover", e => e.preventDefault());

    el.addEventListener("drop", async e => {
        e.preventDefault();
        const urls = await filesToUrls(e.dataTransfer.files);
        if (urls.length)
            await dotnet.invokeMethodAsync("OnImagesDropped", urls, e.clientX, e.clientY);
    });

    // paste anywhere on the page
    document.addEventListener("paste", async e => {
        const items = [...e.clipboardData.items]
            .filter(i => i.type.startsWith("image/"))
            .map(i => i.getAsFile());
        const urls = await filesToUrls(items);
        if (urls.length)
            await dotnet.invokeMethodAsync("OnImagesPasted", urls);
    });
}