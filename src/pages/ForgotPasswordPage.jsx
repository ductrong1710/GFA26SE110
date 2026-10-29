import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api } from '../services/api'
import PasswordField from '../components/PasswordField'

export default function ForgotPasswordPage() {
  const navigate = useNavigate()
  const [stage, setStage] = useState('request')
  const [email, setEmail] = useState('')
  const [pending, setPending] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')

  async function requestReset(event) {
    event.preventDefault()
    setPending(true)
    setError('')
    try {
      await api.requestPasswordReset(email.trim())
      setMessage('Nếu email đủ điều kiện, hướng dẫn đặt lại mật khẩu sẽ được gửi đến hộp thư của bạn. Vui lòng kiểm tra cả thư mục spam.')
      setStage('reset')
    } catch (requestError) {
      setError(requestError.message)
    } finally { setPending(false) }
  }

  async function resetPassword(event) {
    event.preventDefault()
    setPending(true)
    setError('')
    const data = new FormData(event.currentTarget)
    const token = String(data.get('token') ?? '').trim()
    const newPassword = String(data.get('newPassword') ?? '')
    if (newPassword !== data.get('confirmPassword')) {
      setError('Mật khẩu xác nhận không khớp.')
      setPending(false)
      return
    }
    try {
      await api.resetPassword(token, newPassword)
      navigate('/dang-nhap', { replace: true, state: { message: 'Đặt lại mật khẩu thành công. Hãy đăng nhập bằng mật khẩu mới.' } })
    } catch (requestError) {
      setError(requestError.message)
    } finally { setPending(false) }
  }

  return <main className="account-screen account-screen--forgot">
    <div className="account-card">
      <section className="account-form-panel">
        <section className="account-form-content account-login-form forgot-password-content">
          <div className="account-brand-group">
            <Link className="account-brand" to="/" aria-label="Smart Farm trang chủ"><span className="wordmark-mark"><i /><i /><i /></span>Smart Farm</Link>
            <Link className="forgot-login-link" to="/dang-nhap">← Quay lại đăng nhập</Link>
          </div>
          <h1>{stage === 'request' ? 'Quên mật khẩu?' : 'Tạo mật khẩu mới'}</h1>
          {message && <p className="account-message" role="status">{message}</p>}
          {stage === 'request' ? <form onSubmit={requestReset}>
            <label>Email<input required name="email" type="email" autoComplete="email" maxLength={255} placeholder="ban@email.com" value={email} onChange={event => setEmail(event.target.value)} /></label>
            {error && <p className="account-message account-message--error" role="alert">{error}</p>}
            <button className="account-submit" type="submit" disabled={pending}>{pending ? 'Đang gửi yêu cầu…' : 'Khôi phục mật khẩu'}</button>
          </form> : <form onSubmit={resetPassword}>
            <label>Mã khôi phục<input required name="token" autoComplete="one-time-code" minLength={64} maxLength={64} pattern="[a-fA-F0-9]{64}" title="Mã khôi phục gồm 64 ký tự chữ và số" placeholder="Nhập mã trong email" /></label>
            <label>Mật khẩu mới<PasswordField required name="newPassword" autoComplete="new-password" minLength={12} maxLength={1024} placeholder="Ít nhất 12 ký tự" /></label>
            <label>Xác nhận mật khẩu mới<PasswordField required name="confirmPassword" autoComplete="new-password" minLength={12} maxLength={1024} placeholder="Nhập lại mật khẩu mới" /></label>
            {error && <p className="account-message account-message--error" role="alert">{error}</p>}
            <button className="account-submit" type="submit" disabled={pending}>{pending ? 'Đang cập nhật…' : 'Đặt lại mật khẩu'}</button>
          </form>}
        </section>
      </section>
    </div>
  </main>
}
