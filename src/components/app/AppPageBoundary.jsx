import { Component } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState } from './ui'
import { useUiText } from '../../utils/useUiText'

function PageErrorFallback() {
  const t = useUiText({
    'Unable to display this page': 'Không thể hiển thị trang này',
    'Return to the dashboard, or reload to retry. Reloading discards unsaved changes and keeps your sign-in.': 'Quay lại trang tổng quan hoặc tải lại để thử. Tải lại sẽ bỏ các thay đổi chưa lưu và giữ phiên đăng nhập.',
    'Reload page': 'Tải lại trang',
    'Return to Dashboard': 'Quay lại trang tổng quan',
  })
  return <div role="alert"><EmptyState title={t('Unable to display this page')}
    description={t('Return to the dashboard, or reload to retry. Reloading discards unsaved changes and keeps your sign-in.')}
    actions={<><button className="app-ui-button" onClick={() => window.location.reload()}>{t('Reload page')}</button>
      <Link className="app-ui-button" to="/app/dashboard">{t('Return to Dashboard')}</Link></>} /></div>
}

// Keep workspace navigation available if a page render or lazy import fails.
export default class AppPageBoundary extends Component {
  state = { failed: false }

  static getDerivedStateFromError() { return { failed: true } }

  render() {
    if (!this.state.failed) return this.props.children
    return <PageErrorFallback />
  }
}
