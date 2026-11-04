import { completeService } from './complete-service'
import { expect } from '@playwright/test'
import { vietnamDateTimeInput } from '../src/utils/display.js'

export async function verifyMaintenance(owner, admin, technician, contractId, uav, technicianId) {
  await admin.goto('/app/maintenance-schedules')
  await admin.getByRole('button', { name: 'New schedule' }).click()
  await admin.getByLabel('Schedule scope').selectOption('DEVICE')
  await admin.getByLabel('Rental contract').selectOption(String(contractId))
  await admin.getByRole('combobox', { name: /^Rental device/ }).selectOption({ label: uav.code + ' · ' + uav.name })
  await admin.getByLabel('Service policy').fill('Inspect the orchard UAV dock every month.')
  await admin.getByRole('combobox', { name: /^Technician/ }).selectOption(String(technicianId))
  await admin.getByLabel('Next maintenance').fill(vietnamDateTimeInput(new Date(Date.now() + 80000)))
  await admin.getByRole('button', { name: 'Create schedule' }).click()
  const schedule = await admin.getByRole('heading', { name: /^Schedule #\d+$/ }).innerText()
  const openSchedule = 'Open schedule ' + schedule.match(/#\d+/)[0]
  const history = admin.getByRole('table', { name: 'Maintenance work history' })
  await expect(async () => {
    await admin.reload()
    await admin.getByRole('button', { name: openSchedule }).click()
    await expect(history.getByText(uav.code, { exact: true })).toBeVisible({ timeout: 1000 })
  }).toPass({ timeout: 110000, intervals: [2000, 5000] })
  const jobId = await history.getByRole('link').innerText()
  await technician.goto('/app/service-jobs')
  await technician.getByRole('button', { name: 'Open service job #' + jobId, exact: true }).click()
  const job = technician.getByRole('region', { name: 'Service job #' + jobId, exact: true })
  await expect(job.getByText('MAINTENANCE · ' + uav.code + ' · Farm #', { exact: false })).toBeVisible()
  await expect(job.getByRole('button', { name: 'Start service work' })).toBeVisible()
  await completeService(owner, admin, technician, jobId, 1, 'Scheduled dock inspection', 'Inspect dock terminals and verify connectivity')
  await admin.goto('/app/maintenance-schedules')
  await admin.getByRole('button', { name: openSchedule }).click()
  await expect(history.getByRole('row').filter({ hasText: uav.code }).getByRole('cell', { name: 'CLOSED', exact: true })).toBeVisible()
}
