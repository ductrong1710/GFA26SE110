import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import ServicePlan from '../src/components/app/ServicePlan'
import { api } from '../src/services/api'

afterEach(() => { cleanup(); vi.restoreAllMocks() })
const job = { id: 8, revision: 4, status: 'IN_PROGRESS' }
function setup(plans = [], props = {}) {
  vi.spyOn(api, 'get').mockResolvedValue({ items: [], pagination: { totalPages: 1 } })
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    if (options.method === 'POST') return []
    return path === '/api/service-jobs/8' ? { ...job, revision: 5 } : plans
  })
  const onUpdated = vi.fn()
  render(<ServicePlan job={job} canWork canReview={false} onUpdated={onUpdated} {...props}>{ready => ready ? <p>Report enabled</p> : null}</ServicePlan>)
  return { request, onUpdated }
}
test('technician submits a no-charge repair plan with job revision and refreshes the job', async () => {
  const { request, onUpdated } = setup()
  fireEvent.change(await screen.findByLabelText('Service plan note'), { target: { value: 'Clean and test motor' } })
  fireEvent.click(screen.getByRole('button', { name: 'Submit service plan' }))
  await vi.waitFor(() => expect(onUpdated).toHaveBeenCalledWith({ ...job, revision: 5 }))
  expect(request).toHaveBeenCalledWith('/api/operations/service-jobs/8/plans', { method: 'POST', body: { expectedRevision: 4, note: 'Clean and test motor', lines: [] } })
  expect(screen.queryByText('Report enabled')).toBeNull()
})
test('administrator reviews proposed plan using the current job revision', async () => {
  const { request } = setup([{ id: 12, status: 'PROPOSED', priceLinesJson: '[]', evidenceJson: '{}' }], { canWork: false, canReview: true })
  fireEvent.change(await screen.findByLabelText('Service plan review note'), { target: { value: 'Approved repair' } })
  fireEvent.click(screen.getByRole('button', { name: 'Approve service plan' }))
  await vi.waitFor(() => expect(request).toHaveBeenCalledWith('/api/operations/service-plans/12/review', { method: 'POST', body: { expectedRevision: 4, approve: true, note: 'Approved repair' } }))
  expect(screen.queryByLabelText('Service plan note')).toBeNull()
})
test('replacement execution requires signed agreement and both serial image groups', async () => {
  const { request } = setup([{ id: 12, status: 'APPROVED', uavId: 3, priceLinesJson: '[]', evidenceJson: '{}' }])
  const button = await screen.findByRole('button', { name: 'Record service plan execution' })
  expect(button.disabled).toBe(true)
  for (const label of ['Signed service agreement', 'Old device serial evidence', 'New device serial evidence']) {
    const group = within(screen.getByRole('group', { name: label }))
    fireEvent.change(group.getByLabelText('Add evidence images'), { target: { files: [new File(['signed'], 'paper.jpg', { type: 'image/jpeg' })] } })
    await group.findByAltText('Evidence image 1')
  }
  fireEvent.click(button)
  await vi.waitFor(() => expect(request).toHaveBeenCalledWith('/api/operations/service-plans/12/execute', expect.objectContaining({ body: expect.objectContaining({ expectedRevision: 4, signedAgreement: [expect.any(Object)], oldDeviceImages: [expect.any(Object)], newDeviceImages: [expect.any(Object)] }) })))
})
test('only executed plans enable the report and stored PascalCase evidence is visible', async () => {
  setup([{ id: 12, status: 'EXECUTED', priceLinesJson: '[{"Name":"Motor","Quantity":1,"UnitPriceVnd":100,"Warranty":true,"Reason":"Covered","TotalVnd":0}]', evidenceJson: '{"SignedAgreement":[{"ContentType":"image/jpeg","DataBase64":"c2lnbmVk"}]}' }])
  expect(await screen.findByText('Report enabled')).toBeTruthy()
  expect(screen.getByAltText('Evidence image 1')).toBeTruthy()
  expect(screen.getByText(/Covered/)).toBeTruthy()
})
