import { useEffect } from 'react'

const appName = 'Task Management'

export function useDocumentTitle(title: string): void {
  useEffect(() => {
    document.title = `${title} · ${appName}`
  }, [title])
}