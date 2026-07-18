let dotnetRef = null;

export function init(iframeId, dotnet) {
    dotnetRef = dotnet;
    const iframe = document.getElementById(iframeId);

    window.addEventListener("message", async (e) => {
        if (e.source !== iframe.contentWindow) return;

        if (e.data instanceof ArrayBuffer) {
            const bytes = new Uint8Array(e.data);
            let binary = "";
            bytes.forEach(b => binary += String.fromCharCode(b));
            await dotnetRef.invokeMethodAsync("OnImageExported", btoa(binary));
        }
    });
}

export function exportPng(iframeId) {
    const iframe = document.getElementById(iframeId);
    iframe.contentWindow.postMessage('app.activeDocument.saveToOE("png");', "*");
}

export function loadImage(iframeId, base64Png) {
    const iframe = document.getElementById(iframeId);
    const script = `app.open("data:image/png;base64,${base64Png}", null, true);`;
    iframe.contentWindow.postMessage(script, "*");
}