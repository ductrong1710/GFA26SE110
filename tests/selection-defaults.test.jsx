import React from 'react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { AuthContext } from '../src/context/AuthContext'
import { api } from '../src/services/api'
import MaintenancePage from '../src/pages/app/MaintenancePage'
import EquipmentDialog from '../src/components/app/EquipmentDialog'
import JobReports from '../src/components/workflow/JobReports'
import ChannelHistory from '../src/components/app/ChannelHistory'
import { ProposalForm } from '../src/components/workflow/ConsultationForms'

const mocks = vi.hoisted(() => ({ queries: vi.fn(), maintenance: [], reports: [] }))
vi.mock('../src/services/api/useApiQuery', () => ({ useApiQuery: (...args) => mocks.queries(...args) }))
vi.mock('../src/services/api', () => ({ api: { request: vi.fn(), post: vi.fn() } }))
function mount(children) {
  return render(<AuthContext.Provider value={{ activeRole: 'ADMINISTRATOR', user: { id: 1 } }}><MemoryRouter>{children}</MemoryRouter></AuthContext.Provider>)
}
beforeEach(() => {
  mocks.maintenance = []
  api.request.mockReset().mockResolvedValue({})
  mocks.reports = []
  mocks.queries.mockReset().mockImplementation((key, _load, enabled = true) => ({
    data: !enabled ? null : key === 'maintenance-schedules' ? { rows: mocks.maintenance, technicians: [{ id: 2, fullName: 'Technician A' }], farms: [{ id: 5, name: 'Farm A', isActive: true }] }
      : key.startsWith('reports:') ? mocks.reports : [], loading: false, error: null, reload: vi.fn(),
  }))
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
  HTMLDialogElement.prototype.close = function () { this.removeAttribute('open') }
})
afterEach(cleanup)

it('opens farm maintenance with a fixed customer appointment and no preselected technician', () => {
  mount(<MaintenancePage />)
  fireEvent.click(screen.getAllByRole('button', { name: 'Assign technician' })[0])
  const arrival = screen.getByLabelText('Scheduled arrival at farm')
  expect(arrival.readOnly).toBe(true)
  expect(arrival.value).not.toBe('')
  expect(screen.getByLabelText('Departure').value).toBe('')
  expect(screen.getByLabelText('Technician').value).toBe('')
  expect(screen.getByRole('button', { name: 'Confirm schedule' }).disabled).toBe(true)
})

it('does not choose a gateway type when registering new equipment', () => {
  mount(<EquipmentDialog kind="gateway" workspace={{ farms: [], zones: [] }} operations={{ missions: [] }} currentFarm={{ id: '' }} onSave={vi.fn()} onClose={vi.fn()} />)
  const type = screen.getByRole('combobox', { name: 'Device Type' })
  expect(type.value).toBe('')
  expect(type.required).toBe(true)
  fireEvent.change(type, { target: { value: 'RASPBERRY_PI' } })
  expect(type.value).toBe('RASPBERRY_PI')
})

it('filters sample maintenance without requesting backend changes', () => {
  mount(<MaintenancePage />)
  fireEvent.change(screen.getByLabelText('Search farms'), { target: { value: 'unknown farm' } })
  expect(screen.getByText('No maintenance visits found')).toBeTruthy()
  fireEvent.click(screen.getByRole('button', { name: 'Clear filters' }))
  expect(screen.getByText('Da Lat Greenhouse')).toBeTruthy()
  expect(api.request).not.toHaveBeenCalled()
})

it('waits for a report revision instead of loading the first report automatically', () => {
  mocks.reports = [{ id: 9, revision: 2, createdAt: '2026-11-04T00:00:00Z' }]
  mount(<JobReports kind="installation" job={{ id: 1, revision: 2 }} admin />)
  expect(screen.getByLabelText('Report revision').value).toBe('')
  expect(mocks.queries.mock.calls.find(([key]) => key.startsWith('report:'))[2]).toBe(false)
  fireEvent.change(screen.getByLabelText('Report revision'), { target: { value: '9' } })
  expect(mocks.queries.mock.calls.some(([key, , enabled]) => key === 'report:installation:9:2' && enabled)).toBe(true)
})

it('waits for an explicit channel choice before loading history', () => {
  mount(<ChannelHistory channels={[{ id: 3, channelCode: 'AIR-TEMP' }]} />)
  const select = screen.getByLabelText('Channel')
  expect(select.value).toBe('')
  expect(mocks.queries.mock.calls[0][2]).toBe(false)
  fireEvent.change(select, { target: { value: '3' } })
  expect(mocks.queries.mock.calls.at(-1)[2]).toBe(true)
})

it('does not preselect a zone for any new proposal line', () => {
  mount(<ProposalForm id={1} revision={0} survey={{ id: 4, zones: [{ id: 2, name: 'North' }] }} />)
  expect(screen.getByLabelText('Zone').value).toBe('')
  fireEvent.click(screen.getByRole('button', { name: 'Add product' }))
  expect(screen.getAllByLabelText('Zone').map(select => select.value)).toEqual(['', ''])
})
