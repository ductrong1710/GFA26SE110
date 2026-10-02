import { useEffect, useState } from 'react'
import EngineerRegistrationWizard from '../components/EngineerRegistrationWizard'
import PasswordField from '../components/PasswordField'
import UserRegistrationForm from '../components/UserRegistrationForm'

function AccountTypePicker({ onSelect }) {
  return <section className="account-form-content account-type-picker">
    <span className="eyebrow">ĐĂNG KÝ TÀI KHOẢN</span>
    <h1>Chọn loại tài khoản</h1>
    <p>Chọn cách bạn muốn sử dụng Smart Farm.</p>
    <button className="account-type-option" type="button" onClick={() => onSelect('user')}><span className="account-type-symbol">U</span><span><strong>Tài khoản người dùng</strong><small>Theo dõi thiết bị và hoạt động nông trại</small></span><i>→</i></button>
    <button className="account-type-option" type="button" onClick={() => onSelect('engineer')}><span className="account-type-symbol">K</span><span><strong>Kỹ sư nông nghiệp</strong><small>Tư vấn và phân tích tình trạng cây trồng</small></span><i>→</i></button>
  </section>
}

export default function AccountPage({ register = false }) {
  const [isRegister, setIsRegister] = useState(register)
  const [registrationType, setRegistrationType] = useState(null)
  const mode = isRegister ? 'register' : 'login'

  useEffect(() => {
    const syncMode = () => {
      const nextRegister = window.location.pathname === '/dang-ky'
      setIsRegister(nextRegister)
      setRegistrationType(null)
    }
    window.addEventListener('popstate', syncMode)
    return () => window.removeEventListener('popstate', syncMode)
  }, [])

  const switchMode = (nextRegister) => {
    if (nextRegister === isRegister) return
    setIsRegister(nextRegister)
    setRegistrationType(null)
    window.history.pushState({}, '', nextRegister ? '/dang-ky' : '/dang-nhap')
  }

  return <main className={'account-screen account-screen--' + mode}>
    <div className="account-card">
      <section className="account-form-panel">
        <div className="account-brand-group">
          <a className="account-brand" href="/" aria-label="Smart Farm trang chủ"><span className="wordmark-mark"><i /><i /><i /></span>Smart Farm</a>
          <a className="account-home-link" href="/">Trở về trang chủ</a>
        </div>
        {!isRegister && <section className="account-form-content account-login-form">
          <h1>Chào mừng trở lại</h1>
          <p className="account-intro">Đăng nhập để quản lý cảm biến, thiết bị thuê và dữ liệu nông trại.</p>
          <form onSubmit={(event) => event.preventDefault()}>
            <label>Email<input required name="email" type="email" autoComplete="email" placeholder="ban@email.com" /></label>
            <label>Mật khẩu<PasswordField required name="password" autoComplete="current-password" placeholder="Nhập mật khẩu" /></label>
            <a className="account-forgot" href="mailto:smartfarm@example.com">Quên mật khẩu?</a>
            <button className="account-submit" type="submit">Đăng nhập</button>
          </form>
        </section>}
        {isRegister && !registrationType && <AccountTypePicker onSelect={setRegistrationType} />}
        {isRegister && registrationType === 'user' && <UserRegistrationForm onBack={() => setRegistrationType(null)} />}
        {isRegister && registrationType === 'engineer' && <EngineerRegistrationWizard onBack={() => setRegistrationType(null)} />}
      </section>
      <aside key={mode} className="account-visual" aria-label="UAV thu thập dữ liệu cảm biến trên nông trại">
        <img src="/images/auth/smart-farm-field-uav.png" alt="UAV bay trên cánh đồng nông trại thông minh" />
        <div className="account-visual-content">
          <span className="eyebrow">SMART FARM · FIELD NETWORK</span>
          <h2>{isRegister ? 'Chào mừng trở lại.' : 'Xin chào, nhà vườn!'}</h2>
          <p>{isRegister ? 'Đăng nhập để tiếp tục theo dõi nông trại của bạn.' : 'Cùng kết nối cảm biến và chăm sóc cánh đồng thông minh hơn.'}</p>
          <button type="button" onClick={() => switchMode(!isRegister)}>{isRegister ? 'Đăng nhập' : 'Đăng ký'}</button>
        </div>
      </aside>
    </div>
  </main>
}
