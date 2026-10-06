import { useCallback, useEffect, useRef, useState } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import AppSidebar from '../components/app/AppSidebar'
import AppTopbar from '../components/app/AppTopbar'
import { createFarmWorkspace, updateFarmWorkspace } from '../data/mock/farmWorkspace'
import { useAuth } from '../context/AuthContext'
import { PERMISSIONS } from '../config/permissions'
import '../styles/app-shell.css'
import '../styles/app-tokens.css'

const mobileQuery = '(max-width: 1023px)'

export default function AppShell() {
  const { user, can } = useAuth()
  const [farmWorkspace, setFarmWorkspace] = useState(createFarmWorkspace)
  const mockFarms = farmWorkspace.farms
  const [mobile, setMobile] = useState(() => window.matchMedia(mobileQuery).matches)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [farmId, setFarmId] = useState(mockFarms[0].id)
  const sidebarRef = useRef(null)
  const menuButtonRef = useRef(null)
  const mainRef = useRef(null)
  const location = useLocation()
  const [lastLocationKey, setLastLocationKey] = useState(location.key)
  // Reset transient drawer state on navigation without remounting the shell.
  if (lastLocationKey !== location.key) {
    setLastLocationKey(location.key)
    setDrawerOpen(false)
  }
  const closeDrawer = useCallback(() => setDrawerOpen(false), [])
  const currentFarm = mockFarms.find((farm) => farm.id === farmId) ?? mockFarms[0] ?? { id: '', name: 'No farms' }
  const changeFarmRecord = (change) => {
    const next = updateFarmWorkspace(farmWorkspace, { ...change, userId: user.id }, can(PERMISSIONS.FARMS_MANAGE))
    setFarmWorkspace(next)
    return next
  }
  const showDrawer = mobile && drawerOpen

  useEffect(() => {
    const media = window.matchMedia(mobileQuery)
    const handleChange = () => { setMobile(media.matches); setDrawerOpen(false) }
    media.addEventListener('change', handleChange)
    return () => media.removeEventListener('change', handleChange)
  }, [])

  useEffect(() => {
    mainRef.current?.scrollTo({ top: 0, left: 0, behavior: 'instant' })
  }, [location.key])

  useEffect(() => {
    if (!showDrawer) return
    const sidebar = sidebarRef.current
    const menuButton = menuButtonRef.current
    const getFocusable = () => [...sidebar.querySelectorAll('a[href], button:not([disabled])')]
      .filter((element) => element.getClientRects().length > 0)
    sidebar.querySelector('.app-sidebar-close').focus()
    const handleKeyDown = (event) => {
      if (event.key === 'Escape') { event.preventDefault(); closeDrawer() }
      if (event.key !== 'Tab') return
      const focusable = getFocusable()
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('keydown', handleKeyDown)
      if (window.matchMedia(mobileQuery).matches) menuButton?.focus()
    }
  }, [showDrawer, closeDrawer])

  return <div className="app-shell">
    <AppSidebar sidebarRef={sidebarRef} mobile={mobile} open={showDrawer} onClose={closeDrawer} />
    {showDrawer && <button type="button" className="app-sidebar-backdrop" tabIndex={-1} aria-label="Close navigation overlay" onClick={closeDrawer} />}
    <div className="app-workspace" inert={showDrawer}>
      <a className="app-skip-link" href="#app-main">Skip to content</a>
      <AppTopbar menuButtonRef={menuButtonRef} drawerOpen={showDrawer} onOpenMenu={() => setDrawerOpen(true)}
        farms={mockFarms} currentFarm={currentFarm} onFarmChange={setFarmId} />
      <main ref={mainRef} id="app-main" className="app-main" tabIndex={-1}>
        <Outlet context={{ currentFarm, selectFarm: setFarmId, farmWorkspace, changeFarmRecord }} />
      </main>
    </div>
  </div>
}
