import { describe, expect, it, vi } from 'vitest'
import { SCENARIOS, findScenario } from '../../../shared/scenarios'
import { consoleUrlFor, launchScenario, shopUrlFor } from './runScenario'
import { readConsoleUrl, sanitiseTraceId, writeConsoleUrl } from './consoleUrlState'
import { summariseTrace } from './traceSummary'
import { safeReturnUrl } from './returnUrl'
import type { NebuLogEntry } from '../api/contracts'

describe('scenario launch', () => {
  it('builds a console URL carrying the scenario filters', () => {
    const checkout = findScenario('checkout')!
    const url = consoleUrlFor(checkout)

    expect(url.startsWith('/dashboard?')).toBe(true)
    const params = new URLSearchParams(url.slice(url.indexOf('?')))
    expect(params.get('scenario')).toBe('checkout')
    expect(params.getAll('service')).toEqual(['shop-orders', 'shop-payments'])
    expect(params.get('follow')).toBe('1')
  })

  it('carries a severity floor when the scenario narrows one', () => {
    const exception = findScenario('exception')!
    expect(new URLSearchParams(consoleUrlFor(exception)).get('level')).toBe('Error')
  })

  it('passes the scenario and the chosen backend to the shop', () => {
    expect(shopUrlFor(findScenario('pipelines')!, 'mvc')).toBe('/apps/shop?scenario=pipelines&api=mvc')
  })

  it('reports a blocked popup so the console can offer a way in', () => {
    const blocked = vi.fn(() => null)
    const launch = launchScenario(findScenario('checkout')!, 'minimal', blocked)

    expect(blocked).toHaveBeenCalledOnce()
    expect(launch.shopOpened).toBe(false)
  })

  it('opens no window for the scenarios that do not use the shop', () => {
    const open = vi.fn()
    const launch = launchScenario(findScenario('byo')!, 'minimal', open as unknown as typeof window.open)

    expect(open).not.toHaveBeenCalled()
    expect(launch.shopOpened).toBe(false)
  })

  it('gives every scenario the fields the demo page renders', () => {
    for (const scenario of SCENARIOS) {
      expect(scenario.number).toMatch(/^\d{2}$/)
      expect(scenario.title.length).toBeGreaterThan(0)
      expect(scenario.youWill.length).toBeGreaterThan(0)
      expect(scenario.watchFor.length).toBeGreaterThan(0)
      // Anything without a shop action has to offer somewhere else to go.
      expect(scenario.action.kind !== 'none' || scenario.link !== undefined).toBe(true)
    }
  })
})

describe('console URL state', () => {
  it('round-trips the shareable filters', () => {
    const query = writeConsoleUrl({
      scenarioId: 'checkout',
      services: new Set(['shop-payments', 'shop-orders']),
      traceId: 'a'.repeat(32),
      minSeverity: 13,
    })

    const state = readConsoleUrl(query)
    expect(state.scenario?.id).toBe('checkout')
    expect([...state.services].sort()).toEqual(['shop-orders', 'shop-payments'])
    expect(state.traceId).toBe('a'.repeat(32))
    expect(state.minSeverity).toBe(13)
  })

  it('writes nothing when no filter is set, so a plain console has a clean URL', () => {
    expect(writeConsoleUrl({ services: new Set(), traceId: '', minSeverity: 0 })).toBe('')
  })

  it('ignores values that did not come from this application', () => {
    const state = readConsoleUrl('?scenario=nope&level=Loud&trace=not-a-trace&service=')
    expect(state.scenario).toBeUndefined()
    expect(state.minSeverity).toBe(0)
    expect(state.traceId).toBe('')
    expect(state.services.size).toBe(0)
  })

  it('accepts only a 32-character hex trace id', () => {
    expect(sanitiseTraceId('A'.repeat(32))).toBe('a'.repeat(32))
    expect(sanitiseTraceId('a'.repeat(31))).toBe('')
    expect(sanitiseTraceId('zz')).toBe('')
    expect(sanitiseTraceId(null)).toBe('')
  })

  it('notices the popup-blocked marker', () => {
    expect(readConsoleUrl('?popup=blocked').popupBlocked).toBe(true)
    expect(readConsoleUrl('').popupBlocked).toBe(false)
  })
})

describe('return URL', () => {
  // Without this the sign-in page would be an open redirect, which is the usual way a phishing
  // link is made to look like it belongs to the site it is impersonating.
  it('accepts only same-origin paths', () => {
    expect(safeReturnUrl('/dashboard?trace=abc')).toBe('/dashboard?trace=abc')
    expect(safeReturnUrl('https://example.com')).toBeNull()
    expect(safeReturnUrl('//example.com')).toBeNull()
    expect(safeReturnUrl(null)).toBeNull()
  })
})

describe('trace summary', () => {
  const entry = (traceId: string, serviceName: string, timestampUnixMs: number): NebuLogEntry =>
    ({
      id: timestampUnixMs,
      traceId,
      serviceName,
      timestampUnixMs,
      body: '',
      severityNumber: 9,
      severityText: 'Info',
      scopeName: '',
      attributes: {},
    }) as unknown as NebuLogEntry

  it('reports the services, count and span of one trace', () => {
    const entries = [
      entry('t1', 'shop-orders', 1000),
      entry('t1', 'shop-payments', 1420),
      entry('t2', 'shop-orders', 9000),
    ]

    const summary = summariseTrace('t1', entries)
    expect(summary.services).toEqual(['shop-orders', 'shop-payments'])
    expect(summary.count).toBe(2)
    expect(summary.spanMs).toBe(420)
  })

  it('reports zeroes rather than NaN before any entry has arrived', () => {
    const summary = summariseTrace('t1', [])
    expect(summary.count).toBe(0)
    expect(summary.spanMs).toBe(0)
    expect(summary.services).toEqual([])
  })
})
