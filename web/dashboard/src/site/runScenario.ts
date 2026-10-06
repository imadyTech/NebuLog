import type { Scenario, ShopApi } from '../../../shared/scenarios'

/** What happened when a scenario card was pressed. */
export interface ScenarioLaunch {
  /** Where the current tab should navigate. */
  consoleUrl: string
  /** False when the browser refused the popup, so the console can offer a way back in. */
  shopOpened: boolean
}

/** Builds the console URL a scenario wants: its filters, pre-set and shareable. */
export function consoleUrlFor(scenario: Scenario): string {
  const params = new URLSearchParams()
  params.set('scenario', scenario.id)
  for (const service of scenario.services) {
    params.append('service', service)
  }
  if (scenario.minSeverity) {
    params.set('level', scenario.minSeverity)
  }
  params.set('follow', '1')

  return `/dashboard?${params.toString()}`
}

/** Builds the shop URL for a scenario. */
export function shopUrlFor(scenario: Scenario, api: ShopApi): string {
  return `/apps/shop?scenario=${encodeURIComponent(scenario.id)}&api=${api}`
}

/**
 * Opens the shop window and works out where the console should go.
 *
 * This must be called synchronously from the click handler. A popup opened after an await is no
 * longer attributable to a user gesture and every browser blocks it — which is why this function
 * does no asynchronous work at all, and why the caller must not await anything before it.
 */
export function launchScenario(
  scenario: Scenario,
  api: ShopApi,
  open: typeof window.open = window.open.bind(window),
): ScenarioLaunch {
  const consoleUrl = consoleUrlFor(scenario)

  if (scenario.action.kind === 'none') {
    return { consoleUrl, shopOpened: false }
  }

  const popup = open(shopUrlFor(scenario, api), 'nebushop', 'width=960,height=720')
  return { consoleUrl, shopOpened: popup !== null && popup !== undefined }
}
