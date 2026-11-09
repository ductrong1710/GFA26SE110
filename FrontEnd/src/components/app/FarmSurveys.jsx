import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from './ApiState'
import SurveyPanel from './SurveyPanel'
import ProposalPanel from './ProposalPanel'
import { SectionCard } from './ui'

export default function FarmSurveys({ farmId }) {
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState(null)
  const query = useApiQuery(`farm-survey-consultations:${farmId}:${page}`, signal => api.request(`/api/farms/${farmId}/survey-consultations`, { query: { page }, signal }))
  return <SectionCard title="Farm survey records">
    <ApiState {...query} />
    {!query.loading && !query.error && !query.data?.items.length && <p>No survey consultations are linked to this farm.</p>}
    <div className="management-row-actions">{query.data?.items.map(row => <button key={row.id} className="app-ui-button" aria-pressed={selected === row.id}
      onClick={() => setSelected(row.id)}>Consultation #{row.id}: {row.serviceNeed}</button>)}</div>
    {query.data && <div className="management-row-actions"><button disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Previous</button>
      <span>Page {page}</span><button disabled={page * 20 >= query.data.pagination.totalItems || query.loading} onClick={() => setPage(page + 1)}>Next</button></div>}
    {selected && <SurveyPanel key={selected} consultationId={selected} editable={false} />}
    {selected && <ProposalPanel key={`proposal:${selected}`} consultationId={selected} />}
  </SectionCard>
}
