import * as pdfjsLib from 'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.min.mjs';

pdfjsLib.GlobalWorkerOptions.workerSrc =
    'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.worker.min.mjs';

let _pdf = null;
let _scale = 1.2;
let _dotnet = null;
let _pageObserver = null;    // reports the current page while scrolling
let _renderObserver = null;  // lazily renders pages as they approach the viewport
let _baseViewport = null;    // page-1 size used for placeholders

// Load a document (bytes arrive as a .NET stream — no base64 inflation) and
// create lightweight placeholders; pages render lazily as they scroll near.
export async function load(streamRef, pagesContainer, dotnetRef) {
    _dotnet = dotnetRef;
    _scale = 1.2;
    const buffer = await streamRef.arrayBuffer();
    _pdf = await pdfjsLib.getDocument({ data: buffer }).promise;
    const page1 = await _pdf.getPage(1);
    _baseViewport = page1.getViewport({ scale: 1 });
    buildPlaceholders(pagesContainer);
    return _pdf.numPages;
}

function buildPlaceholders(pagesContainer) {
    disconnectObservers();
    pagesContainer.innerHTML = '';

    const w = Math.floor(_baseViewport.width * _scale);
    const h = Math.floor(_baseViewport.height * _scale);

    for (let i = 1; i <= _pdf.numPages; i++) {
        const wrapper = document.createElement('div');
        wrapper.className = 'pdf-page-wrapper';
        wrapper.dataset.page = String(i);
        wrapper.dataset.rendered = '';
        wrapper.style.width = `${w}px`;
        wrapper.style.height = `${h}px`;
        pagesContainer.appendChild(wrapper);
    }

    setupObservers(pagesContainer);
}

async function renderPage(wrapper) {
    if (!_pdf || wrapper.dataset.rendered === '1') return;
    wrapper.dataset.rendered = '1';

    const i = parseInt(wrapper.dataset.page, 10);
    try {
        const page = await _pdf.getPage(i);
        const viewport = page.getViewport({ scale: _scale });
        const outputScale = window.devicePixelRatio || 1;

        const canvas = document.createElement('canvas');
        canvas.className = 'pdf-page-canvas';
        canvas.width = Math.floor(viewport.width * outputScale);
        canvas.height = Math.floor(viewport.height * outputScale);
        canvas.style.width = `${Math.floor(viewport.width)}px`;
        canvas.style.height = `${Math.floor(viewport.height)}px`;

        // Fix the placeholder to the page's true size (mixed-size documents)
        wrapper.style.width = canvas.style.width;
        wrapper.style.height = canvas.style.height;

        const transform = outputScale !== 1 ? [outputScale, 0, 0, outputScale, 0, 0] : null;
        await page.render({ canvasContext: canvas.getContext('2d'), transform, viewport }).promise;

        wrapper.replaceChildren(canvas);
    } catch {
        wrapper.dataset.rendered = '';
    }
}

function setupObservers(pagesContainer) {
    // Render pages shortly before they scroll into view
    _renderObserver = new IntersectionObserver((entries) => {
        for (const e of entries) {
            if (e.isIntersecting) renderPage(e.target);
        }
    }, { root: pagesContainer, rootMargin: '800px 0px' });

    // Report the most-visible page for the toolbar counter
    _pageObserver = new IntersectionObserver((entries) => {
        let best = null, bestRatio = 0;
        for (const e of entries) {
            if (e.isIntersecting && e.intersectionRatio > bestRatio) {
                bestRatio = e.intersectionRatio;
                best = e.target;
            }
        }
        if (best && _dotnet) {
            _dotnet.invokeMethodAsync('OnCurrentPageChanged', parseInt(best.dataset.page, 10));
        }
    }, { root: pagesContainer, threshold: [0.1, 0.3, 0.5, 0.75] });

    for (const w of pagesContainer.querySelectorAll('.pdf-page-wrapper')) {
        _renderObserver.observe(w);
        _pageObserver.observe(w);
    }
}

function disconnectObservers() {
    _renderObserver?.disconnect(); _renderObserver = null;
    _pageObserver?.disconnect(); _pageObserver = null;
}

export async function setZoom(pagesContainer, delta) {
    _scale = Math.min(3, Math.max(0.4, _scale + delta));
    if (_pdf) {
        const current = currentTopPage(pagesContainer);
        buildPlaceholders(pagesContainer);   // placeholders only; visible pages lazy-render
        if (current > 1) scrollToPage(pagesContainer, current, 'auto');
    }
    return Math.round(_scale * 100);
}

function currentTopPage(pagesContainer) {
    const top = pagesContainer.scrollTop;
    for (const w of pagesContainer.querySelectorAll('.pdf-page-wrapper')) {
        if (w.offsetTop + w.offsetHeight > top) return parseInt(w.dataset.page, 10);
    }
    return 1;
}

export function scrollToPage(pagesContainer, pageNumber, behavior = 'smooth') {
    const el = pagesContainer.querySelector(`.pdf-page-wrapper[data-page="${pageNumber}"]`);
    if (el) el.scrollIntoView({ behavior, block: 'start' });
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
    disconnectObservers();
    _pdf = null;
    _dotnet = null;
}
