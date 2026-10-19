import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { ManagementContext } from '../src/context/ManagementContext'
import UsersPage from '../src/pages/app/UsersPage'

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

describe('Users page role selection', () => {
  it('allows only one assignable role and submits it with the Save button', async () => {
    const changeManagement = vi.fn().mockResolvedValue(undefined)
    HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
    HTMLDialogElement.prototype.close = function () { this.removeAttribute('open') }

    render(<ManagementContext.Provider value={{
      management: { users: [] },
      changeManagement,
      loading: false,
      error: null,
      reload: vi.fn(),
    }}>
      <UsersPage />
    </ManagementContext.Provider>)

    fireEvent.click(screen.getByRole('button', { name: '+ Add User' }))
    const owner = screen.getByRole('radio', { name: 'Farm Owner' })
    const engineer = screen.getByRole('radio', { name: 'Agricultural Engineer' })
    expect(owner.checked).toBe(true)
    expect(engineer.checked).toBe(false)

    fireEvent.click(engineer)
    expect(owner.checked).toBe(false)
    expect(engineer.checked).toBe(true)

    fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'Test User' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'test@example.com' } })
    fireEvent.change(screen.getByLabelText('Initial password'), { target: { value: 'Test-password-123!' } })
    expect(screen.getByRole('button', { name: 'Save', exact: true })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Save User', exact: true })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Save', exact: true }))

    await waitFor(() => expect(changeManagement).toHaveBeenCalledWith(expect.objectContaining({
      values: expect.objectContaining({ roles: ['AGRICULTURAL_ENGINEER'] }),
    })))
  })
})
