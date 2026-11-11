import { useCallback, useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from '../accounts/AuthContext'
import { getGames, levels, statuses } from './api'
import type { GameSession } from './api'
import { usePolling, useServerNow } from './hooks'
import { sessionTime, vilniusToIso } from './time'
import './styles.css'

interface ActivityOption { id: string; name: string; slug: string }
export function SessionCard({ game, history = false }: { game: GameSession; history?: boolean }) {
  const now = useServerNow(game.serverNow)
  const status = game.status === 'Cancelled' ? 'Cancelled' : Date.parse(game.endsAt) <= now ? 'Expired' : game.status
  return <article className="game-card">
    <div className="game-badges"><span className={`game-status status-${status}`}>{statuses[status]}</span><span>{game.kind === 'Immediate' ? 'Играем сейчас' : 'Запланировано'}</span></div>
    <h3><a href={`#/sessions/${game.id}`}>{game.activityName} · {game.locationName}</a></h3>
    <p>{sessionTime(game.startsAt)} — {sessionTime(game.endsAt)}</p><p>{levels[game.skillLevel]} · Свободно {game.remainingPlaces} из {game.capacity} дополнительных мест</p>
    {game.isOrganiser ? <p className="game-relationship">Ты организатор</p> : game.isParticipating ? <p className="game-relationship">{status === 'Cancelled' || status === 'Expired' ? 'Твоя запись сохранена в истории' : 'Ты записан на игру'}</p> : history ? <p className="game-relationship">Ты вышел из игры</p> : null}
    <a className="source-link" href={`#/sessions/${game.id}`}>Открыть игру ↗</a>
  </article>
}

export function GamesList({ activities = [], locationId, defaultActivityId, mine = false }: {
  activities?: ActivityOption[]; locationId?: string; defaultActivityId?: string; mine?: boolean
}) {
  const { user } = useAuth()
  const [activityId, setActivityId] = useState(defaultActivityId ?? '')
  const [level, setLevel] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [onlyFree, setOnlyFree] = useState(false)
  const [filters, setFilters] = useState(defaultActivityId ? `activityId=${defaultActivityId}` : '')
  const [page, setPage] = useState(1)
  const [formError, setFormError] = useState('')
  const query = new URLSearchParams(filters)
  query.set('page', String(page)); query.set('pageSize', '20')
  if (locationId) query.set('locationId', locationId)
  const queryString = query.toString()
  const loader = useCallback((signal: AbortSignal) => getGames(queryString, signal, mine), [queryString, mine, user?.id])
  const { data, loading, error, refresh } = usePolling(queryString, loader)
  function apply(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setFormError('')
    try {
      const values = new URLSearchParams()
      if (activityId) values.set('activityId', activityId)
      if (level) values.set('level', level)
      if (from) values.set('from', vilniusToIso(from))
      if (to) values.set('to', vilniusToIso(to))
      if (from && to && values.get('from')! >= values.get('to')!) throw new Error('Окончание интервала должно быть позже начала.')
      if (onlyFree) values.set('joinableOnly', 'true')
      setFilters(values.toString()); setPage(1); refresh()
    } catch (failure) { setFormError(failure instanceof Error ? failure.message : 'Проверь фильтры.') }
  }
  return <div className="games-list">
    {!mine && <form className="game-filters" onSubmit={apply}>
      <div className="form-field"><label htmlFor="game-activity">Активность</label><select id="game-activity" value={activityId} onChange={event => setActivityId(event.target.value)}><option value="">Все активности</option>{activities.map(activity => <option key={activity.id} value={activity.id}>{activity.name}</option>)}</select></div>
      <div className="form-field"><label htmlFor="game-level-filter">Уровень</label><select id="game-level-filter" value={level} onChange={event => setLevel(event.target.value)}><option value="">Любой уровень</option>{Object.entries(levels).map(([value, title]) => <option key={value} value={value}>{title}</option>)}</select></div>
      <div className="form-field"><label htmlFor="game-from">Не раньше · Вильнюс</label><input id="game-from" type="datetime-local" value={from} onChange={event => setFrom(event.target.value)} /></div>
      <div className="form-field"><label htmlFor="game-to">Не позже · Вильнюс</label><input id="game-to" type="datetime-local" value={to} onChange={event => setTo(event.target.value)} /></div>
      <label className="checkbox-label"><input type="checkbox" checked={onlyFree} onChange={event => setOnlyFree(event.target.checked)} /> Только со свободными местами</label><button type="submit" className="retry">Найти игры</button>
      {formError && <p className="form-error" role="alert">{formError}</p>}
    </form>}
    {error && <div className="notice error" role="alert"><p>{error}</p><button className="retry" onClick={refresh}>Повторить загрузку игр</button></div>}
    {loading && <p role="status">Загружаем игры…</p>}
    {!loading && data && data.items.length === 0 && <p className="notice">{mine ? 'У тебя пока нет игр на этой странице.' : 'Подходящих игр пока нет. Можно создать свою игру на площадке.'}</p>}
    {!loading && data && <div className="game-grid">{data.items.map(game => <SessionCard key={game.id} game={game} history={mine} />)}</div>}
    <div className="game-pagination"><button className="text-button" disabled={page === 1 || loading} onClick={() => setPage(value => value - 1)}>Предыдущая страница</button><span>Страница {page}</span><button className="text-button" disabled={!data?.hasMore || loading} onClick={() => setPage(value => value + 1)}>Следующая страница</button></div>
  </div>
}

export function MyGames() {
  const { user, error, retry } = useAuth()
  if (error) return <section className="account-panel"><h1>Аккаунт недоступен</h1><p role="alert">{error}</p><button className="retry" onClick={retry}>Повторить</button></section>
  if (user === undefined) return <p className="notice" role="status">Проверяем вход…</p>
  if (!user) return <section className="account-panel"><h1>Мои игры</h1><p>Войди, чтобы посмотреть свои игры и историю участия.</p><a className="primary-link" href="#/login">Войти</a></section>
  return <section className="sessions-page"><a className="back-link" href="#/sessions">← Все игры</a><p className="eyebrow">Организованные игры и участие</p><h1>Мои игры</h1><p className="locations-intro">Здесь сохраняются и завершённые игры. Запись означает намерение прийти; фактическое присутствие не проверяется.</p><GamesList mine /></section>
}

export function BrowseGames({ activities }: { activities: ActivityOption[] }) {
  return <section className="sessions-page"><a className="back-link" href="#/">← Все активности</a><p className="eyebrow">Найди компанию для игры</p><h1>Игры в Вильнюсе</h1><p className="locations-intro">Запланированные встречи и набор игроков прямо сейчас. Время показано по Вильнюсу (Europe/Vilnius).</p><GamesList activities={activities} /></section>
}
