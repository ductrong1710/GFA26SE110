import { Link } from 'react-router-dom'
import Icon from './Icon'

export default function SiteFooter({ innerPage = false }) {
  const sectionHref = (id) => innerPage ? `/#${id}` : `#${id}`
  return (
    <footer className="site-footer" id="contact">
      <div className="page-width">
        <div className="footer-main"><div><Link className="wordmark footer-wordmark" to={innerPage ? '/' : '#home'}><span className="wordmark-mark"><i /><i /><i /></span>Smart Farm</Link><p>Dữ liệu tốt hơn cho những mùa vụ bền vững.</p></div><div className="footer-links"><div><strong>NỀN TẢNG</strong><Link to={sectionHref('platform')}>Cảm biến & dữ liệu</Link><Link to={sectionHref('platform')}>Nhiệm vụ UAV</Link><Link to={sectionHref('platform')}>Đồng bộ ngoại tuyến</Link></div><div><strong>KHÁM PHÁ</strong><Link to={sectionHref('how-it-works')}>Cách hoạt động</Link><Link to={sectionHref('about')}>Nông trại kết nối</Link><Link to={sectionHref('contact')}>Liên hệ</Link></div></div><Link className="footer-cta" to={sectionHref('contact')}>Kết nối với Smart Farm <Icon name="arrow" size={15} /></Link></div>
        <div className="footer-bottom"><span>© 2026 Smart Farm · Nông trại khỏe, tương lai xanh.</span><Link to={innerPage ? '/' : '#home'}>Về đầu trang ↑</Link></div>
      </div>
    </footer>
  )
}
