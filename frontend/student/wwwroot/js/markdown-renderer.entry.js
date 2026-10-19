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
