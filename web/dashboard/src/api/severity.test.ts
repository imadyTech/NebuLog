import { describe, expect, it } from 'vitest'
import { severityBand, severityShortName } from './severity'

describe('severity', () => {
  it.each([
    [0, 'Unspecified', 'NONE'],
    [-1, 'Unspecified', 'NONE'],
    [1, 'Trace', 'TRACE'],
    [4, 'Trace', 'TRACE'],
    [5, 'Debug', 'DEBUG'],
    [9, 'Info', 'INFO'],
    [12, 'Info', 'INFO'],
    [13, 'Warn', 'WARN'],
    [17, 'Error', 'ERROR'],
    [21, 'Fatal', 'FATAL'],
    [24, 'Fatal', 'FATAL'],
  ])('maps %i to %s', (value, band, short) => {
    expect(severityBand(value)).toBe(band)
    expect(severityShortName(value)).toBe(short)
  })
})
