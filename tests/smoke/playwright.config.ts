import { defineConfig, devices } from '@playwright/test'

/**
 * Smoke tests that run against a deployed NebuLog, not a local build.
 *
 * WO-0007 ended with six defects that 215 green tests had not caught, all of which were obvious
 * within a minute of opening the deployed site. The lesson was "after deploying, open it and look";
 * this suite is that lesson written down so it happens every time rather than when someone
 * remembers. It is triggered manually, because it needs a running deployment.
 */
export default defineConfig({
  testDir: '.',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  retries: 1,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: process.env.NEBULOG_URL ?? 'https://nebulog.imady.co.nz',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    ...devices['Desktop Chrome'],
  },
})
