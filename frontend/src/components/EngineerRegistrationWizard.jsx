import { useRef, useState } from 'react'
import PasswordField from './PasswordField'

const specialties = ['Nông học', 'Trồng trọt', 'Bảo vệ thực vật', 'Khoa học cây trồng', 'Công nghệ sinh học', 'Khác']
const fields = ['Cây ăn trái', 'Rau màu', 'Cây lương thực', 'Cây công nghiệp', 'Phòng trừ sâu bệnh', 'Dinh dưỡng cây trồng', 'Đất và nước', 'Khác']
const acceptFiles = '.pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png'
const fileSize = (bytes) => bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`

function UploadField({ title, required = false, files, onFiles, onRemove, inputRef, multiple = false }) {
  return <div className="engineer-upload-field">
    <span className="engineer-field-title">{title}{required && <b> *</b>}</span>
    <label className="engineer-upload-trigger"><input ref={inputRef} type="file" accept={acceptFiles} multiple={multiple} required={required && files.length === 0} onChange={(event) => { onFiles(Array.from(event.target.files || [])); event.target.value = '' }} /><span aria-hidden="true">↑</span><span>Chọn tệp PDF, JPG hoặc PNG</span></label>
    {files.map((file, index) => <div className="engineer-file-row" key={`${file.name}-${file.size}-${index}`}><span className="engineer-file-type">{file.name.split('.').pop()?.toUpperCase()}</span><div className="engineer-file-meta"><strong title={file.name}>{file.name}</strong><span>{fileSize(file.size)} · {file.type || 'Tệp đính kèm'} · Đã tải lên</span></div><span className="engineer-file-status">Đã tải lên</span><button type="button" aria-label={`Xóa tệp ${file.name}`} onClick={() => onRemove(index)}>×</button></div>)}
  </div>
}

export default function EngineerRegistrationWizard({ onBack }) {
  const [step, setStep] = useState(1)
  const [profile, setProfile] = useState({ fullName: '', phone: '', email: '', password: '', confirmPassword: '', specialty: '', specialtyOther: '', years: '', fields: [], fieldOther: '', workplace: '', introduction: '' })
  const [degreeFiles, setDegreeFiles] = useState([])
  const [certificateFiles, setCertificateFiles] = useState([])
  const [otherFiles, setOtherFiles] = useState([])
  const [confirmed, setConfirmed] = useState(false)
  const [error, setError] = useState('')
  const degreeInput = useRef(null)
  const certificateInput = useRef(null)
  const otherInput = useRef(null)
  const update = (event) => setProfile((previous) => ({ ...previous, [event.target.name]: event.target.value }))
  const toggleField = (field) => setProfile((previous) => ({ ...previous, fields: previous.fields.includes(field) ? previous.fields.filter((item) => item !== field) : [...previous.fields, field] }))

  const goNext = (event) => {
    event.preventDefault()
    const form = event.currentTarget
    if (!form.reportValidity()) return
    if (step === 1 && profile.password !== profile.confirmPassword) {
      setError('Mật khẩu xác nhận chưa trùng khớp.')
      return
    }
    if (step === 2 && profile.fields.length === 0) {
      setError('Vui lòng chọn ít nhất một lĩnh vực chuyên môn.')
      return
    }
    setError('')
    setStep((value) => value + 1)
  }

  const submitApplication = (event) => {
    event.preventDefault()
    if (!event.currentTarget.reportValidity()) return
    if (degreeFiles.length === 0) {
      setError('Vui lòng tải lên bằng cấp chuyên môn.')
      return
    }
    if (!confirmed) {
      setError('Vui lòng xác nhận thông tin hồ sơ là chính xác.')
      return
    }
    setError('')
    setStep(4)
  }

  const removeFile = (files, setFiles, ref, index) => {
    setFiles(files.filter((_, fileIndex) => fileIndex !== index))
    if (ref.current) ref.current.value = ''
  }

  if (step === 4) return <section className="account-form-content engineer-success">
    <span className="engineer-success-icon" aria-hidden="true">✓</span>
    <span className="eyebrow">HỒ SƠ KỸ SƯ</span>
    <h1>Đăng ký thành công</h1>
    <p>Hồ sơ của bạn đang chờ Quản trị viên xác minh. Sau khi được phê duyệt, bạn có thể sử dụng các chức năng tư vấn nông nghiệp.</p>
    <strong className="engineer-pending-status"><i /> Đang chờ xác minh</strong>
    <button className="account-submit" type="button" onClick={onBack}>Hoàn tất</button>
  </section>

  return <section className="account-form-content engineer-wizard">
    <button className="account-step-back" type="button" onClick={() => { setError(''); if (step === 1) onBack(); else setStep((value) => value - 1) }}>← {step === 1 ? 'Chọn loại tài khoản' : 'Quay lại bước trước'}</button>
    <div className="engineer-progress" aria-label={`Bước ${step} trên 3`}>{[1, 2, 3].map((item) => <span className={item <= step ? 'is-active' : ''} key={item}><i>{item}</i><b>{['Tài khoản', 'Chuyên môn', 'Xác minh'][item - 1]}</b></span>)}</div>
    {step === 1 && <form className="engineer-step-form" onSubmit={goNext}>
      <h1>Thông tin tài khoản</h1>
      <label>Họ và tên *<input required name="fullName" autoComplete="name" placeholder="Nhập họ và tên" value={profile.fullName} onChange={update} /></label>
      <label>Số điện thoại *<input required name="phone" type="tel" autoComplete="tel" placeholder="Nhập số điện thoại" pattern="(0|\+84)(3|5|7|8|9)[0-9]{8}" title="Nhập số điện thoại Việt Nam hợp lệ, ví dụ 0912345678" value={profile.phone} onChange={update} /></label>
      <label>Email *<input required name="email" type="email" autoComplete="email" placeholder="Nhập địa chỉ email" value={profile.email} onChange={update} /></label>
      <label>Mật khẩu *<PasswordField required name="password" autoComplete="new-password" placeholder="Tối thiểu 8 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt" minLength="8" pattern="(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^A-Za-z0-9]).{8,}" title="Mật khẩu cần ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt" value={profile.password} onChange={update} /></label>
      <label>Xác nhận mật khẩu *<PasswordField required name="confirmPassword" autoComplete="new-password" placeholder="Nhập lại mật khẩu" minLength="8" value={profile.confirmPassword} onChange={update} /></label>
      {error && <p className="account-message account-message--error" role="alert">{error}</p>}
      <div className="engineer-actions"><button className="engineer-secondary" type="button" onClick={onBack}>Quay lại</button><button className="account-submit" type="submit">Tiếp tục</button></div>
    </form>}
    {step === 2 && <form className="engineer-step-form engineer-step-form--expertise" onSubmit={goNext}>
      <h1>Thông tin chuyên môn</h1>
      <label>Chuyên ngành *<select required name="specialty" value={profile.specialty} onChange={update}><option value="">Chọn chuyên ngành</option>{specialties.map((item) => <option key={item}>{item}</option>)}</select></label>
      {profile.specialty === 'Khác' && <label>Chuyên ngành khác *<input required name="specialtyOther" placeholder="Nhập chuyên ngành" value={profile.specialtyOther} onChange={update} /></label>}
      <label>Số năm kinh nghiệm *<input required name="years" type="number" min="0" max="60" placeholder="Ví dụ: 5" value={profile.years} onChange={update} /></label>
      <fieldset className="engineer-fieldset"><legend>Lĩnh vực chuyên môn * <span>(chọn ít nhất một)</span></legend><div className="engineer-specialty-list">{fields.map((field, index) => <label key={field}><input type="checkbox" required={index === 0 && profile.fields.length === 0} checked={profile.fields.includes(field)} onChange={() => toggleField(field)} />{field}</label>)}</div></fieldset>
      {profile.fields.includes('Khác') && <label>Lĩnh vực khác *<input required name="fieldOther" placeholder="Nhập lĩnh vực chuyên môn" value={profile.fieldOther} onChange={update} /></label>}
      <label>Đơn vị công tác<input name="workplace" placeholder="Nhập đơn vị công tác (nếu có)" value={profile.workplace} onChange={update} /></label>
      <label>Giới thiệu chuyên môn<textarea name="introduction" rows="2" placeholder="Chia sẻ kinh nghiệm và lĩnh vực bạn có thể tư vấn" value={profile.introduction} onChange={update} /></label>
      {error && <p className="account-message account-message--error" role="alert">{error}</p>}
      <div className="engineer-actions"><button className="engineer-secondary" type="button" onClick={() => { setError(''); setStep(1) }}>Quay lại</button><button className="account-submit" type="submit">Tiếp tục</button></div>
    </form>}
    {step === 3 && <form className="engineer-step-form engineer-step-form--documents" onSubmit={submitApplication}>
      <h1>Hồ sơ xác minh</h1>
      <p className="engineer-upload-intro">Vui lòng cung cấp giấy tờ chuyên môn để Quản trị viên xác minh hồ sơ.</p>
      <UploadField title="Bằng cấp chuyên môn" required files={degreeFiles} onFiles={setDegreeFiles} onRemove={(index) => removeFile(degreeFiles, setDegreeFiles, degreeInput, index)} inputRef={degreeInput} />
      <UploadField title="Chứng chỉ chuyên môn" files={certificateFiles} onFiles={setCertificateFiles} onRemove={(index) => removeFile(certificateFiles, setCertificateFiles, certificateInput, index)} inputRef={certificateInput} multiple />
      <UploadField title="Giấy tờ xác minh liên quan (nếu có)" files={otherFiles} onFiles={setOtherFiles} onRemove={(index) => removeFile(otherFiles, setOtherFiles, otherInput, index)} inputRef={otherInput} multiple />
      <label className="engineer-confirm"><input required type="checkbox" checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)} />Tôi xác nhận các thông tin cung cấp là chính xác.</label>
      {error && <p className="account-message account-message--error" role="alert">{error}</p>}
      <div className="engineer-actions"><button className="engineer-secondary" type="button" onClick={() => { setError(''); setStep(2) }}>Quay lại</button><button className="account-submit" type="submit">Gửi đăng ký</button></div>
    </form>}
  </section>
}
