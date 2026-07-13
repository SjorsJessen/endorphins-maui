export function getRect(el) {
    const r = el.getBoundingClientRect();
    return [r.left, r.top, r.width, r.height];
}

// Wire paste + drop once, routing results back to .NET
export function initImport(el, dotnet) {
    async function filesToUrls(files) {
        const out = [];
        for (const f of files) {
            if (!f.type.startsWith("image/")) continue;
            const url = await new Promise(res => {
                const r = new FileReader();
                r.onload = () => res(r.result);
                r.readAsDataURL(f);
            });
            out.push(url);
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