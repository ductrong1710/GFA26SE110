import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import EvidenceImages from './EvidenceImages'

export default function MissionNoteImages({ missionId, noteId, count }) {
  const [open, setOpen] = useState(false)
  const query = useApiQuery(`mission-evidence:${missionId}:${noteId}`, signal =>
    api.get(`/api/missions/${missionId}/notes/${noteId}/images`, {}, signal), open)
  if (!count) return null
  return <div>
    {!open && <button type="button" className="app-ui-button" onClick={() => setOpen(true)}>View {count} evidence {count === 1 ? 'image' : 'images'}</button>}
    {open && <><ApiState loading={query.loading} error={query.error} reload={query.reload} /><EvidenceImages images={query.data ?? []} /></>}
  </div>
}
