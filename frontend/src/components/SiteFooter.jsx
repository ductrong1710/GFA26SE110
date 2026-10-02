import Icon from './Icon'

export default function SiteFooter({ innerPage = false }) {
  const sectionHref = (id) => innerPage ? `/#${id}` : `#${id}`
  return (
    <footer className="site-footer" id="contact">
      <div className="page-width">
        <div className="footer-main"><div><a className="wordmark footer-wordmark" href={innerPage ? '/' : '#home'}><span className="wordmark-mark"><i /><i /><i /></span>Smart Farm</a><p>Dữ liệu tốt hơn cho những mùa vụ bền vững.</p></div><div className="footer-links"><div><strong>NỀN TẢNG</strong><a href={sectionHref('platform')}>Cảm biến & dữ liệu</a><a href={sectionHref('platform')}>Nhiệm vụ UAV</a><a href={sectionHref('platform')}>Đồng bộ ngoại tuyến</a></div><div><strong>KHÁM PHÁ</strong><a href={sectionHref('how-it-works')}>Cách hoạt động</a><a href={sectionHref('about')}>Nông trại kết nối</a><a href={sectionHref('contact')}>Liên hệ</a></div></div><a className="footer-cta" href={sectionHref('contact')}>Kết nối với Smart Farm <Icon name="arrow" size={15} /></a></div>
        <div className="footer-bottom"><span>© 2026 Smart Farm · Nông trại khỏe, tương lai xanh.</span><a href={innerPage ? '/' : '#home'}>Về đầu trang ↑</a></div>
      </div>
    </footer>
  )
}
