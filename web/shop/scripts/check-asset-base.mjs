// Verifies that the built page references its assets by the full proxied path.
//
// The shop is served under /apps/shop and opened as `/apps/shop?scenario=...` — with no trailing
// slash. A relative asset base resolves against `/apps/` for that URL, every asset 404s and the
// page renders blank, which is exactly what shipped in WO-0010.
//
// This runs as part of `npm run build` rather than as a vitest case: it inspects build output, so
// it belongs to the build. As a test it passed locally only because a previous build had left the
// file behind, and failed on a clean checkout where `npm test` runs before `npm run build`.
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const expected = '/apps/shop/'
// fileURLToPath, not .pathname: on Windows the latter yields '/E:/...'.
const page = fileURLToPath(new URL('../../../samples/NebuShop.Orders/wwwroot/index.html', import.meta.url))

const html = readFileSync(page, 'utf8')
const assets = [...html.matchAll(/(?:src|href)="([^"]+)"/g)]
  .map((match) => match[1])
  .filter((reference) => reference.includes('assets/'))

if (assets.length === 0) {
  console.error('No asset references found in the built page; the build did not produce what this checks.')
  process.exit(1)
}

const wrong = assets.filter((asset) => !asset.startsWith(expected))
if (wrong.length > 0) {
  console.error(`Assets must be referenced from "${expected}", or the page is blank when opened`)
  console.error(`without a trailing slash. Offending references:\n  ${wrong.join('\n  ')}`)
  process.exit(1)
}

console.log(`Asset base ok: ${assets.length} reference(s) under ${expected}`)
