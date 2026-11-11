import { ApiError } from '../accounts/api'
import type { LocationActivity } from '../locations/api'

export interface Snapshot {
  name: string; description: string; address: string; latitude: number; longitude: number
  sourceUrl: string; coordinateNote: string; accessNotes: string; activityIds: string[]; revision: number; status: string
}
export interface Decision { id: string; action: string; reason: string; decidedAt: string; moderatorName: string; affectedSessions: number }
export interface Submission {
  id: string; locationId: string; kind: 'New' | 'Correction'; state: string; authorName: string
  baseRevision: number; currentRevision: number; locationStatus: string; proposed: Snapshot; original: Snapshot | null
  activities: LocationActivity[]; evidence: string; submittedAt: string; decisions: Decision[]
}
export interface Report {
  id: string; locationId: string | null; sessionId: string | null; targetName: string; authorName: string
  reason: string; details: string; state: string; submittedAt: string; decisions: Decision[]
}
export interface Page<T> { items: T[]; page: number; pageSize: number; hasMore: boolean }
export interface SubmissionDetail { submission: Submission; nearbyLocations: { id: string; name: string; address: string }[] }
export const states: Record<string, string> = { Pending: 'Ожидает проверки', Approved: 'Одобрено', Rejected: 'Отклонено', Open: 'Открыта', Resolved: 'Рассмотрена', Dismissed: 'Отклонена' }
export const actions: Record<string, string> = { Approve: 'Одобрить', Reject: 'Отклонить', Resolve: 'Рассмотрено без снятия', Dismiss: 'Отклонить жалобу', RemoveContent: 'Снять с публикации' }
export const reasons: Record<string, string> = { Inaccurate: 'Неточные сведения', Spam: 'Спам', Abuse: 'Оскорбления', AccessIssue: 'Проблема с доступом', Safety: 'Безопасность', Other: 'Другое' }
export class ContributionError extends ApiError {
  constructor(message: string, fields: Record<string, string[]> = {}, status = 0, public existingSubmissionId?: string, public existingReportId?: string) { super(message, fields, status) }
}
export async function read<T>(path: string, signal?: AbortSignal, options: RequestInit = {}): Promise<T> {
  let response: Response
  try { response = await fetch(path, { ...options, signal, credentials: 'same-origin', cache: 'no-store' }) }
  catch (failure) { if (signal?.aborted) throw failure; throw new ContributionError('Нет связи с сервером. Повтори попытку: отправленные данные сохраняются в форме.') }
  if (!response.ok) {
    const problem = await response.json().catch(() => ({})) as { title?: string; errors?: Record<string, string[]>; existingSubmissionId?: string; existingReportId?: string }
    const message = response.status === 401 ? 'Войди в аккаунт, чтобы продолжить.' : response.status === 403 ? 'Для этого действия нужны права модератора.'
      : response.status === 404 ? 'Обращение или площадка недоступны.' : response.status === 429 ? 'Слишком много обращений. Попробуй позже.'
      : response.status >= 500 ? 'Сервер временно недоступен. Повтори попытку.' : problem.title ?? 'Проверь введённые данные.'
    throw new ContributionError(message, problem.errors, response.status, problem.existingSubmissionId, problem.existingReportId)
  }
  return response.json() as Promise<T>
}
export async function command<T>(path: string, payload: unknown, key: string): Promise<T> {
  const { token } = await read<{ token: string }>('/api/auth/csrf')
  return read<T>(path, undefined, { method: 'POST', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token, 'Idempotency-Key': key }, body: JSON.stringify(payload) })
}
