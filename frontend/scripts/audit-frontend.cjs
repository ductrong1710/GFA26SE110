// Run against npm run dev. Supply PLAYWRIGHT_MODULE if Playwright is installed elsewhere.
const assert = require('node:assert/strict')
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright')
const base = process.env.AUDIT_URL || 'http://127.0.0.1:5190'
const roles = {
  FARM_OWNER: ['dashboard', 'farms', 'sensors', 'sensor-data', 'missions', 'alerts', 'reports'],
  ADMINISTRATOR: ['dashboard', 'farms', 'sensor-data', 'devices', 'missions', 'alerts', 'reports', 'users', 'settings'],
  UAV_DEVICE_OPERATOR: ['dashboard', 'sensors', 'sensor-data', 'devices', 'missions', 'sync', 'alerts', 'reports'],
  AGRICULTURAL_ENGINEER: ['dashboard', 'farms', 'sensors', 'sensor-data', 'alerts', 'reports'],
}
const routes = ['dashboard', 'farms', 'sensors', 'sensors/1', 'sensor-data', 'sensor-data/compare-zones', 'devices', 'missions', 'missions/create', 'missions/1', 'sync', 'alerts', 'reports', 'users', 'settings']
const failures = []
let checks = 0
async function navigate(page, path) {
  await page.evaluate((path) => { history.pushState({}, '', path); dispatchEvent(new PopStateEvent('popstate')) }, path)
  await page.locator('main h1').waitFor()
  await page.waitForFunction(() => {
    const parent = document.querySelector(`.app-nav-link[href="/app/${location.pathname.split('/')[2]}"]`)
    return !parent || parent.getAttribute('aria-current') === 'page'
  })
  await page.waitForTimeout(100) // Allow ResizeObserver to settle after route/viewport changes.
}
function check(condition, message) { checks++; if (!condition) failures.push(message) }
async function inspect(page, label, desktop = true) {
  const result = await page.evaluate(() => {
    const main = document.querySelector('.app-main')
    const sidebar = document.querySelector('#app-sidebar')
    const rect = sidebar.getBoundingClientRect()
    const controls = [...document.querySelectorAll('.app-shell input, .app-shell select, .app-shell textarea')].filter((el) => el.getClientRects().length && el.type !== 'hidden')
    const missingLabels = controls.filter((el) => !el.labels?.length && !el.getAttribute('aria-label') && !el.getAttribute('aria-labelledby')).map((el) => el.outerHTML.slice(0, 180))
    const badCharts = [...document.querySelectorAll('.recharts-wrapper')].filter((el) => el.clientWidth < 1 || el.getBoundingClientRect().right > main.getBoundingClientRect().right)
    const badTables = [...document.querySelectorAll('.app-ui-table-scroll')].filter((el) => !['auto', 'scroll'].includes(getComputedStyle(el).overflowX))
    const nav = getComputedStyle(document.querySelector('.app-nav-link'))
    const topbar = getComputedStyle(document.querySelector('.app-topbar'))
    return { shellCount: document.querySelectorAll('.app-shell').length, sidebarCount: document.querySelectorAll('#app-sidebar').length,
      mounted: sidebar === window.auditSidebar && document.querySelector('.app-topbar') === window.auditTopbar,
      width: rect.width, sidebarRight: rect.right, mainLeft: main.getBoundingClientRect().left,
      padding: parseFloat(getComputedStyle(main).paddingLeft), overflow: main.scrollWidth - main.clientWidth,
      bodyOverflow: document.documentElement.scrollWidth - innerWidth, missingLabels, badCharts: badCharts.length, badTables: badTables.length,
      appearance: JSON.stringify([
        ...['width', 'backgroundColor', 'fontFamily'].map((key) => getComputedStyle(sidebar)[key]),
        ...['padding', 'gap', 'fontSize', 'minHeight'].map((key) => nav[key]),
        document.querySelector('.app-nav-link svg').getBoundingClientRect().width,
        ...['padding', 'gap', 'minHeight', 'backgroundColor', 'borderBottom'].map((key) => topbar[key]),
      ]),
    }
  })
  check(result.shellCount === 1 && result.sidebarCount === 1 && result.mounted, `${label}: shell/sidebar/topbar persistence`)
  check(result.overflow <= 1 && result.bodyOverflow <= 1, `${label}: overflow ${JSON.stringify(result)}`)
  check(!result.missingLabels.length, `${label}: unlabeled controls ${JSON.stringify(result.missingLabels)}`)
  check(!result.badCharts && !result.badTables, `${label}: chart/table sizing`)
  if (desktop) {
    check(result.width === 256 && result.mainLeft >= result.sidebarRight, `${label}: sidebar overlap/width`)
    check(result.padding >= 24 && result.padding <= 32, `${label}: padding ${result.padding}`)
  }
  return result.appearance
}
async function inspectDialog(page, trigger, label) {
  await trigger.click()
  const dialog = page.locator('dialog[open]')
  await dialog.waitFor()
  check(await dialog.evaluate((el) => el.contains(document.activeElement)), `${label}: initial focus`)
  const fields = await dialog.locator('input, select, textarea').evaluateAll((elements) => elements.filter((el) => !el.labels?.length && !el.getAttribute('aria-label')).length)
  check(fields === 0, `${label}: field labels`)
  // Explicitly check both ends of the modal's tab order.
  await dialog.locator('button, input, select, textarea, a[href]').filter({ visible: true }).last().focus()
  await page.keyboard.press('Tab')
  await page.keyboard.press('Tab')
  check(await dialog.evaluate((el) => el.contains(document.activeElement)), `${label}: focus containment`)
  await page.keyboard.press('Escape')
  await dialog.waitFor({ state: 'detached' })
  check(await trigger.evaluate((el) => document.activeElement === el), `${label}: focus restoration`)
}
async function main() {
  const browser = await chromium.launch({ channel: process.env.AUDIT_BROWSER || 'msedge', headless: true })
  try {
    for (const [role, allowed] of Object.entries(roles)) {
      const context = await browser.newContext()
      const page = await context.newPage()
      page.on('pageerror', (error) => failures.push(`${role}: ${error.message}`))
      await page.goto(base + '/dang-nhap')
      await page.evaluate((role) => localStorage.setItem('smartfarm-mock-session', JSON.stringify({ id: { FARM_OWNER: 3, ADMINISTRATOR: 1, UAV_DEVICE_OPERATOR: 2, AGRICULTURAL_ENGINEER: 4 }[role], activeRole: role })), role)
      await page.goto(base + '/app/dashboard')
      await page.locator('main h1').waitFor()
      await page.evaluate(() => { window.auditSidebar = document.querySelector('#app-sidebar'); window.auditTopbar = document.querySelector('.app-topbar') })
      const links = await page.locator('.app-nav-link').evaluateAll((elements) => elements.map((el) => el.getAttribute('href').slice(5)))
      check(JSON.stringify(links) === JSON.stringify(allowed), `${role}: navigation permissions`)
      for (const width of [1440, 1280, 1024]) {
        await page.setViewportSize({ width, height: 960 })
        let appearance
        for (const route of routes) {
          await navigate(page, '/app/' + route)
          const permitted = route === 'missions/create' ? role === 'UAV_DEVICE_OPERATOR' : route === 'sensor-data/compare-zones' ? role === 'AGRICULTURAL_ENGINEER' : allowed.includes(route.split('/')[0])
          const denied = await page.getByRole('heading', { name: 'Access Denied', exact: true }).count()
          check(Boolean(denied) === !permitted, `${role}/${route}: route permission`)
          const style = await inspect(page, `${role}/${route}/${width}`)
          if (appearance) check(style === appearance, `${role}/${route}/${width}: sidebar appearance changed`)
          appearance = style
          if (permitted) check(await page.locator(`.app-nav-link[href="/app/${route.split('/')[0]}"][aria-current="page"]`).count() === 1, `${role}/${route}: active parent`)
        }
      }
      await navigate(page, '/app/alerts')
      const alert = page.locator('.management-table-link').first()
      await alert.click()
      await page.locator('dialog[open]').waitFor()
      check(await page.locator('[aria-label="Administrator actions"]').count() === (role === 'ADMINISTRATOR' ? 1 : 0), `${role}: alert actions`)
      await page.keyboard.press('Escape')
      await inspectDialog(page, alert, `${role}: alert drawer`)
      if (allowed.includes('farms')) {
        await navigate(page, '/app/farms')
        const addFarm = page.getByRole('button', { name: '+ Add Farm', exact: true })
        check(await addFarm.count() === (role === 'ADMINISTRATOR' ? 1 : 0), `${role}: farm actions`)
        if (role === 'ADMINISTRATOR') await inspectDialog(page, addFarm, 'Add Farm')
      }
      if (role === 'ADMINISTRATOR') {
        await navigate(page, '/app/users')
        await inspectDialog(page, page.getByRole('button', { name: '+ Add User', exact: true }), 'Add User')
      }
      if (role === 'UAV_DEVICE_OPERATOR') {
        await navigate(page, '/app/sensors')
        await inspectDialog(page, page.getByRole('button', { name: '+ Register Sensor', exact: true }), 'Register Sensor')
        await page.getByLabel('Search', { exact: true }).fill('no matching sensor')
        check(await page.getByText('No records found', { exact: true }).count() === 1, 'Filtered table empty state')
        await navigate(page, '/app/missions/1')
        await inspectDialog(page, page.getByRole('button', { name: 'Add Operational Note', exact: true }), 'Operational note')
        await navigate(page, '/app/missions/create')
        await page.getByLabel('Mission name', { exact: true }).fill('Audit collection plan')
        await page.getByRole('radio', { name: /Green Valley Farm/ }).check()
        for (let step = 1; step <= 8; step++) {
          if (step === 2) {
            await page.getByLabel('Select NODE_005', { exact: true }).check()
            await page.getByLabel('Select NODE_006', { exact: true }).check()
          }
          if (step === 3) await page.getByRole('button', { name: 'Auto-group Sensors' }).click()
          if (step === 4) {
            await page.getByRole('button', { name: 'Generate Route from Points' }).click()
            await page.getByLabel('Waypoint 1 coordinate type').selectOption('RELATIVE')
            await page.getByLabel('Waypoint 2 altitude').fill('130')
            await page.getByRole('button', { name: 'Next', exact: true }).click()
            await page.getByRole('alert').filter({ hasText: 'Waypoint 2' }).waitFor()
            await page.getByLabel('Waypoint 2 altitude').fill('30')
          }
          if (step === 6) await page.getByRole('radio', { name: /UAV_002/ }).check()
          if (step === 7) await page.getByRole('radio', { name: /GW_002/ }).check()
          for (const width of [1440, 1280, 1024]) {
            await page.setViewportSize({ width, height: 960 })
            await inspect(page, `Mission wizard step ${step}/${width}`)
          }
          if (step < 8) await page.getByRole('button', { name: 'Next', exact: true }).click()
        }
        await page.getByRole('button', { name: 'Save Mission', exact: true }).click()
        await page.getByRole('heading', { name: 'Audit collection plan', exact: true }).waitFor()
        check(true, 'Valid eight-step mission saves successfully')
      }
      for (const width of [768, 390]) {
        await page.setViewportSize({ width, height: 844 })
        await navigate(page, '/app/dashboard')
        await inspect(page, `${role}/mobile/${width}`, false)
        const trigger = page.getByRole('button', { name: 'Open navigation', exact: true })
        await trigger.click()
        check(await page.locator('#app-sidebar').getAttribute('aria-modal') === 'true', `${role}: mobile modal navigation`)
        check(await page.locator('#app-sidebar').evaluate((el) => el.contains(document.activeElement)), `${role}: mobile focus`)
        await page.keyboard.press('Escape')
        check(await trigger.evaluate((el) => document.activeElement === el), `${role}: mobile restore focus`)
        await trigger.click()
        await page.locator('.app-nav-link[href="/app/sensor-data"]').click()
        await page.locator('main h1').waitFor()
        check(await page.locator('#app-sidebar').getAttribute('inert') !== null, `${role}: mobile closes on navigation`)
      }
      await context.close()
    }
    const page = await browser.newPage()
    for (const route of ['/', '/thiet-bi', '/thiet-bi/soil-moisture', '/gio-hang', '/dang-nhap', '/dang-ky']) {
      await page.goto(base + route)
      check(await page.locator('main').count() > 0 && await page.locator('.app-shell').count() === 0, `Public route ${route}`)
      for (const width of [1440, 1280, 1024]) {
        await page.setViewportSize({ width, height: 960 })
        check(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `Public ${route}/${width}: overflow`)
      }
    }
    await page.goto(base + '/app/dashboard')
    await page.waitForURL('**/dang-nhap')
    check(true, 'Guest redirect')
    await page.evaluate(() => localStorage.setItem('smartfarm-mock-session', JSON.stringify({ id: 1, activeRole: 'ADMINISTRATOR' })))
    await page.route('**/src/pages/app/ReportsPage.jsx', (route) => route.abort())
    await page.goto(base + '/app/reports')
    await page.getByText('Unable to display this page', { exact: true }).waitFor()
    check(await page.locator('#app-sidebar').isVisible(), 'Failed page keeps navigation available')
    await page.locator('.app-nav-link[href="/app/farms"]').click()
    await page.getByRole('heading', { name: 'Farms & Zones', exact: true }).waitFor()
    check(true, 'Navigation recovers after a page loading error')
  } finally { await browser.close() }
  console.log(JSON.stringify({ checks, failures }, null, 2))
  assert.equal(failures.length, 0, 'Frontend audit failed')
}
main().catch((error) => { console.error(error); process.exitCode = 1 })
