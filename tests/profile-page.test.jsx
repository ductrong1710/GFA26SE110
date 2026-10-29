import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { AuthContext } from '../src/context/AuthContext'
import { getNavigationForRole } from '../src/config/navigation'
import { HUMAN_ROLES } from '../src/config/roles'
import ProfilePage from '../src/pages/app/ProfilePage'

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
  logout: vi.fn(),
  activeRole: 'FARM_OWNER',
}))

vi.mock('../src/services/api', () => ({ api: { request: mocks.request } }))
vi.mock('../src/context/AuthContext', async importOriginal => ({
  ...await importOriginal(),
  useAuth: () => ({
    user: {
      id: 8,
      fullName: 'Nguyen Thi An',
      email: 'an@example.com',
      roles: ['FARM_OWNER', 'AGRICULTURAL_ENGINEER'],
    },
    activeRoleLabel: mocks.activeRole === 'FARM_OWNER' ? 'Farm Owner' : 'Agricultural Engineer',
    activeRole: mocks.activeRole,
    logout: mocks.logout,
  }),
}))

afterEach(() => {
  cleanup()
  vi.clearAllMocks()
  mocks.activeRole = 'FARM_OWNER'
})

it('makes the profile navigation available to every human role', () => {
  for (const role of HUMAN_ROLES) {
    expect(getNavigationForRole(role).some(item => item.path === '/app/profile')).toBe(true)
  }
})

it('shows the signed-in user and assigned roles', () => {
  render(<AuthContext.Provider value={{ activeRole: mocks.activeRole }}><MemoryRouter><ProfilePage /></MemoryRouter></AuthContext.Provider>)

  expect(screen.getByRole('heading', { name: 'Hồ sơ cá nhân' })).toBeTruthy()
  expect(screen.getAllByText('Nguyen Thi An')).toHaveLength(2)
  expect(screen.getAllByText('an@example.com')).toHaveLength(2)
  expect(screen.getByText('Kỹ sư nông nghiệp')).toBeTruthy()
  expect(screen.queryByLabelText('Mật khẩu hiện tại')).toBeNull()
})

it('keeps the profile in English for non-owner roles and opens the password form on request', () => {
  mocks.activeRole = 'AGRICULTURAL_ENGINEER'
  render(<AuthContext.Provider value={{ activeRole: mocks.activeRole }}><MemoryRouter><ProfilePage /></MemoryRouter></AuthContext.Provider>)

  expect(screen.getByRole('heading', { name: 'Profile' })).toBeTruthy()
  expect(screen.queryByLabelText('Current password')).toBeNull()
  fireEvent.click(screen.getByRole('button', { name: 'Change password' }))
  expect(screen.getByLabelText('Current password')).toBeTruthy()
})

it('rejects mismatched passwords without sending the request', async () => {
  render(<AuthContext.Provider value={{ activeRole: mocks.activeRole }}><MemoryRouter><ProfilePage /></MemoryRouter></AuthContext.Provider>)

  fireEvent.click(screen.getByRole('button', { name: 'Đổi mật khẩu' }))
  fireEvent.change(screen.getByLabelText('Mật khẩu hiện tại'), { target: { value: 'current' } })
  fireEvent.change(screen.getByLabelText('Mật khẩu mới'), { target: { value: 'new-password-123' } })
  fireEvent.change(screen.getByLabelText('Xác nhận mật khẩu mới'), { target: { value: 'different-password' } })
  fireEvent.submit(screen.getByRole('button', { name: 'Cập nhật mật khẩu' }).closest('form'))

  expect((await screen.findByRole('alert')).textContent).toContain('không khớp')
  expect(mocks.request).not.toHaveBeenCalled()
})

it('changes the password, signs out and directs the user back to login', async () => {
  mocks.request.mockResolvedValue(undefined)
  mocks.logout.mockResolvedValue(undefined)

  function LoginNotice() {
    const location = useLocation()
    return <p role="status">{location.state?.message}</p>
  }

  render(<AuthContext.Provider value={{ activeRole: mocks.activeRole }}><MemoryRouter initialEntries={['/app/profile']}>
    <Routes>
      <Route path="/app/profile" element={<ProfilePage />} />
      <Route path="/dang-nhap" element={<LoginNotice />} />
    </Routes>
  </MemoryRouter></AuthContext.Provider>)

  fireEvent.click(screen.getByRole('button', { name: 'Đổi mật khẩu' }))
  fireEvent.change(screen.getByLabelText('Mật khẩu hiện tại'), { target: { value: 'current-password' } })
  fireEvent.change(screen.getByLabelText('Mật khẩu mới'), { target: { value: 'new-password-123' } })
  fireEvent.change(screen.getByLabelText('Xác nhận mật khẩu mới'), { target: { value: 'new-password-123' } })
  fireEvent.submit(screen.getByRole('button', { name: 'Cập nhật mật khẩu' }).closest('form'))

  await waitFor(() => expect(mocks.request).toHaveBeenCalledWith('/api/auth/change-password', {
    method: 'POST',
    body: { currentPassword: 'current-password', newPassword: 'new-password-123' },
    toast: false,
  }))
  expect(mocks.logout).toHaveBeenCalledOnce()
  expect((await screen.findByRole('status')).textContent).toContain('Đổi mật khẩu thành công')
})
