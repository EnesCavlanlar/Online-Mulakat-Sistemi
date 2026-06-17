window.codeEditor = (function () {
    const editors = {};
    let monacoLoadingPromise = null;

    function ensureMonacoLoaded() {
        if (window.monaco) {
            registerCSharpSnippets();
            return Promise.resolve();
        }

        if (monacoLoadingPromise) {
            return monacoLoadingPromise;
        }

        monacoLoadingPromise = new Promise(function (resolve, reject) {
            if (window.require) {
                configureAndLoadMonaco(resolve, reject);
                return;
            }

            const existingLoader = document.querySelector("script[data-monaco-loader='true']");
            if (existingLoader) {
                existingLoader.addEventListener("load", function () {
                    configureAndLoadMonaco(resolve, reject);
                });

                existingLoader.addEventListener("error", function () {
                    reject("Monaco Editor yüklenemedi.");
                });

                return;
            }

            const loaderScript = document.createElement("script");
            loaderScript.src = "https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs/loader.min.js";
            loaderScript.setAttribute("data-monaco-loader", "true");

            loaderScript.onload = function () {
                configureAndLoadMonaco(resolve, reject);
            };

            loaderScript.onerror = function () {
                reject("Monaco Editor yüklenemedi.");
            };

            document.body.appendChild(loaderScript);
        });

        return monacoLoadingPromise;
    }

    function configureAndLoadMonaco(resolve, reject) {
        try {
            if (!window.require) {
                reject("Monaco loader bulunamadı.");
                return;
            }

            window.require.config({
                paths: {
                    vs: "https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.45.0/min/vs"
                }
            });

            window.require(["vs/editor/editor.main"], function () {
                registerCSharpSnippets();
                resolve();
            }, function (err) {
                reject(err || "Monaco Editor yüklenemedi.");
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
            triggerCharacters: ["c", "f", "w", "m", "s"],
            provideCompletionItems: function () {
                return {
                    suggestions: [
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
                            label: "readline",
                            kind: monaco.languages.CompletionItemKind.Snippet,
                            insertText: "var input = Console.ReadLine();",
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            documentation: "Console.ReadLine();"
                        },
                        {
                            label: "parseint",
                            kind: monaco.languages.CompletionItemKind.Snippet,
                            insertText: "int ${1:number} = int.Parse(Console.ReadLine()!);",
                            insertTextRules: monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet,
                            documentation: "Console.ReadLine ile int okuma"
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
                    ]
                };
            }
        });
    }

    async function init(elementId, initialCode) {
        try {
            await ensureMonacoLoaded();

            const container = document.getElementById(elementId);

            if (!container) {
                console.error("[codeEditor] Editör container bulunamadı:", elementId);
                return false;
            }

            dispose(elementId);

            const model = monaco.editor.createModel(
                initialCode || "",
                "csharp"
            );

            const editor = monaco.editor.create(container, {
                model: model,
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
                try {
                    editor.layout();
                    editor.focus();
                } catch {
                }
            }, 200);

            return true;
        } catch (e) {
            console.error("[codeEditor] init error:", e);
            return false;
        }
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

        try {
            const model = editor.getModel();

            editor.dispose();

            if (model) {
                model.dispose();
            }
        } catch (e) {
            console.warn("[codeEditor] dispose error:", e);
        }

        delete editors[elementId];
    }

    function disposeAll() {
        Object.keys(editors).forEach(function (elementId) {
            dispose(elementId);
        });
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

        const lineCount = model.getLineCount();
        const safeLine = Math.min(Math.max(1, marker.line), lineCount);
        const maxColumn = model.getLineMaxColumn(safeLine);
        const safeColumn = Math.min(Math.max(1, marker.column), maxColumn);

        monaco.editor.setModelMarkers(model, "candidate-code", [
            {
                startLineNumber: safeLine,
                startColumn: safeColumn,
                endLineNumber: safeLine,
                endColumn: Math.min(safeColumn + 1, maxColumn),
                message: message,
                severity: monaco.MarkerSeverity.Error
            }
        ]);

        try {
            editor.revealLineInCenter(safeLine);
            editor.setPosition({
                lineNumber: safeLine,
                column: safeColumn
            });
            editor.focus();
        } catch {
        }
    }

    function tryParseLineColumn(message) {
        if (!message) {
            return null;
        }

        const patterns = [
            /Satır\s+(\d+),\s*Sütun\s+(\d+)/i,
            /Line\s+(\d+),\s*Column\s+(\d+)/i,
            /\((\d+),\s*(\d+)\)/i,
            /:(\d+):(\d+)/i
        ];

        for (let i = 0; i < patterns.length; i++) {
            const match = message.match(patterns[i]);

            if (!match) {
                continue;
            }

            const line = parseInt(match[1], 10);
            const column = parseInt(match[2], 10);

            if (!isNaN(line) && !isNaN(column)) {
                return {
                    line: Math.max(1, line),
                    column: Math.max(1, column)
                };
            }
        }

        return null;
    }

    function isReady(elementId) {
        return !!editors[elementId];
    }

    return {
        init: init,
        getValue: getValue,
        setValue: setValue,
        focus: focus,
        dispose: dispose,
        disposeAll: disposeAll,
        setDiagnostics: setDiagnostics,
        isReady: isReady
    };
})();