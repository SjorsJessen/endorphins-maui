window.registerInkLanguage = function () {
    const ready = () => typeof monaco !== 'undefined' && monaco.languages;
    const go = () => {
        if (monaco.languages.getLanguages().some(l => l.id === 'ink')) return;

        monaco.languages.register({id: 'ink'});
        monaco.languages.setMonarchTokensProvider('ink', {
            defaultToken: '',
            tokenPostfix: '.ink',
            keywords: ['VAR', 'CONST', 'LIST', 'INCLUDE', 'EXTERNAL', 'function', 'return', 'ref', 'temp', 'true', 'false'],
            tokenizer: {
                root: [
                    [/\/\/.*$/, 'comment'],
                    [/\/\*/, 'comment', '@blockComment'],
                    [/^\s*TODO\b.*$/, 'comment.doc'],
                    [/^\s*(INCLUDE|EXTERNAL|LIST|VAR|CONST)\b/, 'keyword'],
                    [/^\s*={2,}/, 'keyword', '@knotHeader'],           // === knot ===
                    [/^\s*=(?!=)/, 'keyword', '@knotHeader'],          // = stitch
                    [/^\s*[*+](?:[ \t]*[*+])*/, 'keyword.choice'],
                    [/^\s*-(?!>)(?:[ \t]*-(?!>))*/, 'keyword.gather'],
                    [/^\s*~/, 'keyword.tilde'],                        // ~ logic line
                    [/->->|->|<-/, 'keyword.flow'],                    // diverts / threads
                    [/<>/, 'keyword.glue'],                            // glue
                    [/#.*$/, 'tag'],                                   // # tags
                    [/\[/, '@brackets', '@choiceBracket'],            // [choice-only text]
                    [/\{/, 'keyword.brace', '@inlineLogic'],          // { inline logic }
                    [/"/, 'string', '@string'],
                    [/[A-Za-z_]\w*/, {cases: {'@keywords': 'keyword', '@default': ''}}],
                ],
                knotHeader: [
                    [/\bfunction\b/, 'keyword'],
                    [/=+/, 'keyword'],
                    [/[A-Za-z_]\w*/, 'type.identifier'],
                    [/\(/, '@brackets', '@params'],
                    [/$/, '', '@pop'],
                    [/[^\S\r\n]+/, 'white'],
                    [/./, '', '@pop'],
                ],
                params: [
                    [/\bref\b/, 'keyword'],
                    [/[A-Za-z_]\w*/, 'variable.parameter'],
                    [/,/, 'delimiter'],
                    [/\)/, '@brackets', '@pop'],
                    [/[^\S\r\n]+/, 'white'],
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
                    [/[^{}|:"]+/, 'variable'],
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

        monaco.editor.defineTheme('ink-dark', {
            base: 'vs-dark',
            inherit: true,
            rules: [
                {token: 'comment', foreground: '4A5568', fontStyle: 'italic'},   // tok-comment
                {token: 'type.identifier', foreground: 'E8A07A', fontStyle: 'bold'}, // tok-knot
                {token: 'keyword.flow', foreground: '4F9CF0'},                    // tok-divert
                {token: 'keyword', foreground: 'C586C0'},                        // tok-keyword
                {token: 'keyword.tilde', foreground: 'B1A7C7'},                  // tok-tilde
                {token: 'keyword.glue', foreground: '4F9CF0'},                   // tok-glue
                {token: 'keyword.brace', foreground: 'E8C97A'},                  // tok-brace
                {token: 'keyword.choice', foreground: '4EC9A0'},                 // tok-choice
                {token: 'keyword.gather', foreground: '4EC9A0'},                 // tok-choice
                {token: 'string.bracket', foreground: '4EC9A0'},                 // tok-bracket
                {token: 'variable.parameter', foreground: '9CDCFE'},             // tok-label
            ],
            colors: {},
        });
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