import * as pdfjsLib from 'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.min.mjs';

pdfjsLib.GlobalWorkerOptions.workerSrc =
    'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.worker.min.mjs';

let _pdf = null;
let _scale = 1.2;
let _dotnet = null;
let _observer = null;

// Load a document and render every page into the scrollable container.
export async function load(content, pagesContainer, dotnetRef) {
    _dotnet = dotnetRef;
    _scale = 1.2;
    _pdf = await pdfjsLib.getDocument({ data: content }).promise;
    await renderAll(pagesContainer);
    return _pdf.numPages;
}

async function renderAll(pagesContainer) {
    if (_observer) { _observer.disconnect(); _observer = null; }
    pagesContainer.innerHTML = '';
    const outputScale = window.devicePixelRatio || 1;

    for (let i = 1; i <= _pdf.numPages; i++) {
        const page = await _pdf.getPage(i);
        const viewport = page.getViewport({ scale: _scale });

        const wrapper = document.createElement('div');
        wrapper.className = 'pdf-page-wrapper';
        wrapper.dataset.page = String(i);

        const canvas = document.createElement('canvas');
        canvas.className = 'pdf-page-canvas';
        canvas.width = Math.floor(viewport.width * outputScale);
        canvas.height = Math.floor(viewport.height * outputScale);
        canvas.style.width = `${Math.floor(viewport.width)}px`;
        canvas.style.height = `${Math.floor(viewport.height)}px`;

        const transform = outputScale !== 1 ? [outputScale, 0, 0, outputScale, 0, 0] : null;
        wrapper.appendChild(canvas);
        pagesContainer.appendChild(wrapper);

        await page.render({ canvasContext: canvas.getContext('2d'), transform, viewport }).promise;
    }

    setupObserver(pagesContainer);
}

// Report the most-visible page back to .NET so the toolbar counter follows scroll.
function setupObserver(pagesContainer) {
    if (!_dotnet) return;
    _observer = new IntersectionObserver((entries) => {
        let best = null, bestRatio = 0;
        for (const e of entries) {
            if (e.isIntersecting && e.intersectionRatio > bestRatio) {
                bestRatio = e.intersectionRatio;
                best = e.target;
            }
        }
        if (best) {
            _dotnet.invokeMethodAsync('OnCurrentPageChanged', parseInt(best.dataset.page, 10));
        }
    }, { root: pagesContainer, threshold: [0.1, 0.3, 0.5, 0.75] });

    pagesContainer.querySelectorAll('.pdf-page-wrapper').forEach(w => _observer.observe(w));
}

export async function setZoom(pagesContainer, delta) {
    _scale = Math.min(3, Math.max(0.4, _scale + delta));
    if (_pdf) await renderAll(pagesContainer);
    return Math.round(_scale * 100);
}

export function scrollToPage(pagesContainer, pageNumber) {
    const el = pagesContainer.querySelector(`.pdf-page-wrapper[data-page="${pageNumber}"]`);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

// Nested outline with each entry's destination resolved to a 1-based page number.
export async function getOutline() {
    if (!_pdf) return [];
    const outline = await _pdf.getOutline();
    if (!outline) return [];

    const resolve = async (items) => {
        const out = [];
        for (const it of items) {
            let page = null;
            try {
                let dest = it.dest;
                if (typeof dest === 'string') dest = await _pdf.getDestination(dest);
                if (Array.isArray(dest) && dest[0]) {
                    page = (await _pdf.getPageIndex(dest[0])) + 1;
                }
            } catch { /* unresolved destination */ }
            out.push({
                title: it.title,
                page,
                items: (it.items && it.items.length) ? await resolve(it.items) : []
            });
        }
        return out;
    };

    return await resolve(outline);
}

export function dispose() {
    if (_observer) { _observer.disconnect(); _observer = null; }
    _pdf = null;
    _dotnet = null;
}
