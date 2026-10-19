import { useState } from 'react'
import { useManagement } from '../../context/ManagementContext'
import { ROLE_LABELS, ROLES, USER_ASSIGNABLE_ROLES } from '../../config/roles'
import { displayTime } from '../../utils/display'
import { PageHeader, DataTable, StatusBadge, FilterBar } from '../../components/app/ui'
import ManagementDialog from '../../components/app/ManagementDialog'
import '../../styles/management.css'

function UserDialog({ mode, record, onSave, onClose }) {
  const [pending, setPending] = useState(false)
  const [values, setValues] = useState(() => ({ fullName: record?.fullName ?? '', email: record?.email ?? '', roles: [record?.roles?.find(role => USER_ASSIGNABLE_ROLES.includes(role)) ?? ROLES.FARM_OWNER], status: mode === 'status' ? record.status === 'ACTIVE' ? 'INACTIVE' : 'ACTIVE' : record?.status ?? 'ACTIVE' }))
  const [error, setError] = useState('')
  const statusAction = values.status === 'ACTIVE' ? 'Activate' : 'Deactivate'
  const title = mode === 'status' ? `${statusAction} User` : mode === 'roles' ? 'Assign Role' : mode === 'edit' ? 'Edit User' : 'Add User'
  return <ManagementDialog title={title} onClose={onClose}>
    <form onSubmit={async (event) => { event.preventDefault(); setPending(true); try { await onSave(values) } catch (err) { setError(err.message) } finally { setPending(false) } }}>
      <div className="management-form">
        {mode === 'status' ? <p>{statusAction} <strong>{record.fullName}</strong>?</p> : <>
          {mode !== 'roles' && <><label>Name<input value={values.fullName} required maxLength={120} onChange={(event) => setValues({ ...values, fullName: event.target.value })} /></label>
            <label>Email<input type="email" value={values.email} required maxLength={254} onChange={(event) => setValues({ ...values, email: event.target.value })} /></label></>}
          {mode === 'roles' && <p>Assign one or more roles to <strong>{record.fullName}</strong>.</p>}
          {mode === 'add' && <label>Initial password<input type="password" required minLength={12} autoComplete="new-password" value={values.password ?? ''} onChange={event => setValues({ ...values, password: event.target.value })} /></label>}
          {(mode === 'roles' || mode === 'add') && <fieldset><legend>Available roles</legend>{USER_ASSIGNABLE_ROLES.map((role) => <label key={role} className="management-check"><input type="radio" name="user-role" checked={values.roles[0] === role} onChange={() => setValues({ ...values, roles: [role] })} />{ROLE_LABELS[role]}</label>)}</fieldset>}
        </>}
        {error && <p className="management-error" role="alert">{error}</p>}
      </div>
      <footer><button type="button" className="app-ui-button" onClick={onClose}>Cancel</button><button className="app-ui-button app-ui-button--primary" type="submit" disabled={pending}>{pending ? 'Saving…' : mode === 'status' ? statusAction : 'Save'}</button></footer>
    </form>
  </ManagementDialog>
}

export default function UsersPage() {
  const { management, changeManagement, loading, error, reload } = useManagement()
  const [dialog, setDialog] = useState(null)
  const [query, setQuery] = useState('')
  const [notice, setNotice] = useState('')
  const rows = management.users.filter((user) => `${user.fullName} ${user.email}`.toLowerCase().includes(query.trim().toLowerCase()))
  const save = async (values) => { await changeManagement({ kind: 'user', id: dialog.record?.id, mode: dialog.mode, values }); setNotice('User directory updated.'); setDialog(null) }
  return <div className="app-ui management-page">
    <PageHeader eyebrow="ADMINISTRATION" title="Users & Roles" description="Manage the people and role assignments in your farm workspace." actions={<button className="app-ui-button app-ui-button--primary" onClick={() => setDialog({ mode: 'add' })}>+ Add User</button>} />
    <p className="management-note">Account changes are saved to the server. Roles determine access; farm assignments are managed separately.</p>
    {loading && <p role="status">Loading users…</p>}
    {error && <p role="alert">{error.message} <button className="app-ui-button" onClick={reload}>Retry</button></p>}
    {notice && <p role="status" className="management-success">{notice}</p>}
    <FilterBar label="Search users"><label>Search users<input type="search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Name or email" /></label></FilterBar>
    <DataTable caption="Users and roles" rows={rows} columns={[
      { key: 'fullName', header: 'Name' }, { key: 'email', header: 'Email' },
      { key: 'roles', header: 'Role', render: (roles) => <div className="management-role-list">{roles.map((role) => <span key={role}>{role === ROLES.UAV_DEVICE_OPERATOR ? 'UAV & Device Operator' : ROLE_LABELS[role]}</span>)}</div> },
      { key: 'status', header: 'Status', render: (status) => <StatusBadge status={status} /> },
      { key: 'createdAt', header: 'Created', render: (at) => at ? <time dateTime={at}>{displayTime(at)}</time> : 'Not recorded' },
      { key: 'actions', header: 'Actions', render: (_, record) => {
        const isAdministrator = record.roles.includes(ROLES.ADMINISTRATOR)
        return <div className="management-row-actions">
          <button onClick={() => setDialog({ mode: 'edit', record })} aria-label={`Edit ${record.fullName}`}>Edit</button>
          {!isAdministrator && <button onClick={() => setDialog({ mode: 'roles', record })} aria-label={`Assign role to ${record.fullName}`}>Assign Role</button>}
          {!isAdministrator && <button onClick={() => setDialog({ mode: 'status', record })} aria-label={`${record.status === 'ACTIVE' ? 'Deactivate' : 'Activate'} ${record.fullName}`}>{record.status === 'ACTIVE' ? 'Deactivate' : 'Activate'}</button>}
        </div>
      } },
    ]} footer={`${rows.length} of ${management.users.length} users · Times in Vietnam (UTC+7)`} />
    {dialog && <UserDialog {...dialog} onSave={save} onClose={() => setDialog(null)} />}
  </div>
}
