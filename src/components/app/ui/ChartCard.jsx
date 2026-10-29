import SectionCard from './SectionCard'
import EmptyState from './EmptyState'
import { useUiText } from '../../../utils/useUiText'

// Accepts a chart as children; the primitive does not fetch data or choose a chart library.
export default function ChartCard({ title, children, caption, loading = false, error, errorAction, className = '', ...props }) {
  const t = useUiText({
    'Loading chart…': 'Đang tải biểu đồ…',
    'Unable to load chart': 'Không thể tải biểu đồ',
    'No chart data': 'Chưa có dữ liệu biểu đồ',
    'Data will appear here when available.': 'Dữ liệu sẽ hiển thị tại đây khi có sẵn.',
  })
  return <SectionCard {...props} title={t(title)} className={`app-ui-chart-card ${className}`}>
    {loading ? <p className="app-ui-loading" role="status">{t('Loading chart…')}</p>
      : error ? <div role="alert"><EmptyState title={t('Unable to load chart')} description={error} actions={errorAction} /></div>
        : children ? <figure className="app-ui-chart-figure"><div className="app-ui-chart-content">{children}</div>{caption && <figcaption>{t(caption)}</figcaption>}</figure>
          : <EmptyState title={t('No chart data')} description={t('Data will appear here when available.')} />}
  </SectionCard>
}
