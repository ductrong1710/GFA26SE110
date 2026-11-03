import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import PasswordField from '../components/PasswordField'
import { useAuth } from '../context/AuthContext'
import { api } from '../services/api'

const titles = { forgot: 'Quên mật khẩu', reset: 'Đặt lại mật khẩu', change: 'Đổi mật khẩu' }
export default function PasswordPage({ mode }) {
  const { logout } = useAuth()
  const navigate = useNavigate()
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const forgot = mode === 'forgot'
  const reset = mode === 'reset'
  async function submit(event) {
    event.preventDefault()
    if (pending) return
    const data = new FormData(event.currentTarget)
    setError('')
    if (!forgot && data.get('newPassword') !== data.get('confirm')) {
      setError('Hai mật khẩu mới chưa khớp.')
      return
    }
    const body = forgot ? { email: data.get('email').trim() } : reset
      ? { token: data.get('token').trim(), newPassword: data.get('newPassword') }
      : { currentPassword: data.get('currentPassword'), newPassword: data.get('newPassword') }
    setPending(true)
    try {
      await api.request(`/api/auth/${mode}-password`, { method: 'POST', body, auth: mode === 'change', toast: false })
      if (forgot) navigate('/dat-lai-mat-khau')
      else {
        if (!reset) await logout()
        navigate('/dang-nhap', { replace: true, state: { passwordUpdated: true } })
      }
    } catch (cause) { setError(cause.message) }
    finally { setPending(false) }
  }
  return <main className="account-screen"><section className="account-form-panel"><div className="account-form-content">
    <h1>{titles[mode]}</h1>
    {forgot && <p>Nhập email của tài khoản để nhận hướng dẫn đặt lại mật khẩu.</p>}
    {reset && <p>Nếu tài khoản đủ điều kiện, bạn sẽ nhận được email chứa mã dùng một lần. Mã có hiệu lực trong 15 phút.</p>}
    <form onSubmit={submit}>
      {forgot && <label>Email<input type="email" name="email" autoComplete="email" required maxLength={255} /></label>}
      {reset && <label>Mã khôi phục<input name="token" autoComplete="one-time-code" autoCapitalize="none" spellCheck={false} required pattern="[A-Fa-f0-9]{64}" title="Mã 64 ký tự trong email" /></label>}
      {mode === 'change' && <label>Mật khẩu hiện tại<PasswordField name="currentPassword" autoComplete="current-password" required maxLength={1024} /></label>}
      {!forgot && <><label>Mật khẩu mới (ít nhất 12 ký tự)<PasswordField name="newPassword" autoComplete="new-password" required minLength={12} maxLength={1024} /></label>
        <label>Nhập lại mật khẩu mới<PasswordField name="confirm" autoComplete="new-password" required minLength={12} maxLength={1024} /></label></>}
      {error && <p role="alert">{error}</p>}
      <button className="account-submit" disabled={pending}>{pending ? 'Đang xử lý…' : forgot ? 'Gửi hướng dẫn' : titles[mode]}</button>
    </form>
    {forgot && <p><Link to="/dat-lai-mat-khau">Tôi đã có mã khôi phục</Link></p>}
    {reset && <p><Link to="/quen-mat-khau">Yêu cầu mã mới</Link></p>}
    <Link to={mode === 'change' ? '/app/dashboard' : '/dang-nhap'}>{mode === 'change' ? 'Quay lại tổng quan' : 'Quay lại đăng nhập'}</Link>
  </div></section></main>
}
