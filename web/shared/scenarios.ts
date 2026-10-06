/**
 * The eight guided scenarios.
 *
 * This file is the single source of truth: the shop window reads it to know which action a
 * scenario asks for, and the `/demo` page (WO-0011) renders the cards from the same array. Keeping
 * one list means a scenario cannot describe one thing and do another.
 */

/** Which backend implementation serves a shop request. */
export type ShopApi = 'minimal' | 'mvc'

/** What the shop should do when a scenario opens it. */
export type ScenarioAction =
  | { kind: 'none' }
  | { kind: 'open-order'; orderId: number }
  | { kind: 'checkout' }
  | { kind: 'invalid-order' }
  | { kind: 'signin' }
  | { kind: 'broken' }
  | { kind: 'burst' }

/** Where a scenario sends the visitor when it is not a shop action. */
export type ScenarioLink = { href: string; label: string }

/** One guided scenario. */
export interface Scenario {
  /** Stable identifier; travels in the URL as `?scenario=`. */
  id: string
  /** Two-digit number shown on the card. */
  number: string
  /** Card title. */
  title: string
  /** Roughly how long it takes to watch. */
  duration: string
  /** What the visitor does. */
  youWill: string
  /** What to look for in the console. */
  watchFor: string
  /** Technical tags shown on the card. */
  tags: string[]
  /** Services the console should filter to. */
  services: string[]
  /** Lowest severity the console should show, when the scenario narrows it. */
  minSeverity?: 'Trace' | 'Debug' | 'Info' | 'Warn' | 'Error' | 'Fatal'
  /** What the shop window does, if the scenario uses the shop. */
  action: ScenarioAction
  /** Where to go instead, for the two scenarios that do not use the shop. */
  link?: ScenarioLink
}

const SHOP_ORDERS = 'shop-orders'
const SHOP_PAYMENTS = 'shop-payments'

/** Every scenario, in the order the `/demo` page shows them. */
export const SCENARIOS: readonly Scenario[] = [
  {
    id: 'pipelines',
    number: '01',
    title: 'One request, two pipelines',
    duration: 'about 1 minute',
    youWill: 'Open order #42 in the Minimal API shop, then in the MVC shop.',
    watchFor:
      'Identical business entries from both, and far more framework entries from MVC: route matching, model binding, filters and result execution.',
    tags: ['Minimal API', 'MVC', 'Routing'],
    services: [SHOP_ORDERS],
    action: { kind: 'open-order', orderId: 42 },
  },
  {
    id: 'checkout',
    number: '02',
    title: 'One checkout across two services',
    duration: 'about 1 minute',
    youWill: 'Check out a basket. The order service calls the payments service over HTTP.',
    watchFor:
      'Entries from shop-orders and shop-payments sharing one TraceId. The payment is deliberately slow often enough to produce a warning.',
    tags: ['W3C traceparent', 'HttpClient'],
    services: [SHOP_ORDERS, SHOP_PAYMENTS],
    action: { kind: 'checkout' },
  },
  {
    id: 'validation',
    number: '03',
    title: 'A request that fails validation',
    duration: 'under a minute',
    youWill: 'Submit an order with a quantity of zero.',
    watchFor:
      'A 400 ProblemDetails response and one Warning entry whose attributes name the field that failed.',
    tags: ['ProblemDetails', 'Validation'],
    services: [SHOP_ORDERS],
    action: { kind: 'invalid-order' },
  },
  {
    id: 'exception',
    number: '04',
    title: 'An unhandled exception',
    duration: 'under a minute',
    youWill: "Press the shop's deliberately broken button.",
    watchFor: 'One Error entry carrying the exception type, message and stack trace as attributes.',
    tags: ['IExceptionHandler', 'Stack trace'],
    services: [SHOP_ORDERS],
    minSeverity: 'Error',
    action: { kind: 'broken' },
  },
  {
    id: 'redaction',
    number: '05',
    title: 'Secrets never leave the process',
    duration: 'about 1 minute',
    youWill: 'Sign in to the shop with a password.',
    watchFor:
      'The password arrives as *** because the redaction processor masks attribute values before export. Note what it cannot do: a secret formatted into the message text is already part of the sentence.',
    tags: ['RedactionProcessor'],
    services: [SHOP_ORDERS],
    action: { kind: 'signin' },
  },
  {
    id: 'burst',
    number: '06',
    title: 'A thousand logs per second',
    duration: '10 seconds',
    youWill: 'Start a burst from the shop.',
    watchFor:
      'The ring buffer absorbs the rate, counters climb, and scrolling stays smooth. Only one burst may run at a time.',
    tags: ['Ring buffer', 'Virtual list'],
    services: [SHOP_ORDERS],
    action: { kind: 'burst' },
  },
  {
    id: 'otlp-browser',
    number: '07',
    title: 'Any language, through OTLP',
    duration: 'about 1 minute',
    youWill: 'Send a log from a plain web page with fetch and no API key of its own.',
    watchFor: "The entry's source reads Otlp; the console treats it exactly like the .NET logs.",
    tags: ['OTLP/HTTP', 'CORS'],
    services: [],
    action: { kind: 'none' },
    link: { href: '/samples/browser/', label: 'Open the browser sample' },
  },
  {
    id: 'byo',
    number: '08',
    title: 'Bring your own application',
    duration: 'as long as you like',
    youWill: 'Follow the README to point your own application at this server.',
    watchFor: 'Two integration paths: the NebuLog exporter, or the stock OTLP exporter.',
    tags: ['README'],
    services: [],
    action: { kind: 'none' },
    link: { href: 'https://github.com/imadyTech/NebuLog#readme', label: 'Open the README' },
  },
]

/** Finds a scenario by id. */
export function findScenario(id: string | null | undefined): Scenario | undefined {
  if (!id) {
    return undefined
  }

  return SCENARIOS.find((scenario) => scenario.id === id)
}
