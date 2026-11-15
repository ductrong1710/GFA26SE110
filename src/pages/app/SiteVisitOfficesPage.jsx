import { useState } from 'react'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from '../../components/app/ApiState'
import { DataTable, PageHeader, SectionCard } from '../../components/app/ui'
import '../../styles/management.css'

export default function SiteVisitOfficesPage() {
  const [name, setName] = useState('')
  const [mapsUrl, setMapsUrl] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const query = useApiQuery('site-visit-offices', signal => api.request('/api/site-visit-offices', { signal }))

  async function create(event) {
    event.preventDefault()
    if (pending) return
    setPending(true)
    setError('')
    setNotice('')
    try {
      await api.request('/api/site-visit-offices', {
        method: 'POST', retry: false, body: { name: name.trim(), mapsUrl: mapsUrl.trim() },
      })
      setName('')
      setMapsUrl('')
      setNotice('Đã thêm trụ sở.')
      query.reload()
    } catch (err) {
      setError(err.message)
    } finally {
      setPending(false)
    }
  }

  return <>
    <PageHeader title="Quản lý trụ sở" description="Khai báo các điểm xuất phát cho chuyến tư vấn tại vườn." />
    <SectionCard title="Thêm trụ sở">
      <form className="management-form" onSubmit={create}>
        <label>Tên trụ sở<input required maxLength={120} value={name} onChange={event => setName(event.target.value)} /></label>
        <label>Link Google Maps của trụ sở<input required type="url" maxLength={2048} value={mapsUrl} onChange={event => setMapsUrl(event.target.value)} /></label>
        <button type="submit" className="app-ui-button app-ui-button--primary" disabled={pending || !name.trim() || !mapsUrl.trim()}>
          {pending ? 'Đang lưu…' : 'Thêm trụ sở'}
        </button>
        {error && <p role="alert">{error}</p>}
        {notice && <p role="status">{notice}</p>}
      </form>
    </SectionCard>
    <ApiState vietnamese {...query} />
    {query.data && <DataTable vietnamese caption="Danh sách trụ sở" rowKey="code" rows={query.data} columns={[
      { key: 'label', header: 'Tên trụ sở' },
      { key: 'mapsUrl', header: 'Vị trí', render: (url, row) =>
        <a href={url} target="_blank" rel="noreferrer" aria-label={`Mở vị trí ${row.label} trên Google Maps`}>Mở Google Maps</a> },
    ]} />}
  </>
}
