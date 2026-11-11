import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { getActivities } from '../api'
import type { Activity } from '../api'
import { getLocation } from '../locations/api'
import type { SportLocation } from '../locations/api'
import type { Submission } from './api'
import { Access, Failure, useContributionCommand } from './shared'

export function ProposalPage({ locationId }: { locationId?: string }) {
  return <Access><ProposalLoader locationId={locationId} /></Access>
}
function ProposalLoader({ locationId }: { locationId?: string }) {
  const [data, setData] = useState<{ activities: Activity[]; location?: SportLocation }>()
  const [error, setError] = useState('')
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const controller = new AbortController(); setError('')
    Promise.all([getActivities(controller.signal), locationId ? getLocation(locationId, controller.signal) : Promise.resolve(undefined)])
      .then(([activities, location]) => setData({ activities, location }))
      .catch((failure: unknown) => { if (!controller.signal.aborted) setError(failure instanceof Error ? failure.message : 'Не удалось загрузить форму.') })
    return () => controller.abort()
  }, [locationId, attempt])
  return <section className="review-page"><a className="back-link" href={locationId ? `#/locations/${locationId}` : '#/contributions'}>← Назад</a><p className="eyebrow">Помоги игрокам найти место</p><h1>{locationId ? 'Предложить исправление' : 'Предложить площадку'}</h1>
    <p>Предложение появится в каталоге после проверки модератором. Статус и причину решения можно посмотреть в «Моих обращениях».</p>
    {error ? <div role="alert" className="notice error"><p>{error}</p><button className="retry" onClick={() => setAttempt(value => value + 1)}>Повторить загрузку формы</button></div> : data ? <ProposalForm key={locationId ?? 'new'} {...data} /> : <p role="status">Загружаем форму…</p>}
  </section>
}
function ProposalForm({ activities, location }: { activities: Activity[]; location?: SportLocation }) {
  const [name, setName] = useState(location?.name ?? '')
  const [address, setAddress] = useState(location?.address ?? '')
  const [latitude, setLatitude] = useState(location ? String(location.latitude) : '')
  const [longitude, setLongitude] = useState(location ? String(location.longitude) : '')
  const [description, setDescription] = useState(location?.description ?? '')
  const [sourceUrl, setSourceUrl] = useState(location?.sourceUrl ?? '')
  const [coordinateNote, setCoordinateNote] = useState(location?.coordinateNote ?? '')
  const [accessNotes, setAccessNotes] = useState(location?.accessNotes ?? '')
  const [evidence, setEvidence] = useState('')
  const [selected, setSelected] = useState<string[]>(location?.activities.map(item => item.id) ?? [])
  const [localError, setLocalError] = useState('')
  const { run, busy, error } = useContributionCommand()
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setLocalError('')
    if (!selected.length) { setLocalError('Выбери хотя бы одну активность.'); return }
    if (sourceUrl && !sourceUrl.startsWith('https://')) { setLocalError('Источник должен начинаться с https://.'); return }
    const result = await run<Submission>('/api/location-submissions', { name, address, latitude: Number(latitude), longitude: Number(longitude), activityIds: selected, description, sourceUrl, coordinateNote, accessNotes, evidence,
      ...(location ? { locationId: location.id, baseRevision: location.revision } : {}) })
    if (result) window.location.hash = `/submissions/${result.id}`
  }
  return <form className="review-form" onSubmit={submit} aria-busy={busy}><fieldset disabled={busy}>
    <div className="form-field"><label htmlFor="proposal-name">Название площадки</label><input id="proposal-name" required minLength={2} maxLength={200} value={name} onChange={e => setName(e.target.value)} /></div>
    <div className="form-field"><label htmlFor="proposal-address">Адрес</label><input id="proposal-address" required minLength={2} maxLength={300} value={address} onChange={e => setAddress(e.target.value)} /></div>
    <div className="review-coordinates"><div className="form-field"><label htmlFor="proposal-latitude">Широта</label><input id="proposal-latitude" type="number" required min={-90} max={90} step="0.000001" value={latitude} onChange={e => setLatitude(e.target.value)} /></div><div className="form-field"><label htmlFor="proposal-longitude">Долгота</label><input id="proposal-longitude" type="number" required min={-180} max={180} step="0.000001" value={longitude} onChange={e => setLongitude(e.target.value)} /></div></div>
    <p className="field-hint">Укажи координаты самой площадки, а не адреса здания рядом.</p>
    <fieldset className="review-sports"><legend>Доступные активности</legend>{activities.map(item => <label key={item.id}><input type="checkbox" checked={selected.includes(item.id)} onChange={e => setSelected(previous => e.target.checked ? [...previous, item.id] : previous.filter(id => id !== item.id))} />{item.name}</label>)}</fieldset>
    <div className="form-field"><label htmlFor="proposal-description">Описание</label><textarea id="proposal-description" maxLength={4000} value={description} onChange={e => setDescription(e.target.value)} /></div>
    <div className="form-field"><label htmlFor="proposal-access">Условия доступа и инвентарь</label><textarea id="proposal-access" maxLength={2000} value={accessNotes} onChange={e => setAccessNotes(e.target.value)} /></div>
    <div className="form-field"><label htmlFor="proposal-coordinates">Комментарий к координатам</label><textarea id="proposal-coordinates" maxLength={500} value={coordinateNote} onChange={e => setCoordinateNote(e.target.value)} /></div>
    <div className="form-field"><label htmlFor="proposal-source">Источник сведений (необязательно)</label><input id="proposal-source" type="url" placeholder="https://…" maxLength={1000} value={sourceUrl} onChange={e => setSourceUrl(e.target.value)} /></div>
    <div className="form-field"><label htmlFor="proposal-evidence">{location ? 'Что изменилось и почему' : 'Комментарий для модератора (необязательно)'}</label><textarea id="proposal-evidence" required={!!location} minLength={location ? 10 : undefined} maxLength={2000} value={evidence} onChange={e => setEvidence(e.target.value)} /><span className="field-hint">{location ? 'Минимум 10 символов. Укажи источник: ссылку выше или своё наблюдение с датой. ' : ''}Комментарий видят автор и модераторы. Не добавляй личные данные.</span></div>
    {localError && <p className="form-error" role="alert">{localError}</p>}<Failure error={error} />
    <button className="submit-button" type="submit">{busy ? 'Отправляем…' : 'Отправить на проверку'}</button>
  </fieldset></form>
}
