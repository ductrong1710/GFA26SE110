import React from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Outlet, Route, Routes } from 'react-router-dom'
import { AuthContext } from '../src/context/AuthContext'
import { OperationsContext } from '../src/context/OperationsContext'
import DevicesPage from '../src/pages/app/DevicesPage'
import { api } from '../src/services/api/index.js'
import { session } from '../src/services/api/session.js'
import { hasBackendPermission } from '../src/config/backendPermissions.js'
import { ROLES } from '../src/config/roles.js'

const gateway = { id: 11, code: 'GW-ESP32-01', name: 'Field gateway', gatewayType: 'ESP32', isActive: true }
const empty = { gatewayId: 11, gatewayCode: gateway.code, hasCredential: false, isActive: false, createdAt: null, lastUsedAt: null, revokedAt: null, keyPrefix: null }
const active = { ...empty, hasCredential: true, isActive: true, createdAt: '2026-10-13T01:00:00Z', keyPrefix: 'test-prefix', lastUsedAt: '2026-10-13T02:00:00Z' }
let response, status, requests, clipboard
const auth = (role = ROLES.ADMINISTRATOR, extra = {}) => ({ user: { id: 1, isMock: false, ...extra }, activeRole: role, can: permission => hasBackendPermission(role, permission) })
function Page({ account = auth(), device = gateway }) {
  return <AuthContext.Provider value={account}><OperationsContext.Provider value={{ operations: { gateways: [device], uavs: [{ id: 2, code: 'UAV-01', isActive: true }], syncBatches: [], missions: [] }, changeOperations: vi.fn() }}>
    <MemoryRouter><Routes><Route element={<Outlet context={{ farmWorkspace: { farms: [] }, currentFarm: {} }} />}><Route path="/" element={<DevicesPage />} /></Route></Routes></MemoryRouter>
  </OperationsContext.Provider></AuthContext.Provider>
}
const click = name => fireEvent.click(screen.getByRole('button', { name, exact: true }))
async function open() {
  fireEvent.click(screen.getByRole('tab', { name: 'Mobile Gateways' }))
  click('Credential')
  await screen.findByRole('dialog')
}
beforeEach(() => {
  status = { ...empty }
  // Test-only transient server response; no fixture or application storage contains a key.
  response = { gatewayId: 11, gatewayCode: gateway.code, createdAt: active.createdAt, apiKey: crypto.randomUUID() }
  requests = []
  clipboard = vi.fn().mockResolvedValue(undefined)
  Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText: clipboard } })
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', '') }
  HTMLDialogElement.prototype.close = function () { this.removeAttribute('open') }
  vi.spyOn(api, 'request').mockImplementation(async (path, options = {}) => {
    requests.push({ path, ...options })
    if (path.endsWith('/status')) return { ...status }
    if (options.method === 'DELETE') { status = { ...active, isActive: false, revokedAt: '2026-10-13T03:00:00Z' }; return null }
    status = { ...active }
    return response
  })
})
afterEach(() => { cleanup(); vi.restoreAllMocks() })

