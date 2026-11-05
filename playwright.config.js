import { defineConfig } from '@playwright/test'

const browser = process.env.E2E_BROWSER ?? 'msedge'
if (!['msedge', 'chrome', 'firefox'].includes(browser)) throw new Error('E2E_BROWSER must be msedge, chrome or firefox.')

export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/test-mailbox.js',
  workers: 1,
  timeout: 180000,
  use: { baseURL: 'http://127.0.0.1:5179', browserName: browser === 'firefox' ? 'firefox' : 'chromium',
    channel: browser === 'firefox' ? undefined : browser,
    launchOptions: { executablePath: process.env.E2E_BROWSER_EXECUTABLE },
    headless: true, screenshot: 'only-on-failure', trace: 'retain-on-failure' },
  webServer: { command: 'node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5179 --strictPort', url: 'http://127.0.0.1:5179', reuseExistingServer: false },
})
