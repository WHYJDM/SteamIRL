import { useCallback, useEffect, useState } from 'react'
import type { Activity } from '../api'
import { getLocation, getLocations, locationDate } from './api'
import type { Bounds, SportLocation } from './api'
import { LocationMap } from './LocationMap'
import { GamesList } from '../sessions/SessionList'

function DetailsContent({ location }: { location: SportLocation }) {
  return <>
    <p className="location-address">{location.address}</p><p className="location-description">{location.description}</p>
    <div className="activity-tags">{location.activities.map(activity => <a key={activity.id} href={`#/activities/${activity.slug}`}>{activity.name}</a>)}</div>
    <dl className="location-meta"><dt>Обновлено</dt><dd>{locationDate(location.updatedAt)}</dd><dt>{location.sourceUrl ? 'Сведения проверены по источнику' : 'Сведения проверены модератором'}</dt><dd>{locationDate(location.sourceCheckedAt)}</dd></dl>
    <p className="coordinate-note">{location.coordinateNote}</p><p className="location-description">{location.accessNotes}</p>
    {location.sourceUrl ? <a className="source-link" href={location.sourceUrl} target="_blank" rel="noopener noreferrer">Открыть источник сведений ↗</a> : <p className="coordinate-note">Внешний источник не указан. Сведения предложены пользователем.</p>}
  </>
}

export function LocationsExplorer({ activity }: { activity: Activity }) {
  const [items, setItems] = useState<SportLocation[]>([])
  const [page, setPage] = useState(1)
  const [hasMore, setHasMore] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [attempt, setAttempt] = useState(0)
  const [selected, setSelected] = useState<SportLocation>()
  const [markers, setMarkers] = useState<SportLocation[]>([])
  const [mapError, setMapError] = useState('')
  const [mapMore, setMapMore] = useState(false)
  const [mapLoading, setMapLoading] = useState(true)
  const [bounds, setBounds] = useState<Bounds>()
  const [mapAttempt, setMapAttempt] = useState(0)
  const latestBounds = useCallback((value: Bounds) => setBounds(value), [])
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError('')
    getLocations(activity.id, controller.signal, page).then(result => {
      setItems(previous => page === 1 ? result.items : [...previous, ...result.items.filter(item => !previous.some(existing => existing.id === item.id))])
      setHasMore(result.hasMore); setLoading(false)
    }).catch((failure: unknown) => { if (!controller.signal.aborted) { setError(failure instanceof Error ? failure.message : 'Не удалось загрузить площадки.'); setLoading(false) } })
    return () => controller.abort()
  }, [activity.id, page, attempt])
  useEffect(() => {
    if (!bounds) return
    const controller = new AbortController(); setMapError(''); setMarkers([]); setMapMore(false); setMapLoading(true)
    getLocations(activity.id, controller.signal, 1, bounds).then(result => { setMarkers(result.items); setMapMore(result.hasMore); setMapLoading(false) })
      .catch((failure: unknown) => { if (!controller.signal.aborted) { setMapError(failure instanceof Error ? failure.message : 'Не удалось загрузить метки.'); setMapLoading(false) } })
    return () => controller.abort()
  }, [activity.id, bounds, mapAttempt])

  return <section className="locations-page" aria-labelledby="locations-title">
    <a className="back-link" href="#/">← Все активности</a><p className="eyebrow">Вильнюс · площадки для игры</p><h1 id="locations-title">{activity.name}</h1>
    <p className="locations-intro">Выбирай место на карте или в списке. В карточке — адрес, доступные активности и источник сведений.</p>
    <p><a href="#/locations/new">Предложить площадку ↗</a></p>
    <div className="locations-layout"><div className="location-list-block"><h2>Все площадки активности</h2>
      {error && <div className="notice error" role="alert"><p>{error}</p><button className="retry" onClick={() => setAttempt(value => value + 1)}>Повторить загрузку площадок</button></div>}
      {loading && <p role="status">Загружаем площадки…</p>}
      {!loading && !error && items.length === 0 && <p className="notice">Для этой активности пока нет одобренных площадок.</p>}
      <ul className="location-list">{items.map(location => <li key={location.id}>
        <button className="location-list-button" aria-pressed={selected?.id === location.id} onClick={() => setSelected(location)}><strong>{location.name}</strong><span>{location.address}</span><small>Обновлено {locationDate(location.updatedAt)}</small></button>
        <a className="location-detail-link" href={`#/locations/${location.id}?activity=${activity.slug}`}>Открыть карточку ↗</a>
      </li>)}</ul>
      {hasMore && !loading && !error && <button className="retry" onClick={() => setPage(value => value + 1)}>Показать ещё</button>}
    </div><div className="location-map-column"><h2>Площадки на карте</h2><LocationMap locations={markers} selected={selected} onSelect={setSelected} onBounds={latestBounds} />
      <p className="map-help" role="status">{mapLoading ? 'Загружаем метки…' : !mapError ? markers.length ? `Мест в этой области: ${markers.length}.` : 'В этой области пока нет площадок.' : ''}</p>
      {mapError && <div className="notice error" role="alert"><p>{mapError}</p><button className="retry" onClick={() => setMapAttempt(value => value + 1)}>Повторить загрузку меток</button></div>}
      {mapMore && <p className="map-help">В этой области больше 100 площадок. Увеличь масштаб, чтобы увидеть остальные.</p>}
      {selected && <article className="selected-location" aria-labelledby="selected-location-title"><h2 id="selected-location-title">{selected.name}</h2><DetailsContent location={selected} /><a className="primary-link" href={`#/locations/${selected.id}?activity=${activity.slug}`}>Открыть карточку ↗</a></article>}
    </div></div>
  </section>
}

