const assert = require('node:assert/strict')
const path = require('node:path')
const { chromium } = require(process.env.STEAMIRL_PLAYWRIGHT_MODULE || path.resolve('.local/browser/node_modules/playwright'))

const base = process.env.STEAMIRL_WEB_URL || 'http://127.0.0.1:4173'
const user = { id: 'user-1', email: 'ux@example.invalid', displayName: '[ДЕМО UX] Игрок', isModerator: false }
const activity = { id: 'activity-1', slug: 'volleyball', name: 'Волейбол', description: 'Учебная активность' }
const location = {
  id: 'location-1', name: '[ДЕМО UX] Площадка', description: 'Учебная площадка', address: 'Учебный адрес',
  latitude: 54.6856, longitude: 25.295, updatedAt: '2026-11-13T10:00:00Z', sourceUrl: '',
  sourceCheckedAt: '2026-11-13T10:00:00Z', coordinateNote: 'Учебная точка', accessNotes: 'Учебные условия',
  activities: [activity], revision: 1,
}
const game = {
  id: 'game-1', locationId: location.id, locationName: location.name, activityId: activity.id, activityName: activity.name,
  activitySlug: activity.slug, organiserId: 'organiser-1', organiserName: '[ДЕМО UX] Организатор', kind: 'Immediate',
  skillLevel: 'Intermediate', equipmentStatus: 'BallNeeded', capacity: 1, participantCount: 0, remainingPlaces: 1,
  startsAt: '2026-11-13T18:00:00Z', endsAt: '2026-11-13T19:00:00Z', status: 'Open', isJoinable: true,
  isOrganiser: false, isParticipating: false, serverNow: '2026-11-13T18:10:00Z', cancellationReason: null,
}

;(async () => {
  const browser = await chromium.launch({ executablePath: process.env.STEAMIRL_BROWSER_PATH || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true })
  try {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 } })
    let signedIn = false
    await page.route('**/api/**', async route => {
      const request = route.request()
      const url = new URL(request.url())
      const json = value => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(value) })
      if (url.pathname === '/api/auth/session') return json({ user: signedIn ? user : null })
      if (url.pathname === '/api/auth/csrf') return json({ token: 'synthetic-csrf' })
      if (url.pathname === '/api/auth/login') { signedIn = true; return json(user) }
      if (url.pathname === '/api/activities') return json([activity])
      if (url.pathname === `/api/locations/${location.id}`) return json(location)
      if (url.pathname === `/api/sessions/${game.id}` && request.method() === 'GET') return json(game)
      if (url.pathname === `/api/sessions/${game.id}/join`) return route.fulfill({ status: 409, contentType: 'application/json', body: JSON.stringify({ title: 'Последнее место уже занято.' }) })
      if (url.pathname === '/api/sessions' && request.method() === 'GET') return json({ items: [], page: 1, pageSize: 20, hasMore: false })
      return route.fulfill({ status: 404, contentType: 'application/json', body: '{}' })
    })

    await page.goto(`${base}/#/sessions/new?location=${location.id}&activity=${activity.slug}`)
    await page.getByRole('link', { name: 'Войти и вернуться к форме' }).click()
    await page.getByLabel('Email').fill('ux@example.invalid')
    await page.getByLabel('Пароль').fill('Password123')
    await page.getByRole('button', { name: 'Войти' }).click()
    await page.getByRole('heading', { name: 'Создать игру' }).waitFor()
    assert.equal(new URL(page.url()).hash, `#/sessions/new?location=${location.id}&activity=${activity.slug}`)

    const start = page.getByLabel('Начало · время Вильнюса *')
    const end = page.getByLabel('Окончание · время Вильнюса *')
    await end.fill(await start.inputValue())
    await page.getByRole('button', { name: 'Создать запланированную игру' }).click()
    await page.getByText('Окончание игры должно быть позже начала.', { exact: true }).waitFor()
    assert.equal(await end.getAttribute('aria-invalid'), 'true')
    assert((await end.getAttribute('aria-describedby')).includes('new-end-error'))
    assert.equal(await page.evaluate(() => document.activeElement?.id), 'new-end')

    const requiredLegend = page.getByText('— обязательные поля', { exact: false })
    await requiredLegend.waitFor()
    const navHeights = await page.locator('.account-nav a').evaluateAll(nodes => nodes.map(node => node.getBoundingClientRect().height))
    assert(navHeights.length > 0 && navHeights.every(height => height >= 44), `navigation targets: ${navHeights.join(', ')}`)
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true)

    await page.goto(`${base}/#/locations/${location.id}?activity=${activity.slug}`)
    const cta = page.getByRole('link', { name: 'Создать игру на этой площадке' })
    await cta.waitFor()
    assert.equal(await cta.evaluate(node => Boolean(node.compareDocumentPosition(document.querySelector('.location-detail-layout')) & Node.DOCUMENT_POSITION_FOLLOWING)), true)

    await page.goto(`${base}/#/sessions/${game.id}`)
    await page.getByRole('button', { name: 'Присоединиться' }).click()
    await page.getByRole('button', { name: 'Подтвердить участие' }).click()
    await page.getByRole('alert').getByText('Последнее место уже занято.', { exact: true }).waitFor()
    await page.getByRole('alert').getByRole('button', { name: 'Обновить игру' }).waitFor()
    await page.getByRole('alert').getByRole('link', { name: 'Найти другую игру' }).waitFor()

    const checks = [
      'return after login without automatic action',
      'inline time validation, association and focus',
      'required-field legend and 44px navigation targets',
      'early place-card creation CTA at 390px',
      'recovery actions are adjacent to session command error',
    ]
    for (const check of checks) console.log(`PASS: ${check}`)
    require('node:fs').writeFileSync('docs/ux/web-ux-test-results.json', JSON.stringify({
      date: '2026-11-13', checks, passed: checks.length,
      viewport: { width: 390, height: 844 },
      data: 'Synthetic labelled API responses; no production accounts or records.',
      limitations: 'Automated browser verification; no human usability outcomes, screen-reader session or full WCAG audit.',
    }, null, 2))
  } finally {
    await browser.close()
  }
})().catch(error => { console.error(error); process.exit(1) })
