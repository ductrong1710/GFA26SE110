import FarmAccess from '../../components/app/FarmAccess'
import { useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS } from '../../config/permissions'
import { useOperations } from '../../context/OperationsContext'
import { PageHeader, SectionCard, EmptyState, StatusBadge } from '../../components/app/ui'
import FarmRecordDialog from '../../components/app/FarmRecordDialog'
import FarmZoneMap from '../../components/app/FarmZoneMap'
import '../../styles/admin-workspace.css'

export default function FarmsPage() {
  const { currentFarm, selectFarm, farmWorkspace, changeFarmRecord } = useOutletContext()
  const { can } = useAuth()
  const { operations } = useOperations()
  const { sensorNodes } = operations
  const canManage = can(PERMISSIONS.FARMS_MANAGE)
  const [selectedZoneId, setSelectedZoneId] = useState(null)
  const [dialog, setDialog] = useState(null)
  const [notice, setNotice] = useState('')
  const { farms, zones } = farmWorkspace
  const farm = farms.find(({ id }) => id === currentFarm.id)
  const farmZones = zones.filter(({ farmId }) => farmId === farm?.id)
  const zone = farmZones.find(({ id }) => id === selectedZoneId) ?? farmZones[0]
  const zoneNodes = sensorNodes.filter(({ zoneId }) => zoneId === zone?.id)
  const openDialog = (kind, record) => { if (canManage) setDialog({ kind, record }) }
  const save = async (values) => {
    if (!canManage) throw new Error('This role has read-only access.')
    const next = await changeFarmRecord({ kind: dialog.kind, id: dialog.record?.id, values })
    if (!dialog.record) {
      if (dialog.kind === 'farm') { selectFarm(next.farms.at(-1).id); setSelectedZoneId(null) }
      else { const added = next.zones.at(-1); selectFarm(added.farmId); setSelectedZoneId(added.id) }
    }
    setNotice(`${dialog.kind === 'farm' ? 'Farm' : 'Zone'} saved successfully.`)
    setDialog(null)
  }
  return <div className="app-ui farms-page">
    <PageHeader eyebrow="FARM DIRECTORY" title="Farms & Zones" description="Explore your farms, growing areas, and sensor coverage."
      actions={canManage ? <><button className="app-ui-button" onClick={() => openDialog('farm')}>+ Add Farm</button><button className="app-ui-button app-ui-button--primary" disabled={!farm} onClick={() => openDialog('zone')}>+ Add Zone</button></> : <StatusBadge status="INFO" label="Read-only access" />} />
    <p className="admin-scope-note">Farms and zones from your authorized workspace.</p>
    {notice && <p className="farm-success" role="status">{notice}</p>}
    <div className="farms-layout">
      <SectionCard title="Farm hierarchy" description={`${farms.length} farms · ${zones.length} zones`} className="farm-hierarchy">
        <nav aria-label="Farm and zone hierarchy"><ul>{farms.map((item) => <li key={item.id}>
          <button className="farm-tree-farm" aria-current={farm?.id === item.id ? 'true' : undefined} onClick={() => { selectFarm(item.id); setSelectedZoneId(null) }}>{item.name}<small>{zones.filter(({ farmId }) => farmId === item.id).length} zones</small></button>
          <ul>{zones.filter(({ farmId }) => farmId === item.id).map((child) => <li key={child.id}><button className="farm-tree-zone" aria-current={zone?.id === child.id ? 'true' : undefined} onClick={() => { selectFarm(item.id); setSelectedZoneId(child.id) }}>{child.name}</button></li>)}</ul>
        </li>)}</ul></nav>
        {!farms.length && <EmptyState title="No farms yet" />}
      </SectionCard>
      <div className="farm-detail-column">
        {farm ? <>
          <SectionCard title={farm.name} description={farm.description} actions={canManage && <button className="app-ui-button" onClick={() => openDialog('farm', farm)}>Edit Farm</button>}>
            <FarmZoneMap farm={farm} zones={farmZones} selectedZoneId={zone?.id} onSelect={setSelectedZoneId} nodes={sensorNodes} />
          </SectionCard>
          <FarmAccess farm={farm} onChanged={() => changeFarmRecord({ kind: 'reload' })} />
          <SectionCard title="Selected zone details" description={zone ? 'Coverage and boundary information' : 'Choose or add a growing area'} actions={canManage && zone && <button className="app-ui-button" onClick={() => openDialog('zone', zone)}>Edit Zone</button>}>
            {zone ? <dl className="farm-detail-grid">
              <div><dt>Name</dt><dd>{zone.name}</dd></div><div><dt>Description</dt><dd>{zone.description || 'No description provided'}</dd></div>
              <div><dt>Sensor Count</dt><dd>{zoneNodes.length}</dd></div><div><dt>Online / Offline Count</dt><dd>{zoneNodes.filter(({ status }) => status === 'ONLINE').length} online / {zoneNodes.filter(({ status }) => status === 'OFFLINE').length} offline</dd></div>
              <div><dt>Coordinates</dt><dd>{zone.latitude?.toFixed(5) ?? 'Not recorded'}, {zone.longitude?.toFixed(5) ?? 'Not recorded'}</dd></div><div><dt>Boundary summary</dt><dd>{zone.boundarySummary || 'Boundary not recorded'}</dd></div>
            </dl> : <EmptyState title="No zones yet" description="Zones and sensor coverage will appear here when configured." />}
          </SectionCard>
        </> : <EmptyState title="No farm selected" description="Add a farm to start organizing zones." />}
      </div>
    </div>
    {canManage && dialog && <FarmRecordDialog key={`${dialog.kind}-${dialog.record?.id ?? 'new'}`} {...dialog} farm={farm} farms={farms} onSave={save} onClose={() => setDialog(null)} />}
  </div>
}
