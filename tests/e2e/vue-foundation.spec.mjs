import { expect, test } from './fixtures.mjs'

test('Vue public pages render without loading the admin API', async ({ page, baseURL }) => {
  const apiRequests = []
  page.on('request', request => {
    if (new URL(request.url()).pathname.startsWith('/api/')) apiRequests.push(request.url())
  })

  await page.goto(`${baseURL}/app/about`, { waitUntil: 'networkidle' })
  await expect(page).toHaveTitle(/關於 EasyLottery/)
  await expect(page.locator('html')).toHaveAttribute('lang', 'zh-Hant')
  await expect(page.getByRole('heading', { name: '關於 EasyLottery' })).toBeVisible()
  await expect(page.getByRole('link', { name: '前往完整舊版' })).toBeVisible()
  expect(apiRequests).toHaveLength(0)

  await page.goto(`${baseURL}/app/privacy-policy`, { waitUntil: 'networkidle' })
  await expect(page.getByRole('heading', { name: '隱私權政策' })).toBeVisible()
  await expect(page.getByText(/YAML 或 SQLite/)).toBeVisible()
  expect(apiRequests).toHaveLength(0)
})

test('Vue administrator login starts with a blank, browser-validated email', async ({ page, baseURL }) => {
  await page.goto(`${baseURL}/app/login`, { waitUntil: 'networkidle' })
  const email = page.getByLabel('管理員 email')

  await expect(page.getByRole('heading', { name: '使用 Passkey 登入' })).toBeVisible()
  await expect(email).toHaveValue('')
  await email.fill('not-an-email')
  expect(await email.evaluate(element => element.checkValidity())).toBe(false)
})
