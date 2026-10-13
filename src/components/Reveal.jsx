import { useEffect, useRef } from 'react'

export default function Reveal({ children, className = '', delay = 0, ...props }) {
  const ref = useRef(null)
  useEffect(() => {
    const node = ref.current
    if (!node) return
    if (!('IntersectionObserver' in window) || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      node.classList.add('is-visible')
      return
    }
    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) {
        node.classList.add('is-visible')
        observer.unobserve(node)
      }
    }, { threshold: 0.16, rootMargin: '0px 0px -30px 0px' })
    observer.observe(node)
    return () => observer.disconnect()
  }, [])
  return <div ref={ref} {...props} className={`reveal ${className}`} style={{ '--reveal-delay': `${delay}ms`, ...props.style }}>{children}</div>
}
