import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { ApiError, getPublicProfile, login, register, updateProfile } from './api'
import type { PublicProfile } from './api'
import { useAuth } from './AuthContext'

export function useRoute() {
  const read = () => window.location.hash.startsWith('#/') ? window.location.hash.slice(1) : '/'
  const [route, setRoute] = useState(read)
  useEffect(() => { const changed = () => { setRoute(read()); if (window.location.hash.startsWith('#/')) window.scrollTo(0, 0) }; window.addEventListener('hashchange', changed); return () => window.removeEventListener('hashchange', changed) }, [])
  return route
}

export function AccountNavigation() {
  const { user, error, retry } = useAuth()
  if (error) return <button className="text-button" onClick={retry}>Повторить проверку входа</button>
  if (user === undefined) return <span className="account-loading" role="status">Проверяем вход…</span>
  return <nav className="account-nav" aria-label="Аккаунт">{user
    ? <><a href="#/sessions">Все игры</a><a href="#/my-games">Мои игры</a><a href="#/contributions">Мои обращения</a>{user.isModerator && <a href="#/moderation">Модерация</a>}<a className="account-link" href="#/me">Мой профиль</a></>
    : <><a href="#/sessions">Игры</a><a href="#/login">Войти</a><a className="account-link" href="#/register">Создать аккаунт</a></>}</nav>
}

export function AccountPage({ route }: { route: string }) {
  const { user, error, retry } = useAuth()
  const [path, query = ''] = route.split('?')
  const requestedReturn = new URLSearchParams(query).get('returnTo')
  const returnTo = requestedReturn?.startsWith('/') && !requestedReturn.startsWith('//') && !requestedReturn.startsWith('/login') && !requestedReturn.startsWith('/register')
    ? requestedReturn : '/me'
  if (route.startsWith('/profiles/')) return <PublicProfilePage id={route.slice('/profiles/'.length)} />
  if (error) return <section className="account-panel"><h1>Аккаунт недоступен</h1><p role="alert">{error}</p><button className="retry" onClick={retry}>Повторить</button></section>
  if (user === undefined) return <p className="notice" role="status">Проверяем вход…</p>
  if (route === '/me') return user ? <MyProfile /> : <section className="account-panel"><h1>Войди в аккаунт</h1><p>Чтобы открыть свой профиль, нужно войти.</p><a className="primary-link" href="#/login">Войти</a></section>
  if (path === '/login' || path === '/register') return user
    ? <section className="account-panel"><h1>Ты уже в аккаунте</h1><a className="primary-link" href={`#${returnTo}`}>{returnTo === '/me' ? 'Открыть профиль' : 'Вернуться к действию'}</a></section>
    : <AuthForm key={route} mode={path === '/register' ? 'register' : 'login'} returnTo={returnTo} />
  return <section className="account-panel"><h1>Страница не найдена</h1><a href="#/">Вернуться к активностям</a></section>
}

function AuthForm({ mode, returnTo }: { mode: 'login' | 'register'; returnTo: string }) {
  const { setUser } = useAuth()
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<ApiError | null>(null)
  const signingUp = mode === 'register'
  const fieldError = (name: string) => failure?.fields[name]?.join(' ')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy) return
    setFailure(null)
    if (signingUp && password !== confirmation) { setFailure(new ApiError('Пароли не совпадают.', { confirmation: ['Повтори тот же пароль.'] })); return }
    if (signingUp && (!/[A-Z]/.test(password) || !/[a-z]/.test(password) || !/[0-9]/.test(password))) {
      setFailure(new ApiError('Проверь пароль.', { password: ['Добавь заглавную и строчную латинские буквы, а также цифру.'] })); return
    }
    setBusy(true)
    try {
      const user = signingUp ? await register({ email, password, displayName }) : await login({ email, password })
      setUser(user)
      window.location.hash = returnTo
    } catch (error) { setFailure(error instanceof ApiError ? error : new ApiError('Не удалось выполнить действие.')) }
    finally { setBusy(false) }
  }

  return <section className="account-panel" aria-labelledby="account-title">
    <a className="back-link" href="#/">← К активностям</a>
    <p className="eyebrow">Твоя компания для игры</p>
    <h1 id="account-title">{signingUp ? 'Создать аккаунт' : 'С возвращением'}</h1>
    <p className="form-intro">{signingUp ? 'Другие игроки увидят твой псевдоним. Email останется приватным.' : 'Войди, чтобы открыть свой профиль.'}</p>
    <form onSubmit={submit} aria-busy={busy}>
      <fieldset disabled={busy}>
        {signingUp && <div className="form-field"><label htmlFor="displayName">Псевдоним</label><input id="displayName" name="displayName" autoComplete="nickname" required minLength={2} maxLength={60} value={displayName} onChange={e => setDisplayName(e.target.value)} aria-invalid={!!fieldError('displayName')} aria-describedby={fieldError('displayName') ? 'displayName-error' : undefined} />{fieldError('displayName') && <span id="displayName-error" className="field-error">{fieldError('displayName')}</span>}</div>}
        <div className="form-field"><label htmlFor="email">Email</label><input id="email" name="email" type="email" autoComplete="email" required maxLength={254} value={email} onChange={e => setEmail(e.target.value)} aria-invalid={!!fieldError('email')} aria-describedby={fieldError('email') ? 'email-error' : undefined} />{fieldError('email') && <span id="email-error" className="field-error">{fieldError('email')}</span>}</div>
        <div className="form-field"><label htmlFor="password">Пароль</label><input id="password" name="password" type="password" autoComplete={signingUp ? 'new-password' : 'current-password'} required minLength={signingUp ? 10 : undefined} maxLength={128} value={password} onChange={e => setPassword(e.target.value)} aria-invalid={!!fieldError('password')} aria-describedby={[signingUp ? 'password-hint' : '', fieldError('password') ? 'password-error' : ''].filter(Boolean).join(' ') || undefined} />{signingUp && <span id="password-hint" className="field-hint">10–128 символов, заглавная и строчная латинские буквы, цифра.</span>}{fieldError('password') && <span id="password-error" className="field-error">{fieldError('password')}</span>}</div>
        {signingUp && <div className="form-field"><label htmlFor="confirmation">Повтори пароль</label><input id="confirmation" name="confirmation" type="password" autoComplete="new-password" required maxLength={128} value={confirmation} onChange={e => setConfirmation(e.target.value)} aria-invalid={!!fieldError('confirmation')} aria-describedby={fieldError('confirmation') ? 'confirmation-error' : undefined} />{fieldError('confirmation') && <span id="confirmation-error" className="field-error">{fieldError('confirmation')}</span>}</div>}
        {failure && <p className="form-error" role="alert">{failure.message}</p>}
        <button className="submit-button" type="submit">{busy ? 'Подожди…' : signingUp ? 'Зарегистрироваться' : 'Войти'}</button>
      </fieldset>
    </form>
    <p className="form-switch">{signingUp ? 'Уже есть аккаунт?' : 'Первый раз здесь?'} <a href={`${signingUp ? '#/login' : '#/register'}${returnTo === '/me' ? '' : `?returnTo=${encodeURIComponent(returnTo)}`}`}>{signingUp ? 'Войти' : 'Создать аккаунт'}</a></p>
  </section>
}

