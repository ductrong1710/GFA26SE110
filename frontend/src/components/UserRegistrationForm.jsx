import { useState } from 'react'
import PasswordField from './PasswordField'

export default function UserRegistrationForm({ onBack }) {
  const [notice, setNotice] = useState('')
  const handleSubmit = (event) => {
    event.preventDefault()
    const form = event.currentTarget
    if (form.elements.password.value !== form.elements.confirmPassword.value) {
      setNotice('Mật khẩu xác nhận chưa khớp.')
      return
    }
    setNotice('Đã nhận thông tin đăng ký tài khoản người dùng.')
  }

  return <section className="account-form-content account-user-registration">
    <button className="account-step-back" type="button" onClick={onBack}>← Chọn loại tài khoản</button>
    <h1>Tạo tài khoản người dùng</h1>
    <form className="account-form--register" onSubmit={handleSubmit}>
      <label>Họ và tên<input required name="fullName" autoComplete="name" placeholder="Nhập họ và tên" /></label>
      <label>Số điện thoại<input required name="phone" type="tel" autoComplete="tel" placeholder="Nhập số điện thoại" pattern="(0|\+84)(3|5|7|8|9)[0-9]{8}" title="Nhập số điện thoại Việt Nam hợp lệ, ví dụ 0912345678" /></label>
      <label>Email<input required name="email" type="email" autoComplete="email" placeholder="Nhập địa chỉ email" /></label>
      <label>Mật khẩu<PasswordField required name="password" autoComplete="new-password" placeholder="Nhập mật khẩu" minLength="8" pattern="(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).{8,}" title="Mật khẩu cần ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt" /></label>
      <label>Xác nhận mật khẩu<PasswordField required name="confirmPassword" autoComplete="new-password" placeholder="Nhập lại mật khẩu" minLength="8" /></label>
      {notice && <p className="account-message" role="status">{notice}</p>}
      <button className="account-submit" type="submit">Đăng ký tài khoản người dùng</button>
    </form>
  </section>
}
