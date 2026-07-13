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
                    [/^\s*~/, 'keyword'],                              // ~ logic line
                    [/->->|->|<-/, 'keyword.flow'],                    // diverts / threads
                    [/<>/, 'keyword.glue'],                            // glue
                    [/#.*$/, 'tag'],                                   // # tags
                    [/\[/, '@brackets', '@choiceBracket'],            // [choice-only text]
                    [/\{/, '@brackets', '@inlineLogic'],              // { inline logic }
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
                    [/[^\]]+/, 'string'],
                    [/\]/, '@brackets', '@pop'],
                ],
                inlineLogic: [
                    [/[^{}|:"]+/, 'variable'],
                    [/[|:]/, 'delimiter'],
                    [/"/, 'string', '@string'],
                    [/\{/, '@brackets', '@push'],
                    [/\}/, '@brackets', '@pop'],
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
                {token: 'keyword.choice', foreground: 'C586C0', fontStyle: 'bold'},
                {token: 'keyword.gather', foreground: '4EC9B0'},
                {token: 'keyword.flow', foreground: 'DCDCAA', fontStyle: 'bold'},
                {token: 'keyword.glue', foreground: '808080'},
                {token: 'type.identifier', foreground: '4FC1FF'},
                {token: 'tag', foreground: '6A9955', fontStyle: 'italic'},
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