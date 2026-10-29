import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { useManagement } from '../src/context/ManagementContext'
import ApiManagementProvider from '../src/context/ApiManagementProvider'

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
  showToast: vi.fn(),
}))

vi.mock('../src/services/api', () => ({
  api: {
    request: mocks.request,
    post: vi.fn(),
    put: vi.fn(),
    patch: vi.fn(),
  },
}))
vi.mock('../src/services/api/useApiQuery', () => ({
  useApiQuery: () => ({ data: [], reload: vi.fn(), loading: false, error: null }),
}))
vi.mock('../src/context/AuthContext', async importOriginal => ({
  ...await importOriginal(),
  useAuth: () => ({ user: { id: 1 }, activeRole: 'ADMINISTRATOR', can: () => true }),
}))
vi.mock('../src/utils/toast', () => ({
  showToast: mocks.showToast,
}))

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
})

function renderProvider() {
  function Trigger() {
    const { changeManagement } = useManagement()
    return <button onClick={() => changeManagement({
      kind: 'user',
      id: 5,
      mode: 'edit',
      values: {
        email: 'user@example.com',
        fullName: 'Test User',
        roles: ['AGRICULTURAL_ENGINEER'],
        rolesChanged: true,
      },
    })}>Update user</button>
  }

  render(
    <MemoryRouter initialEntries={['/app/users']}>
      <ApiManagementProvider><Trigger /></ApiManagementProvider>
    </MemoryRouter>,
  )
}

it('updates profile and changed role together without duplicate API toasts', async () => {
  mocks.request.mockResolvedValue(undefined)
  renderProvider()

  fireEvent.click(screen.getByRole('button', { name: 'Update user' }))

  await waitFor(() => expect(mocks.request).toHaveBeenCalledTimes(1))
  expect(mocks.request).toHaveBeenCalledWith('/api/users/5', {
    method: 'PUT',
    body: { email: 'user@example.com', fullName: 'Test User', roles: ['FarmEngineer'] },
    toast: false,
  })
  await waitFor(() => expect(mocks.showToast).toHaveBeenCalledTimes(1))
  expect(mocks.showToast).toHaveBeenCalledWith('success', 'Cập nhật người dùng thành công.')
})
