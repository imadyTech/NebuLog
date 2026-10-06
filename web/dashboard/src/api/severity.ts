/** Severity bands, mirroring NebuLog.Contracts.Severity. */
export const SeverityNumbers = {
  unspecified: 0,
  trace: 1,
  debug: 5,
  info: 9,
  warn: 13,
  error: 17,
  fatal: 21,
} as const

export type SeverityBand = 'Trace' | 'Debug' | 'Info' | 'Warn' | 'Error' | 'Fatal' | 'Unspecified'

/** Returns the band a severity number falls into. */
export function severityBand(severityNumber: number): SeverityBand {
  if (severityNumber >= SeverityNumbers.fatal) return 'Fatal'
  if (severityNumber >= SeverityNumbers.error) return 'Error'
  if (severityNumber >= SeverityNumbers.warn) return 'Warn'
  if (severityNumber >= SeverityNumbers.info) return 'Info'
  if (severityNumber >= SeverityNumbers.debug) return 'Debug'
  if (severityNumber >= SeverityNumbers.trace) return 'Trace'
  return 'Unspecified'
}

/** A short, fixed-width label for the severity band. */
export function severityShortName(severityNumber: number): string {
  const band = severityBand(severityNumber)
  return band === 'Unspecified' ? 'NONE' : band.toUpperCase()
}

/** The bands offered in the minimum-level filter, lowest first. */
export const severityFilterOptions: ReadonlyArray<{ label: string; value: number }> = [
  { label: 'All', value: 0 },
  { label: 'Trace', value: SeverityNumbers.trace },
  { label: 'Debug', value: SeverityNumbers.debug },
  { label: 'Info', value: SeverityNumbers.info },
  { label: 'Warn', value: SeverityNumbers.warn },
  { label: 'Error', value: SeverityNumbers.error },
  { label: 'Fatal', value: SeverityNumbers.fatal },
]
