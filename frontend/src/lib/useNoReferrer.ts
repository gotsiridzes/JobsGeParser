import { useEffect } from 'react'

/**
 * Pages under /a/* carry a capability token in their path. While one is mounted, tell the
 * browser to send no Referer at all, so the token never reaches a third party (fonts, links).
 */
export function useNoReferrer() {
  useEffect(() => {
    const meta = document.createElement('meta')
    meta.name = 'referrer'
    meta.content = 'no-referrer'
    document.head.appendChild(meta)
    return () => {
      meta.remove()
    }
  }, [])
}
