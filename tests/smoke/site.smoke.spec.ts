import { expect, test, type Page } from '@playwright/test'

/**
 * The deployed site, exercised the way a visitor would.
 *
 * Every assertion here corresponds to something that has actually broken in this project at least
 * once: a blank page from a mis-resolved asset path, a console that rendered no rows because of a
 * casing mismatch, a service reporting as `unknown_service`, a trace that did not cross the service
 * boundary. Unit tests did not catch any of them, because none of them are unit-sized.
 */

/** Signs in as the read-only demo visitor and waits for the guided demos. */
async function signInAsDemoVisitor(page: Page) {
  await page.goto('/login')
  await page.getByRole('button', { name: 'Continue as demo visitor' }).click()
  await expect(page).toHaveURL(/\/demo/)
}

test('the landing page is public and shows live figures', async ({ page }) => {
  const failures: string[] = []
  page.on('console', (message) => {
    if (message.type() === 'error') {
      failures.push(message.text())
    }
  })

  await page.goto('/')

  await expect(page.getByRole('heading', { name: /Watch your \.NET logs/ })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Try the live demo' }).first()).toBeVisible()

  // The counter is fed by the anonymous summary endpoint; "—" means the call failed.
  await expect(page.getByText(/\d[\d,]* entries · \d+ services?/)).toBeVisible()

  // A content-security-policy violation shows up here and nowhere else. Two known exceptions:
  // Cloudflare's analytics beacon, which the CSP blocks by design (WO-0008 §9.5), and the 401 from
  // the session probe, which is simply the correct answer for an anonymous visitor — Chrome logs a
  // console error for any non-2xx fetch, so it cannot be suppressed at the source.
  const unexpected = failures.filter(
    (text) => !text.includes('cloudflareinsights') && !text.includes('401'),
  )
  expect(unexpected, `unexpected console errors:\n${unexpected.join('\n')}`).toEqual([])
})

test('the public summary endpoint returns aggregates and no log content', async ({ request }) => {
  const response = await request.get('/api/public/summary')
  expect(response.ok()).toBe(true)

  const body = await response.json()
  expect(Object.keys(body).sort()).toEqual([
    'ingestedTotal',
    'ratePerSecondLast60s',
    'serviceCount',
    'uptimeSeconds',
  ])
})

test('the authenticated API stays closed to anonymous callers', async ({ request }) => {
  for (const path of ['/api/logs', '/api/summary', '/api/info']) {
    expect((await request.get(path)).status(), path).toBe(401)
  }
})

test('a demo visitor can sign in and reach the console', async ({ page }) => {
  await signInAsDemoVisitor(page)
  await page.getByRole('link', { name: 'Live console' }).click()

  await expect(page).toHaveURL(/\/dashboard/)
  await expect(page.getByText('Connected')).toBeVisible()

  // The console's table is a virtualised grid of divs, not a <table>: addressed by role.
  // Rows arriving at all is the check that caught the PascalCase/camelCase mismatch in WO-0007.
  await expect(page.getByRole('grid', { name: 'Log entries' }).getByRole('row').nth(1)).toBeVisible()
})

test('a checkout produces one trace across both shop services', async ({ context, page }) => {
  await signInAsDemoVisitor(page)

  // The scenario card opens the shop in a second window and switches this tab to the console.
  const [shop] = await Promise.all([
    context.waitForEvent('page'),
    page
      .locator('li', { hasText: 'One checkout across two services' })
      .getByRole('button', { name: 'Run scenario' })
      .click(),
  ])

  await shop.waitForLoadState('domcontentloaded')

  // A blank shop page is exactly what the relative-asset-path defect produced.
  await expect(shop.getByRole('heading', { name: 'NebuShop' })).toBeVisible()

  // The scenario checks out on open; give the rate limiter room and do it once more if needed.
  const row = shop.locator('table tbody tr').first()
  await expect(row).toBeVisible()

  if ((await row.innerText()).includes('429')) {
    await shop.waitForTimeout(6000)
    await shop.getByRole('button', { name: 'Checkout' }).click()
  }

  const successful = shop.locator('table tbody tr', { hasText: '200' }).first()
  await expect(successful).toBeVisible()

  const traceId = (await successful.locator('td').nth(4).innerText()).trim()
  expect(traceId, 'the shop should report a TraceId for a successful request').not.toBe('—')

  // Now the point of the whole scenario: both services, one trace.
  await expect(page).toHaveURL(/\/dashboard\?.*scenario=checkout/)
  await page.reload()

  const rows = page.getByRole('grid', { name: 'Log entries' }).getByRole('row')
  await expect(rows.nth(1)).toBeVisible()

  const seen = new Set(await rows.allInnerTexts())
  expect([...seen].some((name) => name.includes('shop-orders'))).toBe(true)
  expect([...seen].some((name) => name.includes('shop-payments'))).toBe(true)

  // And no service reported itself as unknown — the defect WO-0007 shipped with.
  expect([...seen].some((name) => name.includes('unknown_service'))).toBe(false)
})

test('a shared console link restores the same filters', async ({ page }) => {
  await signInAsDemoVisitor(page)
  await page.goto('/dashboard?service=shop-orders&level=Warn')

  // Wait for the console itself before asserting on its chips: the shell renders a loading state
  // while the session is confirmed, and asserting through it made this test flaky.
  await expect(page.getByRole('grid', { name: 'Log entries' })).toBeVisible()

  await expect(page.getByText('service: shop-orders')).toBeVisible()
  await expect(page.getByText('level ≥ Warn')).toBeVisible()
})

test('the public pages work at phone width', async ({ browser }) => {
  // WO-0011 §2.8 asks for 375 px. The in-app browser could not emulate that width, so this is where
  // the requirement is actually checked.
  const context = await browser.newContext({ viewport: { width: 375, height: 812 } })
  const page = await context.newPage()

  try {
    for (const path of ['/', '/login']) {
      await page.goto(path)
      const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth > window.innerWidth + 1,
      )
      expect(overflow, `${path} scrolls horizontally at 375 px`).toBe(false)
    }

    await page.goto('/login')
    await page.getByRole('button', { name: 'Continue as demo visitor' }).click()
    await expect(page).toHaveURL(/\/demo/)

    const demoOverflow = await page.evaluate(
      () => document.documentElement.scrollWidth > window.innerWidth + 1,
    )
    expect(demoOverflow, '/demo scrolls horizontally at 375 px').toBe(false)

    // Every control has to stay tappable: 24 px is the floor WCAG 2.2 SC 2.5.8 sets.
    const tooSmall = await page.evaluate(() =>
      Array.from(document.querySelectorAll('button, a'))
        .map((element) => ({
          label: (element.textContent ?? '').trim().slice(0, 30),
          height: Math.round(element.getBoundingClientRect().height),
        }))
        .filter((item) => item.height > 0 && item.height < 24),
    )
    expect(tooSmall, 'controls under 24 px tall').toEqual([])
  } finally {
    await context.close()
  }
})
