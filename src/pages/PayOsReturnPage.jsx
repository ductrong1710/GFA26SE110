import { useEffect, useState } from 'react'
import { useLocation, useSearchParams } from 'react-router-dom'
import { api } from '../services/api'

const labels = {
  SUCCEEDED: 'PayOS đã xác nhận thanh toán thành công.',
  FAILED: 'Giao dịch chưa hoàn tất. Bạn có thể quay lại link thanh toán hoặc liên hệ nhân viên tư vấn.',
  EXPIRED: 'Link thanh toán đã hết hạn. Vui lòng liên hệ nhân viên tư vấn để được hỗ trợ.',
  RECONCILIATION_REQUIRED: 'Chưa xác định được kết quả thanh toán. Nhân viên hỗ trợ sẽ kiểm tra giao dịch.',
  PENDING: 'Đang chờ PayOS xác nhận giao dịch.',
  CREATING: 'Đang kiểm tra giao dịch với PayOS.',
}

export default function PayOsReturnPage({ cancelled = false }) {
  const [params] = useSearchParams()
  const location = useLocation()
  const token = params.get('token') ?? ''
  const [status, setStatus] = useState(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [refresh, setRefresh] = useState(0)

  useEffect(() => {
    if (!/^[a-fA-F0-9]{64}$/.test(token)) { setLoading(false); return undefined }
    const controller = new AbortController()
    let timer
    let attempts = 0
    async function load() {
      setLoading(true); setError('')
      try {
        const result = await api.request(`/api/payments/payos/public/${token}`, {
          method: 'GET', auth: false, retry: false, signal: controller.signal,
        })
        if (controller.signal.aborted) return
        setStatus(result.status)
        if (['CREATING', 'PENDING'].includes(result.status) && ++attempts < 12)
          timer = setTimeout(load, 5000)
      } catch (err) { if (!controller.signal.aborted) setError(err.message) }
      finally { if (!controller.signal.aborted) setLoading(false) }
    }
    load()
    return () => { controller.abort(); clearTimeout(timer) }
  }, [token, refresh, location.pathname])

  const title = cancelled ? 'Thanh toán chưa hoàn tất' : 'Trạng thái thanh toán PayOS'
  const message = status === 'SUCCEEDED' ? labels.SUCCEEDED
    : cancelled && ['PENDING', 'CREATING'].includes(status) ? 'Bạn đã quay lại trước khi thanh toán hoàn tất. Trạng thái vẫn đang được kiểm tra.'
      : labels[status] ?? (error ? 'Không thể tra cứu trạng thái lúc này.' : 'Đang xác minh trạng thái thanh toán.')

  return <main className="account-screen"><section className="account-form-panel"><div className="account-form-content">
    <h1>{title}</h1>
    {/^[a-fA-F0-9]{64}$/.test(token) ? <>
      {!error && <p role="status">{message}</p>}
      {error && <p role="alert">{error}</p>}
      {status === 'RECONCILIATION_REQUIRED' && <p>Vui lòng không thanh toán lại cho đến khi nhân viên xác nhận kết quả.</p>}
      <button type="button" className="account-submit" disabled={loading} onClick={() => setRefresh(value => value + 1)}>Cập nhật trạng thái</button>
    </> : <p role="alert">Liên kết tra cứu không hợp lệ hoặc đã hết hạn.</p>}
  </div></section></main>
}
