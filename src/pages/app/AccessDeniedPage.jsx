import { Link } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useUiText } from '../../utils/useUiText'

export default function AccessDeniedPage() {
  const { activeRole, activeRoleLabel } = useAuth()
  const t = useUiText({
    'Access Denied': 'Không có quyền truy cập',
    'Your active role ({role}) does not have access to this page.': 'Vai trò hiện tại ({role}) không có quyền truy cập trang này.',
    'Return to Dashboard →': 'Quay lại trang tổng quan →',
    'Return to sign in →': 'Quay lại đăng nhập →',
    'Farm Owner': 'Chủ trang trại',
    'Agricultural Engineer': 'Kỹ sư nông nghiệp',
    'Administrator': 'Quản trị viên',
    'UAV and Device Operator': 'Nhân viên UAV và thiết bị',
  })

  return <section className="app-placeholder app-access-denied" aria-labelledby="access-denied-heading">
    <h1 id="access-denied-heading">{t('Access Denied')}</h1>
    <p>{t('Your active role ({role}) does not have access to this page.').replace('{role}', t(activeRoleLabel))}</p>
    <p>{activeRole ? <Link className="app-access-denied-link" to="/app/dashboard">{t('Return to Dashboard →')}</Link> : <Link className="app-access-denied-link" to="/dang-nhap">{t('Return to sign in →')}</Link>}</p>
  </section>
}
