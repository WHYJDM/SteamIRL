import { useCallback, useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from '../accounts/AuthContext'
import { equipment, getGame, levels, statuses } from './api'
import type { GameSession } from './api'
import { usePolling, useServerNow, useSessionCommand } from './hooks'
import { sessionTime } from './time'

export function SessionDetails({ id }: { id: string }) {
  const { user } = useAuth()
  const loader = useCallback((signal: AbortSignal) => getGame(id, signal), [id, user?.id])
  const { data, error, loading, refresh } = usePolling(id, loader)
  const now = useServerNow(data?.serverNow)
  const status = data?.status === 'Cancelled' ? 'Cancelled' : data && Date.parse(data.endsAt) <= now ? 'Expired' : data?.status
  return <section className="session-detail-page"><a className="back-link" href="#/sessions">← Все игры</a>
    {error && <div className="notice error" role="alert"><p>{error}</p><button className="retry" onClick={refresh}>Обновить игру</button></div>}
    {loading && <p role="status">Обновляем игру…</p>}
    {data && <><p className="eyebrow">{data.kind === 'Immediate' ? 'Играем сейчас' : 'Запланированная игра'}</p><h1>{data.activityName} · {data.locationName}</h1>
      <div className="session-detail-layout"><article className="game-information"><span className={`game-status status-${status}`}>{statuses[status!]}</span>
        <dl><dt>Начало</dt><dd>{sessionTime(data.startsAt)}</dd><dt>Окончание</dt><dd>{sessionTime(data.endsAt)}</dd><dt>Время</dt><dd>Вильнюс (Europe/Vilnius)</dd><dt>Уровень</dt><dd>{levels[data.skillLevel]}</dd><dt>Инвентарь</dt><dd>{equipment[data.equipmentStatus]}</dd><dt>Дополнительные места</dt><dd>Свободно {data.remainingPlaces} из {data.capacity} · Записались {data.participantCount}</dd><dt>Организатор</dt><dd><a href={`#/profiles/${data.organiserId}`}>{data.organiserName}</a></dd></dl>
        {status === 'Cancelled' && data.cancellationReason && <p className="notice">{data.cancellationReason}</p>}
        <a className="source-link" href={`#/locations/${data.locationId}?activity=${data.activitySlug}`}>Открыть площадку ↗</a>
        <p><a href={`#/reports/new?session=${data.id}`}>Пожаловаться на игру</a></p>
        <p className="coordinate-note">Запись в игру означает намерение прийти. Она не подтверждает фактическое присутствие и не бронирует площадку.</p>
      </article><SessionActions key={data.id} game={data} closed={status === 'Expired' || status === 'Cancelled'} refreshing={loading || !!error} refresh={refresh} /></div>
    </>}
  </section>
}

function SessionActions({ game, closed, refreshing, refresh }: { game: GameSession; closed: boolean; refreshing: boolean; refresh: () => void }) {
  const { user, error: authError, retry } = useAuth()
  const { run, busy, error } = useSessionCommand()
  const [capacity, setCapacity] = useState(String(game.capacity))
  const [confirmJoin, setConfirmJoin] = useState(false)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [success, setSuccess] = useState('')
  async function action(name: 'join' | 'leave' | 'cancel' | 'capacity', payload?: unknown) {
    setSuccess('')
    const response = await run(`/api/sessions/${game.id}/${name}`, name === 'capacity' ? 'PATCH' : 'POST', payload)
    if (response) {
      setSuccess(name === 'join' ? 'Ты записан на игру.' : name === 'leave' ? 'Ты вышел из игры.' : name === 'cancel' ? 'Игра отменена.' : 'Количество мест обновлено.')
      setConfirmJoin(false); setConfirmCancel(false)
    }
    refresh()
  }
  function changeCapacity(event: FormEvent<HTMLFormElement>) { event.preventDefault(); void action('capacity', { capacity: Number(capacity) }) }
  if (authError) return <aside className="game-actions"><p role="alert">{authError}</p><button className="retry" onClick={retry}>Проверить вход ещё раз</button></aside>
  if (user === undefined) return <aside className="game-actions"><p role="status">Проверяем вход…</p></aside>
  if (!user && closed) return <aside className="game-actions"><h2>Игра закрыта</h2><p>Эта игра завершена или отменена. Выбери другое приглашение.</p><a className="primary-link" href="#/sessions">Найти другую игру</a></aside>
  if (!user && !game.isJoinable) return <aside className="game-actions"><h2>Набор сейчас недоступен</h2><p>Проверь другие игры со свободными местами.</p><a className="primary-link" href="#/sessions">Найти другую игру</a></aside>
  if (!user) return <aside className="game-actions"><h2>Хочешь присоединиться?</h2><p>Войди, чтобы занять одно дополнительное место.</p><a className="primary-link" href={`#/login?returnTo=${encodeURIComponent(`/sessions/${game.id}`)}`}>Войти и вернуться к игре</a></aside>
  return <aside className="game-actions" aria-busy={busy}><h2>{game.isOrganiser ? 'Управление игрой' : 'Участие в игре'}</h2>
    {game.isOrganiser ? <>
      {!closed && <form onSubmit={changeCapacity}><fieldset disabled={busy || refreshing}><div className="form-field"><label htmlFor="edit-capacity">Дополнительных мест всего</label><input id="edit-capacity" type="number" min={Math.max(1, game.participantCount)} max={30} step={1} required value={capacity} onChange={event => setCapacity(event.target.value)} /><span className="field-hint">Не меньше числа записавшихся игроков. Сейчас: {game.capacity}.</span></div><button className="submit-button" type="submit">Сохранить количество мест</button></fieldset></form>}
      {!closed && (confirmCancel ? <div className="game-confirm"><p>Отменить игру? Участники увидят отмену после обновления.</p><button className="logout-button" disabled={busy || refreshing} onClick={() => void action('cancel')}>Да, отменить игру</button><button className="text-button" disabled={busy} onClick={() => setConfirmCancel(false)}>Продолжить игру</button></div> : <button className="logout-button" disabled={busy || refreshing} onClick={() => setConfirmCancel(true)}>Отменить игру</button>)}
      {closed && <p>Игра закрыта. Изменение набора недоступно.</p>}
    </> : game.isParticipating ? <><p>Ты записан на игру.</p><button className="logout-button" disabled={busy || refreshing} onClick={() => void action('leave')}>Выйти из игры</button></>
      : !closed && game.isJoinable ? confirmJoin ? <div className="game-confirm"><p>Занять одно место в этой игре?</p><button className="submit-button" disabled={busy || refreshing} onClick={() => void action('join')}>Подтвердить участие</button><button className="text-button" disabled={busy} onClick={() => setConfirmJoin(false)}>Пока не записываться</button></div>
        : <button className="submit-button" disabled={busy || refreshing} onClick={() => setConfirmJoin(true)}>Присоединиться</button>
        : <p>{closed ? 'Игра завершена или отменена.' : game.status === 'Full' ? 'Все места уже заняты.' : 'Набор сейчас недоступен.'}</p>}
    {busy && <p role="status">Сохраняем…</p>}{error && <div role="alert" className="form-error action-error"><p>{error.message}</p><div className="recovery-actions"><button className="retry" type="button" onClick={refresh}>Обновить игру</button><a href="#/sessions">Найти другую игру</a></div></div>}{success && <p role="status" className="form-success">{success}</p>}
    <a className="public-profile-link" href="#/my-games">Мои игры ↗</a>
  </aside>
}
