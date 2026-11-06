import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import CropSupportDetail from '../src/components/app/CropSupportDetail'
import CropReportForm from '../src/components/app/CropReportForm'
import CropSupportPage from '../src/pages/app/CropSupportPage'
import { api } from '../src/services/api'
import { PERMISSIONS as P } from '../src/config/permissions'
const auth = vi.hoisted(() => ({ user: { id: 4 }, can: () => true }))
vi.mock('../src/context/AuthContext', () => ({ useAuth: () => auth }))
afterEach(() => { cleanup(); vi.restoreAllMocks(); auth.user = { id: 4 }; auth.can = () => true })
const report = { id: 9, farmId: 1, crop: 'Rice', description: 'Yellow leaves', reporterId: 3, engineerId: 4, revision: 4, status: 'IN_REVIEW', images: [], updates: [] }
test('owner submits typed farm zone and sensor references and clears references on farm change', async () => {
  vi.spyOn(api, 'get').mockImplementation(async path => ({ items: path === '/api/farms' ? [{ id: 1, name: 'Rice farm', isActive: true }, { id: 2, name: 'Other farm', isActive: true }]
    : path.endsWith('/zones') ? [{ id: 5, name: 'North' }] : path.endsWith('/channels') ? [{ id: 8, channelCode: 'temperature' }] : [{ id: 7, name: 'Node', deviceCode: 'N1' }], pagination: { totalPages: 1 } }))
  const request = vi.spyOn(api, 'request').mockResolvedValue(report)
  render(<CropReportForm onSaved={vi.fn()} />)
  await screen.findByRole('option', { name: 'Rice farm' })
  fireEvent.change(screen.getByLabelText('Farm'), { target: { value: '1' } })
  await screen.findByRole('option', { name: 'North' })
  fireEvent.change(screen.getByLabelText('Zone'), { target: { value: '5' } })
  await screen.findByRole('option', { name: 'Node (N1)' })
  fireEvent.change(screen.getByLabelText('Related sensor node (optional)'), { target: { value: '7' } })
  fireEvent.click(await screen.findByLabelText('temperature'))
  fireEvent.change(screen.getByLabelText('Crop'), { target: { value: 'Rice' } })
  fireEvent.change(screen.getByLabelText('Symptoms'), { target: { value: 'Yellow leaves' } })
  fireEvent.submit(screen.getByRole('form', { name: 'Report crop condition' }))
  await vi.waitFor(() => expect(request).toHaveBeenCalledWith('/api/crop-condition-reports', { method: 'POST', toastMessage: 'Crop condition report submitted successfully.', body: { farmId: 1, zoneId: 5, crop: 'Rice', description: 'Yellow leaves', sensorChannelIds: [8], images: [] } }))
  fireEvent.click(screen.getByLabelText('temperature'))
  fireEvent.change(screen.getByLabelText('Farm'), { target: { value: '2' } })
  expect(screen.queryByRole('button', { name: 'Remove temperature' })).toBeNull()
  expect(screen.getByLabelText('Zone').value).toBe('')
})
test('engineer page accepts an inbox report and refreshes processing controls without general farm calls', async () => {
  auth.can = permission => permission === P.CROP_SUPPORT_PROCESS || permission === P.CROP_SUPPORT_VIEW
  const inbox = { ...report, status: 'NEW', engineerId: null, revision: 1 }
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options) => options?.method === 'POST' ? report
    : path.endsWith('/9') ? inbox : { items: path.endsWith('/sensor-readings') ? [] : [inbox], pagination: { totalItems: 1 } })
  render(<CropSupportPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open crop report #9' }))
  const accept = await screen.findByRole('button', { name: 'Accept crop report' })
  expect(request.mock.calls.some(([path]) => path.endsWith('/crop-seasons'))).toBe(false)
  fireEvent.click(accept)
  expect(await screen.findByRole('button', { name: 'Record recommendation' })).toBeTruthy()
  await screen.findByRole('heading', { name: 'Supporting crop seasons' })
  expect(request.mock.calls.some(([path]) => path.endsWith('/9/crop-seasons'))).toBe(true)
  expect(request).toHaveBeenCalledWith('/api/crop-condition-reports/9/claim', { method: 'POST' })
  expect(request.mock.calls.every(([path]) => path.startsWith('/api/crop-condition-reports'))).toBe(true)
})
test('assigned engineer submits a human recommendation with loaded revision and survey flag', async () => {
  const saved = vi.fn()
  const request = vi.spyOn(api, 'request').mockResolvedValue({ items: [], pagination: { totalItems: 0 } })
  render(<CropSupportDetail report={report} onSaved={saved} onReload={vi.fn()} />)
  fireEvent.change(screen.getByLabelText('Consultation note'), { target: { value: 'Inspect roots' } })
  fireEvent.click(screen.getByLabelText('Recommend an on-site survey'))
  fireEvent.click(screen.getByRole('button', { name: 'Record recommendation' }))
  await vi.waitFor(() => expect(saved).toHaveBeenCalled())
  expect(request).toHaveBeenCalledWith('/api/crop-condition-reports/9/recommend', { method: 'POST', body: { expectedRevision: 4, content: 'Inspect roots', recommendSiteSurvey: true, images: [] } })
})
test('conflict preserves the note and offers explicit reload', async () => {
  const reload = vi.fn()
  vi.spyOn(api, 'request').mockImplementation(async (_, options) => { if (options?.method === 'POST') throw new Error('Report changed. Reload.'); return { items: [], pagination: { totalItems: 0 } } })
  render(<CropSupportDetail report={report} onSaved={vi.fn()} onReload={reload} />)
  fireEvent.change(screen.getByLabelText('Consultation note'), { target: { value: 'Keep my note' } })
  fireEvent.click(screen.getByRole('button', { name: 'Resolve report' }))
  expect(await screen.findByRole('alert')).toBeTruthy()
  expect(screen.getByLabelText('Consultation note').value).toBe('Keep my note')
  fireEvent.click(screen.getByRole('button', { name: 'Reload report' })); expect(reload).toHaveBeenCalled()
})
test('monitoring admin and unassigned engineer do not get processing controls', () => {
  auth.can = permission => permission === P.CROP_SUPPORT_VIEW
  vi.spyOn(api, 'request').mockResolvedValue({ items: [], pagination: { totalItems: 0 } })
  render(<CropSupportDetail report={report} onSaved={vi.fn()} onReload={vi.fn()} />)
  expect(screen.queryByLabelText('Consultation note')).toBeNull()
  expect(screen.queryByRole('button', { name: 'Resolve report' })).toBeNull()
})
test('reporter replies only while awaiting information', async () => {
  auth.user = { id: 3 }; auth.can = permission => permission === P.CROP_SUPPORT_CREATE
  const request = vi.spyOn(api, 'request').mockResolvedValue({ items: [], pagination: { totalItems: 0 } })
  render(<CropSupportDetail report={{ ...report, status: 'AWAITING_INFORMATION' }} onSaved={vi.fn()} onReload={vi.fn()} />)
  fireEvent.change(screen.getByLabelText('Consultation note'), { target: { value: 'Observed yesterday' } })
  fireEvent.click(screen.getByRole('button', { name: 'Send additional information' }))
  await vi.waitFor(() => expect(request).toHaveBeenCalledWith('/api/crop-condition-reports/9/reply', expect.objectContaining({ method: 'POST', body: expect.objectContaining({ expectedRevision: 4 }) })))
  expect(screen.queryByRole('button', { name: 'Resolve report' })).toBeNull()
})
