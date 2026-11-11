import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { getLocation } from '../locations/api'
import { getGame } from '../sessions/api'
import { reasons } from './api'
import type { Report } from './api'
import { Access, Failure, useContributionCommand } from './shared'

export function ReportPage({ route }: { route: string }) {
  const query = new URLSearchParams(route.split('?')[1]); const location = query.get('location'); const session = query.get('session')
  return <Access>{!!location === !!session ? <section className="account-panel"><h1>Выбери предмет жалобы</h1><a href="#/sessions">Открыть игры</a></section> : <ReportForm locationId={location} sessionId={session} />}</Access>
}
function ReportForm({ locationId, sessionId }: { locationId: string | null; sessionId: string | null }) {
  const [name, setName] = useState('')
  const [loadError, setLoadError] = useState('')
  const [attempt, setAttempt] = useState(0)
  const [reason, setReason] = useState('Inaccurate')
  const [details, setDetails] = useState('')
  const { run, busy, error } = useContributionCommand()
  useEffect(() => {
    const controller = new AbortController(); setLoadError('')
    const load = async () => locationId ? (await getLocation(locationId, controller.signal)).name : (await getGame(sessionId!, controller.signal)).locationName
    load().then(setName).catch((failure: unknown) => { if (!controller.signal.aborted) setLoadError(failure instanceof Error ? failure.message : 'Не удалось загрузить объект жалобы.') })
    return () => controller.abort()
  }, [locationId, sessionId, attempt])
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const response = await run<Report>('/api/content-reports', { reason, details, locationId, sessionId })
    if (response) window.location.hash = `/reports/${response.id}`
  }
  return <section className="review-page"><a className="back-link" href={locationId ? `#/locations/${locationId}` : `#/sessions/${sessionId}`}>← Назад</a><h1>{sessionId ? 'Пожаловаться на игру' : 'Пожаловаться на площадку'}</h1>
    {loadError ? <div role="alert"><p>{loadError}</p><button className="retry" onClick={() => setAttempt(value => value + 1)}>Повторить загрузку</button></div> : !name ? <p role="status">Загружаем сведения…</p> : <><p>{name}</p><p>Жалобу видят только её автор и модераторы. Решение появится в «Моих обращениях».</p><form className="review-form" onSubmit={submit}><fieldset disabled={busy}>
      <div className="form-field"><label htmlFor="report-reason">Причина жалобы</label><select id="report-reason" value={reason} onChange={e => setReason(e.target.value)}>{Object.entries(reasons).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></div>
      <div className="form-field"><label htmlFor="report-details">Подробности (необязательно)</label><textarea id="report-details" maxLength={2000} value={details} onChange={e => setDetails(e.target.value)} /></div>
      <Failure error={error} /><button className="submit-button">{busy ? 'Отправляем…' : 'Отправить жалобу'}</button>
    </fieldset></form></>}
  </section>
}
