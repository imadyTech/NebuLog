import { findScenario, type Scenario } from '../../../shared/scenarios'

/** Severity names as they appear in the URL, and the numbers the filter uses. */
const LEVELS: Record<string, number> = {
  Trace: 1,
  Debug: 5,
  Info: 9,
  Warn: 13,
  Error: 17,
  Fatal: 21,
}

/** The console state that lives in the query string, so a view can be shared as a link. */
export interface ConsoleUrlState {
  scenario: Scenario | undefined
  services: ReadonlySet<string>
  traceId: string
  minSeverity: number
  follow: boolean
  popupBlocked: boolean
}

export const emptyConsoleUrlState: ConsoleUrlState = {
  scenario: undefined,
  services: new Set<string>(),
  traceId: '',
  minSeverity: 0,
  follow: false,
  popupBlocked: false,
}

/** Reads the console state out of a query string. Unknown values are ignored, not trusted. */
export function readConsoleUrl(search: string): ConsoleUrlState {
  const params = new URLSearchParams(search)
  const level = params.get('level')

  return {
    scenario: findScenario(params.get('scenario')),
    services: new Set(params.getAll('service').filter((value) => value.length > 0)),
    traceId: sanitiseTraceId(params.get('trace')),
    minSeverity: level !== null && level in LEVELS ? LEVELS[level] : 0,
    follow: params.get('follow') === '1',
    popupBlocked: params.get('popup') === 'blocked',
  }
}

/**
 * Writes the console state back into a query string.
 *
 * Only non-default values are written, so a console with no filters has a clean URL and the
 * "copy this link" promise produces something a person can read.
 */
export function writeConsoleUrl(state: {
  scenarioId?: string
  services: ReadonlySet<string>
  traceId: string
  minSeverity: number
}): string {
  const params = new URLSearchParams()

  if (state.scenarioId) {
    params.set('scenario', state.scenarioId)
  }

  for (const service of [...state.services].sort()) {
    params.append('service', service)
  }

  if (state.traceId) {
    params.set('trace', state.traceId)
  }

  const level = Object.entries(LEVELS).find(([, value]) => value === state.minSeverity)
  if (level) {
    params.set('level', level[0])
  }

  const query = params.toString()
  return query.length > 0 ? `?${query}` : ''
}

/**
 * A TraceId is 32 hex characters. Anything else came from a hand-edited URL or a stray paste and
 * is dropped rather than fed into the filter.
 */
export function sanitiseTraceId(value: string | null | undefined): string {
  if (!value) {
    return ''
  }

  return /^[0-9a-f]{32}$/i.test(value) ? value.toLowerCase() : ''
}
