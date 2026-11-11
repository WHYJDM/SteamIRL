param([string]$Psql = 'psql', [string]$OutputDirectory = 'docs')
$ErrorActionPreference='Stop'
# Connection comes from standard PGHOST/PGPORT/PGDATABASE/PGUSER/PGPASSWORD variables.
$raw = & $Psql -X --no-password -v ON_ERROR_STOP=1 -At -f database/describe.sql
if ($LASTEXITCODE -ne 0) { throw 'Cannot read schema metadata. No document was changed.' }
$tables = ($raw -join "`n") | ConvertFrom-Json
$purpose = @{
 activities='Управляемый справочник видов спорта (US03).'; locations='Площадка и её текущая одобренная/ожидающая версия (US03,05,06,14).';
 location_activities='Связь площадки и активности; отключение без удаления истории.'; sessions='Приглашение на игру, дополнительные места и срок действия (US08–11).';
 participations='Отклик игрока; намерение прийти, без подтверждения присутствия (US10).'; public_profiles='Публичный псевдоним отдельно от приватного аккаунта (US02).';
 location_submissions='Новая площадка или версия исправления, ожидающая проверки (US05–06).'; submission_activities='Предложенные активности конкретной заявки.';
 content_reports='Приватная жалоба с ровно одной типизированной целью (US13).'; moderation_decisions='Неизменяемое окончательное решение и исторические снимки аудита (US14).';
 idempotency_requests='Результат команды для безопасного повтора в течение 24 часов.'; AspNetUsers='Приватные данные авторизации ASP.NET Core Identity (US01).';
 AspNetRoles='Роли приложения, включая Moderator; это не роли PostgreSQL.'; AspNetUserRoles='Принадлежность аккаунта роли приложения.';
 AspNetRoleClaims='Служебные утверждения роли Identity.'; AspNetUserClaims='Служебные утверждения аккаунта Identity.'; AspNetUserLogins='Служебные связи с провайдерами входа Identity; внешние провайдеры пока не включены.';
 AspNetUserTokens='Служебные токены Identity; не публичные данные.'; __EFMigrationsHistory='Технический журнал применённых миграций EF Core.';
 account_directory='Представление для контролируемого чтения: email полностью маскирован, поля авторизации исключены.';
 location_catalogue='Представление одобренных площадок и включённых активностей.'; session_overview='Отчёт о приглашениях и откликах; статус вычисляется, это не учёт посещаемости.'
}
$fields = @{
 Id='Идентификатор записи'; UserId='Связанный аккаунт'; RoleId='Роль приложения'; LocationId='Целевая площадка'; ActivityId='Активность справочника'; SessionId='Игровое приглашение';
 AuthorId='Автор вклада/жалобы'; ModeratorId='Аккаунт принявшего решение'; OrganiserId='Организатор приглашения'; SubmissionId='Предложение площадки/исправления'; ReportId='Жалоба';
 Name='Название'; Description='Описание'; Address='Адрес или ориентир площадки'; DisplayName='Публичный псевдоним'; Latitude='Широта общественной площадки, не геолокация человека'; Longitude='Долгота общественной площадки';
 ApprovalStatus='Pending/Approved/Rejected/Removed'; Revision='Версия текущей записи для защиты от устаревших исправлений'; IsEnabled='Поддерживается ли активность сейчас';
 UpdatedAt='Момент последнего изменения'; CreatedAt='Момент создания'; SubmittedAt='Серверное время отправки'; SourceUrl='Необязательная HTTPS-ссылка на источник'; SourceCheckedAt='Время проверки сведений'; CoordinateNote='Точность точки и ориентиры'; AccessNotes='Условия доступа и инвентарь';
 Kind='Вид записи: Planned/Immediate для игры, New/Correction для заявки'; SkillLevel='Beginner/Intermediate/Advanced'; EquipmentStatus='BallAvailable/BallNeeded/NotSpecified'; Capacity='Требуемые дополнительные игроки, 1–30';
 StartsAt='Начало приглашения'; EndsAt='Окончание приглашения'; CancelledAt='Момент отмены, NULL означает отсутствие отмены'; CancellationReason='Общая публичная причина отмены без приватной жалобы'; JoinedAt='Время отклика'; LeftAt='Время выхода; NULL означает активный отклик';
 Key='Ключ повторяемой команды'; Operation='Вид команды'; RequestHash='SHA-256 нормализованных параметров'; ResponseJson='Сохранённый исходный ответ команды'; ResponseStatus='Исходный HTTP-статус'; ContentType='Тип ответа'; ResourceLocation='Ссылка на созданный ресурс'; ExpiresAt='Окончание гарантии повтора';
 State='Состояние рассмотрения'; BaseRevision='Версия площадки, на основе которой подготовлено предложение'; Fingerprint='Отпечаток для проверки точного дубля'; OriginalDataJson='Исторический снимок прежней версии'; Evidence='Источник/обоснование изменения, приватно для автора и модератора';
 Reason='Причина жалобы или обоснование решения'; Details='Приватные подробности жалобы'; Action='Approve/Reject/Resolve/Dismiss/RemoveContent'; BeforeJson='Снимок до решения, не публичный ответ'; AfterJson='Снимок после решения'; AffectedSessions='Число отменённых активных игр'; DecidedAt='Время решения';
 UserName='Имя входа Identity'; NormalizedUserName='Нормализованное имя входа'; Email='Приватный email'; NormalizedEmail='Email в форме для поиска/уникальности'; EmailConfirmed='Подтверждён ли email'; PasswordHash='Хеш пароля Identity, не открытый пароль';
 SecurityStamp='Метка изменения безопасности'; ConcurrencyStamp='Токен конкурентного обновления Identity'; PhoneNumber='Неиспользуемое сейчас служебное поле Identity, не собирается UI'; PhoneNumberConfirmed='Служебный признак Identity'; TwoFactorEnabled='Служебный признак Identity'; LockoutEnd='Окончание блокировки входа'; LockoutEnabled='Разрешена ли блокировка входа'; AccessFailedCount='Число неудачных входов';
 NormalizedName='Нормализованное имя роли'; ClaimType='Тип утверждения'; ClaimValue='Значение утверждения'; LoginProvider='Провайдер входа'; ProviderKey='Идентификатор у провайдера'; ProviderDisplayName='Название провайдера'; Value='Служебное значение токена'; MigrationId='Идентификатор применённой миграции'; ProductVersion='Версия EF Core';
 slug='Уникальное техническое имя активности'; user_id='Идентификатор аккаунта'; display_name='Публичный псевдоним'; email_masked='Полная маска email; исходный адрес не выдаётся';
 location_id='Площадка'; location_name='Название площадки'; updated_at='Время обновления площадки'; activity_id='Активность'; activity_slug='Техническое имя активности'; activity_name='Название активности';
 session_id='Приглашение'; additional_capacity='Количество дополнительных мест'; active_participants='Активные отклики, не подтверждённые посетители'; remaining_places='Свободные дополнительные места'; starts_at='Начало'; ends_at='Окончание'; status='Cancelled → Expired → Full → Open, вычисляется при чтении'
}
function Cell($value) { if ($null -eq $value -or "$value" -eq '') { return '—' }; return ("$value".Replace('|','\|').Replace("`n",' ')) }
$lines = New-Object 'System.Collections.Generic.List[string]'
$lines.Add('# Словарь данных SteamIRL'); $lines.Add(''); $lines.Add('Сгенерирован из PostgreSQL после шести миграций. Имена, типы, NULL, значения по умолчанию, ключи и индексы читаются из системного каталога; строки пользователей не выгружаются. Время — `timestamptz` (UTC-момент), UI показывает Europe/Vilnius.');
foreach ($table in $tables) {
 $lines.Add(''); $lines.Add("## $($table.schema_name).$($table.table_name)"); $lines.Add(''); $lines.Add($purpose[$table.table_name]); $lines.Add('');
 $lines.Add('| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |'); $lines.Add('| --- | --- | --- | --- | --- |');
 foreach ($column in $table.columns) {
  $description=$fields[$column.name]; if (!$description) { throw "Missing dictionary description for $($table.table_name).$($column.name)" }
  $default=Cell $column.default; if ($column.identity) { $default="IDENTITY ($($column.identity))" }
  $lines.Add("| $($column.name) | $($column.type) | $($column.required) | $default | $description |")
 }
 if ($table.constraints) { $lines.Add(''); $lines.Add('Ограничения:'); $lines.Add(''); foreach ($constraint in $table.constraints) { $lines.Add('- `' + $constraint.name + '`: `' + (Cell $constraint.definition) + '`.') } }
 if ($table.indexes) { $lines.Add(''); $lines.Add('Индексы:'); $lines.Add(''); foreach ($index in $table.indexes) { $lines.Add('- `' + (Cell $index) + '`.') } }
}
[IO.File]::WriteAllLines((Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'data-dictionary.md'),$lines,[Text.UTF8Encoding]::new($false))
$diagram=New-Object 'System.Collections.Generic.List[string]'
$diagram.Add('# Логическая схема реализованной БД'); $diagram.Add(''); $diagram.Add('Схема связей реализованных таблиц после шести миграций. PK/FK и ограничения перечислены в [словаре данных](data-dictionary.md). Параллельные FK к одной таблице обозначают разные роли аккаунта. Планируемые функции описаны отдельно в [соответствии BA](ba-traceability.md).'); $diagram.Add(''); $diagram.Add('```mermaid'); $diagram.Add('erDiagram');
foreach ($table in $tables | Where-Object {$_.kind -eq 'r' -and $_.table_name -ne '__EFMigrationsHistory'}) {
 foreach ($fk in @($table.constraints | Where-Object {$_.type -eq 'f'})) {
  $parentCardinality=if ($fk.optional) {'|o'} else {'||'}
  $childCardinality=if ($fk.unique) {'o|'} else {'o{'}
  $diagram.Add("    $($fk.target) $parentCardinality--$childCardinality $($table.table_name) : references")
 }
}
$diagram.Add('```'); $diagram.Add(''); $diagram.Add('Представления reporting не являются дополнительными хранилищами: account_directory читает AspNetUsers/public_profiles с полной маской email; location_catalogue читает locations/location_activities/activities; session_overview добавляет sessions/participations и вычисляет статус. Журнал __EFMigrationsHistory технический.');
[IO.File]::WriteAllLines((Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'logical-schema.md'),$diagram,[Text.UTF8Encoding]::new($false))
Write-Host "Exported metadata for $($tables.Count) tables/views. No user records or credentials exported."
