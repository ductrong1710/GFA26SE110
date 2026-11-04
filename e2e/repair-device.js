import { completeService } from './complete-service'
import { expect } from '@playwright/test'

export async function repairDevice(owner, admin, technician, contractId, uav, technicianId) {
  await owner.getByRole('combobox', { name: 'Rental contract' }).selectOption(String(contractId))
  await owner.getByRole('combobox', { name: 'Assigned device' }).selectOption({ label: uav.code + ' · ' + uav.name + ' · Orchard dock' })
  await owner.getByLabel('Incident description').fill('Dock connector intermittently loses power.')
  await owner.getByRole('button', { name: 'Report incident' }).click()
  const heading = await owner.getByRole('heading', { name: /Device incident #\d+/ }).innerText()
  const incidentId = Number(heading.match(/#(\d+)/)[1])
  const incidentButton = 'Open incident #' + incidentId
  await admin.getByRole('link', { name: 'Device incidents', exact: true }).click()
  await admin.getByRole('button', { name: incidentButton, exact: true }).click()
  await admin.getByRole('combobox', { name: 'Service type' }).selectOption('REPAIR')
  await admin.getByRole('combobox', { name: 'Technician' }).selectOption(String(technicianId))
  const day = new Date(Date.now() + 2 * 86400000).toISOString().slice(0, 10)
  await admin.getByLabel('Appointment').fill(day + 'T10:00')
  await admin.getByLabel('Assignment note').fill('Inspect dock power connector.')
  await admin.getByRole('button', { name: 'Create service job' }).click()
  const receipt = await admin.getByText(/Service job #\d+ created/).innerText()
  const jobId = Number(receipt.match(/#(\d+)/)[1])
  await admin.getByRole('button', { name: incidentButton, exact: true }).click()
  await expect(admin.getByRole('link', { name: 'View service job #' + jobId })).toBeVisible()
  await expect(admin.getByRole('button', { name: 'Create service job' })).toHaveCount(0)
  for (const cycle of [1, 2]) {
    if (cycle === 2) {
      await admin.getByLabel('Appointment').fill(day + 'T14:00')
      await admin.getByLabel('Assignment note').fill('Owner reported the connector fault again')
      await admin.getByRole('button', { name: 'Reopen service work', exact: true }).click()
      await expect(admin.getByRole('region', { name: 'Service job #' + jobId, exact: true }).getByText('REOPENED', { exact: true })).toBeVisible()
      await owner.reload()
      await owner.getByRole('button', { name: incidentButton, exact: true }).click()
      await expect(owner.getByRole('region', { name: heading, exact: true }).getByText('Open', { exact: true })).toBeVisible()
    }
    await completeService(owner, admin, technician, jobId, cycle, 'Loose terminal at dock', 'Reseat the dock terminal and secure the cable')
    await owner.getByRole('link', { name: 'Device incidents', exact: true }).click()
    await owner.getByRole('button', { name: incidentButton, exact: true }).click()
    await expect(owner.getByRole('region', { name: heading, exact: true }).getByText('Resolved', { exact: true })).toBeVisible()
  }
  await admin.goto('/app/dashboard')
  for (const [group, label] of [['Mission outcomes', 'Failed'], ['Mission outcomes', 'Completed'], ['Job progress', 'Repair closed'], ['Alert history', 'Opened']]) {
    await expect(admin.getByRole('region', { name: group }).getByRole('meter', { name: new RegExp(' · ' + label + '$') }).last()).toBeVisible()
  }
}
