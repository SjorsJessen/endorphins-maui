window.registerInkLanguage = function () {
    const ready = () => typeof monaco !== 'undefined' && monaco.languages;
    const go = () => {
        const alreadyRegistered = monaco.languages.getLanguages().some(l => l.id === 'ink');
        if (!alreadyRegistered) {
            monaco.languages.register({id: 'ink'});
            monaco.languages.setMonarchTokensProvider('ink', {
                defaultToken: '',
                tokenPostfix: '.ink',
                // Whole-construct highlighting: each rule consumes the entire
                // token text (marker + name/target/expression), not just the
                // leading symbol, so all of the text gets coloured.
                keywords: ['VAR', 'CONST', 'LIST', 'INCLUDE', 'EXTERNAL', 'END', 'DONE',
                           'function', 'return', 'ref', 'temp', 'true', 'false'],
                tokenizer: {
                    root: [
                        // Comments
                        [/\/\/.*$/, 'comment'],
                        [/\/\*/, 'comment', '@blockComment'],
                        [/^\s*TODO\b.*$/, 'comment.doc'],

                        // Declarations (INCLUDE / VAR / CONST / LIST / EXTERNAL) — colour the whole line
                        [/^\s*(?:INCLUDE|EXTERNAL)\b.*$/, 'keyword'],
                        [/^\s*(?:VAR|CONST|LIST)\b/, 'keyword'],

                        // Knot header  === knot ===  /  === function foo(x) ===  → whole line orange
                        [/^\s*={2,}.*$/, 'keyword.knot'],
                        // Stitch header  = stitch  → whole line
                        [/^\s*=(?!=).*$/, 'keyword.stitch'],

                        // Choice / gather markers (marker only; choice text stays body colour)
                        [/^\s*[*+](?:[ \t]*[*+])*/, 'keyword.choice'],
                        [/^\s*-(?!>)(?:[ \t]*-(?!>))*/, 'keyword.gather'],

                        // Tilde logic line  ~ temp x = 5  → whole line
                        [/^\s*~.*$/, 'keyword.tilde'],

                        // Diverts / threads — marker AND its target path
                        [/(?:->->|->|<-)[ \t]*[A-Za-z0-9_.]*/, 'keyword.flow'],

                        // Glue
                        [/<>/, 'keyword.glue'],

                        // Tags  # tag  → whole tag
                        [/#.*$/, 'tag'],

                        // Labels  (label)
                        [/\([ \t]*[A-Za-z_]\w*[ \t]*\)/, 'variable.parameter'],

                        // Bracketed choice-only text  [ ... ]
                        [/\[/, 'string.bracket', '@choiceBracket'],

                        // Inline logic  { ... }
                        [/\{/, 'keyword.brace', '@inlineLogic'],

                        // Strings
                        [/"/, 'string', '@string'],

                        // Bare keywords elsewhere; everything else is body text
                        [/[A-Za-z_]\w*/, {cases: {'@keywords': 'keyword', '@default': ''}}],
                    ],
                    blockComment: [
                        [/[^/*]+/, 'comment'],
                        [/\*\//, 'comment', '@pop'],
                        [/[/*]/, 'comment'],
                    ],
                    choiceBracket: [
                        [/[^\]]+/, 'string.bracket'],
                        [/\]/, 'string.bracket', '@pop'],
                    ],
                    inlineLogic: [
                        [/(?:->->|->|<-)[ \t]*[A-Za-z0-9_.]*/, 'keyword.flow'],
                        [/[^{}|:"]+/, 'keyword.brace'],
                        [/[|:]/, 'delimiter'],
                        [/"/, 'string', '@string'],
                        [/\{/, 'keyword.brace', '@push'],
                        [/\}/, 'keyword.brace', '@pop'],
                    ],
                    string: [
                        [/[^"]+/, 'string'],
                        [/"/, 'string', '@pop'],
                    ],
                },
            });
        }

        // Theme is (re)defined on every call so it survives editor reloads.
        // Token colours mirror the .hl-* palette; `colors` themes the editor
        // chrome to the design tokens (--bg canvas, --surface gutter, etc.).
        monaco.editor.defineTheme('ink-dark', {
            base: 'vs-dark',
            inherit: true,
            rules: [
                {token: 'comment', foreground: '4A5568', fontStyle: 'italic'},   // hl-comment
                {token: 'comment.doc', foreground: '4A5568', fontStyle: 'italic'},
                {token: 'keyword.knot', foreground: 'E8A07A', fontStyle: 'bold'}, // hl-knot  === knot ===
                {token: 'keyword.stitch', foreground: 'C9933A'},                 // hl-stitch = stitch
                {token: 'keyword.flow', foreground: '4F9CF0'},                   // hl-divert -> target
                {token: 'keyword.tilde', foreground: 'B1A7C7'},                  // hl-tilde  ~ x = y
                {token: 'keyword.glue', foreground: '4F9CF0'},                   // hl-glue   <>
                {token: 'keyword.brace', foreground: 'E8C97A'},                  // hl-brace  { }
                {token: 'keyword.choice', foreground: '4EC9A0'},                 // hl-choice * +
                {token: 'keyword.gather', foreground: '4EC9A0'},                 // hl-gather -
                {token: 'keyword', foreground: 'C586C0'},                        // hl-keyword END DONE VAR…
                {token: 'string.bracket', foreground: '4EC9A0'},                 // hl-bracket [ ]
                {token: 'variable.parameter', foreground: '9CDCFE'},             // hl-label (label)
                {token: 'tag', foreground: '5A7A5A'},                            // hl-tag  # tag
                {token: 'string', foreground: 'C8CDD5'},                         // hl-text
                {token: 'delimiter', foreground: '8A929E'},
            ],
            colors: {
                'editor.background': '#111214',                    // --bg
                'editor.foreground': '#C8CDD5',                    // --text
                'editorLineNumber.foreground': '#5A6070',          // --text-dim
                'editorLineNumber.activeForeground': '#C8CDD5',    // --text
                'editorGutter.background': '#17191C',              // --surface
                'editor.lineHighlightBackground': '#4F9CF014',     // accent @ ~8%
                'editor.lineHighlightBorder': '#00000000',
                'editor.selectionBackground': '#1E4A7A',
                'editor.inactiveSelectionBackground': '#1E4A7A80',
                'editor.selectionHighlightBackground': '#1E4A7A40',
                'editorCursor.foreground': '#C8CDD5',
                'editorWhitespace.foreground': '#2A2D32',          // --border
                'editorIndentGuide.background': '#2A2D32',
                'editorIndentGuide.activeBackground': '#363A40',   // --border2
                'editorWidget.background': '#1E2124',              // --surface2
                'editorWidget.border': '#363A40',
                'editorSuggestWidget.background': '#1E2124',
                'editorSuggestWidget.border': '#363A40',
                'editorSuggestWidget.foreground': '#C8CDD5',
                'editorSuggestWidget.selectedBackground': '#232830', // --choice-hover
                'editorHoverWidget.background': '#1E2124',
                'editorHoverWidget.border': '#363A40',
                'input.background': '#1E2124',
                'input.foreground': '#C8CDD5',
                'input.border': '#363A40',
                'focusBorder': '#4F9CF0',
                'scrollbarSlider.background': '#363A4080',
                'scrollbarSlider.hoverBackground': '#363A40B0',
                'scrollbarSlider.activeBackground': '#4F9CF0B0',
                'editorError.foreground': '#E87A7A',               // --red
                'editorWarning.foreground': '#E8C97A',             // --yellow
                'editorBracketMatch.background': '#1E3A5F',        // --accent-dim
                'editorBracketMatch.border': '#4F9CF0',
            },
        });

        // Apply immediately to any editors already created with this theme.
        monaco.editor.setTheme('ink-dark');
    };
    if (ready()) {
        go();
    } else {
        const t = setInterval(() => {
            if (ready()) {
                clearInterval(t);
                go();
            }
        }, 50);
    }
};