describe('Gateway credentials on the Devices page', () => {
  it.each([ROLES.ADMINISTRATOR, ROLES.FARM_OWNER])('shows Credential only for authorized gateway rows (%s)', async role => {
    render(<Page account={auth(role)} />)
    expect(screen.queryByRole('button', { name: 'Credential' })).toBeNull()
    await open()
    expect(await screen.findByRole('button', { name: 'Generate Credential' })).toBeTruthy()
  })
  it('creates a credential and displays the raw key with a one-time warning', async () => {
    render(<Page />); await open()
    click('Generate Credential')
    expect((await screen.findByLabelText('API Key')).value).toBe(response.apiKey)
    expect(screen.getByText('Copy this API key now. It will not be shown again.')).toBeTruthy()
    expect(requests.some(r => r.path === '/api/gateways/11/credential' && r.method === 'POST')).toBe(true)
    expect(screen.getByText(/POST \/api\/device\/gateways\/11\/sync/).textContent).toContain('X-Api-Key: <API KEY>')
  })
  it('copies code, key, and ESP32 configuration only on explicit clicks without storage writes', async () => {
    const writes = vi.spyOn(Storage.prototype, 'setItem')
    render(<Page />); await open(); click('Generate Credential'); await screen.findByLabelText('API Key')
    click('Copy Gateway Code'); await waitFor(() => expect(clipboard).toHaveBeenLastCalledWith(gateway.code))
    click('Copy API Key'); await waitFor(() => expect(clipboard).toHaveBeenLastCalledWith(response.apiKey))
    click('Copy ESP32 Configuration'); await waitFor(() => expect(clipboard).toHaveBeenLastCalledWith(`GATEWAY_CODE=${gateway.code}\nAPI_KEY=${response.apiKey}`))
    expect(writes).not.toHaveBeenCalled()
  })
  it('discards the raw key when closed and reopening retrieves metadata only', async () => {
    render(<Page />); await open(); click('Generate Credential'); await screen.findByLabelText('API Key')
    click('Close dialog'); expect(screen.queryByLabelText('API Key')).toBeNull()
    click('Credential'); await screen.findByText('test-prefix')
    expect(screen.queryByLabelText('API Key')).toBeNull()
    expect(document.body.textContent).not.toContain(response.apiKey)
  })
  it('shows existing credential metadata without fetching or revealing a raw key', async () => {
    status = { ...active }; render(<Page />); await open()
    await screen.findByText('test-prefix')
    expect(screen.getByText('Active')).toBeTruthy()
    expect(screen.getByText('Created')).toBeTruthy()
    expect(screen.getByText('Last used')).toBeTruthy()
    expect(screen.queryByLabelText('API Key')).toBeNull()
    expect(requests).toHaveLength(1)
  })
  it('rotates after confirmation and shows the new key once', async () => {
    status = { ...active }; render(<Page />); await open(); await screen.findByText('test-prefix')
    click('Rotate Credential'); expect(requests).toHaveLength(1)
    click('Confirm Rotate'); await screen.findByLabelText('API Key')
    expect(requests.some(r => r.path.endsWith('/credential/rotate') && r.method === 'POST')).toBe(true)
    expect(screen.getByLabelText('API Key').value).toBe(response.apiKey)
  })
  it('revokes after confirmation, retains metadata and permits generating again', async () => {
    status = { ...active }; render(<Page />); await open(); await screen.findByText('test-prefix')
    click('Revoke Credential'); click('Confirm Revoke')
    await screen.findByText('Revoked')
    expect(requests.some(r => r.method === 'DELETE')).toBe(true)
    expect(screen.getByRole('button', { name: 'Generate Credential' })).toBeTruthy()
    expect(screen.queryByLabelText('API Key')).toBeNull()
  })
  it('hides all management for read-only users and makes no credential requests', () => {
    render(<Page account={auth(ROLES.AGRICULTURAL_ENGINEER)} />)
    fireEvent.click(screen.getByRole('tab', { name: 'Mobile Gateways' }))
    for (const name of ['Credential', 'Generate Credential', 'Rotate Credential', 'Revoke Credential']) expect(screen.queryByRole('button', { name })).toBeNull()
    expect(requests).toHaveLength(0)
  })
  it('unmounts and discards secrets when account or permission changes', async () => {
    const view = render(<Page />); await open(); click('Generate Credential'); await screen.findByLabelText('API Key')
    view.rerender(<Page account={auth(ROLES.ADMINISTRATOR, { id: 9 })} />)
    expect(screen.queryByRole('dialog')).toBeNull()
    expect(screen.queryByLabelText('API Key')).toBeNull()
    click('Credential'); await screen.findByText('test-prefix')
    view.rerender(<Page account={auth(ROLES.AGRICULTURAL_ENGINEER, { id: 9 })} />)
    expect(screen.queryByRole('dialog')).toBeNull()
  })
  it('does not reveal a response arriving after close, and cancels its request', async () => {
    render(<Page />); await open(); await screen.findByRole('button', { name: 'Generate Credential' })
    let resolve, signal
    api.request.mockImplementationOnce((path, options) => { signal = options.signal; return new Promise(done => { resolve = done }) })
    click('Generate Credential'); click('Close dialog')
    expect(signal.aborted).toBe(true)
    await act(async () => resolve(response))
    expect(screen.queryByLabelText('API Key')).toBeNull()
    click('Credential'); await screen.findByRole('button', { name: 'Generate Credential' })
    expect(screen.queryByLabelText('API Key')).toBeNull()
  })
  it('blocks credential creation for inactive gateways', async () => {
    render(<Page device={{ ...gateway, isActive: false }} />); await open()
    expect((await screen.findByRole('button', { name: 'Generate Credential' })).disabled).toBe(true)
    expect(screen.getByText(/Activate this gateway/)).toBeTruthy()
  })
  it('never fabricates demo credentials or calls real APIs in demo mode', async () => {
    render(<Page account={auth(ROLES.ADMINISTRATOR, { isMock: true })} />); await open()
    expect(screen.getByText(/Credential management requires a real account/)).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Generate Credential' })).toBeNull()
    expect(requests).toHaveLength(0)
  })
  it('shows a recoverable status error without enabling generation against unknown state', async () => {
    api.request.mockRejectedValueOnce(new Error('offline'))
    render(<Page />); await open(); await screen.findByRole('alert')
    expect(screen.queryByRole('button', { name: 'Generate Credential' })).toBeNull()
    click('Retry'); await screen.findByRole('button', { name: 'Generate Credential' })
  })
  it('handles clipboard failure without hiding the one-time key', async () => {
    clipboard.mockRejectedValueOnce(new Error('denied'))
    render(<Page />); await open(); click('Generate Credential'); await screen.findByLabelText('API Key'); click('Copy API Key')
    await screen.findByText(/Unable to copy/)
    expect(screen.getByLabelText('API Key').value).toBe(response.apiKey)
  })
  it('discards secrets when a real session is replaced even for the same user', async () => {
    const view = render(<Page />); await open(); click('Generate Credential'); await screen.findByLabelText('API Key')
    act(() => session.set(null))
    view.rerender(<Page account={auth()} />)
    expect(screen.queryByRole('dialog')).toBeNull()
  })
})
