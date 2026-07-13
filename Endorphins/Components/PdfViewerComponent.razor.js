import * as pdfjsLib from 'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.min.mjs';

pdfjsLib.GlobalWorkerOptions.workerSrc =
    'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.worker.min.mjs';

export async function renderPage(content, canvasElement, pageNumber = 1, scale = 1.5) {
    const pdf = await pdfjsLib.getDocument({ data: content }).promise;
    const page = await pdf.getPage(pageNumber);

    const viewport = page.getViewport({ scale });
    const outputScale = window.devicePixelRatio || 1;
    const context = canvasElement.getContext('2d');

    canvasElement.width = Math.floor(viewport.width * outputScale);
    canvasElement.height = Math.floor(viewport.height * outputScale);
    canvasElement.style.width = `${Math.floor(viewport.width)}px`;
    canvasElement.style.height = `${Math.floor(viewport.height)}px`;

    const transform = outputScale !== 1 ? [outputScale, 0, 0, outputScale, 0, 0] : null;

    await page.render({ canvasContext: context, transform, viewport }).promise;

    return pdf.numPages;
}

export async function getOutline(url) {
    const pdf = await pdfjsLib.getDocument({ url }).promise;
    return await pdf.getOutline();
}