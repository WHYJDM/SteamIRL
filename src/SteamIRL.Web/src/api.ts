export interface Activity {
  id: string
  slug: string
  name: string
  description: string
}

export async function getActivities(signal: AbortSignal): Promise<Activity[]> {
  const response = await fetch('/api/activities', { signal, headers: { Accept: 'application/json' } })
  if (!response.ok) throw new Error('Не удалось загрузить активности. Попробуй ещё раз чуть позже.')
  return response.json() as Promise<Activity[]>
}
