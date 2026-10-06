import EmptyState from './EmptyState'
import '../../../styles/app-ui.css'

function cellValue(value) {
  if (value === null || value === undefined) return '—'
  if (typeof value === 'boolean') return value ? 'Yes' : 'No'
  return typeof value === 'string' || typeof value === 'number' ? value : '—'
}

// Controlled table: the caller owns sorting, filtering, pagination, and permissions.
export default function DataTable({ columns = [], rows = [], rowKey = 'id', caption = 'Data table', loading = false,
  error, errorAction, emptyState, sort, onSortChange, footer, className = '', ...props }) {
  const getRowKey = typeof rowKey === 'function' ? rowKey : (row) => row[rowKey]
  return <div {...props} className={`app-ui app-ui-data-table ${className}`}>
    <div className="app-ui-table-scroll" role="region" aria-label={`${caption} scroll area`} tabIndex={0}>
      <table aria-busy={loading}>
        <caption className="app-ui-sr-only">{caption}</caption>
        <thead><tr>{columns.map((column) => {
          const sortable = column.sortable && typeof onSortChange === 'function'
          const direction = sort?.key === column.key ? sort.direction : null
          return <th key={column.key} scope="col" data-align={column.align ?? 'start'}
            aria-sort={sortable ? direction === 'asc' ? 'ascending' : direction === 'desc' ? 'descending' : 'none' : undefined}>
            {sortable ? <button type="button" className="app-ui-table-sort" onClick={() => onSortChange({ key: column.key, direction: direction === 'asc' ? 'desc' : 'asc' })}>
              {column.header}<span aria-hidden="true">{direction === 'asc' ? '↑' : direction === 'desc' ? '↓' : '↕'}</span>
            </button> : column.header}
          </th>
        })}</tr></thead>
        <tbody>{loading || error || !rows.length ? <tr><td colSpan={Math.max(1, columns.length)}>
          {loading ? <p className="app-ui-loading" role="status">Loading data…</p>
            : error ? <div role="alert"><EmptyState title="Unable to load data" description={error} actions={errorAction} /></div>
              : emptyState ?? <EmptyState title="No records found" description="Try adjusting your filters or check back later." />}
        </td></tr> : rows.map((row) => <tr key={getRowKey(row)}>{columns.map((column) => <td key={column.key} data-align={column.align ?? 'start'}>
          {column.render ? column.render(row[column.key], row) : cellValue(row[column.key])}
        </td>)}</tr>)}</tbody>
      </table>
    </div>
    {footer && <div className="app-ui-table-footer">{footer}</div>}
  </div>
}
