import { afterEach, expect, test, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import ServiceReportForm from '../src/components/app/ServiceReportForm'
import ServiceReports from '../src/components/app/ServiceReports'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks() })

test('submits explicit checks with the job revision and retains the draft on failure', async () => {
  const request = vi.spyOn(api, 'request').mockRejectedValue(new Error('Job changed. Reload the latest revision.'))
  render(<ServiceReportForm job={{ id: 8, revision: 4 }} onSaved={vi.fn()} />)
  for (const [label, value] of [['Cause', 'Loose connector'], ['Work performed', 'Reconnected cable'], ['Components used', 'None'], ['Test results', 'Power restored'], ['Notes', 'Operation still fails']])
    fireEvent.change(screen.getByLabelText(label), { target: { value } })
  fireEvent.change(screen.getByLabelText('Connectivity'), { target: { value: 'true' } })
  fireEvent.change(screen.getByLabelText('Operation'), { target: { value: 'false' } })
  const handover = within(screen.getByRole('group', { name: 'Signed service handover' }))
  fireEvent.change(handover.getByLabelText('Add evidence images'), { target: { files: [new File(['signed'], 'signed.jpg', { type: 'image/jpeg' })] } })
  await handover.findByAltText('Evidence image 1')
  fireEvent.click(screen.getByRole('button', { name: 'Submit service report' }))
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', expect.stringContaining('Job changed'))
  expect(screen.getByLabelText('Cause').value).toBe('Loose connector')
  expect(request).toHaveBeenCalledWith('/api/service-jobs/8/reports', { method: 'POST', toastMessage: 'Service report submitted successfully.', body: {
    expectedRevision: 4, cause: 'Loose connector', work: 'Reconnected cable', components: 'None', testResults: 'Power restored',
    notes: 'Operation still fails', connectivityPassed: true, operationPassed: false, images: [], signedHandover: [{ contentType: 'image/jpeg', dataBase64: btoa('signed') }],
  } })
})

const report = { id: 5, revision: 2, cause: 'Loose connector', work: 'Reconnected cable', components: 'None', testResults: 'Reading received',
  notes: 'Retested', connectivityPassed: true, operationPassed: true, images: [{ contentType: 'image/png', dataBase64: btoa('photo') }] }

test('owner confirms the displayed latest report and preserves evidence and note on conflict', async () => {
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') throw new Error('Job changed. Reload the latest revision.')
    return path.endsWith('/5') ? report : { items: [report], pagination: { totalItems: 1 } }
  })
  render(<ServiceReports job={{ id: 8, revision: 5, status: 'REPORT_SUBMITTED' }} canConfirm onUpdated={vi.fn()} />)
  fireEvent.click(await screen.findByRole('button', { name: 'View report revision 2' }))
  await screen.findByText('Reading received')
  expect(screen.getByAltText('Evidence image 1')).toBeTruthy()
  expect(screen.queryByLabelText('Add evidence images')).toBeNull()
  fireEvent.change(screen.getByLabelText('Decision note'), { target: { value: 'Operation confirmed' } })
  fireEvent.click(screen.getByRole('button', { name: 'Confirm operation' }))
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', expect.stringContaining('Job changed'))
  expect(screen.getByLabelText('Decision note').value).toBe('Operation confirmed')
  expect(request).toHaveBeenCalledWith('/api/service-jobs/8/confirm-operation', { method: 'POST', body: { expectedRevision: 5, reportId: 5, note: 'Operation confirmed' } })
})

test('administrator can return failed checks but cannot approve them or review an older report', async () => {
  const failed = { ...report, operationPassed: false }
  vi.spyOn(api, 'request').mockImplementation(async path => path.endsWith('/5') ? failed : path.endsWith('/4') ? { ...failed, id: 4, revision: 1 }
    : { items: [failed, { id: 4, revision: 1 }], pagination: { totalItems: 2 } })
  render(<ServiceReports job={{ id: 8, revision: 6, status: 'OWNER_CONFIRMED' }} canReview onUpdated={vi.fn()} />)
  fireEvent.click(await screen.findByRole('button', { name: 'View report revision 2' }))
  expect((await screen.findByRole('button', { name: 'Approve service result' })).disabled).toBe(true)
  expect(screen.getByRole('button', { name: 'Request service rework' }).disabled).toBe(false)
  fireEvent.click(screen.getByRole('button', { name: 'View report revision 1' }))
  await screen.findByRole('heading', { name: 'Report revision 1' })
  expect(screen.queryByRole('button', { name: 'Request service rework' })).toBeNull()
})
