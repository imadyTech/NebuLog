import { describe, expect, it } from 'vitest'
import { resolveStatColor, statPalette } from './colors'

describe('resolveStatColor', () => {
  it('accepts every palette name, case-insensitively', () => {
    for (const [name, hex] of Object.entries(statPalette)) {
      expect(resolveStatColor(name)).toBe(hex)
      expect(resolveStatColor(name.toUpperCase())).toBe(hex)
    }
  })

  it('accepts plain hex in both lengths', () => {
    expect(resolveStatColor('#abc')).toBe('#abc')
    expect(resolveStatColor('#A1B2C3')).toBe('#A1B2C3')
    expect(resolveStatColor('  #abc  ')).toBe('#abc')
  })

  it.each([
    null,
    undefined,
    '',
    '   ',
    'rebeccapurple',
    '#12',
    '#1234',
    '#gggggg',
    'red; background: url(http://evil.test)',
    'url(http://evil.test)',
    'var(--accent)',
    'expression(alert(1))',
    'rgb(1,2,3)',
    'javascript:alert(1)',
  ])('rejects %p', (value) => {
    expect(resolveStatColor(value)).toBeNull()
  })
})
