import * as pdfjsLib from 'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.min.mjs';

pdfjsLib.GlobalWorkerOptions.workerSrc =
    'https://cdn.jsdelivr.net/npm/pdfjs-dist@4.0.379/build/pdf.worker.min.mjs';

let _pdf = null;
let _scale = 1.2;
let _dotnet = null;
let _renderObserver = null;  // lazily renders pages as they approach the viewport
let _baseViewport = null;    // page-1 size used for placeholders
let _pagesContainer = null;  // scroll container, for page tracking
let _scrollHandler = null;   // throttled scroll listener reporting the current page
let _scrollRaf = 0;
let _lastReportedPage = 0;   // guards against redundant .NET round-trips

// Load a document (bytes arrive as a .NET stream — no base64 inflation) and
// create lightweight placeholders; pages render lazily as they scroll near.
export async function load(streamRef, pagesContainer, dotnetRef) {
    _dotnet = dotnetRef;
    _scale = 1.2;
    _lastReportedPage = 0;
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
    _pagesContainer = pagesContainer;

    // Render pages shortly before they scroll into view
    _renderObserver = new IntersectionObserver((entries) => {
        for (const e of entries) {
            if (e.isIntersecting) renderPage(e.target);
        }
    }, { root: pagesContainer, rootMargin: '800px 0px' });

    for (const w of pagesContainer.querySelectorAll('.pdf-page-wrapper')) {
        _renderObserver.observe(w);
    }

    // Track the current page from scroll position rather than intersection
    // ratios: the page crossing the viewport's vertical centre is unambiguous,
    // so the counter no longer flickers between adjacent pages.
    _scrollHandler = () => {
        if (_scrollRaf) return;
        _scrollRaf = requestAnimationFrame(() => {
            _scrollRaf = 0;
            reportCurrentPage();
        });
    };
    pagesContainer.addEventListener('scroll', _scrollHandler, { passive: true });
    reportCurrentPage();
}

// The page whose box straddles the container's vertical midline is "current".
// Uses viewport coords (getBoundingClientRect) so it's independent of offsetParent.
function reportCurrentPage() {
    if (!_pagesContainer || !_dotnet) return;
    const contRect = _pagesContainer.getBoundingClientRect();
    const midY = contRect.top + _pagesContainer.clientHeight / 2;
    let current = 1;
    for (const w of _pagesContainer.querySelectorAll('.pdf-page-wrapper')) {
        if (w.getBoundingClientRect().top <= midY) {
            current = parseInt(w.dataset.page, 10);
        } else {
            break;
        }
    }
    if (current !== _lastReportedPage) {
        _lastReportedPage = current;
        _dotnet.invokeMethodAsync('OnCurrentPageChanged', current);
    }
}

function disconnectObservers() {
    _renderObserver?.disconnect(); _renderObserver = null;
    if (_pagesContainer && _scrollHandler) {
        _pagesContainer.removeEventListener('scroll', _scrollHandler);
    }
    _scrollHandler = null;
    if (_scrollRaf) { cancelAnimationFrame(_scrollRaf); _scrollRaf = 0; }
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
    _pagesContainer = null;
    _pdf = null;
    _dotnet = null;
}
