import SectionCard from './SectionCard'
import EmptyState from './EmptyState'

// Accepts a chart as children; the primitive does not fetch data or choose a chart library.
export default function ChartCard({ title, children, caption, loading = false, error, errorAction, className = '', ...props }) {
  return <SectionCard {...props} title={title} className={`app-ui-chart-card ${className}`}>
    {loading ? <p className="app-ui-loading" role="status">Loading chart…</p>
      : error ? <div role="alert"><EmptyState title="Unable to load chart" description={error} actions={errorAction} /></div>
        : children ? <figure className="app-ui-chart-figure"><div className="app-ui-chart-content">{children}</div>{caption && <figcaption>{caption}</figcaption>}</figure>
          : <EmptyState title="No chart data" description="Data will appear here when available." />}
  </SectionCard>
}
