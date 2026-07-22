// Editor-side helpers for the ink editor's copy / cut / paste.
//
// The .NET side owns the *native* macOS pasteboard (via MAUI Clipboard, which works where the
// WebView's own clipboard does not) and calls these to read the selection and apply edits. We
// operate directly on the Monaco instance rather than through BlazorMonaco's wrappers, whose
// GetValueInRange/ExecuteEdits calls silently do nothing in this WebView — the same reason the
// built-in copy/cut/paste don't work, while JS-level editor operations (like Duplicate's
// Trigger) do.
(function () {
    'use strict';

    function inkEditor() {
        if (!window.monaco || !monaco.editor || !monaco.editor.getEditors) return null;
        const editors = monaco.editor.getEditors();
        return editors.find(e => {
            const node = e.getDomNode && e.getDomNode();
            return node && node.closest('.ink-editor');
        }) || editors[0] || null;
    }

    // The selection, or the whole current line (including its newline) when the selection is empty.
    function targetRange(ed) {
        const model = ed.getModel();
        const sel = ed.getSelection();
        if (sel && !sel.isEmpty()) return sel;

        const line = sel ? sel.startLineNumber : ed.getPosition().lineNumber;
        const lineCount = model.getLineCount();
        if (line < lineCount) {
            // From the line start to the start of the next line — carries the trailing newline.
            return new monaco.Range(line, 1, line + 1, 1);
        }
        if (line > 1) {
            // Last line: take the preceding newline so a cut leaves no dangling blank line.
            return new monaco.Range(line - 1, model.getLineMaxColumn(line - 1), line, model.getLineMaxColumn(line));
        }
        return new monaco.Range(line, 1, line, model.getLineMaxColumn(line));
    }

    // Text that copy/cut should place on the clipboard.
    window.inkClipboardGetText = function () {
        const ed = inkEditor();
        if (!ed) return '';
        return ed.getModel().getValueInRange(targetRange(ed));
    };

    // Removes what a cut just copied.
    window.inkClipboardDelete = function () {
        const ed = inkEditor();
        if (!ed) return;
        ed.executeEdits('ink-cut', [{ range: targetRange(ed), text: '', forceMoveMarkers: true }]);
        ed.focus();
    };

    // Inserts pasted text over the selection (or at the caret).
    window.inkClipboardInsert = function (text) {
        const ed = inkEditor();
        if (!ed || text == null) return;
        const sel = ed.getSelection();
        ed.executeEdits('ink-paste', [{ range: sel, text: text, forceMoveMarkers: true }]);
        ed.focus();
    };
})();
