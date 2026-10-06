import { useEffect, useId, useRef, useState } from 'react'

export default function FarmRecordDialog({ kind, record, farm, farms, onSave, onClose, remove = false, blocked }) {
  const ref = useRef(null)
  const titleId = useId()
  const [error, setError] = useState('')
  const [values, setValues] = useState(() => ({ name: record?.name ?? '', description: record?.description ?? '',
    location: record?.location ?? '', farmId: record?.farmId ?? farm?.id ?? '',
    latitude: record?.latitude ?? farm?.latitude ?? '', longitude: record?.longitude ?? farm?.longitude ?? '',
    areaHectares: record?.areaHectares ?? '', boundarySummary: record?.boundarySummary ?? '' }))
  const name = kind === 'farm' ? 'Farm' : 'Zone'
  const title = `${remove ? 'Delete' : record ? 'Edit' : 'Add'} ${name}`
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
  const submit = (event) => {
    event.preventDefault()
    try { onSave(values) } catch (err) { setError(err.message) }
  }
  return <dialog ref={ref} className="app-ui farm-dialog" aria-labelledby={titleId} onCancel={(event) => { event.preventDefault(); onClose() }}>
    <form onSubmit={submit}>
      <header><div><p className="farm-dialog-eyebrow">LOCAL DEMO</p><h2 id={titleId}>{title}</h2></div><button type="button" className="app-ui-button" aria-label="Close dialog" onClick={onClose}>×</button></header>
      {remove ? <div className="farm-dialog-fields"><p>Delete <strong>{record.name}</strong> from this demo session?</p><p>{blocked ?? 'This removes the record from the hierarchy. Cancel to keep it.'}</p></div> : <div className="farm-dialog-fields">
        {kind === 'zone' && <label>Farm<select name="farmId" value={values.farmId} onChange={change} disabled={Boolean(record)} required>{farms.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
        {field('name', 'Name', { required: true, maxLength: 120 })}
        <label>Description<textarea name="description" value={values.description} onChange={change} maxLength={500} rows={3} /></label>
        {kind === 'farm' && field('location', 'Location', { maxLength: 160 })}
        <div className="farm-form-pair">{field('latitude', 'Latitude', { type: 'number', min: -90, max: 90, step: 'any', required: true })}{field('longitude', 'Longitude', { type: 'number', min: -180, max: 180, step: 'any', required: true })}</div>
        {field('areaHectares', 'Area (hectares)', { type: 'number', min: 0.01, max: 1000000, step: 'any', required: true })}
        {kind === 'zone' && field('boundarySummary', 'Boundary summary', { maxLength: 300, placeholder: 'Describe the extent or nearby landmarks' })}
        <p className="farm-form-note">Coordinates mark the center. The map is an illustrative overview, not a surveyed boundary.</p>
      </div>}
      {error && <p className="farm-form-error" role="alert">{error}</p>}
      <footer><button type="button" className="app-ui-button" onClick={onClose} autoFocus={remove}>Cancel</button><button type="submit" className={`app-ui-button ${remove ? 'farm-danger' : 'app-ui-button--primary'}`} disabled={remove && Boolean(blocked)}>{remove ? 'Delete' : 'Save'}</button></footer>
    </form>
  </dialog>
}
