import { describe, expect, it } from 'vitest'
import { readShopUrl } from './urlState.ts'
import { apiBase, apiPath } from './shopClient.ts'
import {
  focusTraceMessage,
  isDemoMessage,
  requestMessage,
} from '../../shared/demoChannel.ts'

describe('readShopUrl', () => {
  it('reads the scenario and backend a demo card passed', () => {
    const state = readShopUrl('?scenario=checkout&api=mvc')

    expect(state.scenario?.id).toBe('checkout')
    expect(state.api).toBe('mvc')
  })

  it('falls back to the Minimal API when the backend is missing or unknown', () => {
    expect(readShopUrl('').api).toBe('minimal')
    expect(readShopUrl('?api=graphql').api).toBe('minimal')
  })

  it('ignores a scenario id that does not exist', () => {
    expect(readShopUrl('?scenario=not-a-scenario').scenario).toBeUndefined()
  })
})

describe('apiBase', () => {
  // The shop is reverse-proxied under /apps/shop, so a root-relative call would bypass the proxy
  // prefix and reach the NebuLog host instead of the shop.
  it('keeps the proxy prefix when the page is served under it', () => {
    expect(apiBase('/apps/shop/')).toBe('/apps/shop')
    expect(apiBase('/apps/shop/index.html')).toBe('/apps/shop')
  })

  it('is empty when the shop is served at the root, as it is under vite dev', () => {
    expect(apiBase('/')).toBe('')
  })

  it('builds the path for each implementation', () => {
    expect(apiPath('minimal', '/orders/42')).toBe('/api/minimal/orders/42')
    expect(apiPath('mvc', '/checkout')).toBe('/api/mvc/checkout')
  })
})

describe('demo channel messages', () => {
  it('builds a request message the console accepts', () => {
    const message = requestMessage({
      traceId: 'abc',
      method: 'POST',
      path: '/api/minimal/checkout',
      status: 200,
      ms: 412,
      scenario: 'checkout',
    })

    expect(message.type).toBe('request')
    expect(isDemoMessage(message)).toBe(true)
  })

  it('builds a focus-trace message', () => {
    expect(isDemoMessage(focusTraceMessage('abc'))).toBe(true)
  })

  it('rejects payloads that are not demo messages', () => {
    expect(isDemoMessage(null)).toBe(false)
    expect(isDemoMessage({ type: 'request' })).toBe(false)
    expect(isDemoMessage({ type: 'focus-trace', traceId: '' })).toBe(false)
    expect(isDemoMessage({ type: 'something-else', traceId: 'abc' })).toBe(false)
  })
})
