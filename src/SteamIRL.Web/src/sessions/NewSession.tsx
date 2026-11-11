import { useCallback, useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from '../accounts/AuthContext'
import { getLocation } from '../locations/api'
import type { SportLocation } from '../locations/api'
import { equipment, levels } from './api'
import type { CreateSession, Equipment, SessionKind, SkillLevel } from './api'
import { usePolling, useSessionCommand } from './hooks'
import { vilniusInput, vilniusToIso } from './time'

export function NewSession({ route }: { route: string }) {
  const params = new URLSearchParams(route.split('?')[1])
  const id = params.get('location') ?? ''
  const activitySlug = params.get('activity')
  const { user, error: authError, retry } = useAuth()
  const loader = useCallback((signal: AbortSignal) => getLocation(id, signal), [id])
  const { data: location, error, refresh } = usePolling(id, loader)
  if (authError) return <section className="account-panel"><h1>Аккаунт недоступен</h1><p role="alert">{authError}</p><button className="retry" onClick={retry}>Повторить</button></section>
  if (user === undefined) return <p className="notice" role="status">Проверяем вход…</p>
  if (!user) return <section className="account-panel"><h1>Создать игру</h1><p>Войди, чтобы пригласить других игроков.</p><a className="primary-link" href="#/login">Войти</a></section>
  if (!id) return <section className="account-panel"><h1>Сначала выбери площадку</h1><a className="primary-link" href="#/">Открыть активности</a></section>
  if (error) return <section className="account-panel"><h1>Площадка недоступна</h1><p role="alert">{error}</p><button className="retry" onClick={refresh}>Повторить</button></section>
  if (!location) return <p className="notice" role="status">Загружаем площадку…</p>
  if (!location.activities.length) return <p className="notice">У этой площадки пока нет поддерживаемых активностей.</p>
  return <NewSessionForm key={`${location.id}/${activitySlug}`} location={location} activitySlug={activitySlug} />
}

function NewSessionForm({ location, activitySlug }: { location: SportLocation; activitySlug: string | null }) {
  const [activityId, setActivityId] = useState(location.activities.find(activity => activity.slug === activitySlug)?.id ?? location.activities[0].id)
  const [kind, setKind] = useState<SessionKind>('Planned')
  const [level, setLevel] = useState<SkillLevel>('Intermediate')
  const [ball, setBall] = useState<Equipment>('NotSpecified')
  const [capacity, setCapacity] = useState(2)
  const tomorrow = vilniusInput(Date.now() + 86400000).slice(0, 10)
  const [startsAt, setStartsAt] = useState(tomorrow + 'T18:00')
  const [endsAt, setEndsAt] = useState(tomorrow + 'T19:30')
  const [duration, setDuration] = useState(60)
  const [validation, setValidation] = useState('')
  const { run, busy, error } = useSessionCommand()
  const selectedActivity = location.activities.find(activity => activity.id === activityId)
  const ballSport = ['volleyball', 'basketball', 'football'].includes(selectedActivity?.slug ?? '')
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (busy) return; setValidation('')
    if (!selectedActivity) { setValidation('Выбери доступную активность.'); return }
    let payload: CreateSession
    try {
      payload = { locationId: location.id, activityId, kind, skillLevel: level, capacity, equipmentStatus: ballSport ? ball : 'NotSpecified' }
      if (kind === 'Planned') {
        payload.startsAt = vilniusToIso(startsAt); payload.endsAt = vilniusToIso(endsAt)
        if (payload.endsAt <= payload.startsAt) throw new Error('Окончание игры должно быть позже начала.')
      } else payload.durationMinutes = duration
    } catch (failure) { setValidation(failure instanceof Error ? failure.message : 'Проверь время игры.'); return }
    const response = await run('/api/sessions', 'POST', payload)
    if (response) window.location.hash = `/sessions/${response.id}`
  }
  return <section className="game-form-page"><a className="back-link" href={`#/locations/${location.id}`}>← К площадке</a><p className="eyebrow">Собери компанию</p><h1>Создать игру</h1><p className="locations-intro">{location.name} · {location.address}</p>
    <form onSubmit={submit} aria-busy={busy}><fieldset disabled={busy}>
      <div className="form-field"><label htmlFor="new-activity">Активность</label><select id="new-activity" required value={selectedActivity?.id ?? ''} onChange={event => setActivityId(event.target.value)} aria-describedby={!selectedActivity ? 'activity-unavailable' : undefined}>{!selectedActivity && <option value="" disabled>Выбери активность</option>}{location.activities.map(activity => <option key={activity.id} value={activity.id}>{activity.name}</option>)}</select>
        {!selectedActivity && <p id="activity-unavailable" role="status" className="form-error">Выбранная активность больше недоступна на площадке. Выбери другую.</p>}
      </div>
      <div className="form-field"><label htmlFor="new-kind">Когда играем</label><select id="new-kind" value={kind} onChange={event => setKind(event.target.value as SessionKind)}><option value="Planned">Запланировать игру</option><option value="Immediate">Нужны игроки сейчас</option></select></div>
      {kind === 'Planned' ? <div className="game-time-fields"><div className="form-field"><label htmlFor="new-start">Начало · время Вильнюса</label><input id="new-start" type="datetime-local" required value={startsAt} onChange={event => setStartsAt(event.target.value)} /></div><div className="form-field"><label htmlFor="new-end">Окончание · время Вильнюса</label><input id="new-end" type="datetime-local" required value={endsAt} onChange={event => setEndsAt(event.target.value)} /></div></div>
        : <div className="form-field"><label htmlFor="new-duration">Срок набора от текущего момента</label><select id="new-duration" value={duration} onChange={event => setDuration(Number(event.target.value))}>{[15, 30, 60, 90, 120, 180, 240].map(value => <option key={value} value={value}>{value} минут</option>)}</select></div>}
      <div className="form-field"><label htmlFor="new-capacity">Сколько дополнительных игроков нужно</label><div className="capacity-stepper"><button type="button" aria-label="На одного игрока меньше" disabled={capacity <= 1} onClick={() => setCapacity(value => value - 1)}>−</button><input id="new-capacity" type="number" min={1} max={30} step={1} required value={capacity} onChange={event => setCapacity(Number(event.target.value))} /><button type="button" aria-label="На одного игрока больше" disabled={capacity >= 30} onClick={() => setCapacity(value => value + 1)}>+</button></div><span className="field-hint">От 1 до 30. Организатор и уже собранная компания в это число не входят.</span></div>
      <div className="form-field"><label htmlFor="new-level">Ожидаемый уровень</label><select id="new-level" value={level} onChange={event => setLevel(event.target.value as SkillLevel)}>{Object.entries(levels).map(([value, title]) => <option key={value} value={value}>{title}</option>)}</select></div>
      {ballSport && <div className="form-field"><label htmlFor="new-equipment">Инвентарь</label><select id="new-equipment" value={ball} onChange={event => setBall(event.target.value as Equipment)}>{Object.entries(equipment).map(([value, title]) => <option key={value} value={value}>{title}</option>)}</select></div>}
      <div className="game-preview"><h2>Предпросмотр приглашения</h2><p>{selectedActivity?.name ?? 'Выбери доступную активность'} · {location.name}</p><p>Нужны {capacity} дополнительных игроков · {levels[level]} уровень</p><p>{kind === 'Immediate' ? `Начало сейчас, набор на ${duration} минут.` : `${startsAt.replace('T', ' ')} — ${endsAt.replace('T', ' ')} · Вильнюс`}</p><p>Инвентарь: {equipment[ballSport ? ball : 'NotSpecified']}</p></div>
      {validation && <p role="alert" className="form-error">{validation}</p>}{error && <div role="alert" className="form-error"><p>{error.message}</p>{error.existingSessionId && <a href={`#/sessions/${error.existingSessionId}`}>Открыть действующее приглашение</a>}</div>}
      <p className="field-hint">Приглашение не бронирует площадку. Время указано по Вильнюсу (Europe/Vilnius).</p>
      <button className="submit-button" type="submit">{busy ? 'Публикуем…' : kind === 'Immediate' ? 'Позвать игроков сейчас' : 'Создать запланированную игру'}</button>
    </fieldset></form>
  </section>
}
