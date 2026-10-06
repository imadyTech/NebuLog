// Verifies the theme meets WCAG AA, for WO-0006 §6.
//
// The tokens are parsed out of src/index.css rather than copied here, so the check cannot drift
// from the stylesheet it is meant to police. Text pairs need 4.5:1; the border of an interactive
// control needs 3:1 (WCAG 1.4.11 Non-text Contrast).
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const css = readFileSync(fileURLToPath(new URL('../src/index.css', import.meta.url)), 'utf8')

// The site tokens live in web/shared so the console, the shop and the public pages share one
// palette. They are checked here rather than in a second script: one failing build is clearer than
// two places to look (WO-0011 §2.8).
const siteCss = readFileSync(fileURLToPath(new URL('../../shared/tokens.css', import.meta.url)), 'utf8')

/** Reads the custom properties from one `:root` block. */
function tokens(block) {
  const found = {}
  for (const match of block.matchAll(/--([\w-]+):\s*(#[0-9a-f]{3,8})/gi)) {
    found[match[1]] = match[2]
  }

  return found
}

// The first :root block is light; the one inside the prefers-color-scheme query overrides it.
const darkQuery = css.indexOf('@media (prefers-color-scheme: dark)')
const light = tokens(css.slice(0, darkQuery))
const dark = { ...light, ...tokens(css.slice(darkQuery)) }

function luminance(hex) {
  const raw = hex.replace('#', '')
  const full = raw.length === 3 ? [...raw].map((c) => c + c).join('') : raw.slice(0, 6)
  const [r, g, b] = [0, 2, 4]
    .map((i) => parseInt(full.slice(i, i + 2), 16) / 255)
    .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4))

  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function ratio(a, b) {
  const [high, low] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (high + 0.05) / (low + 0.05)
}

const severities = ['trace', 'debug', 'info', 'warn', 'error', 'fatal']
const site = tokens(siteCss)
let failures = 0

// The shared site palette. Light only: these tokens are used by the landing page, the login page
// and the guided demos, which are light-only by design (site-design.md §2).
console.log()
console.log('== site tokens ==')
const siteChecks = [
  ['ink on ground', site.ink, site.ground, 4.5],
  ['ink on surface', site.ink, site.surface, 4.5],
  ['text-2 on ground', site['text-2'], site.ground, 4.5],
  ['text-2 on surface', site['text-2'], site.surface, 4.5],
  ['muted on ground', site.muted, site.ground, 4.5],
  ['muted on surface', site.muted, site.surface, 4.5],
  ['accent on ground', site.accent, site.ground, 4.5],
  ['accent on surface', site.accent, site.surface, 4.5],
  ['accent on accent-soft', site.accent, site['accent-soft'], 4.5],
  ['surface on ink (primary button)', site.surface, site.ink, 4.5],
  ['line-strong on surface', site['line-strong'], site.surface, 3],
  ['line-strong on ground', site['line-strong'], site.ground, 3],
  // --accent-soft-border tints the edge of a panel or chip; it never outlines a control, so
  // WCAG 1.4.11's 3:1 does not apply to it (it measures 1.68:1 by design). What does have to hold
  // is that the controls sitting *inside* those tinted areas are still distinguishable, and that
  // their text is readable on the tint — both checked here.
  ['muted border on accent-soft (controls inside the banner)', site.muted, site['accent-soft'], 3],
  ['ink on accent-soft', site.ink, site['accent-soft'], 4.5],
  ['dark-text on dark', site['dark-text'], site.dark, 4.5],
  ['dark-text on dark-surface', site['dark-text'], site['dark-surface'], 4.5],
  ['dark-muted on dark', site['dark-muted'], site.dark, 4.5],
  ['dark-muted on dark-surface', site['dark-muted'], site['dark-surface'], 4.5],
  ['dark-accent on dark', site['dark-accent'], site.dark, 4.5],
  ['dark on dark-accent (primary button)', site.dark, site['dark-accent'], 4.5],
  ['shop-accent on surface', site['shop-accent'], site.surface, 4.5],
  ['surface on shop-accent (shop button)', site.surface, site['shop-accent'], 4.5],
  ...['trace', 'debug', 'info', 'warn', 'error', 'fatal'].map((band) => [
    `sev-${band} badge`,
    site[`sev-${band}-fg`],
    site[`sev-${band}-bg`],
    4.5,
  ]),
]

for (const [label, foreground, background, minimum] of siteChecks) {
  if (foreground === undefined || background === undefined) {
    console.error(`MISSING token for "${label}"`)
    failures++
    continue
  }

  const measured = ratio(foreground, background)
  const ok = measured >= minimum
  if (!ok) failures++
  console.log(`${ok ? 'PASS' : 'FAIL'} ${measured.toFixed(2)}:1 (need ${minimum}) ${label}`)
}

for (const [name, t] of [
  ['light', light],
  ['dark', dark],
]) {
  console.log(`\n== ${name} ==`)

  const checks = [
    ['text on surface', t.text, t.surface, 4.5],
    ['text on surface-sunken', t.text, t['surface-sunken'], 4.5],
    ['text on surface-raised', t.text, t['surface-raised'], 4.5],
    ['text-muted on surface', t['text-muted'], t.surface, 4.5],
    ['text-muted on surface-sunken', t['text-muted'], t['surface-sunken'], 4.5],
    ['accent-contrast on accent', t['accent-contrast'], t.accent, 4.5],
    ['accent (link) on surface', t.accent, t.surface, 4.5],
    ['border-strong on surface', t['border-strong'], t.surface, 3],
    ['border-strong on surface-sunken', t['border-strong'], t['surface-sunken'], 3],
    ...severities.flatMap((band) => [
      [`severity-${band} on surface`, t[`severity-${band}`], t.surface, 4.5],
      [`severity-${band} on surface-sunken`, t[`severity-${band}`], t['surface-sunken'], 4.5],
    ]),
  ]

  for (const [label, foreground, background, minimum] of checks) {
    if (foreground === undefined || background === undefined) {
      console.error(`MISSING token for "${label}"`)
      failures++
      continue
    }

    const measured = ratio(foreground, background)
    const ok = measured >= minimum
    if (!ok) failures++
    console.log(`${ok ? 'PASS' : 'FAIL'} ${measured.toFixed(2)}:1 (need ${minimum}) ${label}`)
  }
}

if (failures > 0) {
  console.error(`\n${failures} pair(s) below WCAG AA.`)
  process.exit(1)
}

console.log('\nAll pairs meet WCAG AA.')
