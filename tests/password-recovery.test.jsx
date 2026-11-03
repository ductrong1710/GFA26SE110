import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import PasswordPage from '../src/pages/PasswordPage'
import { api } from '../src/services/api'
import { AuthContext } from '../src/context/AuthContext'

afterEach(() => { cleanup(); vi.restoreAllMocks() })
function open(path, logout = vi.fn()) {
  render(<AuthContext.Provider value={{ logout }}><MemoryRouter initialEntries={[path]}><Routes>
    <Route path="/quen-mat-khau" element={<PasswordPage key="forgot" mode="forgot" />} />
    <Route path="/dat-lai-mat-khau" element={<PasswordPage key="reset" mode="reset" />} />
    <Route path="/doi-mat-khau" element={<PasswordPage mode="change" />} />
    <Route path="/dang-nhap" element={<h1>Login</h1>} />
  </Routes></MemoryRouter></AuthContext.Provider>)
}
const fill = (name, value) => fireEvent.change(document.querySelector(`input[name="${name}"]`), { target: { value } })
test('public recovery submits email, accepts an emailed token and preserves password whitespace', async () => {
  const request = vi.spyOn(api, 'request').mockResolvedValue({})
  open('/quen-mat-khau')
  fill('email', 'owner@example.com')
  fireEvent.click(screen.getByRole('button', { name: 'Gửi hướng dẫn' }))
  await screen.findByLabelText('Mã khôi phục')
  expect(request).toHaveBeenCalledWith('/api/auth/forgot-password', { method: 'POST', auth: false, toast: false, body: { email: 'owner@example.com' } })
  const token = 'ABCDEF12'.repeat(8)
  const password = ' new password 123 '
  fill('token', token); fill('newPassword', password); fill('confirm', password)
  fireEvent.click(screen.getByRole('button', { name: 'Đặt lại mật khẩu' }))
  await screen.findByRole('heading', { name: 'Login' })
  expect(request).toHaveBeenCalledWith('/api/auth/reset-password', { method: 'POST', auth: false, toast: false, body: { token, newPassword: password } })
})
test('change requires matching confirmation and signs out only after success', async () => {
  const request = vi.spyOn(api, 'request').mockRejectedValueOnce(new Error('Current password is invalid')).mockResolvedValue({})
  const logout = vi.fn()
  open('/doi-mat-khau', logout)
  fill('currentPassword', 'wrong'); fill('newPassword', 'new password 123'); fill('confirm', 'different password')
  const submit = screen.getByRole('button', { name: 'Đổi mật khẩu' })
  fireEvent.click(submit)
  expect(request).not.toHaveBeenCalled()
  fill('confirm', 'new password 123')
  fireEvent.click(submit)
  await screen.findByText('Current password is invalid')
  expect(logout).not.toHaveBeenCalled()
  fill('currentPassword', 'correct')
  fireEvent.click(submit)
  await screen.findByRole('heading', { name: 'Login' })
  expect(logout).toHaveBeenCalledTimes(1)
  expect(request).toHaveBeenLastCalledWith('/api/auth/change-password', { method: 'POST', auth: true, toast: false, body: { currentPassword: 'correct', newPassword: 'new password 123' } })
})
