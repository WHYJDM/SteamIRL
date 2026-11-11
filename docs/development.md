# Разработка и запуск SteamIRL

## Что уже реализовано

- Решение .NET 10 с ASP.NET Core Web API, EF Core и провайдером Npgsql.
- Первая миграция PostgreSQL: таблица активностей, уникальный slug и начальный каталог из четырёх видов спорта.
- `GET /api/activities`, проверка жизни `/health/live` и готовности базы и схемы `/health/ready`.
- `GET /api/locations` и `GET /api/locations/{id}`: одобренные места, активности, источник и даты. Начальные данные содержат два комплекса Вильнюса; [границы проверки данных](location-sources.md).
- Игровые сессии: создание, фильтры, подтверждение вступления, выход, изменение вместимости, отмена и «Мои игры». [Правила API и отчёт](sessions-verification.md).
- Централизованные ответы об ошибках в формате Problem Details и JSON-логи.
- Регистрация, вход, выход, проверка сессии, собственный профиль и изменение псевдонима. Публичный профиль доступен гостям и содержит только ID и псевдоним.
- ASP.NET Core Identity, хеширование паролей, HttpOnly cookie, CSRF-токены для изменяющих запросов, блокировка после пяти неверных паролей на пять минут и лимит 20 запросов регистрации/входа в минуту на адрес, видимый API.
- React + TypeScript + Vite: каталог с выбором активности, состояния загрузки, ошибки и пустого ответа, адаптивная страница.
- Docker Compose с постоянным томом базы, отдельным применением миграций и общим origin для интерфейса и API через nginx.
- Интеграционные тесты на PostgreSQL и CI для сборок, тестов и проверки контейнерного запуска.

После выбора спорта открывается `/#/activities/{slug}` со списком и картой. Страница `/#/locations/{id}` показывает карточку места и его игры. Поиск игр: `/#/sessions`, создание с выбранной площадки: `/#/sessions/new?location={id}&activity={slug}`, карточка: `/#/sessions/{id}`, личная история: `/#/my-games`.

Параметры `/api/locations`: необязательный `activityId`, `page` (по умолчанию 1), `pageSize` (по умолчанию 50, максимум 100) и область `minLat`, `maxLat`, `minLon`, `maxLon`. Границы передаются вместе; обратные интервалы и переход через антимеридиан не поддерживаются. Ответ: `{ items, page, pageSize, hasMore }`. Без активности доступны все одобренные площадки. На карте загружаются первые 100 записей видимой области; при превышении лимита интерфейс предлагает увеличить масштаб.

Фон карты загружается напрямую с OpenStreetMap, с атрибуцией и обычным браузерным кешем. Загрузка извне может блокироваться сетью, но адреса и карточки доступны через список. Проверка геометрии карты: `node --test tests/SteamIRL.Web.Tests/map-math.test.mjs` из корня проекта.

Страницы аккаунта: `/#/register`, `/#/login`, `/#/me`, `/#/profiles/{id}`. После регистрации вход выполняется автоматически. Сессия сохраняется на семь дней с продлением при активности; выход удаляет cookie из браузера. Пароль: 10–128 символов, минимум одна заглавная и строчная латинская буква и цифра. Псевдоним: 2–60 символов после удаления пробелов по краям.

## Запуск через Docker Compose

Нужен запущенный Docker Engine с Linux-контейнерами и Docker Compose v2. Все команды выполняются из корня проекта.

