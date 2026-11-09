import '../../../styles/app-ui.css'

export default function FilterBar({ children, actions, onSubmit, label = 'Filter data', className = '', ...props }) {
  return <form {...props} className={`app-ui app-ui-filter-bar ${className}`} aria-label={label}
    onSubmit={(event) => { event.preventDefault(); onSubmit?.(event) }}>
    <div className="app-ui-filter-fields">{children}</div>
    {actions && <div className="app-ui-actions">{actions}</div>}
  </form>
}
