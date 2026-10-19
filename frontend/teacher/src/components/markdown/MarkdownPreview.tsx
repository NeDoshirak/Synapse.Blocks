import { useEffect, useRef } from "react";
import katex from "katex";
import "katex/dist/katex.min.css";

export function MarkdownPreview({ html, loading, error }: { html: string; loading?: boolean; error?: string }) {
  const contentRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const root = contentRef.current;
    if (!root) return;
    let cancelled = false;

    for (const code of root.querySelectorAll<HTMLElement>("pre > code.language-mermaid")) {
      const diagram = document.createElement("div");
      diagram.className = "mermaid";
      diagram.textContent = code.textContent ?? "";
      code.parentElement?.replaceWith(diagram);
    }

    for (const math of root.querySelectorAll<HTMLElement>(".math, .math-inline, .math-display")) {
      const source = (math.textContent ?? "").trim().replace(/^\$\$?\s*/, "").replace(/\s*\$\$?$/, "");
      try {
        katex.render(source, math, {
          displayMode: math.tagName.toLowerCase() === "div" || math.classList.contains("math-display"),
          throwOnError: false,
          trust: false,
          maxSize: 24,
          maxExpand: 1000,
          strict: "ignore",
        });
      } catch {
        math.textContent = source;
      }
    }

    const diagrams = root.querySelectorAll<HTMLElement>(".mermaid");
    if (diagrams.length) {
      void import("mermaid").then(({ default: mermaid }) => {
        if (cancelled) return;
        mermaid.initialize({ startOnLoad: false, securityLevel: "strict", suppressErrorRendering: true });
        return mermaid.run({ nodes: Array.from(diagrams) });
      }).catch(() => undefined);
    }
    return () => { cancelled = true; };
  }, [html]);

  return <div className="markdown-preview-shell">
    {loading && <div className="markdown-preview-state">Обновляем предпросмотр…</div>}
    {error && <div className="markdown-preview-error">{error}</div>}
    {!html && !loading && !error && <div className="markdown-preview-state">Предпросмотр появится здесь</div>}
    <div ref={contentRef} className="markdown-preview-content" dangerouslySetInnerHTML={{ __html: html }} />
  </div>;
}
