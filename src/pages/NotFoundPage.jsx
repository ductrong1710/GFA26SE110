import { Link } from 'react-router-dom'
import SiteHeader from '../components/SiteHeader'
import SiteFooter from '../components/SiteFooter'

export default function NotFoundPage({ application = false }) {
  const content = <section className="product-not-found">
    <h1>404 · Không tìm thấy trang</h1>
    <Link to={application ? '/app/dashboard' : '/'}>{application ? 'Về tổng quan' : 'Trở về trang chủ'}</Link>
  </section>

  return application ? content : <><SiteHeader innerPage /><main>{content}</main><SiteFooter innerPage /></>
}
