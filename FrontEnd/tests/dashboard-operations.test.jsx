import { afterEach, expect, test } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import DashboardOperations from '../src/components/app/DashboardOperations'
import AdminHistory from '../src/components/app/AdminHistory'

afterEach(cleanup)

test('admin history distinguishes days and outcomes and exposes recorded counts', () => {
  render(<AdminHistory history={[
    { date: '2026-11-01', category: 'MISSION_COMPLETED', count: 2 },
    { date: '2026-11-02', category: 'MISSION_FAILED', count: 1 },
  ]} />)
  expect(screen.getByRole('meter', { name: '2026-11-01 · Completed' }).getAttribute('value')).toBe('2')
  expect(screen.getByRole('meter', { name: '2026-11-02 · Failed' }).getAttribute('value')).toBe('1')
  expect(screen.getByText('No alerts recorded in this period.')).toBeTruthy()
})

test('operational dashboard renders persisted counts and cultivation without inventing outcomes', () => {
  render(<MemoryRouter><DashboardOperations data={{ openInstallations: 2, openMaintenance: 0, openRepairs: 3,
    activeRentals: 1, expiringRentals: 1, expiredRentals: 0, warningDays: 7, activeSeasons: 1,
    recentCultivation: [{ id: 4, seasonName: 'Spring rice', date: '2026-11-01', type: 'WATERING', description: 'Watered north field' }] }} /></MemoryRouter>)
  expect(screen.getByText('2 installation jobs')).toBeTruthy()
  expect(screen.getByText('3 repair jobs')).toBeTruthy()
  expect(screen.getByText('1 expiring within 7 days')).toBeTruthy()
  expect(screen.getByText('Watered north field')).toBeTruthy()
  expect(screen.getByRole('link', { name: 'View seasons and compare' }).getAttribute('href')).toBe('/app/crop-seasons')
})

test('missing summary is unavailable rather than zero', () => {
  render(<MemoryRouter><DashboardOperations /></MemoryRouter>)
  expect(screen.getByText('Operational summary unavailable.')).toBeTruthy()
  expect(screen.queryByText('0 active contracts')).toBeNull()
})
