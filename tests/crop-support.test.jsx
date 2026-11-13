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
  fireEvent.change(screen.getByLabelText('Observed at (local time)'), { target: { value: '2026-11-01T10:00' } })
  fireEvent.submit(screen.getByRole('form', { name: 'Report crop condition' }))
  await vi.waitFor(() => expect(request).toHaveBeenCalledWith('/api/crop-condition-reports', { method: 'POST', toastMessage: 'Crop condition report submitted successfully.', body: { farmId: 1, zoneId: 5, crop: 'Rice', description: 'Yellow leaves', cropSeasonId: null, observedAt: new Date('2026-11-01T10:00').toISOString(), sensorChannelIds: [8], images: [] } }))
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
  expect(request.mock.calls.some(([path]) => path.endsWith('/sensor-readings') || path.endsWith('/alerts') || path.endsWith('/care-activities'))).toBe(false)
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

test('page reload keeps the conflicting draft and requires accepting the new revision', async () => {
  auth.can = permission => permission === P.CROP_SUPPORT_PROCESS || permission === P.CROP_SUPPORT_VIEW
  let current = report
  let conflicted = false
  let finishReload
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options) => {
    if (options?.method === 'POST') {
      if (!conflicted) { conflicted = true; current = { ...report, revision: 5 }; throw new Error('Revision conflict') }
      return { ...current, revision: 6 }
    }
    if (path === '/api/crop-condition-reports/9') return conflicted ? new Promise(resolve => { finishReload = resolve }) : current
    return { items: path === '/api/crop-condition-reports' ? [current] : [], pagination: { totalItems: 1, totalPages: 1 } }
  })
  render(<CropSupportPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Open crop report #9' }))
  fireEvent.change(await screen.findByLabelText('Consultation note'), { target: { value: 'Preserve inspection advice' } })
  fireEvent.click(screen.getByLabelText('Recommend an on-site survey'))
  fireEvent.change(screen.getByLabelText('Add evidence images'), { target: { files: [new File([new Uint8Array([1])], 'plant.png', { type: 'image/png' })] } })
  await screen.findByRole('button', { name: 'Remove image 1' })
  fireEvent.click(screen.getByRole('button', { name: 'Record recommendation' }))
  await screen.findByText('Revision conflict')
  fireEvent.click(screen.getByRole('button', { name: 'Reload report' }))
  expect(screen.getByLabelText('Consultation note').value).toBe('Preserve inspection advice')
  expect(screen.getByRole('button', { name: 'Reload report' }).disabled).toBe(true)
  finishReload(current)
  await screen.findByRole('button', { name: 'Use latest revision for this draft' })
  expect(screen.getByLabelText('Consultation note').value).toBe('Preserve inspection advice')
  expect(screen.getByLabelText('Recommend an on-site survey').checked).toBe(true)
  expect(screen.getByRole('button', { name: 'Remove image 1' })).toBeTruthy()
  expect(screen.getByRole('button', { name: 'Record recommendation' }).disabled).toBe(true)
  fireEvent.click(screen.getByRole('button', { name: 'Use latest revision for this draft' }))
  fireEvent.click(screen.getByRole('button', { name: 'Record recommendation' }))
  await screen.findByText('Crop report saved.')
  expect(request).toHaveBeenCalledWith('/api/crop-condition-reports/9/recommend', expect.objectContaining({ body: {
    expectedRevision: 5, content: 'Preserve inspection advice', recommendSiteSurvey: true, images: [{ contentType: 'image/png', dataBase64: 'AQ==' }],
  } }))
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

test('season intake keeps prefilled context and draft on API failure, clears season on scope changes', async () => {
  auth.user = { id: 3 }
  const season = { id: 12, farmId: 1, zoneId: 5, ownerId: 3, name: 'Autumn rice', crop: 'Rice', status: 'COMPLETED' }
  vi.spyOn(api, 'get').mockImplementation(async path => ({ items: path === '/api/farms' ? [{ id: 1, name: 'Rice farm', isActive: true }] : path.endsWith('/zones') ? [{ id: 5, name: 'North' }] : [], pagination: { totalPages: 1 } }))
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options) => {
    if (options?.method === 'POST') throw new Error('Season changed; verify context')
    return { items: [season, { ...season, id: 13, ownerId: 99, name: 'Private' }], pagination: { totalPages: 1 } }
  })
  render(<CropReportForm initialSeason={season} onSaved={vi.fn()} />)
  await screen.findByRole('option', { name: 'North' })
  expect(screen.getByLabelText('Crop season (optional)').value).toBe('12')
  expect(screen.queryByRole('option', { name: /Private/ })).toBeNull()
  expect(screen.getByLabelText('Crop').readOnly).toBe(true)
  fireEvent.change(screen.getByLabelText('Symptoms'), { target: { value: 'Keep these observations' } })
  fireEvent.change(screen.getByLabelText('Observed at (local time)'), { target: { value: '2026-11-01T10:00' } })
  fireEvent.submit(screen.getByRole('form', { name: 'Report crop condition' }))
  await screen.findByText('Season changed; verify context')
  expect(screen.getByLabelText('Symptoms').value).toBe('Keep these observations')
  expect(request).toHaveBeenCalledWith('/api/crop-condition-reports', expect.objectContaining({ body: expect.objectContaining({ cropSeasonId: 12, zoneId: 5 }) }))
  fireEvent.change(screen.getByLabelText('Zone'), { target: { value: '' } })
  expect(screen.getByLabelText('Crop season (optional)').value).toBe('')
})

