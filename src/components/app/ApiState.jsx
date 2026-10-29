import { useUiText } from '../../utils/useUiText'

export default function ApiState({ loading, error, reload }) {
  const t = useUiText()
  if (loading) return <p role="status" className="operations-note">{t('Loading…')}</p>
  if (error) return <div role="alert" className="management-error"><p>{error.message}</p>{reload && <button className="app-ui-button" onClick={reload}>{t('Try again')}</button>}</div>
  return null
}
