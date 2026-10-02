import Icon from './Icon'

export default function HeroSection() {
  return (
    <section className="hero-section" id="home">
      <div className="hero-image" />
      <video className="hero-video" autoPlay muted loop playsInline preload="auto" poster="https://images.unsplash.com/photo-1686683189848-31dc37ae4ed4?auto=format&fit=crop&w=1200&q=75" aria-hidden="true">
        <source src="https://videos.pexels.com/video-files/6984744/6984744-sd_960_540_30fps.mp4" type="video/mp4" />
      </video>
      <div className="hero-shade" />
      <div className="hero-copy">
        <span className="eyebrow hero-eyebrow">NÔNG NGHIỆP KẾT NỐI · DỮ LIỆU TỪ TRÊN CAO</span>
        <h1>Nông trại khỏe,<br /><em>tương lai xanh.</em></h1>
        <p>Kết nối cảm biến, dữ liệu và những chuyến bay UAV để chăm sóc từng tấc đất tốt hơn.</p>
        <div className="hero-actions">
          <a className="hero-pill hero-pill-primary" href="#platform">Khám phá nền tảng <Icon name="arrow" size={15} /></a>
          <a className="hero-pill hero-pill-secondary" href="#how-it-works">Xem cách hoạt động <Icon name="arrow" size={15} /></a>
        </div>
      </div>
      <a className="scroll-cue" href="#sensor-band"><span>CUỘN ĐỂ KHÁM PHÁ</span><i /></a>
    </section>
  )
}
