import React, { useState } from 'react'
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import { AuthContext } from '../src/context/AuthContext'
import AppSidebar from '../src/components/app/AppSidebar'
import AppTopbar from '../src/components/app/AppTopbar'
import CropSeasonsPage from '../src/pages/app/CropSeasonsPage'
import WorkOrdersPage from '../src/pages/app/WorkOrdersPage'
import PublicContentPage from '../src/pages/app/PublicContentPage'
import { Badge } from '../src/components/workflow/WorkflowUi'
import { PageHeader } from '../src/components/app/ui'
import { PageHeaderContext } from '../src/context/PageHeaderContext'

vi.mock('../src/services/api/useNotifications', () => ({ useNotifications: () => ({ hasUnread: false }) }))
vi.mock('../src/services/api/useApiQuery', () => ({ useApiQuery: (key, _load, enabled = true) => ({
  data: !enabled ? null : key.startsWith('work-orders:') ? { jobs: [], requests: [], dossiers: [] } : [], loading: false, error: null, reload: vi.fn(),
}) }))
const auth = { activeRole: 'ADMINISTRATOR', activeRoleLabel: 'Administrator', canAny: () => true, user: { id: 1, fullName: 'Sample Administrator', email: 'admin@sep.local' }, logout: vi.fn() }
function mount(children, role = 'ADMINISTRATOR') {
  return render(<AuthContext.Provider value={{ ...auth, activeRole: role }}><MemoryRouter><Routes>
    <Route element={<Outlet context={{ farmWorkspace: { farms: [] } }} />}><Route path="/" element={children} /></Route>
  </Routes></MemoryRouter></AuthContext.Provider>)
}
afterEach(cleanup)

it('keeps admin season viewing and Profile only in the account dropdown', () => {
  mount(<><AppSidebar /><AppTopbar farms={[{ id: 1, name: 'Farm' }]} currentFarm={{ id: 1 }} /></>)
  const sidebar = screen.getByRole('navigation', { name: 'Main navigation' })
  expect(within(sidebar).getByRole('link', { name: 'Crop seasons & activity log' })).toBeTruthy()
  expect(within(sidebar).queryByRole('link', { name: 'Profile' })).toBeNull()
  expect(sidebar.textContent).not.toMatch(/[\u00c0-\u024f\u1e00-\u1eff]/)
  fireEvent.click(screen.getByRole('button', { name: 'Account: Sample Administrator, Administrator' }))
  const dropdown = screen.getByRole('region', { name: 'Your account' })
  expect(within(dropdown).getByRole('link', { name: 'Profile' })).toBeTruthy()
  expect(within(dropdown).queryByText('admin@sep.local')).toBeNull()
  expect(within(dropdown).queryByText('Administrator', { exact: true })).toBeNull()
})

it.each([[CropSeasonsPage, 'Crop seasons & cultivation log'], [WorkOrdersPage, 'Installation & service'], [PublicContentPage, 'Website content & pricing']])('shows English admin copy on %s', (Page, heading) => {
  const { container } = mount(<Page />)
  expect(screen.getByRole('heading', { name: heading })).toBeTruthy()
  expect(container.textContent).not.toMatch(/[\u00c0-\u024f\u1e00-\u1eff]/)
})

it('keeps admin seasons read-only and preserves owner management in Vietnamese', () => {
  mount(<CropSeasonsPage />)
  expect(screen.queryByRole('button', { name: /Add crop season/ })).toBeNull()
  cleanup()
  mount(<CropSeasonsPage />, 'FARM_OWNER')
  expect(screen.getByRole('button', { name: 'Thêm mùa vụ' })).toBeTruthy()
})

it('translates status labels for admin', () => {
  mount(<Badge value="WAITING_ADMIN" />)
  expect(screen.getByText('Awaiting dossier review')).toBeTruthy()
})

it('shows the page title without an irrelevant farm selector on Website content', () => {
  render(<AuthContext.Provider value={auth}><MemoryRouter initialEntries={['/app/public-content']}>
    <AppTopbar farms={[{ id: 1, name: 'Farm A' }]} currentFarm={{ id: 1 }} />
  </MemoryRouter></AuthContext.Provider>)
  expect(screen.getByText('Website content')).toBeTruthy()
  expect(screen.queryByLabelText('Current farm')).toBeNull()
})

it('keeps the working farm selector under the dashboard title', () => {
  const onChange = vi.fn()
  render(<AuthContext.Provider value={auth}><MemoryRouter initialEntries={['/app/dashboard']}>
    <AppTopbar farms={[{ id: 1, name: 'Farm A' }, { id: 2, name: 'Farm B' }]} currentFarm={{ id: 1 }} onFarmChange={onChange} />
  </MemoryRouter></AuthContext.Provider>)
  expect(screen.getByText('Farm operations')).toBeTruthy()
  fireEvent.change(screen.getByLabelText('Current farm'), { target: { value: '2' } })
  expect(onChange).toHaveBeenCalledWith(2)
})

it('moves the page heading to the topbar without removing page actions', () => {
  const action = vi.fn()
  function Page() {
    const [header, setHeader] = useState(null)
    return <PageHeaderContext.Provider value={setHeader}>
      <AppTopbar farms={[]} currentFarm={{ id: '' }} pageTitle={header?.title} />
      <main><PageHeader title="Create a mission" description="Old header description" actions={<button onClick={action}>Create</button>} /></main>
    </PageHeaderContext.Provider>
  }
  mount(<Page />)
  expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1)
  expect(screen.getByRole('heading', { name: 'Create a mission' })).toBeTruthy()
  expect(screen.queryByText('Old header description')).toBeNull()
  expect(document.querySelector('main h1')).toBeNull()
  fireEvent.click(screen.getByRole('button', { name: 'Create' }))
  expect(action).toHaveBeenCalled()
})
