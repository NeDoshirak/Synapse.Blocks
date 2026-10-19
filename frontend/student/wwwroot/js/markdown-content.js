export async function enhanceMarkdown(root) {
    const hasMath = root.querySelector(".math, .math-inline, .math-display");
    const hasDiagram = root.querySelector("pre > code.language-mermaid, .mermaid");
    if (!hasMath && !hasDiagram) return;
    const renderer = await import("../lib/markdown/markdown-renderer.js");
    await renderer.enhanceMarkdown(root);
}
