window.codeEditor = (function () {
    const editors = {};

    function ensureMonacoLoaded() {
        return new Promise(function (resolve, reject) {
            if (window.monaco) {
                resolve();
                return;
            }

            if (window.require) {
                configureAndLoadMonaco(resolve, reject);
                return;
            }

            const loaderScript = document.createElement("script");
            loaderScript.src = "https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs/loader.min.js";

            loaderScript.onload = function () {
                configureAndLoadMonaco(resolve, reject);
            };

            loaderScript.onerror = function () {
                reject("Monaco Editor yüklenemedi.");
            };

            document.body.appendChild(loaderScript);
        });
    }

    function configureAndLoadMonaco(resolve, reject) {
        try {
            window.require.config({
                paths: {
                    vs: "https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs"
                }
            });

            window.require(["vs/editor/editor.main"], function () {
                registerCSharpSnippets();
                resolve();
            });
        } catch (e) {
            reject(e);
        }
    }

    function registerCSharpSnippets() {
        if (!window.monaco || window.__csharpSnippetsRegistered) {
            return;
        }

        window.__csharpSnippetsRegistered = true;

        monaco.languages.registerCompletionItemProvider("csharp", {
            triggerCharacters: ["c", "f", "w", "m"],
            provideCompletionItems: function () {
                const suggestions = [
                    {
                        label: "cw",
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: "Console.WriteLine(${1});",
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: "Console.WriteLine();"
                    },
                    {
                        label: "fori",
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            "for (int ${1:i} = ${2:0}; ${1:i} < ${3:n}; ${1:i}++)",
                            "{",
                            "    ${0}",
                            "}"
                        ].join("\n"),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: "for döngüsü"
                    },
                    {
                        label: "while",
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            "while (${1:condition})",
                            "{",
                            "    ${0}",
                            "}"
                        ].join("\n"),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: "while döngüsü"
                    },
                    {
                        label: "main",
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            "using System;",
                            "",
                            "public class Program",
                            "{",
                            "    public static void Main()",
                            "    {",
                            "        ${0}",
                            "    }",
                            "}"
                        ].join("\n"),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: "C# Program Main template"
                    },
                    {
                        label: "sum1to100",
                        kind: monaco.languages.CompletionItemKind.Snippet,
                        insertText: [
                            "int toplam = 0;",
                            "",
                            "for (int i = 1; i <= 100; i++)",
                            "{",
                            "    toplam += i;",
                            "}",
                            "",
                            "Console.WriteLine(toplam);"
                        ].join("\n"),
                        insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                        documentation: "1'den 100'e toplam örneği"
                    }
                ];

                return {
                    suggestions: suggestions
                };
            }
        });
    }

    async function init(elementId, initialCode) {
        await ensureMonacoLoaded();

        const container = document.getElementById(elementId);

        if (!container) {
            console.error("[codeEditor] Editör container bulunamadı:", elementId);
            return false;
        }

        if (editors[elementId]) {
            editors[elementId].dispose();
            delete editors[elementId];
        }

        const editor = monaco.editor.create(container, {
            value: initialCode || "",
            language: "csharp",
            theme: "vs-dark",
            automaticLayout: true,
            fontSize: 15,
            minimap: {
                enabled: false
            },
            scrollBeyondLastLine: false,
            wordWrap: "on",
            tabSize: 4,
            insertSpaces: true,
            lineNumbers: "on",
            roundedSelection: false,
            readOnly: false,
            cursorStyle: "line",
            quickSuggestions: true,
            suggestOnTriggerCharacters: true,
            acceptSuggestionOnEnter: "on",
            tabCompletion: "on",
            snippetSuggestions: "top"
        });

        editors[elementId] = editor;

        setTimeout(function () {
            editor.layout();
            editor.focus();
        }, 200);

        return true;
    }

    function getValue(elementId) {
        const editor = editors[elementId];

        if (!editor) {
            return "";
        }

        return editor.getValue();
    }

    function setValue(elementId, value) {
        const editor = editors[elementId];

        if (!editor) {
            return false;
        }

        editor.setValue(value || "");
        return true;
    }

    function focus(elementId) {
        const editor = editors[elementId];

        if (!editor) {
            return false;
        }

        editor.focus();
        return true;
    }

    function dispose(elementId) {
        const editor = editors[elementId];

        if (!editor) {
            return;
        }

        editor.dispose();
        delete editors[elementId];
    }

    function setDiagnostics(elementId, message) {
        const editor = editors[elementId];

        if (!editor || !window.monaco) {
            return;
        }

        const model = editor.getModel();

        if (!model) {
            return;
        }

        if (!message) {
            monaco.editor.setModelMarkers(model, "candidate-code", []);
            return;
        }

        const marker = tryParseLineColumn(message);

        if (!marker) {
            monaco.editor.setModelMarkers(model, "candidate-code", []);
            return;
        }

        monaco.editor.setModelMarkers(model, "candidate-code", [
            {
                startLineNumber: marker.line,
                startColumn: marker.column,
                endLineNumber: marker.line,
                endColumn: marker.column + 1,
                message: message,
                severity: monaco.MarkerSeverity.Error
            }
        ]);

        editor.revealLineInCenter(marker.line);
        editor.setPosition({
            lineNumber: marker.line,
            column: marker.column
        });
        editor.focus();
    }

    function tryParseLineColumn(message) {
        if (!message) {
            return null;
        }

        const regex = /Satır\s+(\d+),\s*Sütun\s+(\d+)/i;
        const match = message.match(regex);

        if (!match) {
            return null;
        }

        const line = parseInt(match[1], 10);
        const column = parseInt(match[2], 10);

        if (isNaN(line) || isNaN(column)) {
            return null;
        }

        return {
            line: Math.max(1, line),
            column: Math.max(1, column)
        };
    }

    return {
        init: init,
        getValue: getValue,
        setValue: setValue,
        focus: focus,
        dispose: dispose,
        setDiagnostics: setDiagnostics
    };
})();