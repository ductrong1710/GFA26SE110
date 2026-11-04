import React from 'react'
import { afterEach, beforeEach, expect, it } from 'vitest'
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import MaintenancePage from '../src/pages/app/MaintenancePage'
import { dateAt, reservationEnd, conflictFor, technicians } from '../src/components/workflow/maintenanceDemo'

beforeEach(() => {
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
})
afterEach(cleanup)
function mount() { render(<MemoryRouter><MaintenancePage /></MemoryRouter>) }
function configure(days) {
  fireEvent.click(screen.getByRole('button', { name: 'Scheduling settings' }))
  fireEvent.change(screen.getByLabelText('Maintenance reservation (days)'), { target: { value: String(days) } })
  fireEvent.click(screen.getByRole('button', { name: 'Save settings' }))
}
it('applies new defaults to new visits and preserves saved assignments', () => {
  mount()
  configure(4)
  fireEvent.click(screen.getAllByRole('button', { name: 'Assign technician' })[0])
  expect(screen.getByLabelText('Reservation duration (days)').value).toBe('4')
  expect(screen.getByLabelText('Scheduled arrival at farm').readOnly).toBe(true)
  fireEvent.click(screen.getByRole('button', { name: 'Cancel' }))
  fireEvent.click(screen.getByRole('button', { name: 'View / reschedule' }))
  expect(screen.getByLabelText('Reservation duration (days)').value).toBe('7')
})
it('saves a trip-specific duration without requiring a reason', () => {
  mount()
  fireEvent.click(screen.getAllByRole('button', { name: 'Assign technician' })[0])
  fireEvent.change(screen.getByLabelText('Departure'), { target: { value: dateAt(1) } })
  fireEvent.click(screen.getByLabelText('Adjust reservation for this trip'))
  fireEvent.change(screen.getByLabelText('Reservation duration (days)'), { target: { value: '3' } })
  fireEvent.change(screen.getByLabelText('Technician'), { target: { value: technicians[2] } })
  expect(screen.queryByLabelText('Reason for adjustment')).toBeNull()
  expect(screen.getByRole('button', { name: 'Confirm schedule' }).disabled).toBe(false)
  fireEvent.click(screen.getByRole('button', { name: 'Confirm schedule' }))
  fireEvent.click(screen.getAllByRole('button', { name: 'View / reschedule' })[0])
  expect(screen.getByLabelText('Reservation duration (days)').value).toBe('3')
  expect(screen.queryByLabelText('Reason for adjustment')).toBeNull()
})
it('rejects invalid defaults and checks the whole configured interval', () => {
  mount()
  fireEvent.click(screen.getByRole('button', { name: 'Scheduling settings' }))
  fireEvent.change(screen.getByLabelText('Maintenance reservation (days)'), { target: { value: '0' } })
  expect(screen.getByRole('button', { name: 'Save settings' }).disabled).toBe(true)
  expect(reservationEnd('2026-11-01T08:00', 3)).toBe('2026-11-04T08:00')
  expect(reservationEnd('2026-11-01T08:00', 0)).toBe('')
  const visits = [{ id: 1, farm: 'Test farm', assignment: { technician: technicians[2], departure: dateAt(4), reservedUntil: dateAt(6) } }]
  expect(conflictFor(technicians[2], dateAt(0), reservationEnd(dateAt(0), 3), visits)).toBeUndefined()
  expect(conflictFor(technicians[2], dateAt(0), reservationEnd(dateAt(0), 7), visits)?.farm).toBe('Test farm')
})
