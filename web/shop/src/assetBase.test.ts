import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { apiBase } from './shopClient.ts'

/**
 * The shop is reverse-proxied under /apps/shop and is opened as `/apps/shop?scenario=...`, with no
 * trailing slash. A relative asset base resolves against `/apps/` for that URL, so every asset
 * 404s and the page renders blank — which is exactly what happened before this test existed. The
 * earlier manual check passed only because it requested `/apps/shop/`, with the slash.
 */
describe('built asset paths', () => {
  // Resolved from the Vite root rather than import.meta.url: under the jsdom environment the
  // module URL is not a file: URL.
  const html = readFileSync(
    resolve(process.cwd(), '../../samples/NebuShop.Orders/wwwroot/index.html'),
    'utf8',
  )

  it('references assets by the full proxy path, not relative to the document', () => {
    const references = [...html.matchAll(/(?:src|href)="([^"]+)"/g)].map((match) => match[1])
    const assets = references.filter((reference) => reference.includes('assets/'))

    expect(assets.length).toBeGreaterThan(0)
    for (const asset of assets) {
      expect(asset.startsWith('/apps/shop/')).toBe(true)
    }
  })

  it('derives the API base from the page path with or without a trailing slash', () => {
    expect(apiBase('/apps/shop')).toBe('/apps/shop')
    expect(apiBase('/apps/shop/')).toBe('/apps/shop')
  })
})
