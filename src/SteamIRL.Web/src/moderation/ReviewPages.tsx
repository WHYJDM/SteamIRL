import { useCallback, useState } from 'react'
import type { FormEvent } from 'react'
import { getActivities } from '../api'
import { usePolling } from '../sessions/hooks'
import { sessionTime } from '../sessions/time'
import { read, states, reasons, actions } from './api'
import type { Page, Report, Submission, Snapshot, SubmissionDetail } from './api'
import { Access, Failure, History, Paging, useContributionCommand } from './shared'

export function Contributions({ moderator = false }: { moderator?: boolean }) {
  return <Access moderator={moderator}><Queue moderator={moderator} /></Access>
}
function Queue({ moderator }: { moderator: boolean }) {
  const [tab, setTab] = useState<'submissions' | 'reports'>('submissions')
  const [state, setState] = useState('Pending')
  const [page, setPage] = useState(1)
  const path = `/api/${moderator ? 'moderation' : 'me'}/${tab}?page=${page}&pageSize=20${moderator ? `&state=${state}` : ''}`
  const loader = useCallback((signal: AbortSignal) => read<Page<Submission | Report>>(path, signal), [path])
  const { data, loading, error, refresh } = usePolling(path, loader)
  const prefix = moderator ? '/moderation' : ''
  return <section className="review-page"><p className="eyebrow">{moderator ? 'Проверка материалов' : 'Твой вклад в каталог'}</p><h1>{moderator ? 'Модерация' : 'Мои обращения'}</h1>
    <div className="review-links"><a className="primary-link" href="#/locations/new">Предложить площадку</a>{moderator && <a href="#/contributions">Мои обращения</a>}</div>
    <div className="review-links"><button className="retry" aria-pressed={tab === 'submissions'} onClick={() => { setTab('submissions'); setPage(1); setState('Pending') }}>Предложения площадок</button><button className="retry" aria-pressed={tab === 'reports'} onClick={() => { setTab('reports'); setPage(1); setState('Open') }}>Жалобы</button></div>
    {moderator && <div className="form-field"><label htmlFor="queue-state">Статус обращения</label><select id="queue-state" value={state} onChange={e => { setState(e.target.value); setPage(1) }}>{(tab === 'submissions' ? ['Pending', 'Approved', 'Rejected'] : ['Open', 'Resolved', 'Dismissed']).map(value => <option key={value} value={value}>{states[value]}</option>)}</select></div>}
    <button className="text-button" onClick={refresh}>Обновить обращения</button>
    {error && <p className="form-error" role="alert">{error}</p>}{loading && <p role="status">Обновляем обращения…</p>}
    {data && !loading && !error && <><ul className="review-list">{data.items.map(item => <li key={item.id}><a href={`#${prefix}/${tab === 'submissions' ? 'submissions' : 'reports'}/${item.id}`}><strong>{'proposed' in item ? item.proposed.name : item.targetName}</strong><span>{'proposed' in item ? item.kind === 'New' ? 'Новая площадка' : 'Исправление' : reasons[item.reason]} · {states[item.state]}</span><small>{sessionTime(item.submittedAt)}{moderator ? ` · ${item.authorName}` : ''}</small></a></li>)}</ul>{!data.items.length && <p className="notice">Обращений в этом списке пока нет.</p>}<Paging page={page} hasMore={data.hasMore} setPage={setPage} /></>}
  </section>
}
export function SubmissionPage({ id, moderator = false }: { id: string; moderator?: boolean }) {
  return <Access moderator={moderator}><SubmissionView id={id} moderator={moderator} /></Access>
}
function SubmissionView({ id, moderator }: { id: string; moderator: boolean }) {
  const loader = useCallback(async (signal: AbortSignal): Promise<SubmissionDetail> => moderator ? read<SubmissionDetail>(`/api/moderation/submissions/${id}`, signal) : { submission: await read<Submission>(`/api/location-submissions/${id}`, signal), nearbyLocations: [] }, [id, moderator])
  const { data, error, loading, refresh } = usePolling(id, loader)
  const item = data?.submission
  const stale = item && (item.currentRevision !== item.baseRevision || item.locationStatus !== (item.kind === 'New' ? 'Pending' : 'Approved'))
  return <section className="review-page"><a className="back-link" href={moderator ? '#/moderation' : '#/contributions'}>← К обращениям</a>
    {error && <p role="alert" className="form-error">{error}</p>}{loading && <p role="status">Обновляем предложение…</p>}<button className="text-button" onClick={refresh}>Обновить предложение</button>
    {item && <><p className="eyebrow">{item.kind === 'New' ? 'Новая площадка' : 'Исправление площадки'} · {states[item.state]}</p><h1>{item.proposed.name}</h1><p>{item.authorName} · {sessionTime(item.submittedAt)}</p>
      {item.state === 'Pending' && <p className="notice">Предложение ожидает проверки. Изменения ещё не опубликованы.</p>}
      {item.locationStatus === 'Approved' && <p><a href={`#/locations/${item.locationId}`}>Открыть опубликованную площадку ↗</a></p>}
      <div className="review-comparison">{item.original && <section><h2>До исправления</h2><SnapshotView snapshot={item.original} /></section>}<section><h2>Предложенные сведения</h2><SnapshotView snapshot={item.proposed} activityNames={item.activities.map(activity => activity.name)} /></section></div>
      {item.evidence && <section><h2>Комментарий автора</h2><p className="review-text">{item.evidence}</p></section>}
      {moderator && item.kind === 'New' && <section><h2>Площадки рядом</h2><p>Проверь возможные дубликаты. Совпадение координат само по себе не означает дубликат.</p>{data!.nearbyLocations.length ? <ul>{data!.nearbyLocations.map(place => <li key={place.id}><a href={`#/locations/${place.id}`}>{place.name}</a> · {place.address}</li>)}</ul> : <p>В ближайшей области опубликованных площадок не найдено.</p>}</section>}
      <History decisions={item.decisions} />
      {moderator && item.state === 'Pending' && <>{stale && <p role="status" className="notice">Площадка уже изменилась. Одобрение этой версии недоступно; предложение можно отклонить с пояснением.</p>}<DecisionForm key={item.id} path={`/api/moderation/submissions/${id}/decision`} allowed={stale ? ['Reject'] : ['Approve', 'Reject']} disabled={loading || !!error} refresh={refresh} /></>}
    </>}
  </section>
}
function SnapshotView({ snapshot, activityNames }: { snapshot: Snapshot; activityNames?: string[] }) {
  return <dl className="review-snapshot"><dt>Название</dt><dd>{snapshot.name}</dd><dt>Адрес</dt><dd>{snapshot.address}</dd><dt>Координаты</dt><dd>{snapshot.latitude}, {snapshot.longitude}</dd><dt>Активности</dt><dd>{activityNames?.join(', ') ?? <ActivityNames ids={snapshot.activityIds} />}</dd><dt>Описание</dt><dd>{snapshot.description || 'Не указано'}</dd><dt>Доступ</dt><dd>{snapshot.accessNotes || 'Не указано'}</dd><dt>Комментарий к координатам</dt><dd>{snapshot.coordinateNote || 'Не указан'}</dd><dt>Источник</dt><dd>{snapshot.sourceUrl ? <a href={snapshot.sourceUrl} target="_blank" rel="noopener noreferrer">Открыть источник ↗</a> : 'Не указан'}</dd></dl>
}
// The catalogue has stable identifiers; load names so original snapshots remain readable after sport removal.
function ActivityNames({ ids }: { ids: string[] }) {
  const loader = useCallback((signal: AbortSignal) => getActivities(signal), [])
  const { data, error, refresh } = usePolling('activity-names', loader)
  return error ? <button className="text-button" onClick={refresh}>Повторить загрузку активностей</button> : data ? <>{ids.map(id => data.find(activity => activity.id === id)?.name ?? 'Неизвестная активность').join(', ')}</> : <>Загружаем активности…</>
}
export function ReportDetailPage({ id, moderator = false }: { id: string; moderator?: boolean }) {
  return <Access moderator={moderator}><ReportView id={id} moderator={moderator} /></Access>
}
function ReportView({ id, moderator }: { id: string; moderator: boolean }) {
  const loader = useCallback((signal: AbortSignal) => read<Report>(`/api/content-reports/${id}`, signal), [id])
  const { data: item, error, loading, refresh } = usePolling(id, loader)
  return <section className="review-page"><a className="back-link" href={moderator ? '#/moderation' : '#/contributions'}>← К обращениям</a><h1>Жалоба</h1>
    {error && <p className="form-error" role="alert">{error}</p>}{loading && <p role="status">Обновляем жалобу…</p>}<button className="text-button" onClick={refresh}>Обновить жалобу</button>
    {item && <><p className="eyebrow">{states[item.state]} · {reasons[item.reason]}</p><h2>{item.targetName}</h2><p>{item.authorName} · {sessionTime(item.submittedAt)}</p><p><a href={item.sessionId ? `#/sessions/${item.sessionId}` : `#/locations/${item.locationId}`}>Открыть {item.sessionId ? 'игру' : 'площадку'} ↗</a></p><p className="review-text">{item.details || 'Автор не добавил подробностей.'}</p><History decisions={item.decisions} />
      {moderator && item.state === 'Open' && <DecisionForm path={`/api/moderation/reports/${id}/decision`} allowed={['Resolve', 'Dismiss', 'RemoveContent']} disabled={loading || !!error} refresh={refresh} />}
    </>}
  </section>
}
function DecisionForm({ path, allowed, disabled, refresh }: { path: string; allowed: string[]; disabled: boolean; refresh: () => void }) {
  const [selected, setSelected] = useState(allowed[0])
  const action = allowed.includes(selected) ? selected : allowed[0]
  const [reason, setReason] = useState('')
  const { run, busy, error } = useContributionCommand()
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await run<Submission | Report>(path, { action, reason })
    refresh()
  }
  return <form className="review-form decision-form" onSubmit={submit}><h2>Принять решение</h2><fieldset disabled={busy || disabled}>
    <div className="form-field"><label htmlFor="decision-action">Действие</label><select id="decision-action" value={action} onChange={e => setSelected(e.target.value)}>{allowed.map(value => <option key={value} value={value}>{actions[value]}</option>)}</select></div>
    {action === 'RemoveContent' && <p className="notice">Снятие площадки отменит её активные игры. Снятие отдельной игры отменит только эту игру. История участия сохранится.</p>}
    {action === 'Approve' && <p>Сведения будут опубликованы. Исключение активности из площадки отменит её активные игры этого вида спорта.</p>}
    <div className="form-field"><label htmlFor="decision-reason">Обоснование решения</label><textarea id="decision-reason" required minLength={10} maxLength={1000} value={reason} onChange={e => setReason(e.target.value)} /><span className="field-hint">10–1000 символов. Автор увидит обоснование; не добавляй чужие личные данные.</span></div>
    <button className="submit-button">{busy ? 'Сохраняем…' : 'Сохранить решение'}</button>
  </fieldset><Failure error={error} /></form>
}