function MyProfile() {
  const { user, setUser, signOut } = useAuth()
  const [name, setName] = useState(user?.displayName ?? '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (busy) return
    setBusy(true); setError(''); setSuccess('')
    try { const updated = await updateProfile(name); setUser(updated); setName(updated.displayName); setSuccess('Профиль сохранён.') }
    catch (failure) { if (failure instanceof ApiError && failure.status === 401) setUser(null); else setError(failure instanceof Error ? failure.message : 'Не удалось сохранить профиль.') }
    finally { setBusy(false) }
  }
  async function leave() {
    if (busy) return
    setBusy(true); setError(''); setSuccess('')
    try { await signOut(); window.location.hash = '/' }
    catch (failure) { if (failure instanceof ApiError && failure.status === 401) setUser(null); else setError(failure instanceof Error ? failure.message : 'Не удалось выйти.'); setBusy(false) }
  }
  if (!user) return null
  return <section className="account-panel" aria-labelledby="profile-title"><a className="back-link" href="#/">← К активностям</a><p className="eyebrow">Личный кабинет</p><h1 id="profile-title">Мой профиль</h1>
    <div className="private-email"><span>Email · виден только тебе</span><strong>{user.email}</strong></div>
    <form onSubmit={save} aria-busy={busy}><fieldset disabled={busy}><div className="form-field"><label htmlFor="profile-name">Публичный псевдоним</label><input id="profile-name" name="displayName" autoComplete="nickname" required minLength={2} maxLength={60} value={name} onChange={e => { setName(e.target.value); setSuccess('') }} /><span className="field-hint">От 2 до 60 символов. Этот псевдоним увидят другие игроки.</span></div><button className="submit-button" type="submit">{busy ? 'Подожди…' : 'Сохранить профиль'}</button></fieldset></form>
    {error && <p className="form-error" role="alert">{error}</p>}{success && <p className="form-success" role="status">{success}</p>}
    <a className="public-profile-link" href={`#/profiles/${user.id}`}>Посмотреть публичный профиль ↗</a>
    <button className="logout-button" disabled={busy} onClick={leave}>Выйти из аккаунта</button>
  </section>
}

function PublicProfilePage({ id }: { id: string }) {
  const [profile, setProfile] = useState<PublicProfile | null>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    const controller = new AbortController(); setProfile(null); setError('')
    getPublicProfile(id, controller.signal).then(setProfile).catch((failure: unknown) => {
      if (!controller.signal.aborted) setError(failure instanceof Error ? failure.message : 'Не удалось загрузить профиль.')
    })
    return () => controller.abort()
  }, [id])
  return <section className="account-panel"><a className="back-link" href="#/">← К активностям</a><p className="eyebrow">Публичный профиль</p>
    {error ? <><h1>Профиль недоступен</h1><p role="alert">{error}</p></> : profile ? <><div className="profile-avatar" aria-hidden="true">{profile.displayName.slice(0, 1).toLocaleUpperCase()}</div><h1 className="public-name">{profile.displayName}</h1><p className="form-intro">Игрок SteamIRL</p></> : <p role="status">Загружаем профиль…</p>}
  </section>
}
