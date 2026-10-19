import { mkdir, rm } from "node:fs/promises";
import { build } from "esbuild";

const outputDirectory = new URL("../wwwroot/lib/markdown/", import.meta.url);
await rm(outputDirectory, { recursive: true, force: true });
await mkdir(outputDirectory, { recursive: true });

await build({
  entryPoints: [new URL("../wwwroot/js/markdown-renderer.entry.js", import.meta.url).pathname],
  bundle: true,
  format: "esm",
  splitting: true,
  target: "es2022",
  outdir: new URL("../wwwroot/lib/markdown/", import.meta.url).pathname,
  entryNames: "markdown-renderer",
  chunkNames: "chunks/[name]-[hash]",
  assetNames: "fonts/[name]-[hash]",
  loader: { ".woff": "file", ".woff2": "file", ".ttf": "file" },
  minify: true,
  sourcemap: false,
  logLevel: "info"
});
