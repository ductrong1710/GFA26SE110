import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../services/api'

function PaymentStatus({ token }) {
  const [status, setStatus] = useState(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [refresh, setRefresh] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    let timer
    let attempts = 0
    async function load() {
      setLoading(true); setError('')
      try {
        const result = await api.request('/api/payments/vnpay/status', {
          method: 'GET', query: { token }, auth: false, retry: false, signal: controller.signal,
        })
        if (controller.signal.aborted) return
        setStatus(result.status)
        // Bound automatic polling to one minute; manual refresh starts a new check.
        if (result.status === 'PENDING' && ++attempts < 12) timer = setTimeout(load, 5000)
      } catch (err) { if (!controller.signal.aborted) setError(err.message) }
      finally { if (!controller.signal.aborted) setLoading(false) }
    }
    load()
    return () => { controller.abort(); clearTimeout(timer) }
  }, [token, refresh])
  return <>
    {!error && <p role="status">{status === 'SUCCEEDED' ? 'Thanh toán thử nghiệm thành công'
      : status === 'FAILED' ? 'Thanh toán thử nghiệm không thành công'
        : status === 'UNKNOWN' || status === 'RECONCILIATION_REQUIRED' ? 'Chưa xác định được kết quả thanh toán'
          : 'Đang xác nhận thanh toán'}</p>}
    {['UNKNOWN', 'RECONCILIATION_REQUIRED'].includes(status) && <p>Giao dịch cần được đối soát. Vui lòng liên hệ nhân viên hỗ trợ.</p>}
    {error && <p role="alert">{error}</p>}
    {status === 'PENDING' && !error && <p>Hệ thống đang chờ VNPay xác nhận giao dịch.</p>}
    <button type="button" className="account-submit" disabled={loading} onClick={() => setRefresh(value => value + 1)}>Cập nhật trạng thái</button>
  </>
}

export default function VnpayReturnPage() {
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  return <main className="account-screen"><section className="account-form-panel"><div className="account-form-content">
    <h1>Kết quả VNPAY Sandbox</h1>
    <p>Chỉ dùng thử nghiệm, không thanh toán bằng tiền thật.</p>
    {/^[a-fA-F0-9]{64}$/.test(token) ? <PaymentStatus key={token} token={token} />
      : <p role="alert">Liên kết thanh toán không hợp lệ hoặc thiếu mã tra cứu.</p>}
  </div></section></main>
}
