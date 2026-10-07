// Fails the build if any source file reaches for raw HTML injection.
//
// Log bodies and attributes come from whatever a producer sent, so injecting them as HTML is
// exactly the XSS hole v1 had. oxlint's react/no-danger covers the JSX prop; this covers the DOM
// properties it does not see. Required by WO-0006 §6.
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const forbidden = [/\bdangerouslySetInnerHTML\b/, /\binnerHTML\b/, /\bouterHTML\b/, /\binsertAdjacentHTML\b/]
// fileURLToPath, not .pathname: on Windows the latter yields '/E:/...', which then resolves to
// 'E:\E:\...' and makes this script crash instead of check.
const root = fileURLToPath(new URL('../src', import.meta.url))
const offences = []

function walk(directory) {
  for (const name of readdirSync(directory)) {
    const path = join(directory, name)
    if (statSync(path).isDirectory()) {
      walk(path)
      continue
    }

    if (!/\.(ts|tsx)$/.test(name)) {
      continue
    }

    const lines = readFileSync(path, 'utf8').split('\n')
    lines.forEach((line, index) => {
      // The check script and the tests that assert on it are allowed to name the patterns.
      if (path.endsWith('check-no-raw-html.mjs')) return

      for (const pattern of forbidden) {
        if (pattern.test(line)) {
          offences.push(`${path}:${index + 1}: ${line.trim()}`)
        }
      }
    })
  }
}

walk(root)

if (offences.length > 0) {
  console.error('Raw HTML injection is not allowed in the dashboard:\n' + offences.join('\n'))
  process.exit(1)
}

console.log('No raw HTML injection found.')
