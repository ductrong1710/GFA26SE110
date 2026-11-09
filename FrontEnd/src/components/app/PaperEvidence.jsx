import EvidenceImages from './EvidenceImages'

export function PaperGroup({ label, images = [], onChange, onBusyChange }) {
  return <fieldset><legend>{label}</legend><EvidenceImages images={images} onChange={onChange} onBusyChange={onBusyChange} /></fieldset>
}

// Paperwork JSON was stored by .NET with PascalCase property names.
export function readPaper(raw) {
  if (!raw) return null
  return JSON.parse(raw, (_, value) => {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return value
    return Object.fromEntries(Object.entries(value).map(([key, v]) => [key[0].toLowerCase() + key.slice(1), v]))
  })
}
