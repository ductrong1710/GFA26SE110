import { useState } from 'react'
import { useManagement } from '../../context/ManagementContext'
import { HUMAN_ROLES, ROLE_LABELS, ROLES } from '../../config/roles'
import { displayTime } from '../../data/mock/alertSelectors'
import { PageHeader, DataTable, StatusBadge, FilterBar } from '../../components/app/ui'
import ManagementDialog from '../../components/app/ManagementDialog'
import '../../styles/management.css'

function UserDialog({ mode, record, onSave, onClose }) {
  const [values, setValues] = useState(() => ({ fullName: record?.fullName ?? '', email: record?.email ?? '', roles: record?.roles ?? [ROLES.FARM_OWNER], status: mode === 'status' ? record.status === 'ACTIVE' ? 'INACTIVE' : 'ACTIVE' : record?.status ?? 'ACTIVE' }))
  const [error, setError] = useState('')
  const statusAction = values.status === 'ACTIVE' ? 'Activate' : 'Deactivate'
  const title = mode === 'status' ? `${statusAction} User` : mode === 'roles' ? 'Assign Role' : mode === 'edit' ? 'Edit User' : 'Add User'
  const toggleRole = (role) => setValues({ ...values, roles: values.roles.includes(role) ? values.roles.filter((item) => item !== role) : [...values.roles, role] })
  return <ManagementDialog title={title} onClose={onClose}>
    <form onSubmit={(event) => { event.preventDefault(); try { onSave(values) } catch (err) { setError(err.message) } }}>
      <div className="management-form">
        {mode === 'status' ? <p>{statusAction} <strong>{record.fullName}</strong> in the local user directory?</p> : <>
          {mode !== 'roles' && <><label>Name<input value={values.fullName} required maxLength={120} onChange={(event) => setValues({ ...values, fullName: event.target.value })} /></label>
            <label>Email<input type="email" value={values.email} required maxLength={254} onChange={(event) => setValues({ ...values, email: event.target.value })} /></label></>}
          {mode === 'roles' && <p>Assign one or more roles to <strong>{record.fullName}</strong>.</p>}
          <fieldset><legend>Human roles</legend>{HUMAN_ROLES.map((role) => <label key={role} className="management-check"><input type="checkbox" checked={values.roles.includes(role)} onChange={() => toggleRole(role)} />{role === ROLES.UAV_DEVICE_OPERATOR ? 'UAV & Device Operator' : ROLE_LABELS[role]}</label>)}</fieldset>
          {mode !== 'roles' && <label>Status<select aria-label="Status" value={values.status} onChange={(event) => setValues({ ...values, status: event.target.value })}><option value="ACTIVE">Active</option><option value="INACTIVE">Inactive</option></select></label>}
        </>}
        {error && <p className="management-error" role="alert">{error}</p>}
      </div>
      <footer><button type="button" className="app-ui-button" onClick={onClose}>Cancel</button><button className="app-ui-button app-ui-button--primary" type="submit">{mode === 'status' ? statusAction : 'Save User'}</button></footer>
    </form>
  </ManagementDialog>
}

export default function UsersPage() {
  const { management, changeManagement } = useManagement()
  const [dialog, setDialog] = useState(null)
  const [query, setQuery] = useState('')
  const [notice, setNotice] = useState('')
  const rows = management.users.filter((user) => `${user.fullName} ${user.email}`.toLowerCase().includes(query.trim().toLowerCase()))
  const save = (values) => { changeManagement({ kind: 'user', id: dialog.record?.id, values }); setNotice('User directory updated for this session.'); setDialog(null) }
  return <div className="app-ui management-page">
    <PageHeader eyebrow="ADMINISTRATION" title="Users & Roles" description="Manage the people and role assignments in your farm workspace." actions={<button className="app-ui-button app-ui-button--primary" onClick={() => setDialog({ mode: 'add' })}>+ Add User</button>} />
    <p className="management-note">Local directory only. Changes reset on refresh and do not change demo sign-in credentials or the current preview role.</p>
    {notice && <p role="status" className="management-success">{notice}</p>}
    <FilterBar label="Search users"><label>Search users<input type="search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Name or email" /></label></FilterBar>
    <DataTable caption="Users and roles" rows={rows} columns={[
      { key: 'fullName', header: 'Name' }, { key: 'email', header: 'Email' },
      { key: 'roles', header: 'Role', render: (roles) => <div className="management-role-list">{roles.map((role) => <span key={role}>{role === ROLES.UAV_DEVICE_OPERATOR ? 'UAV & Device Operator' : ROLE_LABELS[role]}</span>)}</div> },
      { key: 'status', header: 'Status', render: (status) => <StatusBadge status={status} /> },
      { key: 'lastLoginAt', header: 'Last Login', render: (at) => at ? <time dateTime={at}>{displayTime(at)}</time> : 'Never' },
      { key: 'actions', header: 'Actions', render: (_, record) => <div className="management-row-actions"><button onClick={() => setDialog({ mode: 'edit', record })} aria-label={`Edit ${record.fullName}`}>Edit</button><button onClick={() => setDialog({ mode: 'roles', record })} aria-label={`Assign role to ${record.fullName}`}>Assign Role</button><button onClick={() => setDialog({ mode: 'status', record })} aria-label={`${record.status === 'ACTIVE' ? 'Deactivate' : 'Activate'} ${record.fullName}`}>{record.status === 'ACTIVE' ? 'Deactivate' : 'Activate'}</button></div> },
    ]} footer={`${rows.length} of ${management.users.length} users · Times in Vietnam (UTC+7)`} />
    {dialog && <UserDialog {...dialog} onSave={save} onClose={() => setDialog(null)} />}
  </div>
}