export function LocationDetails({ route }: { route: string }) {
  const [path, query] = route.split('?'); const id = path.slice('/locations/'.length)
  const activity = new URLSearchParams(query).get('activity')
  const [location, setLocation] = useState<SportLocation>()
  const [error, setError] = useState('')
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    const controller = new AbortController(); setLocation(undefined); setError('')
    getLocation(id, controller.signal).then(setLocation).catch((failure: unknown) => {
      if (!controller.signal.aborted) setError(failure instanceof Error ? failure.message : 'Не удалось загрузить карточку.')
    })
    return () => controller.abort()
  }, [id, attempt])
  const back = activity && /^[a-z-]+$/.test(activity) ? `#/activities/${activity}` : '#/'
  return <section className="location-detail-page"><a className="back-link" href={back}>← К площадкам</a>
    {error ? <div className="notice error" role="alert"><h1>Площадка недоступна</h1><p>{error}</p><button className="retry" onClick={() => setAttempt(value => value + 1)}>Повторить загрузку карточки</button></div>
      : location ? <><p className="eyebrow">Спортивная площадка · Вильнюс</p><h1>{location.name}</h1><div className="location-detail-layout"><article><DetailsContent location={location} /></article><LocationMap key={location.id} locations={[location]} selected={location} /></div>
        <div className="review-links"><a href={`#/location-corrections/${location.id}`}>Предложить исправление</a><a href={`#/reports/new?location=${location.id}`}>Пожаловаться на площадку</a></div>
        <section className="location-games" aria-labelledby="location-games-title"><div className="location-games-heading"><h2 id="location-games-title">Игры на этой площадке</h2><a className="primary-link" href={`#/sessions/new?location=${location.id}${activity ? `&activity=${encodeURIComponent(activity)}` : ''}`}>Создать игру</a></div>
          <GamesList key={location.id} locationId={location.id} activities={location.activities} defaultActivityId={location.activities.find(item => item.slug === activity)?.id} />
        </section></>
        : <p className="notice" role="status">Загружаем карточку…</p>}
  </section>
}
