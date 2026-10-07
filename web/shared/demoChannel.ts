/**
 * The contract between the shop window and the console tab.
 *
 * Both run on the same origin (the shop is proxied under `/apps/shop`), so a BroadcastChannel is
 * enough — no server round trip, and nothing leaves the browser.
 */

/** The channel both windows join. */
export const DEMO_CHANNEL = 'nebulog-demo'

/** A request the shop has just completed. */
export interface DemoRequestMessage {
  type: 'request'
  traceId: string
  method: string
  path: string
  status: number
  ms: number
  scenario: string | null
}

/** A request to bring one trace to the front of the console. */
export interface DemoFocusTraceMessage {
  type: 'focus-trace'
  traceId: string
}

/** Everything the shop may send the console. */
export type DemoMessage = DemoRequestMessage | DemoFocusTraceMessage

/**
 * Narrows an unknown value to a demo message.
 *
 * A BroadcastChannel is same-origin, but the console still validates: the message drives what it
 * highlights, and a malformed payload should be dropped rather than rendered.
 */
export function isDemoMessage(value: unknown): value is DemoMessage {
  if (typeof value !== 'object' || value === null) {
    return false
  }

  const candidate = value as Partial<DemoRequestMessage> & Partial<DemoFocusTraceMessage>

  if (candidate.type === 'focus-trace') {
    return typeof candidate.traceId === 'string' && candidate.traceId.length > 0
  }

  if (candidate.type === 'request') {
    return (
      typeof candidate.traceId === 'string' &&
      typeof candidate.method === 'string' &&
      typeof candidate.path === 'string' &&
      typeof candidate.status === 'number' &&
      typeof candidate.ms === 'number'
    )
  }

  return false
}

/** Builds a request message. */
export function requestMessage(
  init: Omit<DemoRequestMessage, 'type'>,
): DemoRequestMessage {
  return { type: 'request', ...init }
}

/** Builds a focus-trace message. */
export function focusTraceMessage(traceId: string): DemoFocusTraceMessage {
  return { type: 'focus-trace', traceId }
}