test('owner requests review on a closed legacy report without an activity and reload preserves conflict draft', async () => {
  auth.user = { id: 3 }; auth.can = permission => permission === P.CROP_SUPPORT_CREATE
  const closed = { ...report, status: 'RESOLVED', allowedActions: ['follow-ups'], canReadEvidence: true }
  const request = vi.spyOn(api, 'request').mockImplementation(async (_, options) => {
    if (options?.method === 'POST') throw new Error('Revision conflict')
    return { items: [], pagination: { totalPages: 0 } }
  })
  const props = { onSaved: vi.fn(), onReload: vi.fn() }
  const ui = render(<CropSupportDetail report={closed} {...props} />)
  fireEvent.change(screen.getByLabelText('Consultation note'), { target: { value: 'No improvement after care' } })
  fireEvent.click(screen.getByRole('button', { name: 'Report no improvement / request review' }))
  await screen.findByText('Revision conflict')
  expect(request).toHaveBeenCalledWith('/api/crop-condition-reports/9/follow-ups', { method: 'POST', body: { expectedRevision: 4, type: 'NOT_IMPROVED', content: 'No improvement after care', images: [], cultivationActivityId: null } })
  ui.rerender(<CropSupportDetail report={{ ...closed, revision: 5 }} {...props} />)
  expect(screen.getByLabelText('Consultation note').value).toBe('No improvement after care')
  expect(screen.getByRole('button', { name: 'Report no improvement / request review' }).disabled).toBe(true)
  fireEvent.click(screen.getByRole('button', { name: 'Use latest revision for this draft' }))
  expect(screen.getByRole('button', { name: 'Report no improvement / request review' }).disabled).toBe(false)
})

test('admin assigns only active engineers and engineer reopens with revision and reason', async () => {
  auth.can = permission => permission === P.CROP_SUPPORT_ASSIGN
  vi.spyOn(api, 'get').mockResolvedValue({ items: [{ id: 4, fullName: 'Active engineer', isActive: true, roles: ['FarmEngineer'] }, { id: 7, fullName: 'Inactive engineer', isActive: false, roles: ['FarmEngineer'] }], pagination: { totalPages: 1 } })
  const request = vi.spyOn(api, 'request').mockResolvedValue({ items: [], pagination: { totalPages: 0 } })
  const props = { onSaved: vi.fn(), onReload: vi.fn() }
  const ui = render(<CropSupportDetail report={{ ...report, status: 'NEW', engineerId: null, allowedActions: ['assign'] }} {...props} />)
  await screen.findByRole('option', { name: 'Active engineer (#4)' })
  expect(screen.queryByRole('option', { name: /Inactive engineer/ })).toBeNull()
  fireEvent.change(screen.getByLabelText('Responsible engineer'), { target: { value: '4' } })
  fireEvent.click(screen.getByRole('button', { name: 'Assign engineer' }))
  await vi.waitFor(() => expect(request).toHaveBeenCalledWith('/api/crop-condition-reports/9/assign', { method: 'POST', body: { expectedRevision: 4, engineerId: 4 } }))
  ui.unmount(); auth.can = permission => permission === P.CROP_SUPPORT_PROCESS
  render(<CropSupportDetail report={{ ...report, status: 'RESOLVED', allowedActions: ['reopen'] }} {...props} />)
  fireEvent.change(screen.getByLabelText('Consultation note'), { target: { value: 'Review continued symptoms' } })
  fireEvent.click(screen.getByRole('button', { name: 'Reopen report with reason' }))
  await vi.waitFor(() => expect(request).toHaveBeenCalledWith('/api/crop-condition-reports/9/reopen', expect.objectContaining({ body: expect.objectContaining({ expectedRevision: 4, content: 'Review continued symptoms' }) })))
})

