import { useEffect, useId, useRef } from 'react'
import '../../styles/management.css'

export default function ManagementDialog({ title, onClose, children, drawer = false }) {
  const ref = useRef(null)
  const titleId = useId()
  useEffect(() => {
    const dialog = ref.current
    const previous = document.activeElement
    dialog.showModal()
    return () => { dialog.close(); if (previous?.isConnected) previous.focus() }
  }, [])
  return <dialog ref={ref} className={`app-ui management-dialog${drawer ? ' management-drawer' : ''}`} aria-labelledby={titleId}
    onCancel={(event) => { event.preventDefault(); onClose() }}>
    <header><h2 id={titleId}>{title}</h2><button className="app-ui-button" type="button" onClick={onClose} aria-label="Close dialog">×</button></header>
    {children}
  </dialog>
}
