#!/usr/bin/env bash
# Build all attachment PDFs from their Markdown sources.
#
# Pipeline:
#   1. Markdown → HTML via `marked` (run through npx, no global install)
#   2. HTML wrapped with print-friendly CSS
#   3. HTML → PDF via headless Chrome (Google Chrome.app on macOS)
#
# Usage:
#   cd docs/attachments && ./build-pdfs.sh
#
# Requirements (macOS):
#   - node + npx (Homebrew: `brew install node`)
#   - Google Chrome at /Applications/Google Chrome.app/
set -euo pipefail

cd "$(dirname "$0")"

CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"

if [[ ! -x "$CHROME" ]]; then
  echo "ERROR: Google Chrome not found at $CHROME" >&2
  exit 1
fi

# Print-friendly CSS — A4 page, readable typography, code/table styling
read -r -d '' CSS << 'EOF' || true
<style>
  @page { size: A4; margin: 18mm 18mm 18mm 18mm; }
  * { box-sizing: border-box; }
  html { font-size: 10.5pt; }
  body {
    font-family: -apple-system, BlinkMacSystemFont, "Helvetica Neue", Helvetica, Arial, sans-serif;
    color: #222;
    line-height: 1.45;
    max-width: none;
    margin: 0;
    padding: 0;
  }
  h1 { font-size: 18pt; margin: 0 0 0.4em 0; border-bottom: 2px solid #444; padding-bottom: 4px; page-break-after: avoid; }
  h2 { font-size: 13pt; margin: 1.3em 0 0.4em 0; color: #1a3a5c; page-break-after: avoid; }
  h3 { font-size: 11pt; margin: 1.1em 0 0.3em 0; color: #2c4a6b; page-break-after: avoid; }
  p { margin: 0.4em 0; }
  blockquote {
    border-left: 3px solid #ccc;
    margin: 0.6em 0;
    padding: 0.2em 0.8em;
    color: #555;
    background: #f7f7f7;
    font-size: 0.95em;
  }
  code {
    font-family: "SF Mono", Menlo, Consolas, monospace;
    font-size: 0.88em;
    background: #f0f0f0;
    padding: 1px 4px;
    border-radius: 3px;
  }
  pre {
    font-family: "SF Mono", Menlo, Consolas, monospace;
    font-size: 0.82em;
    background: #f5f5f5;
    border: 1px solid #ddd;
    border-radius: 4px;
    padding: 8px 10px;
    overflow-x: auto;
    white-space: pre-wrap;
    word-wrap: break-word;
    page-break-inside: avoid;
  }
  pre code { background: none; padding: 0; }
  table {
    border-collapse: collapse;
    margin: 0.6em 0;
    width: 100%;
    font-size: 0.9em;
    page-break-inside: avoid;
  }
  th, td { border: 1px solid #bbb; padding: 4px 7px; text-align: left; vertical-align: top; }
  th { background: #eaeef2; font-weight: 600; }
  ul, ol { margin: 0.4em 0 0.4em 1.4em; padding: 0; }
  li { margin: 0.15em 0; }
  hr { border: none; border-top: 1px solid #ccc; margin: 1.2em 0; }
  a { color: #1a4f8c; text-decoration: none; }
  strong { font-weight: 600; }
  /* Avoid orphaned headings + table rows */
  h1, h2, h3 { break-after: avoid; }
  tr { break-inside: avoid; }
</style>
EOF

build_one() {
  local md="$1"
  local base="${md%.md}"
  local html="${base}.html"
  local pdf="${base}.pdf"

  echo "→ ${base}.md → ${pdf}"

  # 1. Markdown → HTML body via marked (npx auto-installs cache copy)
  local body
  body="$(npx --yes marked "$md")"

  # 2. Wrap with full HTML document including print CSS
  cat > "$html" <<HTMLEOF
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>${base}</title>
${CSS}
</head>
<body>
${body}
</body>
</html>
HTMLEOF

  # 3. HTML → PDF via headless Chrome
  "$CHROME" \
    --headless=new \
    --disable-gpu \
    --no-pdf-header-footer \
    --print-to-pdf="$(pwd)/${pdf}" \
    --print-to-pdf-no-header \
    "file://$(pwd)/${html}" \
    >/dev/null 2>&1

  # Cleanup intermediate HTML
  rm -f "$html"
}

for md in 01-architecture-overview.md \
          02-roadmap.md \
          04-cli-cpu-symphact-interaction.md \
          05-threat-model.md; do
  if [[ -f "$md" ]]; then
    build_one "$md"
  else
    echo "WARN: $md missing — skipped" >&2
  fi
done

echo
echo "Done. PDFs:"
ls -lh ./*.pdf 2>/dev/null || echo "  (none — Chrome may have failed silently)"
