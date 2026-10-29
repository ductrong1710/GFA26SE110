import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import PasswordField from '../components/PasswordField'
import { useAuth } from '../context/AuthContext'

export default function AccountPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const { login } = useAuth()
  const [loginError, setLoginError] = useState('')
  const [pending, setPending] = useState(false)

  const handleLogin = async (event) => {
    event.preventDefault()
    setLoginError('')
    const data = new FormData(event.currentTarget)
    setPending(true)
    try {
      await login(data.get('email'), data.get('password'))
    } catch (error) {
      setLoginError(error.message)
      return
    } finally { setPending(false) }

    // DEMO ONLY: the account resolves activeRole; all roles share this dashboard URL.
    navigate('/app/dashboard', { replace: true })
  }

  return <main className="account-screen account-screen--login">
    <div className="account-card">
      <section className="account-form-panel">
        <div className="account-brand-group">
          <Link className="account-brand" to="/" aria-label="Smart Farm trang chủ"><span className="wordmark-mark"><i /><i /><i /></span>Smart Farm</Link>
          <Link className="account-home-link" to="/">Trở về trang chủ</Link>
        </div>
        <section className="account-form-content account-login-form">
          <h1>Chào mừng trở lại</h1>
          <p className="account-intro">Đăng nhập để quản lý cảm biến, thiết bị thuê và dữ liệu nông trại.</p>
          {location.state?.message && <p className="account-message" role="status">{location.state.message}</p>}
          <form onSubmit={handleLogin}>
            <label>Email<input required name="email" type="email" autoComplete="email" placeholder="ban@email.com" /></label>
            <label>Mật khẩu<PasswordField required name="password" autoComplete="current-password" placeholder="Nhập mật khẩu" /></label>
            <Link className="account-forgot" to="/quen-mat-khau">Quên mật khẩu?</Link>
            {loginError && <p className="account-message account-message--error" role="alert">{loginError}</p>}
            <button className="account-submit" type="submit" disabled={pending}>{pending ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
          </form>
        </section>
      </section>
      <aside className="account-visual" aria-label="UAV thu thập dữ liệu cảm biến trên nông trại">
        <img src="/images/auth/smart-farm-field-uav.png" alt="UAV bay trên cánh đồng nông trại thông minh" />
        <div className="account-visual-content">
          <span className="eyebrow">SMART FARM · FIELD NETWORK</span>
          <h2>Xin chào, nhà vườn!</h2>
          <p>Cùng kết nối cảm biến và chăm sóc cánh đồng thông minh hơn.</p>
        </div>
      </aside>
    </div>
  </main>
}
