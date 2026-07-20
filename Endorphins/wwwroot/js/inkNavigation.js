// Cmd/Ctrl-click navigation for ink diverts and INCLUDEs.
//
// Monaco's link machinery already gives us the interaction the editor should have: the
// target underlines when you hold Cmd and hover, and clicking follows it. We supply the
// ranges (a link provider) and handle the click (a link opener); resolving where a target
// actually lives happens in .NET, which is the side that can read the project's files.
//
// Link URLs carry no location — only what was clicked (`ink:divert/cave.entrance`). The
// provider therefore stays pure and cheap, since it re-runs on every model change, and no
// file access happens until an actual click.
(function () {
    'use strict';

    // Diverts (->, ->->), tunnel returns and threads (<-), followed by a dotted target.
    const DIVERT = /(->->|->|<-)([ \t]*)([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)/g;
    const INCLUDE = /^(\s*INCLUDE\s+)(\S.*?)\s*$/;
    const COMMENT = /^\s*\/\//;

    // Flow keywords are destinations the runtime understands, not authored knots — there is
    // nothing to navigate to, so leave them un-linked.
    const KEYWORDS = new Set(['END', 'DONE']);

    function linksFor(model) {
        const links = [];
        const lineCount = model.getLineCount();

        for (let line = 1; line <= lineCount; line++) {
            const text = model.getLineContent(line);
            if (COMMENT.test(text)) continue;

            const include = INCLUDE.exec(text);
            if (include) {
                const start = include[1].length + 1;
                links.push({
                    range: new monaco.Range(line, start, line, start + include[2].length),
                    url: 'ink:include/' + encodeURIComponent(include[2]),
                    tooltip: 'Open ' + include[2],
                });
                continue;
            }

            DIVERT.lastIndex = 0;
            let m;
            while ((m = DIVERT.exec(text)) !== null) {
                const target = m[3];
                if (KEYWORDS.has(target)) continue;
                const start = m.index + m[1].length + m[2].length + 1;
                links.push({
                    range: new monaco.Range(line, start, line, start + target.length),
                    url: 'ink:divert/' + encodeURIComponent(target),
                    tooltip: 'Go to ' + target,
                });
            }
        }
        return links;
    }

    // Registered once per page; the .NET reference is swapped so a rebuilt editor component
    // does not stack duplicate providers (which would double every link).
    let owner = null;

    window.registerInkNavigation = function (dotNetRef) {
        owner = dotNetRef;
        if (window.__inkNavigationRegistered) return;
        window.__inkNavigationRegistered = true;

        monaco.languages.registerLinkProvider('ink', {
            provideLinks: (model) => ({links: linksFor(model)}),
        });

        monaco.editor.registerLinkOpener({
            open: (uri) => {
                if (uri.scheme !== 'ink' || !owner) return false;
                const slash = uri.path.indexOf('/');
                if (slash < 0) return false;
                owner.invokeMethodAsync(
                    'NavigateInkLink',
                    uri.path.substring(0, slash),
                    decodeURIComponent(uri.path.substring(slash + 1)));
                return true;   // handled — stop Monaco treating it as an external URL
            },
        });
    };
})();
