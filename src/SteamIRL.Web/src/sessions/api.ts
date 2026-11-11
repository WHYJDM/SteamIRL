import { ApiError } from '../accounts/api'

export type SessionKind = 'Planned' | 'Immediate'
export type SkillLevel = 'Beginner' | 'Intermediate' | 'Advanced'
export type Equipment = 'NotSpecified' | 'BallAvailable' | 'BallNeeded'
export interface GameSession {
  id: string; locationId: string; locationName: string; activityId: string; activityName: string; activitySlug: string
  organiserId: string; organiserName: string; kind: SessionKind; skillLevel: SkillLevel; equipmentStatus: Equipment
  capacity: number; participantCount: number; remainingPlaces: number; startsAt: string; endsAt: string
  status: 'Open' | 'Full' | 'Expired' | 'Cancelled'; isJoinable: boolean; isOrganiser: boolean; isParticipating: boolean; serverNow: string
  cancellationReason: string | null
}
export interface SessionPage { items: GameSession[]; page: number; pageSize: number; hasMore: boolean }
export interface CreateSession {
  locationId: string; activityId: string; kind: SessionKind; skillLevel: SkillLevel; equipmentStatus: Equipment; capacity: number
  startsAt?: string; endsAt?: string; durationMinutes?: number
}
export class SessionError extends ApiError {
  constructor(message: string, status = 0, public existingSessionId?: string) { super(message, {}, status) }
}
async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response
  try { response = await fetch(path, { ...options, credentials: 'same-origin' }) }
  catch (failure) { if (options.signal?.aborted) throw failure; throw new SessionError('Нет связи с сервером. Повтори действие: повторная отправка не займёт второе место.') }
  if (!response.ok) {
    const body = await response.json().catch(() => ({})) as { title?: string; existingSessionId?: string }
    throw new SessionError(response.status === 401 ? 'Войди в аккаунт, чтобы продолжить.'
      : response.status === 404 ? 'Игра или выбранная площадка не найдена.'
      : response.status >= 500 ? 'Сервис временно недоступен. Повтори попытку позже.'
      : body.title ?? 'Не удалось выполнить действие.', response.status, body.existingSessionId)
  }
  return response.json() as Promise<T>
}
export const getGame = (id: string, signal: AbortSignal) => request<GameSession>(`/api/sessions/${encodeURIComponent(id)}`, { signal })
export const getGames = (query: string, signal: AbortSignal, mine = false) => request<SessionPage>(`${mine ? '/api/me/sessions' : '/api/sessions'}?${query}`, { signal })
export async function command(path: string, method: string, data: unknown, key: string) {
  const { token } = await request<{ token: string }>('/api/auth/csrf')
  return request<GameSession>(path, { method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token, 'Idempotency-Key': key },
    body: data === undefined ? undefined : JSON.stringify(data) })
}
export const levels: Record<SkillLevel, string> = { Beginner: 'Начинающий', Intermediate: 'Средний', Advanced: 'Продвинутый' }
export const equipment: Record<Equipment, string> = { NotSpecified: 'Не указан', BallAvailable: 'Мяч есть', BallNeeded: 'Нужен мяч' }
export const statuses: Record<GameSession['status'], string> = { Open: 'Набор открыт', Full: 'Мест нет', Expired: 'Завершена', Cancelled: 'Отменена' }
