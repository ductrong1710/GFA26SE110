import FeatureSection from '../components/FeatureSection'
import HeroSection from '../components/HeroSection'
import ResourceSection from '../components/ResourceSection'
import RentalSection from '../components/RentalSection'
import SensorBand from '../components/SensorBand'
import SiteFooter from '../components/SiteFooter'
import SiteHeader from '../components/SiteHeader'
import WorkflowSection from '../components/WorkflowSection'

export default function HomePage() {
  return (
    <>
      <SiteHeader />
      <main>
        <HeroSection />
        <SensorBand />
        <FeatureSection />
        <WorkflowSection />
        <RentalSection />
        <ResourceSection />
      </main>
      <SiteFooter />
    </>
  )
}
