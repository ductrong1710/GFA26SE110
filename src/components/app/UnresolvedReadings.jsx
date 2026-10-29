import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { api } from '../../services/api/index'
import { useApiQuery } from '../../services/api/useApiQuery'
import { SectionCard, DataTable } from './ui'
import ApiState from './ApiState'
import { displayTime } from '../../utils/display'
import { useUiText } from '../../utils/useUiText'

export default function UnresolvedReadings({ nodeId }) {
  const { user, activeRole } = useAuth()
  const t = useUiText({
    'Readings needing review': 'Dữ liệu cần xem xét',
    'Original records with unknown or inconsistent time, or unavailable measurements. Preserved separately and excluded from charts and alerts.': 'Bản ghi gốc có thời gian không xác định hoặc không nhất quán, hoặc không có số đo. Dữ liệu được lưu riêng và không đưa vào biểu đồ hay cảnh báo.',
    'Preserved sensor records': 'Bản ghi cảm biến được lưu giữ',
    'Record': 'Bản ghi',
    'Received by server': 'Thời điểm máy chủ nhận',
    'Original data': 'Dữ liệu gốc',
    'View original record': 'Xem bản ghi gốc',
    'Previous': 'Trước',
    'Page': 'Trang',
    'Next': 'Tiếp',
  })
  const [page, setPage] = useState(1)
  const query = useApiQuery(`unresolved:${user.id}:${activeRole}:${nodeId}:${page}`,
    signal => api.get(`/api/sensor-nodes/${nodeId}/unresolved-records`, { page, pageSize: 20 }, signal))
  return <SectionCard title={t('Readings needing review')} description={t('Original records with unknown or inconsistent time, or unavailable measurements. Preserved separately and excluded from charts and alerts.')}>
    <ApiState {...query} />
    <DataTable caption={t('Preserved sensor records')} rows={query.data?.items ?? []} columns={[
      { key: 'sourceRecordKey', header: t('Record') },
      { key: 'receivedAt', header: t('Received by server'), render: displayTime },
      { key: 'payload', header: t('Original data'), render: payload => <details><summary>{t('View original record')}</summary><pre>{JSON.stringify(payload, null, 2)}</pre></details> },
    ]} />
    <button className="app-ui-button" disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>{t('Previous')}</button>
    <span aria-live="polite"> {t('Page')} {page} </span>
    <button className="app-ui-button" disabled={query.loading || page >= (query.data?.pagination.totalPages ?? 0)} onClick={() => setPage(page + 1)}>{t('Next')}</button>
  </SectionCard>
}
