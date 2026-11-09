import { useState } from 'react'

export default function PasswordField(props) {
  const [visible, setVisible] = useState(false)
  return <span className="password-field">
    <input {...props} type={visible ? 'text' : 'password'} />
    <button type="button" className="password-visibility" aria-label={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} aria-pressed={visible} onClick={() => setVisible((value) => !value)}>
      {visible
        ? <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 3l18 18M10.6 10.7a2 2 0 0 0 2.7 2.7M9.9 5.2A10.8 10.8 0 0 1 12 5c5.3 0 9 5.1 10 7-.4.8-1.4 2.1-2.9 3.3M6.2 6.2C3.9 7.6 2.5 10 2 12c1 1.9 4.7 7 10 7 1 0 2-.2 2.9-.6" /></svg>
        : <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2 12s3.7-7 10-7 10 7 10 7-3.7 7-10 7S2 12 2 12Z" /><circle cx="12" cy="12" r="3" /></svg>}
    </button>
  </span>
}
