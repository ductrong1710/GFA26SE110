import { useEffect, useId, useRef, useState } from 'react'
import BoundaryEditorMap from './BoundaryEditorMap'
import { calculatePolygonCentroid, calculatePolygonArea } from '../../utils/geo'

export default function FarmRecordDialog({ kind, record, farm, farms, onSave, onClose }) {
  const [pending, setPending] = useState(false)
  const ref = useRef(null)
  const titleId = useId()
  const [error, setError] = useState('')
  const [boundaryValid, setBoundaryValid] = useState(true)
  const [values, setValues] = useState(() => ({ name: record?.name ?? '', description: record?.description ?? '',
    farmId: record?.farmId ?? farm?.id ?? '',
    latitude: record?.latitude ?? farm?.latitude ?? '', longitude: record?.longitude ?? farm?.longitude ?? '',
    areaHectares: record?.areaHectares ?? '', boundarySummary: record?.boundarySummary ?? '', boundary: record?.boundary ?? null }))
  const parentFarm = farms.find(item => item.id === Number(values.farmId))
  const changeBoundary = boundary => setValues(current => ({ ...current, boundary,
    ...(boundary ? { ...calculatePolygonCentroid(boundary), areaHectares: calculatePolygonArea(boundary) / 10000 } : {}) }))
  const name = kind === 'farm' ? 'Farm' : 'Zone'
  const title = `${record ? 'Edit' : 'Add'} ${name}`
  useEffect(() => {
    const dialog = ref.current
    const previous = document.activeElement
    dialog.showModal()
    return () => { dialog.close(); if (previous?.isConnected) previous.focus() }
  }, [])
  const change = (event) => {
    const { name: field, value } = event.target
    const selectedFarm = field === 'farmId' ? farms.find(({ id }) => id === Number(value)) : null
    setValues({ ...values, [field]: value, ...(selectedFarm ? { latitude: selectedFarm.latitude, longitude: selectedFarm.longitude } : {}) })
  }
  const field = (key, label, props = {}) => <label>{label}<input name={key} value={values[key]} onChange={change} {...props} /></label>
  const submit = async (event) => {
    event.preventDefault()
    if (!boundaryValid) return
    setPending(true); try { await onSave(values) } catch (err) { setError(err.message) } finally { setPending(false) }
  }
  return <dialog ref={ref} className="app-ui farm-dialog" aria-labelledby={titleId} onCancel={(event) => { event.preventDefault(); onClose() }}>
    <form onSubmit={submit}>
      <header><div><p className="farm-dialog-eyebrow">FARM WORKSPACE</p><h2 id={titleId}>{title}</h2></div><button type="button" className="app-ui-button" aria-label="Close dialog" onClick={onClose}>×</button></header>
      <div className="farm-dialog-fields">
        {kind === 'zone' && <label>Farm<select aria-label="Farm" name="farmId" value={values.farmId} onChange={change} disabled={Boolean(record)} required>{farms.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
        {field('name', 'Name', { required: true, maxLength: 120 })}
        <label>Description<textarea name="description" value={values.description} onChange={change} maxLength={500} rows={3} /></label>
        <BoundaryEditorMap boundary={values.boundary} parentBoundary={kind === 'zone' ? parentFarm?.boundary : null}
          center={values} onChange={changeBoundary} onValidityChange={setBoundaryValid} isZone={kind === 'zone'} />
        <div className="farm-form-pair">{field('latitude', 'Center latitude', { type: 'number', min: -90, max: 90, step: 'any', readOnly: Boolean(values.boundary) })}{field('longitude', 'Center longitude', { type: 'number', min: -180, max: 180, step: 'any', readOnly: Boolean(values.boundary) })}</div>
        <p className="farm-form-note">The center is calculated from the boundary. Legacy records can keep only their center coordinates.</p>
      </div>
      {error && <p className="farm-form-error" role="alert">{error}</p>}
      <footer><button type="button" className="app-ui-button" onClick={onClose}>Cancel</button><button type="submit" className="app-ui-button app-ui-button--primary" disabled={pending || !boundaryValid}>Save</button></footer>
    </form>
  </dialog>
}
