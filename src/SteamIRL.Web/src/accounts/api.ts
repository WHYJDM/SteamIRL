export interface Account { id: string; email: string; displayName: string; isModerator: boolean }
export interface PublicProfile { id: string; displayName: string }

export class ApiError extends Error {
  constructor(message: string, public fields: Record<string, string[]> = {}, public status = 0) { super(message) }
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response
  try { response = await fetch(path, { ...options, credentials: 'same-origin' }) }
  catch (error) {
    if (options.signal?.aborted) throw error
    throw new ApiError('Нет связи с сервером. Проверь соединение и повтори попытку.')
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => ({})) as { title?: string; errors?: Record<string, string[]> }
    const message = response.status === 429 ? 'Слишком много попыток. Подожди минуту и попробуй снова.'
      : response.status === 503 ? 'Сервис временно недоступен. Попробуй позже.'
      : response.status === 404 ? 'Профиль не найден.'
      : response.status === 401 && !path.endsWith('/login') ? 'Сессия завершена. Войди в аккаунт снова.'
      : problem.errors ? 'Проверь заполненные поля.'
      : problem.title ?? 'Не удалось выполнить действие. Попробуй ещё раз.'
    throw new ApiError(message, problem.errors, response.status)
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}

async function mutate<T>(path: string, method: string, data?: unknown): Promise<T> {
  const { token } = await request<{ token: string }>('/api/auth/csrf')
  return request<T>(path, { method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token },
    body: data === undefined ? undefined : JSON.stringify(data) })
}

export const getSession = (signal?: AbortSignal) => request<{ user: Account | null }>('/api/auth/session', { signal })
export const register = (data: { email: string; password: string; displayName: string }) => mutate<Account>('/api/auth/register', 'POST', data)
export const login = (data: { email: string; password: string }) => mutate<Account>('/api/auth/login', 'POST', data)
export const logout = () => mutate<void>('/api/auth/logout', 'POST')
export const updateProfile = (displayName: string) => mutate<Account>('/api/me/profile', 'PATCH', { displayName })
export const getPublicProfile = (id: string, signal: AbortSignal) => request<PublicProfile>(`/api/profiles/${encodeURIComponent(id)}`, { signal })