test('evidence preserves zero, labels invalid readings and missing values, and uses report child APIs', async () => {
  const request = vi.spyOn(api, 'request').mockImplementation(async path => ({ items: path.endsWith('/sensor-readings') ? [
    { id: 1, channelCode: 'valid-zero', value: 0, isValid: true }, { id: 2, channelCode: 'invalid', value: 99, isValid: false, qualityStatus: 'ERROR' }, { id: 3, channelCode: 'missing', value: null },
  ] : [], pagination: { totalPages: 1 } }))
  render(<CropSupportDetail report={{ ...report, canReadEvidence: true, observedAt: '2026-11-01T03:00:00Z', cropSeasonId: 12 }} onSaved={vi.fn()} onReload={vi.fn()} />)
  await screen.findByText('Invalid reading (99)')
  expect(screen.getByText('0')).toBeTruthy()
  expect(screen.getByText('Not recorded')).toBeTruthy()
  expect(request.mock.calls.every(([path]) => path.startsWith('/api/crop-condition-reports/9/'))).toBe(true)
  fireEvent.change(screen.getByLabelText('Evidence from (local time)'), { target: { value: '2026-11-01T00:00' } })
  fireEvent.change(screen.getByLabelText('Evidence to (local time)'), { target: { value: '2026-11-02T00:00' } })
  fireEvent.click(screen.getByRole('button', { name: 'Apply evidence window' }))
  await vi.waitFor(() => expect(request).toHaveBeenCalledWith('/api/crop-condition-reports/9/sensor-readings', expect.objectContaining({ query: { page: 1, from: new Date('2026-11-01T00:00').toISOString(), to: new Date('2026-11-02T00:00').toISOString() } })))
})

test('owner follows up with an optional existing activity and engineer records pending video referral', async () => {
  auth.user = { id: 3 }; auth.can = permission => permission === P.CROP_SUPPORT_CREATE
  const request = vi.spyOn(api, 'request').mockImplementation(async path => ({ items: path.endsWith('/activities') ? [{ id: 22, date: '2026-11-01', description: 'Inspection recorded' }] : [], pagination: { totalPages: 1 } }))
  const props = { onSaved: vi.fn(), onReload: vi.fn() }
  const ui = render(<CropSupportDetail report={{ ...report, cropSeasonId: 12, allowedActions: ['follow-ups'] }} {...props} />)
  await screen.findByRole('option', { name: '#22 · 2026-11-01 · Inspection recorded' })
  fireEvent.change(screen.getByLabelText('Feedback type'), { target: { value: 'ACTION_TAKEN' } })
  fireEvent.change(screen.getByLabelText('Linked care activity (optional)'), { target: { value: '22' } })
  fireEvent.change(screen.getByLabelText('Consultation note'), { target: { value: 'Inspected after advice' } })
  fireEvent.click(screen.getByRole('button', { name: 'Send follow-up' }))
  await vi.waitFor(() => expect(request).toHaveBeenCalledWith('/api/crop-condition-reports/9/follow-ups', { method: 'POST', body: { expectedRevision: 4, type: 'ACTION_TAKEN', content: 'Inspected after advice', cultivationActivityId: 22, images: [] } }))
  ui.unmount(); auth.user = { id: 4 }; auth.can = permission => permission === P.CROP_SUPPORT_PROCESS
  render(<CropSupportDetail report={{ ...report, allowedActions: ['refer'] }} {...props} />)
  fireEvent.change(screen.getByLabelText('Referral destination'), { target: { value: 'UAV_VIDEO' } })
  fireEvent.change(screen.getByLabelText('Consultation note'), { target: { value: 'Need field footage' } })
  fireEvent.click(screen.getByRole('button', { name: 'Record referral request' }))
  await vi.waitFor(() => expect(request).toHaveBeenCalledWith('/api/crop-condition-reports/9/referrals', { method: 'POST', body: { expectedRevision: 4, type: 'UAV_VIDEO', content: 'Need field footage', deviceIncidentId: null, images: [] } }))
  expect(request.mock.calls.some(([path]) => path.includes('/missions'))).toBe(false)
})
