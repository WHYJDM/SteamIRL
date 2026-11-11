export interface LocationActivity { id: string; slug: string; name: string }
export interface SportLocation {
  id: string; name: string; description: string; address: string
  latitude: number; longitude: number; updatedAt: string
  sourceUrl: string; sourceCheckedAt: string; coordinateNote: string; accessNotes: string
  activities: LocationActivity[]; revision: number
}
export interface LocationPage { items: SportLocation[]; page: number; pageSize: number; hasMore: boolean }
export interface Bounds { minLat: number; minLon: number; maxLat: number; maxLon: number }

async function read<T>(path: string, signal: AbortSignal): Promise<T> {
  let response: Response
  try { response = await fetch(path, { signal, headers: { Accept: 'application/json' } }) }
  catch (error) { if (signal.aborted) throw error; throw new Error('Нет связи с сервером. Попробуй загрузить площадки ещё раз.') }
  if (response.status === 404) throw new Error('Площадка или активность не найдена.')
  if (!response.ok) throw new Error('Не удалось загрузить площадки. Попробуй ещё раз позже.')
  return response.json() as Promise<T>
}
export function getLocations(activityId: string, signal: AbortSignal, page = 1, bounds?: Bounds) {
  const query = new URLSearchParams({ activityId, page: String(page), pageSize: bounds ? '100' : '50' })
  if (bounds) for (const [key, value] of Object.entries(bounds)) query.set(key, value.toFixed(6))
  return read<LocationPage>(`/api/locations?${query}`, signal)
}
export const getLocation = (id: string, signal: AbortSignal) => read<SportLocation>(`/api/locations/${encodeURIComponent(id)}`, signal)
export const locationDate = (date: string) => new Intl.DateTimeFormat('ru', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'Europe/Vilnius' }).format(new Date(date))
