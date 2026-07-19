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
let _highlights = [];        // [{id, page, rects:[{x,y,w,h}]}] — rects stored at scale 1
let _highlightSeq = 1;

// Load a document by URL (served by the in-app loopback file server, which
// supports HTTP range requests so pdf.js streams pages on demand) and create
// lightweight placeholders; pages render lazily as they scroll near.
export async function load(url, pagesContainer, dotnetRef) {
    _dotnet = dotnetRef;
    _scale = 1.2;
    _lastReportedPage = 0;
    _highlights = [];
    _pdf = await pdfjsLib.getDocument({ url }).promise;
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
        renderHighlightLayer(wrapper, i);
        await renderTextLayer(wrapper, page, viewport);
    } catch {
        wrapper.dataset.rendered = '';
    }
}

// Selectable (transparent) text overlay — enables text selection for highlighting.
async function renderTextLayer(wrapper, page, viewport) {
    const layer = document.createElement('div');
    layer.className = 'pdf-text-layer';
    layer.style.setProperty('--scale-factor', viewport.scale);
    wrapper.appendChild(layer);
    const textContent = await page.getTextContent();
    await pdfjsLib.renderTextLayer({
        textContentSource: textContent,
        container: layer,
        viewport,
    }).promise;
}

// ── Highlights ──────────────────────────────────────────────────────

function renderHighlightLayer(wrapper, pageNum) {
    wrapper.querySelector('.pdf-highlight-layer')?.remove();
    const layer = document.createElement('div');
    layer.className = 'pdf-highlight-layer';
    for (const h of _highlights.filter(h => h.page === pageNum)) {
        for (const r of h.rects) {
            const div = document.createElement('div');
            div.className = 'pdf-highlight';
            div.title = 'Click to remove highlight';
            div.style.left = `${r.x * _scale}px`;
            div.style.top = `${r.y * _scale}px`;
            div.style.width = `${r.w * _scale}px`;
            div.style.height = `${r.h * _scale}px`;
            div.addEventListener('click', (e) => {
                e.stopPropagation();
                removeHighlight(h.id);
            });
            layer.appendChild(div);
        }
    }
    wrapper.appendChild(layer);
}

function removeHighlight(id) {
    const h = _highlights.find(x => x.id === id);
    _highlights = _highlights.filter(x => x.id !== id);
    if (h) rerenderHighlightsForPage(h.page);
    notifyHighlightsChanged();
}

function rerenderHighlightsForPage(pageNum) {
    const wrapper = _pagesContainer?.querySelector(`.pdf-page-wrapper[data-page="${pageNum}"]`);
    if (wrapper && wrapper.dataset.rendered === '1') renderHighlightLayer(wrapper, pageNum);
}

function notifyHighlightsChanged() {
    _dotnet?.invokeMethodAsync('OnHighlightsChanged', JSON.stringify(_highlights));
}

/** Restores previously saved highlights (JSON produced by OnHighlightsChanged). */
export function setHighlights(json) {
    try { _highlights = JSON.parse(json) ?? []; } catch { _highlights = []; }
    _highlightSeq = Math.max(0, ..._highlights.map(h => h.id)) + 1;
    for (const h of _highlights) rerenderHighlightsForPage(h.page);
}

/** Turns the current text selection into persistent highlights. Returns true if anything was added. */
export function highlightSelection() {
    const sel = window.getSelection();
    if (!sel || sel.isCollapsed || sel.rangeCount === 0 || !_pagesContainer) return false;

    // Collect selection rects per page, normalised to scale 1.
    const perPage = new Map();
    for (let i = 0; i < sel.rangeCount; i++) {
        for (const rect of sel.getRangeAt(i).getClientRects()) {
            if (rect.width < 1 || rect.height < 1) continue;
            const cx = rect.left + rect.width / 2, cy = rect.top + rect.height / 2;
            for (const wrapper of _pagesContainer.querySelectorAll('.pdf-page-wrapper')) {
                const wr = wrapper.getBoundingClientRect();
                if (cx < wr.left || cx > wr.right || cy < wr.top || cy > wr.bottom) continue;
                const page = parseInt(wrapper.dataset.page, 10);
                if (!perPage.has(page)) perPage.set(page, []);
                perPage.get(page).push({
                    x: (rect.left - wr.left) / _scale,
                    y: (rect.top - wr.top) / _scale,
                    w: rect.width / _scale,
                    h: rect.height / _scale,
                });
                break;
            }
        }
    }
    if (perPage.size === 0) return false;

    for (const [page, rects] of perPage) {
        _highlights.push({ id: _highlightSeq++, page, rects: mergeLineRects(rects) });
        rerenderHighlightsForPage(page);
    }
    sel.removeAllRanges();
    notifyHighlightsChanged();
    return true;
}

// Selections produce one sliver-rect per glyph run; merge rects sharing a line
// so a highlight is a few clean bars instead of dozens of fragments.
function mergeLineRects(rects) {
    const sorted = [...rects].sort((a, b) => a.y - b.y || a.x - b.x);
    const merged = [];
    for (const r of sorted) {
        const last = merged[merged.length - 1];
        const sameLine = last && Math.abs(r.y - last.y) < last.h * 0.5;
        if (sameLine && r.x <= last.x + last.w + 4) {
            const right = Math.max(last.x + last.w, r.x + r.w);
            const bottom = Math.max(last.y + last.h, r.y + r.h);
            last.w = right - last.x;
            last.h = bottom - last.y;
        } else {
            merged.push({ ...r });
        }
    }
    return merged;
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