1. Выполнить `powershell -NoProfile -File scripts/New-LocalSecrets.ps1`: создаётся игнорируемый `.env` с четырьмя разными паролями. Существующий файл не перезаписывается. `.env.example` — только пустой шаблон.
2. Выполнить `docker compose up --build --wait --wait-timeout 180`.
3. Открыть [http://localhost:8088](http://localhost:8088).

База доступна с компьютера по `localhost:55432`. Эти порты можно изменить в `.env`. Backend напрямую наружу не публикуется; каталог доступен по `http://localhost:8088/api/activities`.

Порядок запуска: PostgreSQL → `bootstrap` создаёт роли/передаёт владение → `migrate` под владельцем применяет миграции → `grant-access` выдаёт явные права → API под ограниченным пользователем → nginx. Статус Exited (0) у одноразовых сервисов нормален. Администраторский пароль не передаётся в `api`. С существующим томом сохранить первоначальный POSTGRES_USER и добавить три новых пароля; не удалять том ради смены ролей.

Отдельный том `auth-keys` хранит ключи Data Protection для cookie между пересозданиями API-контейнера. В локальном Compose явно разрешены cookie по HTTP; в другом окружении по умолчанию требуется HTTPS. При локальном `dotnet run` ключи хранятся в игнорируемой папке `src/SteamIRL.Api/.local/keys`; путь переопределяется через `DataProtection__KeyPath`.

Команды для диагностики:

```powershell
docker compose ps -a
docker compose logs api bootstrap migrate grant-access web
```

`docker compose down` останавливает окружение и сохраняет данные в томе. `docker compose down --volumes` удаляет базу этого Compose-проекта; использовать только когда её данные больше не нужны. Смена пароля в `.env` не меняет пароль уже инициализированного PostgreSQL.

## Локальная разработка

Нужны .NET SDK 10.0.200 или новее в ветке 10.0, Node.js 24 и PostgreSQL 17. Конкретные версии npm-зависимостей зафиксированы в `package-lock.json`; версии .NET-пакетов — в файлах проектов.

Базу можно поднять отдельно:

```powershell
docker compose up -d --wait postgres
```

В первом терминале собрать проект, настроить роли и применить миграции к уже созданной выделенной базе, затем запустить API:

```powershell
dotnet restore SteamIRL.sln
dotnet build SteamIRL.sln --no-restore
powershell -NoProfile -File scripts/Initialize-LocalDatabase.ps1
powershell -NoProfile -File scripts/Start-LocalApi.ps1
```

`.env` читают Docker Compose и приведённые скрипты; обычный `dotnet run` сам этот файл не загружает. Для отдельно установленного PostgreSQL предварительно создать базу и задать соответствующие `-DatabaseHost`/`-DatabasePort`. Если административное подключение отличается от `.env`, передать `STEAMIRL_ADMIN_CONNECTION` только скрипту инициализации. API использует `${DATABASE_ROLE_PREFIX}_app`; обычный запуск под владельцем/суперпользователем отклоняется. В текущем рабочем окружении используются порт 55433 и локальный игнорируемый `.local/assessment-secrets.env`; это не универсальные параметры установки.

Во втором терминале:

```powershell
Set-Location src/SteamIRL.Web
npm ci
npm run dev
```

Открыть [http://localhost:5173](http://localhost:5173). Vite проксирует `/api` и `/health` на API по порту 5080. Запросы frontend используют относительные URL; отдельная настройка CORS не требуется.

HTTP и локальная публикация портов предназначены для разработки. Публичное размещение, TLS, резервные копии и секреты окружения проектируются на этапе развёртывания.

## Проверки

Создать **отдельную пустую** базу с именем `steamirl_test` или с таким префиксом. Тесты применяют миграции, сохраняют каталог между запусками и проверяют ограничения на изолированных неуспешных вставках. Не использовать рабочую базу.

Например, при запущенной Compose-базе:

```powershell
docker compose exec postgres createdb -U steamirl steamirl_test
$env:STEAMIRL_TEST_DATABASE = 'Host=localhost;Port=55432;Database=steamirl_test;Username=steamirl;Password=YOUR_LOCAL_PASSWORD'
dotnet test SteamIRL.sln
npm --prefix src/SteamIRL.Web run build
```

`createdb` выполняется один раз. Тесты намеренно завершаются ошибкой без `STEAMIRL_TEST_DATABASE`, чтобы отсутствие PostgreSQL не выглядело как успешная проверка интеграции.

Проверяются каталог, миграции, ограничения, health endpoints, безопасный отказ базы, аккаунты/CSRF/приватность, игры и конкуренция, предложения и модерация, реальные права runtime/reader, маскирование, триггеры аудита и повторяемость demo-seed. Сейчас 111 интеграционных тестов и 7 frontend-тестов. Запуск frontend-тестов: `node --test tests/SteamIRL.Web.Tests/map-math.test.mjs tests/SteamIRL.Web.Tests/session-time.test.mjs`. Отчёты: [аккаунты](accounts-verification.md), [площадки](locations-verification.md), [игры](sessions-verification.md), [модерация](moderation-verification.md), [подготовка БД](database-assessment-verification.md).

Тестовой установочной учётной записи нужны CREATEDB/CREATEROLE или права администратора изолированного тестового кластера: тесты создают уникальные `steamirl_test_moderation_<uuid>` / `steamirl_test_access_<uuid>` и отдельные непривилегированные роли, затем удаляют только свои объекты. Это не учётная запись web-приложения. Тестовый host явно отключает стартовую проверку роли; права отдельно проверяются на реальных подключениях и HTTP-сценарии под runtime-ролью.

Клиент API перед POST/PATCH получает `GET /api/auth/csrf` и отправляет поле `token` из ответа в заголовке `X-CSRF-TOKEN`, сохраняя cookie того же браузера. После входа и выхода нужно получать свежий токен для текущей личности. `GET /api/auth/session` возвращает `{ user: null }` гостю, а `GET /api/me` требует входа. Для изменения профиля используется только `PATCH /api/me/profile` с `{ displayName }`; передать чужой ID нельзя.

Для всех команд игр дополнительно обязателен `Idempotency-Key`. При неясном сетевом результате повторяется исходный ключ с исходными параметрами, при новом намерении создаётся новый ключ. Срок повтора — 24 часа. Время Planned передаётся строго в ISO с `Z`/UTC-offset, а Immediate принимает `durationMinutes` (15–240). Автообновление списков и карточек — каждые 30 секунд и при фокусе окна.

## Обращения и модерация

Для предложений площадок, жалоб и решений модератора обязательны CSRF и `Idempotency-Key`, как для команд игр. Приватные обращения доступны автору и назначенным модераторам.

### Назначение модератора

Зарегистрировать обычный аккаунт; его `id` доступен в собственном ответе `GET /api/me`. При настроенном `ConnectionStrings__Database` выполнить из корня:

```powershell
dotnet run --project src/SteamIRL.Api --no-launch-profile -- --grant-moderator ACCOUNT_UUID
```

После обновления страницы появится ссылка «Модерация» (`#/moderation`). Автоматического назначения роли нет. Для отзыва:

```powershell
dotnet run --project src/SteamIRL.Api --no-launch-profile -- --revoke-moderator ACCOUNT_UUID
```

Эти CLI-команды выполняются с отдельной строкой подключения мигратора в ConnectionStrings__Database: runtime не имеет права менять AspNetUserRoles. Если API уже запущен и Windows блокирует сборку apphost, добавить `--no-build`. В контейнере: `docker compose run --rm migrate --grant-moderator ACCOUNT_UUID` (или `--revoke-moderator`); команду нельзя выполнять с подключением сервиса api. Роль читается из БД при каждом запросе, старые cookie не сохраняют отозванные права.

## Новые миграции

При изменении модели использовать EF Core CLI 10.0.5 (если он не установлен: `dotnet tool install --global dotnet-ef --version 10.0.5`). Установить `ConnectionStrings__Database` как при локальном запуске, затем:

```powershell
dotnet ef migrations add MigrationName --project src/SteamIRL.Api --output-dir Data/Migrations
dotnet run --project src/SteamIRL.Api --no-launch-profile -- --migrate
```

Миграции и snapshot хранятся в репозитории. Обычный web-процесс не меняет схему автоматически.

После миграций повторить `--grant-database-access` с подключением мигратора. Для DDL без EF есть `database/schema.sql`, сгенерированный командой `dotnet ef migrations script --idempotent --project src/SteamIRL.Api --output database/schema.sql`. Применять либо миграции, либо этот скрипт на подготовленной базе; оба используют один журнал EF. Справочник уже включён, `database/seed-reference.sql` — отдельное безопасное повторное заполнение без перезаписи существующих значений.

## Демонстрационная база

Создать отдельную пустую базу `steamirl_demo` административным подключением. Сгенерировать отдельные секреты/префикс, чтобы не менять роли основной базы:

```powershell
powershell -NoProfile -File scripts/New-LocalSecrets.ps1 -OutputPath .local/demo.env -DatabaseName steamirl_demo -RolePrefix steamirl_demo
powershell -NoProfile -File scripts/Initialize-LocalDatabase.ps1 -SecretsPath .local/demo.env
$demoBytes = New-Object byte[] 24
$demoGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
$demoGenerator.GetBytes($demoBytes)
$demoGenerator.Dispose()
$env:Demo__Password = 'Aa1' + ([BitConverter]::ToString($demoBytes)).Replace('-', '')
powershell -NoProfile -File scripts/Seed-LocalDemo.ps1 -SecretsPath .local/demo.env
```

Пароль нужно безопасно сохранить локально для показа; команды его не печатают. После запуска удалить `Env:Demo__Password` из текущего процесса. Вход: `demo-organiser@example.invalid` / `demo-player@example.invalid` с этим сгенерированным паролем. Добавляются два `[ДЕМО]` профиля и одно Planned-приглашение на завтра с одним откликом. Повтор не создаёт дубли и не меняет уже существующую игру; истёкший пример остаётся историей. Демонстрация не назначает модератора автоматически. Запуск CLI требует Development + Demo__AllowSeed, имя БД `_demo` (либо изолированный тестовый префикс), секрет извне.

## Обновление материалов БД

Для генерации словаря и ER-связей установить стандартные переменные подключения psql (`PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`; пароль только в локальном окружении), затем выполнить `powershell -NoProfile -File scripts/Export-DatabaseDocumentation.ps1 -Psql psql`. После генерации проверить diff: этот скрипт читает только системный каталог и не выгружает записи пользователей. Матрица требований и 3НФ дополняются вручную при изменении модели.

Перед локальным коммитом: `powershell -NoProfile -File scripts/Check-Repository.ps1`. Публикация и доступ эксперта пока отложены по указанию владельца.

## Технические основания

Для backend выбрана ветка [.NET 10 LTS](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), совместимая с установленным SDK. [Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html) используется для PostgreSQL. Frontend использует Node 24 с учётом [требований Vite](https://vite.dev/guide/).
