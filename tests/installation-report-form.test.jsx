import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import InstallationReportForm from '../src/components/app/InstallationReportForm'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks() })
it('reports only allocated devices with explicit checks and current revision', async () => {
  const request = vi.spyOn(api, 'request').mockImplementation(async (_, options = {}) => options.method === 'POST' ? { id: 7 } : [
    { id: 1, proposalLineId: 8, sensorNodeId: 2, uavId: null, gatewayId: null, code: 'SN-42', name: 'North sensor' },
  ])
  const saved = vi.fn()
  render(<InstallationReportForm job={{ id: 3, revision: 5 }} onSaved={saved} />)
  fireEvent.click(await screen.findByRole('button', { name: 'Prepare installation report' }))
  fireEvent.change(screen.getByLabelText('Installation location for SN-42'), { target: { value: 'Canal edge' } })
  for (const label of ['Accessories for SN-42', 'Connectivity for SN-42', 'Operation for SN-42']) fireEvent.change(screen.getByLabelText(label), { target: { value: 'true' } })
  fireEvent.change(screen.getByLabelText('Test evidence for SN-42'), { target: { value: 'Live reading received' } })
  fireEvent.change(screen.getByLabelText('Handover and installation notes'), { target: { value: 'Operation demonstrated' } })
  fireEvent.change(screen.getByLabelText('Add evidence images'), { target: { files: [new File(['installation'], 'result.jpg', { type: 'image/jpeg' })] } })
  expect(screen.getByRole('button', { name: 'Submit installation report' }).closest('fieldset').disabled).toBe(true)
  await screen.findByAltText('Evidence image 1')
  fireEvent.click(screen.getByRole('button', { name: 'Submit installation report' }))
  await vi.waitFor(() => expect(saved).toHaveBeenCalledWith({ id: 7 }))
  expect(request).toHaveBeenCalledWith('/api/installation-jobs/3/reports', expect.objectContaining({ method: 'POST', body: {
    expectedRevision: 5, notes: 'Operation demonstrated', issues: null, images: [{ contentType: 'image/jpeg', dataBase64: btoa('installation') }], devices: [{ proposalLineId: 8, sensorNodeId: 2, uavId: null, gatewayId: null,
      installationLocation: 'Canal edge', accessoriesChecked: true, connectivityPassed: true, operationPassed: true, testResults: 'Live reading received' }],
  } }))
})
