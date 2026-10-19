import katex from "katex";
import "katex/dist/katex.min.css";

let mermaidInstance;
let mermaidInitialized = false;
let stylesheetLoaded = false;

function ensureStylesheet() {
    if (stylesheetLoaded || document.querySelector("link[data-synapse-markdown-style]")) return;
    const link = document.createElement("link");
    link.rel = "stylesheet";
    link.href = new URL("./markdown-renderer.css", import.meta.url).href;
    link.dataset.synapseMarkdownStyle = "true";
    document.head.append(link);
    stylesheetLoaded = true;
}

function wrapDelimitedMath(root) {
    const displayExpression = /^\s*\\\[([\s\S]+?)\\\]\s*$/;
    for (const paragraph of root.querySelectorAll("p")) {
        if (paragraph.children.length !== 0) continue;
        const match = (paragraph.textContent ?? "").match(displayExpression);
        if (!match) continue;

        const display = document.createElement("div");
        display.className = "math math-display";
        display.textContent = match[1].trim();
        paragraph.replaceWith(display);
    }

    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    const textNodes = [];
    while (walker.nextNode()) {
        const node = walker.currentNode;
        if (!/\\\([\s\S]+?\\\)/.test(node.textContent ?? "")) continue;
        if (node.parentElement?.closest("pre, code, a, script, style, .katex, .mermaid, .math")) continue;
        textNodes.push(node);
    }

    const inlineExpression = /\\\(([\s\S]+?)\\\)/g;
    for (const node of textNodes) {
        const source = node.textContent ?? "";
        const fragment = document.createDocumentFragment();
        let cursor = 0;
        for (const match of source.matchAll(inlineExpression)) {
            const index = match.index ?? 0;
            fragment.append(document.createTextNode(source.slice(cursor, index)));
            const math = document.createElement("span");
            math.className = "math math-inline";
            math.textContent = match[1].trim();
            fragment.append(math);
            cursor = index + match[0].length;
        }
        fragment.append(document.createTextNode(source.slice(cursor)));
        node.replaceWith(fragment);
    }
}

function renderMath(root) {
    for (const math of root.querySelectorAll(".math:not([data-enhanced]), .math-inline:not([data-enhanced]), .math-display:not([data-enhanced])")) {
        let source = (math.textContent ?? "").trim();
        if (source.startsWith("\\(") && source.endsWith("\\)")) source = source.slice(2, -2).trim();
        else if (source.startsWith("\\[") && source.endsWith("\\]")) source = source.slice(2, -2).trim();
        else source = source.replace(/^\$\$?\s*/, "").replace(/\s*\$\$?$/, "");
        try {
            katex.render(source, math, {
                displayMode: math.tagName.toLowerCase() === "div" || math.classList.contains("math-display"),
                throwOnError: false,
                trust: false,
                maxSize: 24,
                maxExpand: 1000,
                strict: "ignore"
            });
            math.dataset.enhanced = "true";
        } catch {
            math.textContent = source;
        }
    }
}

export async function enhanceMarkdown(root) {
    ensureStylesheet();
    wrapDelimitedMath(root);
    for (const code of root.querySelectorAll("pre > code.language-mermaid")) {
        const diagram = document.createElement("div");
        diagram.className = "mermaid";
        diagram.textContent = code.textContent ?? "";
        code.parentElement?.replaceWith(diagram);
    }

    renderMath(root);
    const diagrams = [...root.querySelectorAll(".mermaid:not([data-enhanced])")];
    if (!diagrams.length) return;
    mermaidInstance ??= (await import("mermaid")).default;
    if (!mermaidInitialized) {
        mermaidInstance.initialize({
            startOnLoad: false,
            securityLevel: "strict",
            suppressErrorRendering: true,
            theme: "dark",
            themeVariables: { primaryColor: "#f36f29", lineColor: "#c3b9ae", textColor: "#efede8", mainBkg: "#201f1c" }
        });
        mermaidInitialized = true;
    }
    diagrams.forEach(diagram => { diagram.dataset.enhanced = "true"; });
    try {
        await mermaidInstance.run({ nodes: diagrams });
    } catch {
        diagrams.forEach(diagram => { delete diagram.dataset.enhanced; });
    }
}
