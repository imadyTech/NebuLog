import type { ShopApi } from '../../shared/scenarios.ts'
import { findScenario, type Scenario } from '../../shared/scenarios.ts'

/** The state a scenario card passes to the shop window through the query string. */
export interface ShopUrlState {
  scenario: Scenario | undefined
  api: ShopApi
}

/** Which backends exist. Anything else in the URL falls back to the Minimal API. */
const APIS: readonly ShopApi[] = ['minimal', 'mvc']

/**
 * Reads `?scenario=` and `?api=` from a URL.
 *
 * The query string is the only channel the `/demo` page has for telling a freshly opened window
 * what it is for, so unknown values are ignored rather than trusted: this runs on whatever the
 * address bar happens to contain.
 */
export function readShopUrl(search: string): ShopUrlState {
  const params = new URLSearchParams(search)
  const api = params.get('api')

  return {
    scenario: findScenario(params.get('scenario')),
    api: APIS.includes(api as ShopApi) ? (api as ShopApi) : 'minimal',
  }
}
