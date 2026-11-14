import '../../../styles/app-ui.css'

export default function TablePagination({ page, totalPages = 1, totalItems, pageSize, onPageChange, disabled = false, label = 'Phân trang bảng' }) {
  const pages = Math.max(1, totalPages)
  const current = Math.min(Math.max(1, page), pages)
  const firstVisible = Math.max(1, Math.min(current - 1, pages - 2))
  const visiblePages = Array.from({ length: Math.min(3, pages) }, (_, index) => firstVisible + index)
  const start = (current - 1) * pageSize
  const range = Number.isFinite(totalItems) && Number.isFinite(pageSize) && totalItems > 0
    ? `${start + 1}–${Math.min(start + pageSize, totalItems)} / ${totalItems}`
    : `Trang ${current} / ${pages}`

  return <nav className="app-ui-pagination" aria-label={label}>
    <span className="app-ui-pagination-count">{range}</span>
    <div className="app-ui-pagination-buttons">
      <button type="button" aria-label="Trang đầu tiên" title="Trang đầu tiên" disabled={disabled || current === 1} onClick={() => onPageChange(1)}>|«</button>
      <button type="button" aria-label="Trang trước" title="Trang trước" disabled={disabled || current === 1} onClick={() => onPageChange(current - 1)}>‹</button>
      {visiblePages.map(number => <button key={number} type="button" aria-label={`Trang ${number}`} aria-current={current === number ? 'page' : undefined}
        className={current === number ? 'is-active' : ''} disabled={disabled} onClick={() => onPageChange(number)}>{number}</button>)}
      <button type="button" aria-label="Trang tiếp theo" title="Trang tiếp theo" disabled={disabled || current >= pages} onClick={() => onPageChange(current + 1)}>›</button>
      <button type="button" aria-label="Trang cuối cùng" title="Trang cuối cùng" disabled={disabled || current >= pages} onClick={() => onPageChange(pages)}>»|</button>
    </div>
  </nav>
}
