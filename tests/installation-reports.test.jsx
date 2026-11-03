import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import InstallationReports from '../src/components/app/InstallationReports'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks() })
it('confirms the displayed report and current job revision, preserving evidence on failure', async () => {
  const row = { id: 5, revision: 1, notes: 'Devices tested', images: [{ contentType: 'image/png', dataBase64: btoa('photo') }], devices: [{ proposalLineId: 8, sensorNodeId: 2, installationLocation: 'North', accessoriesChecked: true, connectivityPassed: true, operationPassed: true, testResults: 'Reading received' }] }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') throw new Error('Job changed. Reload the latest revision.')
    return path.endsWith('/5') ? row : { items: [row], pagination: { totalItems: 1 } }
  })
  render(<InstallationReports job={{ id: 3, revision: 5, status: 'REPORT_SUBMITTED' }} canConfirm onUpdated={vi.fn()} />)
  fireEvent.click(await screen.findByRole('button', { name: 'View report revision 1' }))
  await screen.findByText('Reading received')
  expect(screen.getByAltText('Evidence image 1')).toBeTruthy()
  expect(screen.queryByLabelText('Add evidence images')).toBeNull()
  fireEvent.change(screen.getByLabelText('Decision note'), { target: { value: 'Handover received' } })
  fireEvent.click(screen.getByRole('button', { name: 'Confirm handover' }))
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', expect.stringContaining('Job changed'))
  expect(screen.getByLabelText('Decision note').value).toBe('Handover received')
  expect(request).toHaveBeenCalledWith('/api/installation-jobs/3/confirm-handover', { method: 'POST', body: { expectedRevision: 5, reportId: 5, note: 'Handover received' } })
})
