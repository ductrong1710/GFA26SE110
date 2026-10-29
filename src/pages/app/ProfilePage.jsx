import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ROLE_LABELS } from '../../config/roles'
import { useAuth } from '../../context/AuthContext'
import { useUiText } from '../../utils/useUiText'
import { api } from '../../services/api'
import AppIcon from '../../components/app/AppIcon'
import '../../styles/profile.css'

export default function ProfilePage() {
  const { user, activeRoleLabel, logout } = useAuth()
  const t = useUiText({
    'ACCOUNT': 'TÀI KHOẢN',
    'Personal information': 'THÔNG TIN CÁ NHÂN',
    'Profile': 'Hồ sơ cá nhân',
    'Your account information and security settings.': 'Thông tin tài khoản và cài đặt bảo mật.',
    'Account information': 'Thông tin tài khoản',
    'Information assigned to your account.': 'Thông tin được cấp cho tài khoản của bạn.',
    'Full name': 'Họ và tên',
    'Current role': 'Vai trò hiện tại',
    'Assigned roles': 'Các vai trò được cấp',
    'Change password': 'Đổi mật khẩu',
    'Your new password must be at least 12 characters. You will need to sign in again after changing it.': 'Mật khẩu mới cần có ít nhất 12 ký tự. Sau khi đổi, bạn sẽ cần đăng nhập lại.',
    'Current password': 'Mật khẩu hiện tại',
    'New password': 'Mật khẩu mới',
    'Confirm new password': 'Xác nhận mật khẩu mới',
    'New password and confirmation do not match.': 'Mật khẩu mới và xác nhận mật khẩu không khớp.',
    'Updating…': 'Đang cập nhật…',
    'Update password': 'Cập nhật mật khẩu',
    'Cancel': 'Hủy',
    'Password changed successfully. Please sign in again with your new password.': 'Đổi mật khẩu thành công. Vui lòng đăng nhập lại bằng mật khẩu mới.',
    'Farm Owner': 'Chủ trang trại',
    'Agricultural Engineer': 'Kỹ sư nông nghiệp',
    'Administrator': 'Quản trị viên',
    'UAV and Device Operator': 'Nhân viên UAV và thiết bị',
  })
  const navigate = useNavigate()
  const [values, setValues] = useState({ currentPassword: '', newPassword: '', confirmPassword: '' })
  const [passwordFormOpen, setPasswordFormOpen] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')

  const changePassword = async (event) => {
    event.preventDefault()
    setError('')
    if (values.newPassword !== values.confirmPassword) {
      setError(t('New password and confirmation do not match.'))
      return
    }

    setPending(true)
    try {
      await api.request('/api/auth/change-password', {
        method: 'POST',
        body: { currentPassword: values.currentPassword, newPassword: values.newPassword },
        toast: false,
      })
      await logout({ showSuccess: false })
      navigate('/dang-nhap', {
        replace: true,
        state: { message: t('Password changed successfully. Please sign in again with your new password.') },
      })
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setPending(false)
    }
  }

  const initials = user.fullName.trim().split(/\s+/).slice(-2).map(part => part[0]).join('').toUpperCase()

  return <div className="app-ui profile-page">
    <header className="profile-heading">
      <p className="app-ui-eyebrow">{t('ACCOUNT')}</p>
      <h1>{t('Profile')}</h1>
      <p className="app-ui-description">{t('Your account information and security settings.')}</p>
    </header>

    <section className="profile-card profile-overview" aria-labelledby="profile-overview-title">
      <div className="profile-avatar" aria-hidden="true">{initials}</div>
      <div className="profile-identity">
        <p className="app-ui-eyebrow">{t('Personal information')}</p>
        <h2 id="profile-overview-title">{user.fullName}</h2>
        <p>{user.email}</p>
        <span className="profile-role-active"><AppIcon name="profile" size={16} />{t(activeRoleLabel)}</span>
      </div>
    </section>

    <section className="profile-card" aria-labelledby="profile-details-title">
      <div className="profile-section-heading">
        <span className="profile-section-icon"><AppIcon name="users" /></span>
        <div><h2 id="profile-details-title">{t('Account information')}</h2><p>{t('Information assigned to your account.')}</p></div>
      </div>
      <dl className="profile-details">
        <div><dt>{t('Full name')}</dt><dd>{user.fullName}</dd></div>
        <div><dt>Email</dt><dd>{user.email}</dd></div>
        <div><dt>{t('Current role')}</dt><dd>{t(activeRoleLabel)}</dd></div>
        <div><dt>{t('Assigned roles')}</dt><dd className="profile-role-list">{user.roles.map(role => <span key={role}>{t(ROLE_LABELS[role] ?? role)}</span>)}</dd></div>
      </dl>
    </section>

    <section className="profile-card" aria-labelledby="profile-security-title">
      <div className="profile-section-heading">
        <span className="profile-section-icon"><AppIcon name="lock" /></span>
        <div><h2 id="profile-security-title">{t('Change password')}</h2><p>{t('Your new password must be at least 12 characters. You will need to sign in again after changing it.')}</p></div>
      </div>
      {!passwordFormOpen && <button className="app-ui-button app-ui-button--primary profile-open-password" type="button" onClick={() => setPasswordFormOpen(true)}>{t('Change password')}</button>}
      {passwordFormOpen && <form className="profile-password-form" onSubmit={changePassword}>
        <label>{t('Current password')}
          <input type="password" autoComplete="current-password" minLength={1} required value={values.currentPassword}
            onChange={event => setValues({ ...values, currentPassword: event.target.value })} />
        </label>
        <label>{t('New password')}
          <input type="password" autoComplete="new-password" minLength={12} maxLength={1024} required value={values.newPassword}
            onChange={event => setValues({ ...values, newPassword: event.target.value })} />
        </label>
        <label>{t('Confirm new password')}
          <input type="password" autoComplete="new-password" minLength={12} maxLength={1024} required value={values.confirmPassword}
            onChange={event => setValues({ ...values, confirmPassword: event.target.value })} />
        </label>
        {error && <p className="profile-form-error" role="alert">{error}</p>}
        <button className="app-ui-button app-ui-button--primary" type="submit" disabled={pending}>
          {pending ? t('Updating…') : t('Update password')}
        </button>
        <button className="app-ui-button" type="button" onClick={() => { setPasswordFormOpen(false); setError('') }}>{t('Cancel')}</button>
      </form>}
    </section>
  </div>
}
