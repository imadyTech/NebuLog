/**
 * Colour handling for producer-defined statistics.
 *
 * A `StatDefinition.Color` arrives from a log producer, so it is untrusted input that ends up in a
 * style attribute. Only a fixed palette of names and plain `#rgb` / `#rrggbb` hex are accepted;
 * anything else — `url(...)`, `expression(...)`, a CSS variable, a stray semicolon — is ignored.
 */
export const statPalette: Readonly<Record<string, string>> = {
  slate: '#5b6472',
  blue: '#3a6ea5',
  green: '#1f6b3a',
  amber: '#8a5a00',
  red: '#b3261e',
  purple: '#7b1fa2',
  teal: '#0f6b6b',
}

const hexPattern = /^#(?:[0-9a-f]{3}|[0-9a-f]{6})$/i

/** Returns a safe CSS colour, or null when the value is not allowed. */
export function resolveStatColor(color: string | null | undefined): string | null {
  if (color === null || color === undefined) {
    return null
  }

  const trimmed = color.trim()
  if (trimmed.length === 0) {
    return null
  }

  const named = statPalette[trimmed.toLowerCase()]
  if (named !== undefined) {
    return named
  }

  return hexPattern.test(trimmed) ? trimmed : null
}
