import { useCallback, useEffect, useRef, useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS } from '../../config/permissions'
import { displayTime } from '../../utils/display'
import { session } from '../../services/api/session'
import { createGatewayCredential, getGatewayCredentialStatus, revokeGatewayCredential, rotateGatewayCredential } from '../../services/api/gatewayCredentials'
import ManagementDialog from './ManagementDialog'

export default function GatewayCredentialDialog({ gateway, onClose }) {
  const { can, user, activeRole } = useAuth()
  if (!user || !can(PERMISSIONS.DEVICES_MANAGE)) return null
  return <CredentialContent key={`${user.id}:${activeRole}:${session.identity()}:${gateway.id}`} gateway={gateway} onClose={onClose} />
}

function CredentialContent({ gateway, onClose }) {
  const [status, setStatus] = useState(null)
  const [secret, setSecret] = useState(null)
  const [pending, setPending] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [confirmation, setConfirmation] = useState(null)
  const lifetime = useRef(null)
  const busy = useRef(false)

  const load = useCallback((signal) =>
    getGatewayCredentialStatus(gateway.id, signal).then(metadata => {
      if (!signal.aborted) setStatus(metadata)
    }).catch(() => {
      if (!signal.aborted) setError('Unable to load credential status. Please try again.')
    }).finally(() => { if (!signal.aborted) setPending(false) }), [gateway.id])

  useEffect(() => {
    const controller = new AbortController()
    lifetime.current = controller
    load(controller.signal)
    // Closing unmounts all secret state; ignore even a response that raced abort.
    return () => controller.abort()
  }, [load])

  const close = () => {
    lifetime.current?.abort()
    setSecret(null)
    onClose()
  }
  async function mutate(action) {
    const signal = lifetime.current?.signal
    if (!signal || signal.aborted || busy.current || !status) return
    busy.current = true
    setPending(true); setError(''); setNotice(''); setConfirmation(null)
    try {
      if (action === 'revoke') {
        await revokeGatewayCredential(gateway.id, signal)
        if (signal.aborted) return
        setSecret(null)
        // Unknown metadata must be reloaded before another mutation is offered.
        setStatus(null)
        setNotice('Credential revoked. The old API key can no longer authenticate.')
        await load(signal)
      } else {
        const created = await (action === 'rotate' ? rotateGatewayCredential : createGatewayCredential)(gateway.id, signal)
        if (signal.aborted) return
        setSecret({ apiKey: created.apiKey, gatewayCode: created.gatewayCode })
      }
    } catch (failure) {
      if (!signal.aborted) {
        // Do not render arbitrary response text in a credential surface.
        setError(failure.status === 403 ? 'You no longer have permission to manage this credential.'
          : failure.status === 409 || failure.status === 422 ? 'The gateway or credential has changed. Close and reopen this dialog to refresh its status.'
            : 'The credential request could not be confirmed. Close and reopen to check its status before trying again. If a key was created but not received, rotate it to obtain a new key.')
        setStatus(null)
      }
    } finally { busy.current = false; if (!signal.aborted) setPending(false) }
  }
  async function copy(value, label) {
    const signal = lifetime.current?.signal
    if (!signal || signal.aborted) return
    try {
      await navigator.clipboard.writeText(value)
      if (!signal.aborted) { setNotice(`${label} copied.`); setError('') }
    } catch {
      if (!signal.aborted) setError('Unable to copy. Select and copy the value manually before closing this dialog.')
    }
  }
  const active = status?.isActive
  const hasUnrevokedCredential = status?.hasCredential && !status.revokedAt
  return <ManagementDialog title="Gateway Credential" onClose={close}>
    <div className="management-form">
      <p>Gateway: <strong>{gateway.code}</strong></p>
      <>
        {pending && <p role="status">{status ? 'Updating credential…' : 'Loading credential status…'}</p>}
        {secret ? <>
          <p className="management-error">Copy this API key now. It will not be shown again.</p>
          <label>Gateway Code<input readOnly value={secret.gatewayCode} autoComplete="off" spellCheck={false} /></label>
          <label>API Key<textarea readOnly value={secret.apiKey} autoComplete="off" spellCheck={false} rows={3} /></label>
          <div className="management-row-actions">
            <button className="app-ui-button" onClick={() => copy(secret.gatewayCode, 'Gateway code')}>Copy Gateway Code</button>
            <button className="app-ui-button" onClick={() => copy(secret.apiKey, 'API key')}>Copy API Key</button>
            <button className="app-ui-button" onClick={() => copy(`GATEWAY_CODE=${secret.gatewayCode}\nAPI_KEY=${secret.apiKey}`, 'ESP32 configuration')}>Copy ESP32 Configuration</button>
          </div>
        </> : status && <>
          {!status.hasCredential ? <p>No API credential has been created for this gateway.</p> : <dl className="management-detail-grid">
            <div><dt>Status</dt><dd>{active ? 'Active' : status.revokedAt ? 'Revoked' : 'Inactive'}</dd></div>
            <div><dt>Created</dt><dd>{displayTime(status.createdAt)}</dd></div>
            <div><dt>Last used</dt><dd>{status.lastUsedAt ? displayTime(status.lastUsedAt) : 'Never used'}</dd></div>
            <div><dt>Key prefix</dt><dd>{status.keyPrefix || 'Not available'}</dd></div>
            {status.revokedAt && <div><dt>Revoked at</dt><dd>{displayTime(status.revokedAt)}</dd></div>}
          </dl>}
          {!gateway.isActive && <p className="management-note">Activate this gateway before generating or rotating a credential. Inactive gateways cannot authenticate.</p>}
          {confirmation ? <>
            <p className="management-note">{confirmation === 'rotate' ? 'Rotating immediately invalidates the current API key. Update the physical ESP32 with the new key.' : 'Revoking immediately prevents this API key from authenticating the gateway.'}</p>
            <div className="management-row-actions"><button className="app-ui-button" disabled={pending} onClick={() => setConfirmation(null)}>Cancel</button><button className="app-ui-button app-ui-button--primary" disabled={pending} onClick={() => mutate(confirmation)}>Confirm {confirmation === 'rotate' ? 'Rotate' : 'Revoke'}</button></div>
          </> : <div className="management-row-actions">
            {hasUnrevokedCredential ? <>
              <button className="app-ui-button" disabled={pending || !gateway.isActive || !active} onClick={() => setConfirmation('rotate')}>Rotate Credential</button>
              <button className="app-ui-button" disabled={pending} onClick={() => setConfirmation('revoke')}>Revoke Credential</button>
            </> : <button className="app-ui-button app-ui-button--primary" disabled={pending || !gateway.isActive} onClick={() => mutate('create')}>Generate Credential</button>}
          </div>}
        </>}
        {error && <p className="management-error" role="alert">{error}</p>}
        {!status && !pending && !secret && <button className="app-ui-button" onClick={() => { setPending(true); setError(''); load(lifetime.current.signal) }}>Retry</button>}
        {notice && <p className="management-success" role="status">{notice}</p>}
        <section aria-label="ESP32 usage"><p className="management-note">Use these values on the physical ESP32 gateway.</p>
          <pre className="gateway-credential-example">{`POST /api/device/gateways/${gateway.id}/sync\n\nX-Gateway-Code: ${gateway.code}\nX-Api-Key: <API KEY>\nContent-Type: application/json`}</pre>
        </section>
      </>
    </div>
    <footer><button className="app-ui-button" onClick={close}>Close</button></footer>
  </ManagementDialog>
}
