import { useEffect, useState } from 'react'
import { getActivities } from './api'
import type { Activity } from './api'
import { AccountNavigation, AccountPage, useRoute } from './accounts/AccountPages'
import { LocationDetails, LocationsExplorer } from './locations/LocationPages'
import { BrowseGames, MyGames } from './sessions/SessionList'
import { NewSession } from './sessions/NewSession'
import { SessionDetails } from './sessions/SessionDetails'
import { ProposalPage } from './moderation/ProposalForm'
import { ReportPage } from './moderation/ReportForm'
import { Contributions, SubmissionPage, ReportDetailPage } from './moderation/ReviewPages'
import './moderation/styles.css'

type CatalogueState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'ready'; activities: Activity[] }

const symbols: Record<string, string> = {
  volleyball: '◒', basketball: '◉', football: '⬡', 'table-tennis': '◓',
}

export function App() {
  const route = useRoute()
  const [state, setState] = useState<CatalogueState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    getActivities(controller.signal)
      .then(activities => setState({ status: 'ready', activities }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setState({ status: 'error', message: error instanceof Error ? error.message : 'Не удалось загрузить активности.' })
        }
      })
    return () => controller.abort()
  }, [attempt])

  const selected = state.status === 'ready' && route.startsWith('/activities/')
    ? state.activities.find(activity => activity.slug === route.slice('/activities/'.length)) : undefined

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main" onClick={event => { event.preventDefault(); const main = document.getElementById('main'); main?.focus(); main?.scrollIntoView() }}>Перейти к содержимому</a>
      <header className="header">
        <a className="brand" href="/" aria-label="SteamIRL — главная"><span className="brand-mark" aria-hidden="true">↗</span>Steam<span>IRL</span></a>
        <div className="header-right"><span className="city"><span aria-hidden="true">◎</span> Вильнюс, Литва</span><AccountNavigation /></div>
      </header>
      <main id="main" tabIndex={-1}>
        {route === '/' ? <>
        <section className="hero" aria-labelledby="hero-title">
          <div>
            <p className="eyebrow">Больше игры. Больше общения.</p>
            <h1 id="hero-title">Хорошая игра<br />начинается <em>рядом.</em></h1>
            <p className="intro">Выбери свой спорт, найди площадку Вильнюса и присоединяйся к игре или собирай свою компанию.</p>
            <a className="primary-link" href="#activities">Выбрать активность <span aria-hidden="true">↗</span></a>
          </div>
          <div className="court" aria-hidden="true">
            <span className="court-label">Встречаемся на площадке</span>
            <div className="court-lines"><span /><i /></div>
            <span className="court-ball" />
            <span className="court-caption">OFFLINE IS THE NEW ONLINE</span>
          </div>
        </section>

        <section id="activities" className="activities" aria-labelledby="activities-title">
          <div className="section-heading"><div><p className="eyebrow">Твоя следующая игра</p><h2 id="activities-title">Что будем играть?</h2></div><span className="section-note">Каталог активностей</span></div>
          <div aria-live="polite" aria-busy={state.status === 'loading'}>
            {state.status === 'loading' && <p className="notice" role="status">Загружаем активности…</p>}
            {state.status === 'error' && <div className="notice error" role="alert"><p>{state.message}</p><button className="retry" onClick={() => { setState({ status: 'loading' }); setAttempt(value => value + 1) }}>Повторить загрузку</button></div>}
            {state.status === 'ready' && state.activities.length === 0 && <p className="notice">В каталоге пока нет активностей. Загляни позже.</p>}
          </div>
          {state.status === 'ready' && state.activities.length > 0 && <div className="activity-grid">{state.activities.map(activity => (
            <a key={activity.id} className="activity-card" href={`#/activities/${activity.slug}`}>
              <span className={`sport-symbol sport-${activity.slug}`} aria-hidden="true">{symbols[activity.slug] ?? '◎'}</span>
              <span className="activity-name">{activity.name}</span><span className="activity-description">{activity.description}</span><span className="card-arrow" aria-hidden="true">↗</span>
            </a>
          ))}</div>}
        </section>
        <aside className="project-note"><span className="note-dot" aria-hidden="true" /><p>Выбирай площадку, планируй игру или зови недостающих игроков прямо сейчас.</p></aside>
        </> : route === '/locations/new' ? <ProposalPage key={route} />
          : route.startsWith('/location-corrections/') ? <ProposalPage key={route} locationId={route.slice('/location-corrections/'.length)} />
          : route === '/contributions' ? <Contributions key={route} />
          : route === '/moderation' ? <Contributions key={route} moderator />
          : route.startsWith('/moderation/submissions/') ? <SubmissionPage key={route} id={route.slice('/moderation/submissions/'.length)} moderator />
          : route.startsWith('/moderation/reports/') ? <ReportDetailPage key={route} id={route.slice('/moderation/reports/'.length)} moderator />
          : route.startsWith('/submissions/') ? <SubmissionPage key={route} id={route.slice('/submissions/'.length)} />
          : route.split('?')[0] === '/reports/new' ? <ReportPage key={route} route={route} />
          : route.startsWith('/reports/') ? <ReportDetailPage key={route} id={route.slice('/reports/'.length)} />
          : route.split('?')[0] === '/sessions/new' ? <NewSession key={route} route={route} />
          : route === '/sessions' ? <BrowseGames activities={state.status === 'ready' ? state.activities : []} />
          : route.startsWith('/sessions/') ? <SessionDetails key={route} id={route.slice('/sessions/'.length)} />
          : route === '/my-games' ? <MyGames />
          : route.startsWith('/locations/') ? <LocationDetails key={route} route={route} />
          : route.startsWith('/activities/') ? selected ? <LocationsExplorer key={selected.id} activity={selected} />
            : state.status === 'loading' ? <p className="notice" role="status">Загружаем активность…</p>
              : state.status === 'error' ? <div className="notice error" role="alert"><p>{state.message}</p><button className="retry" onClick={() => { setState({ status: 'loading' }); setAttempt(value => value + 1) }}>Повторить загрузку</button></div>
                : <section className="account-panel"><h1>Активность не найдена</h1><a href="#/">Вернуться к каталогу</a></section>
          : <AccountPage route={route} />}
      </main>
      <footer><a className="footer-brand" href="/">SteamIRL</a><span>Меньше переписок. Больше игры.</span><span>Дипломный проект · Вильнюс</span></footer>
    </div>
  )
}
