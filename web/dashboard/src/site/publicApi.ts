/** The two anonymous endpoints the landing page may call. */

export interface PublicSummary {
  ingestedTotal: number
  ratePerSecondLast60s: number[]
  serviceCount: number
  uptimeSeconds: number
}

export interface SiteConfig {
  blogUrl: string
  gitHubUrl: string
}

async function getJson<T>(path: string): Promise<T | null> {
  try {
    const response = await fetch(path, { credentials: 'same-origin' })
    if (!response.ok) {
      return null
    }

    return (await response.json()) as T
  } catch {
    // The landing page is the first thing a visitor sees; it renders without these, showing
    // placeholders rather than an error, so a rate-limited or restarting server is not a blank page.
    return null
  }
}

export const publicApi = {
  summary: () => getJson<PublicSummary>('/api/public/summary'),
  siteConfig: () => getJson<SiteConfig>('/api/public/site-config'),
}

/** A fixed set of example rows for the hero, so the public page never renders real log content. */
export interface SampleRow {
  time: string
  level: 'INFO' | 'DEBUG' | 'WARN' | 'ERROR'
  service: string
  message: string
}

export const SAMPLE_ROWS: readonly SampleRow[] = [
  { time: '21:14:08.117', level: 'INFO', service: 'shop-orders', message: 'Order 4821 placed: 2 x MER-0101' },
  { time: '21:14:08.119', level: 'DEBUG', service: 'shop-orders', message: 'Checkout 4821 started: 1 lines' },
  { time: '21:14:08.204', level: 'INFO', service: 'shop-payments', message: 'Authorising 378.00 NZD for order 4821' },
  { time: '21:14:08.711', level: 'WARN', service: 'shop-payments', message: 'Payment gateway took 506 ms' },
  { time: '21:14:08.712', level: 'INFO', service: 'shop-payments', message: 'Order 4821 authorised as AUTH-640220' },
  { time: '21:14:08.714', level: 'INFO', service: 'shop-orders', message: 'Checkout 4821 authorised, 506 ms' },
  { time: '21:14:09.002', level: 'ERROR', service: 'shop-orders', message: 'Order 4822 payment declined' },
  { time: '21:14:09.410', level: 'INFO', service: 'inventory-sync', message: 'Supplier feed loaded: 8 of 8 received' },
]
