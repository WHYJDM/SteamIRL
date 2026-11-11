# Словарь данных SteamIRL

Сгенерирован из PostgreSQL после шести миграций. Имена, типы, NULL, значения по умолчанию, ключи и индексы читаются из системного каталога; строки пользователей не выгружаются. Время — `timestamptz` (UTC-момент), UI показывает Europe/Vilnius.

## public.AspNetRoleClaims

Служебные утверждения роли Identity.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| Id | integer | True | IDENTITY (d) | Идентификатор записи |
| RoleId | uuid | True | — | Роль приложения |
| ClaimType | text | False | — | Тип утверждения |
| ClaimValue | text | False | — | Значение утверждения |

Ограничения:

- `FK_AspNetRoleClaims_AspNetRoles_RoleId`: `FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles"("Id") ON DELETE CASCADE`.
- `PK_AspNetRoleClaims`: `PRIMARY KEY ("Id")`.

Индексы:

- `CREATE INDEX "IX_AspNetRoleClaims_RoleId" ON public."AspNetRoleClaims" USING btree ("RoleId")`.
- `CREATE UNIQUE INDEX "PK_AspNetRoleClaims" ON public."AspNetRoleClaims" USING btree ("Id")`.

## public.AspNetRoles

Роли приложения, включая Moderator; это не роли PostgreSQL.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| Id | uuid | True | — | Идентификатор записи |
| Name | character varying(256) | False | — | Название |
| NormalizedName | character varying(256) | False | — | Нормализованное имя роли |
| ConcurrencyStamp | text | False | — | Токен конкурентного обновления Identity |

Ограничения:

- `PK_AspNetRoles`: `PRIMARY KEY ("Id")`.

Индексы:

- `CREATE UNIQUE INDEX "PK_AspNetRoles" ON public."AspNetRoles" USING btree ("Id")`.
- `CREATE UNIQUE INDEX "RoleNameIndex" ON public."AspNetRoles" USING btree ("NormalizedName")`.

## public.AspNetUserClaims

Служебные утверждения аккаунта Identity.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| Id | integer | True | IDENTITY (d) | Идентификатор записи |
| UserId | uuid | True | — | Связанный аккаунт |
| ClaimType | text | False | — | Тип утверждения |
| ClaimValue | text | False | — | Значение утверждения |

Ограничения:

- `FK_AspNetUserClaims_AspNetUsers_UserId`: `FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE`.
- `PK_AspNetUserClaims`: `PRIMARY KEY ("Id")`.

Индексы:

- `CREATE INDEX "IX_AspNetUserClaims_UserId" ON public."AspNetUserClaims" USING btree ("UserId")`.
- `CREATE UNIQUE INDEX "PK_AspNetUserClaims" ON public."AspNetUserClaims" USING btree ("Id")`.

## public.AspNetUserLogins

Служебные связи с провайдерами входа Identity; внешние провайдеры пока не включены.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| LoginProvider | text | True | — | Провайдер входа |
| ProviderKey | text | True | — | Идентификатор у провайдера |
| ProviderDisplayName | text | False | — | Название провайдера |
| UserId | uuid | True | — | Связанный аккаунт |

Ограничения:

- `FK_AspNetUserLogins_AspNetUsers_UserId`: `FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE`.
- `PK_AspNetUserLogins`: `PRIMARY KEY ("LoginProvider", "ProviderKey")`.

Индексы:

- `CREATE INDEX "IX_AspNetUserLogins_UserId" ON public."AspNetUserLogins" USING btree ("UserId")`.
- `CREATE UNIQUE INDEX "PK_AspNetUserLogins" ON public."AspNetUserLogins" USING btree ("LoginProvider", "ProviderKey")`.

## public.AspNetUserRoles

Принадлежность аккаунта роли приложения.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| UserId | uuid | True | — | Связанный аккаунт |
| RoleId | uuid | True | — | Роль приложения |

Ограничения:

- `FK_AspNetUserRoles_AspNetRoles_RoleId`: `FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles"("Id") ON DELETE CASCADE`.
- `FK_AspNetUserRoles_AspNetUsers_UserId`: `FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE`.
- `PK_AspNetUserRoles`: `PRIMARY KEY ("UserId", "RoleId")`.

Индексы:

- `CREATE INDEX "IX_AspNetUserRoles_RoleId" ON public."AspNetUserRoles" USING btree ("RoleId")`.
- `CREATE UNIQUE INDEX "PK_AspNetUserRoles" ON public."AspNetUserRoles" USING btree ("UserId", "RoleId")`.

## public.AspNetUserTokens

Служебные токены Identity; не публичные данные.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| UserId | uuid | True | — | Связанный аккаунт |
| LoginProvider | text | True | — | Провайдер входа |
| Name | text | True | — | Название |
| Value | text | False | — | Служебное значение токена |

Ограничения:

- `FK_AspNetUserTokens_AspNetUsers_UserId`: `FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE`.
- `PK_AspNetUserTokens`: `PRIMARY KEY ("UserId", "LoginProvider", "Name")`.

Индексы:

- `CREATE UNIQUE INDEX "PK_AspNetUserTokens" ON public."AspNetUserTokens" USING btree ("UserId", "LoginProvider", "Name")`.

## public.AspNetUsers

Приватные данные авторизации ASP.NET Core Identity (US01).

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| Id | uuid | True | — | Идентификатор записи |
| CreatedAt | timestamp with time zone | True | — | Момент создания |
| UserName | character varying(256) | False | — | Имя входа Identity |
| NormalizedUserName | character varying(256) | False | — | Нормализованное имя входа |
| Email | character varying(256) | False | — | Приватный email |
| NormalizedEmail | character varying(256) | False | — | Email в форме для поиска/уникальности |
| EmailConfirmed | boolean | True | — | Подтверждён ли email |
| PasswordHash | text | False | — | Хеш пароля Identity, не открытый пароль |
| SecurityStamp | text | False | — | Метка изменения безопасности |
| ConcurrencyStamp | text | False | — | Токен конкурентного обновления Identity |
| PhoneNumber | text | False | — | Неиспользуемое сейчас служебное поле Identity, не собирается UI |
| PhoneNumberConfirmed | boolean | True | — | Служебный признак Identity |
| TwoFactorEnabled | boolean | True | — | Служебный признак Identity |
| LockoutEnd | timestamp with time zone | False | — | Окончание блокировки входа |
| LockoutEnabled | boolean | True | — | Разрешена ли блокировка входа |
| AccessFailedCount | integer | True | — | Число неудачных входов |

Ограничения:

- `PK_AspNetUsers`: `PRIMARY KEY ("Id")`.

Индексы:

- `CREATE UNIQUE INDEX "EmailIndex" ON public."AspNetUsers" USING btree ("NormalizedEmail")`.
- `CREATE UNIQUE INDEX "PK_AspNetUsers" ON public."AspNetUsers" USING btree ("Id")`.
- `CREATE UNIQUE INDEX "UserNameIndex" ON public."AspNetUsers" USING btree ("NormalizedUserName")`.

## public.__EFMigrationsHistory

Технический журнал применённых миграций EF Core.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| MigrationId | character varying(150) | True | — | Идентификатор применённой миграции |
| ProductVersion | character varying(32) | True | — | Версия EF Core |

Ограничения:

- `PK___EFMigrationsHistory`: `PRIMARY KEY ("MigrationId")`.

Индексы:

- `CREATE UNIQUE INDEX "PK___EFMigrationsHistory" ON public."__EFMigrationsHistory" USING btree ("MigrationId")`.

## public.activities

Управляемый справочник видов спорта (US03).

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| id | uuid | True | — | Идентификатор записи |
| slug | character varying(80) | True | — | Уникальное техническое имя активности |
| name | character varying(120) | True | — | Название |
| description | character varying(2000) | True | — | Описание |

Ограничения:

- `PK_activities`: `PRIMARY KEY (id)`.
- `ck_activities_slug`: `CHECK (((slug)::text ~ '^[a-z]+(-[a-z]+)*$'::text))`.

Индексы:

- `CREATE UNIQUE INDEX "IX_activities_slug" ON public.activities USING btree (slug)`.
- `CREATE UNIQUE INDEX "PK_activities" ON public.activities USING btree (id)`.

## public.content_reports

Приватная жалоба с ровно одной типизированной целью (US13).

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| Id | uuid | True | — | Идентификатор записи |
| AuthorId | uuid | True | — | Автор вклада/жалобы |
| LocationId | uuid | False | — | Целевая площадка |
| SessionId | uuid | False | — | Игровое приглашение |
| Reason | character varying(20) | True | — | Причина жалобы или обоснование решения |
| Details | character varying(2000) | True | — | Приватные подробности жалобы |
| State | character varying(16) | True | — | Состояние рассмотрения |
| SubmittedAt | timestamp with time zone | True | — | Серверное время отправки |

Ограничения:

- `FK_content_reports_AspNetUsers_AuthorId`: `FOREIGN KEY ("AuthorId") REFERENCES "AspNetUsers"("Id") ON DELETE RESTRICT`.
- `FK_content_reports_locations_LocationId`: `FOREIGN KEY ("LocationId") REFERENCES locations("Id") ON DELETE RESTRICT`.
- `FK_content_reports_sessions_SessionId`: `FOREIGN KEY ("SessionId") REFERENCES sessions("Id") ON DELETE RESTRICT`.
- `PK_content_reports`: `PRIMARY KEY ("Id")`.
- `ck_report_reason`: `CHECK ((("Reason")::text = ANY ((ARRAY['Inaccurate'::character varying, 'Spam'::character varying, 'Abuse'::character varying, 'AccessIssue'::character varying, 'Safety'::character varying, 'Other'::character varying])::text[])))`.
- `ck_report_state`: `CHECK ((("State")::text = ANY ((ARRAY['Open'::character varying, 'Resolved'::character varying, 'Dismissed'::character varying])::text[])))`.
- `ck_report_target`: `CHECK ((("LocationId" IS NOT NULL) <> ("SessionId" IS NOT NULL)))`.

Индексы:

- `CREATE UNIQUE INDEX "IX_content_reports_AuthorId_LocationId_Reason" ON public.content_reports USING btree ("AuthorId", "LocationId", "Reason") WHERE ((("State")::text = 'Open'::text) AND ("LocationId" IS NOT NULL))`.
- `CREATE UNIQUE INDEX "IX_content_reports_AuthorId_SessionId_Reason" ON public.content_reports USING btree ("AuthorId", "SessionId", "Reason") WHERE ((("State")::text = 'Open'::text) AND ("SessionId" IS NOT NULL))`.
- `CREATE INDEX "IX_content_reports_AuthorId_SubmittedAt" ON public.content_reports USING btree ("AuthorId", "SubmittedAt")`.
- `CREATE INDEX "IX_content_reports_LocationId" ON public.content_reports USING btree ("LocationId")`.
- `CREATE INDEX "IX_content_reports_SessionId" ON public.content_reports USING btree ("SessionId")`.
- `CREATE INDEX "IX_content_reports_State_SubmittedAt" ON public.content_reports USING btree ("State", "SubmittedAt")`.
- `CREATE UNIQUE INDEX "PK_content_reports" ON public.content_reports USING btree ("Id")`.

## public.idempotency_requests

Результат команды для безопасного повтора в течение 24 часов.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| Id | uuid | True | — | Идентификатор записи |
| UserId | uuid | True | — | Связанный аккаунт |
| Key | character varying(128) | True | — | Ключ повторяемой команды |
| Operation | character varying(100) | True | — | Вид команды |
| RequestHash | character varying(64) | True | — | SHA-256 нормализованных параметров |
| ResponseJson | jsonb | True | — | Сохранённый исходный ответ команды |
| ContentType | character varying(80) | True | — | Тип ответа |
| ResourceLocation | character varying(200) | False | — | Ссылка на созданный ресурс |
| ResponseStatus | integer | True | — | Исходный HTTP-статус |
| CreatedAt | timestamp with time zone | True | — | Момент создания |
| ExpiresAt | timestamp with time zone | True | — | Окончание гарантии повтора |

Ограничения:

- `FK_idempotency_requests_AspNetUsers_UserId`: `FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE RESTRICT`.
- `PK_idempotency_requests`: `PRIMARY KEY ("Id")`.

Индексы:

- `CREATE INDEX "IX_idempotency_requests_ExpiresAt" ON public.idempotency_requests USING btree ("ExpiresAt")`.
- `CREATE UNIQUE INDEX "IX_idempotency_requests_UserId_Key" ON public.idempotency_requests USING btree ("UserId", "Key")`.
- `CREATE UNIQUE INDEX "PK_idempotency_requests" ON public.idempotency_requests USING btree ("Id")`.

## public.location_activities

Связь площадки и активности; отключение без удаления истории.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| LocationId | uuid | True | — | Целевая площадка |
| ActivityId | uuid | True | — | Активность справочника |
| IsEnabled | boolean | True | true | Поддерживается ли активность сейчас |

Ограничения:

- `FK_location_activities_activities_ActivityId`: `FOREIGN KEY ("ActivityId") REFERENCES activities(id) ON DELETE RESTRICT`.
- `FK_location_activities_locations_LocationId`: `FOREIGN KEY ("LocationId") REFERENCES locations("Id") ON DELETE RESTRICT`.
- `PK_location_activities`: `PRIMARY KEY ("LocationId", "ActivityId")`.

Индексы:

- `CREATE INDEX "IX_location_activities_ActivityId_LocationId" ON public.location_activities USING btree ("ActivityId", "LocationId")`.
- `CREATE UNIQUE INDEX "PK_location_activities" ON public.location_activities USING btree ("LocationId", "ActivityId")`.

## public.location_submissions

Новая площадка или версия исправления, ожидающая проверки (US05–06).

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| Id | uuid | True | — | Идентификатор записи |
| AuthorId | uuid | True | — | Автор вклада/жалобы |
| LocationId | uuid | True | — | Целевая площадка |
| Kind | character varying(16) | True | — | Вид записи: Planned/Immediate для игры, New/Correction для заявки |
| State | character varying(16) | True | — | Состояние рассмотрения |
| BaseRevision | bigint | True | — | Версия площадки, на основе которой подготовлено предложение |
| Fingerprint | character varying(64) | True | — | Отпечаток для проверки точного дубля |
| Name | character varying(200) | True | — | Название |
| Description | character varying(4000) | True | — | Описание |
| Address | character varying(300) | True | — | Адрес или ориентир площадки |
| Latitude | numeric(9,6) | True | — | Широта общественной площадки, не геолокация человека |
| Longitude | numeric(9,6) | True | — | Долгота общественной площадки |
| SourceUrl | character varying(1000) | True | — | Необязательная HTTPS-ссылка на источник |
| CoordinateNote | character varying(500) | True | — | Точность точки и ориентиры |
| AccessNotes | character varying(2000) | True | — | Условия доступа и инвентарь |
| Evidence | character varying(2000) | True | — | Источник/обоснование изменения, приватно для автора и модератора |
| OriginalDataJson | jsonb | True | — | Исторический снимок прежней версии |
| SubmittedAt | timestamp with time zone | True | — | Серверное время отправки |

Ограничения:

- `FK_location_submissions_AspNetUsers_AuthorId`: `FOREIGN KEY ("AuthorId") REFERENCES "AspNetUsers"("Id") ON DELETE RESTRICT`.
- `FK_location_submissions_locations_LocationId`: `FOREIGN KEY ("LocationId") REFERENCES locations("Id") ON DELETE RESTRICT`.
- `PK_location_submissions`: `PRIMARY KEY ("Id")`.
- `ck_submission_coordinates`: `CHECK (((("Latitude" >= ('-90'::integer)::numeric) AND ("Latitude" <= (90)::numeric)) AND (("Longitude" >= ('-180'::integer)::numeric) AND ("Longitude" <= (180)::numeric))))`.
- `ck_submission_kind`: `CHECK ((("Kind")::text = ANY ((ARRAY['New'::character varying, 'Correction'::character varying])::text[])))`.
- `ck_submission_revision`: `CHECK (("BaseRevision" >= 1))`.
- `ck_submission_state`: `CHECK ((("State")::text = ANY ((ARRAY['Pending'::character varying, 'Approved'::character varying, 'Rejected'::character varying])::text[])))`.

Индексы:

- `CREATE UNIQUE INDEX "IX_location_submissions_AuthorId_Fingerprint" ON public.location_submissions USING btree ("AuthorId", "Fingerprint") WHERE (("State")::text = 'Pending'::text)`.
- `CREATE INDEX "IX_location_submissions_AuthorId_SubmittedAt" ON public.location_submissions USING btree ("AuthorId", "SubmittedAt")`.
- `CREATE UNIQUE INDEX "IX_location_submissions_Fingerprint" ON public.location_submissions USING btree ("Fingerprint") WHERE ((("Kind")::text = 'New'::text) AND (("State")::text = 'Approved'::text))`.
- `CREATE INDEX "IX_location_submissions_LocationId" ON public.location_submissions USING btree ("LocationId")`.
- `CREATE INDEX "IX_location_submissions_State_SubmittedAt" ON public.location_submissions USING btree ("State", "SubmittedAt")`.
- `CREATE UNIQUE INDEX "PK_location_submissions" ON public.location_submissions USING btree ("Id")`.

## public.locations

Площадка и её текущая одобренная/ожидающая версия (US03,05,06,14).

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| Id | uuid | True | — | Идентификатор записи |
| Name | character varying(200) | True | — | Название |
| Description | character varying(4000) | True | — | Описание |
| Address | character varying(300) | True | — | Адрес или ориентир площадки |
| Latitude | numeric(9,6) | True | — | Широта общественной площадки, не геолокация человека |
| Longitude | numeric(9,6) | True | — | Долгота общественной площадки |
| ApprovalStatus | character varying(16) | True | — | Pending/Approved/Rejected/Removed |
| UpdatedAt | timestamp with time zone | True | — | Момент последнего изменения |
| SourceUrl | character varying(1000) | True | — | Необязательная HTTPS-ссылка на источник |
| SourceCheckedAt | timestamp with time zone | True | — | Время проверки сведений |
| CoordinateNote | character varying(500) | True | — | Точность точки и ориентиры |
| AccessNotes | character varying(2000) | True | — | Условия доступа и инвентарь |
| Revision | bigint | True | 1 | Версия текущей записи для защиты от устаревших исправлений |

Ограничения:

- `PK_locations`: `PRIMARY KEY ("Id")`.
- `ck_location_latitude`: `CHECK ((("Latitude" >= ('-90'::integer)::numeric) AND ("Latitude" <= (90)::numeric)))`.
- `ck_location_longitude`: `CHECK ((("Longitude" >= ('-180'::integer)::numeric) AND ("Longitude" <= (180)::numeric)))`.
- `ck_location_name`: `CHECK ((length(TRIM(BOTH FROM "Name")) > 0))`.
- `ck_location_revision`: `CHECK (("Revision" >= 1))`.
- `ck_location_status`: `CHECK ((("ApprovalStatus")::text = ANY ((ARRAY['Pending'::character varying, 'Approved'::character varying, 'Rejected'::character varying, 'Removed'::character varying])::text[])))`.

Индексы:

- `CREATE INDEX "IX_locations_ApprovalStatus_Name_Id" ON public.locations USING btree ("ApprovalStatus", "Name", "Id")`.
- `CREATE UNIQUE INDEX "PK_locations" ON public.locations USING btree ("Id")`.

## public.moderation_decisions

Неизменяемое окончательное решение и исторические снимки аудита (US14).

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| Id | uuid | True | — | Идентификатор записи |
| ModeratorId | uuid | True | — | Аккаунт принявшего решение |
| SubmissionId | uuid | False | — | Предложение площадки/исправления |
| ReportId | uuid | False | — | Жалоба |
| Action | character varying(20) | True | — | Approve/Reject/Resolve/Dismiss/RemoveContent |
| Reason | character varying(1000) | True | — | Причина жалобы или обоснование решения |
| BeforeJson | jsonb | True | — | Снимок до решения, не публичный ответ |
| AfterJson | jsonb | True | — | Снимок после решения |
| AffectedSessions | integer | True | — | Число отменённых активных игр |
| DecidedAt | timestamp with time zone | True | — | Время решения |

Ограничения:

- `FK_moderation_decisions_AspNetUsers_ModeratorId`: `FOREIGN KEY ("ModeratorId") REFERENCES "AspNetUsers"("Id") ON DELETE RESTRICT`.
- `FK_moderation_decisions_content_reports_ReportId`: `FOREIGN KEY ("ReportId") REFERENCES content_reports("Id") ON DELETE RESTRICT`.
- `FK_moderation_decisions_location_submissions_SubmissionId`: `FOREIGN KEY ("SubmissionId") REFERENCES location_submissions("Id") ON DELETE RESTRICT`.
- `PK_moderation_decisions`: `PRIMARY KEY ("Id")`.
- `ck_decision_action`: `CHECK (((("SubmissionId" IS NOT NULL) AND (("Action")::text = ANY ((ARRAY['Approve'::character varying, 'Reject'::character varying])::text[]))) OR (("ReportId" IS NOT NULL) AND (("Action")::text = ANY ((ARRAY['Resolve'::character varying, 'Dismiss'::character varying, 'RemoveContent'::character varying])::text[])))))`.
- `ck_decision_reason`: `CHECK ((length(TRIM(BOTH FROM "Reason")) >= 10))`.
- `ck_decision_target`: `CHECK ((("SubmissionId" IS NOT NULL) <> ("ReportId" IS NOT NULL)))`.

Индексы:

- `CREATE INDEX "IX_moderation_decisions_ModeratorId" ON public.moderation_decisions USING btree ("ModeratorId")`.
- `CREATE UNIQUE INDEX "IX_moderation_decisions_ReportId" ON public.moderation_decisions USING btree ("ReportId") WHERE ("ReportId" IS NOT NULL)`.
- `CREATE UNIQUE INDEX "IX_moderation_decisions_SubmissionId" ON public.moderation_decisions USING btree ("SubmissionId") WHERE ("SubmissionId" IS NOT NULL)`.
- `CREATE UNIQUE INDEX "PK_moderation_decisions" ON public.moderation_decisions USING btree ("Id")`.

## public.participations

Отклик игрока; намерение прийти, без подтверждения присутствия (US10).

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| SessionId | uuid | True | — | Игровое приглашение |
| UserId | uuid | True | — | Связанный аккаунт |
| JoinedAt | timestamp with time zone | True | — | Время отклика |
| LeftAt | timestamp with time zone | False | — | Время выхода; NULL означает активный отклик |

Ограничения:

- `FK_participations_AspNetUsers_UserId`: `FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE RESTRICT`.
- `FK_participations_sessions_SessionId`: `FOREIGN KEY ("SessionId") REFERENCES sessions("Id") ON DELETE RESTRICT`.
- `PK_participations`: `PRIMARY KEY ("SessionId", "UserId")`.
- `ck_participation_time`: `CHECK ((("LeftAt" IS NULL) OR ("LeftAt" >= "JoinedAt")))`.

Индексы:

- `CREATE INDEX "IX_participations_UserId_LeftAt" ON public.participations USING btree ("UserId", "LeftAt")`.
- `CREATE UNIQUE INDEX "PK_participations" ON public.participations USING btree ("SessionId", "UserId")`.

## public.public_profiles

Публичный псевдоним отдельно от приватного аккаунта (US02).

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| UserId | uuid | True | — | Связанный аккаунт |
| DisplayName | character varying(60) | True | — | Публичный псевдоним |

Ограничения:

- `FK_public_profiles_AspNetUsers_UserId`: `FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE`.
- `PK_public_profiles`: `PRIMARY KEY ("UserId")`.

Индексы:

- `CREATE UNIQUE INDEX "PK_public_profiles" ON public.public_profiles USING btree ("UserId")`.

## public.sessions

Приглашение на игру, дополнительные места и срок действия (US08–11).

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| Id | uuid | True | — | Идентификатор записи |
| LocationId | uuid | True | — | Целевая площадка |
| ActivityId | uuid | True | — | Активность справочника |
| OrganiserId | uuid | True | — | Организатор приглашения |
| Kind | character varying(16) | True | — | Вид записи: Planned/Immediate для игры, New/Correction для заявки |
| SkillLevel | character varying(16) | True | — | Beginner/Intermediate/Advanced |
| EquipmentStatus | character varying(20) | True | — | BallAvailable/BallNeeded/NotSpecified |
| Capacity | integer | True | — | Требуемые дополнительные игроки, 1–30 |
| StartsAt | timestamp with time zone | True | — | Начало приглашения |
| EndsAt | timestamp with time zone | True | — | Окончание приглашения |
| CancelledAt | timestamp with time zone | False | — | Момент отмены, NULL означает отсутствие отмены |
| CreatedAt | timestamp with time zone | True | — | Момент создания |
| UpdatedAt | timestamp with time zone | True | — | Момент последнего изменения |
| CancellationReason | character varying(1000) | False | — | Общая публичная причина отмены без приватной жалобы |

Ограничения:

- `FK_sessions_AspNetUsers_OrganiserId`: `FOREIGN KEY ("OrganiserId") REFERENCES "AspNetUsers"("Id") ON DELETE RESTRICT`.
- `FK_sessions_location_activities_LocationId_ActivityId`: `FOREIGN KEY ("LocationId", "ActivityId") REFERENCES location_activities("LocationId", "ActivityId") ON DELETE RESTRICT`.
- `PK_sessions`: `PRIMARY KEY ("Id")`.
- `ck_session_capacity`: `CHECK ((("Capacity" >= 1) AND ("Capacity" <= 30)))`.
- `ck_session_equipment`: `CHECK ((("EquipmentStatus")::text = ANY ((ARRAY['NotSpecified'::character varying, 'BallAvailable'::character varying, 'BallNeeded'::character varying])::text[])))`.
- `ck_session_kind`: `CHECK ((("Kind")::text = ANY ((ARRAY['Planned'::character varying, 'Immediate'::character varying])::text[])))`.
- `ck_session_level`: `CHECK ((("SkillLevel")::text = ANY ((ARRAY['Beginner'::character varying, 'Intermediate'::character varying, 'Advanced'::character varying])::text[])))`.
- `ck_session_time`: `CHECK (("EndsAt" > "StartsAt"))`.

Индексы:

- `CREATE INDEX "IX_sessions_ActivityId_EndsAt" ON public.sessions USING btree ("ActivityId", "EndsAt")`.
- `CREATE INDEX "IX_sessions_LocationId_ActivityId" ON public.sessions USING btree ("LocationId", "ActivityId")`.
- `CREATE INDEX "IX_sessions_LocationId_EndsAt" ON public.sessions USING btree ("LocationId", "EndsAt")`.
- `CREATE INDEX "IX_sessions_OrganiserId_CreatedAt" ON public.sessions USING btree ("OrganiserId", "CreatedAt")`.
- `CREATE UNIQUE INDEX "PK_sessions" ON public.sessions USING btree ("Id")`.

## public.submission_activities

Предложенные активности конкретной заявки.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| SubmissionId | uuid | True | — | Предложение площадки/исправления |
| ActivityId | uuid | True | — | Активность справочника |

Ограничения:

- `FK_submission_activities_activities_ActivityId`: `FOREIGN KEY ("ActivityId") REFERENCES activities(id) ON DELETE RESTRICT`.
- `FK_submission_activities_location_submissions_SubmissionId`: `FOREIGN KEY ("SubmissionId") REFERENCES location_submissions("Id") ON DELETE RESTRICT`.
- `PK_submission_activities`: `PRIMARY KEY ("SubmissionId", "ActivityId")`.

Индексы:

- `CREATE INDEX "IX_submission_activities_ActivityId" ON public.submission_activities USING btree ("ActivityId")`.
- `CREATE UNIQUE INDEX "PK_submission_activities" ON public.submission_activities USING btree ("SubmissionId", "ActivityId")`.

## reporting.account_directory

Представление для контролируемого чтения: email полностью маскирован, поля авторизации исключены.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| user_id | uuid | False | — | Идентификатор аккаунта |
| display_name | character varying(60) | False | — | Публичный псевдоним |
| email_masked | text | False | — | Полная маска email; исходный адрес не выдаётся |

## reporting.location_catalogue

Представление одобренных площадок и включённых активностей.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| location_id | uuid | False | — | Площадка |
| location_name | character varying(200) | False | — | Название площадки |
| address | character varying(300) | False | — | Адрес или ориентир площадки |
| latitude | numeric(9,6) | False | — | Широта общественной площадки, не геолокация человека |
| longitude | numeric(9,6) | False | — | Долгота общественной площадки |
| updated_at | timestamp with time zone | False | — | Время обновления площадки |
| activity_id | uuid | False | — | Активность |
| activity_slug | character varying(80) | False | — | Техническое имя активности |
| activity_name | character varying(120) | False | — | Название активности |

## reporting.session_overview

Отчёт о приглашениях и откликах; статус вычисляется, это не учёт посещаемости.

| Поле | Тип PostgreSQL | NOT NULL | Default / Identity | Назначение |
| --- | --- | --- | --- | --- |
| session_id | uuid | False | — | Приглашение |
| location_id | uuid | False | — | Площадка |
| location_name | character varying(200) | False | — | Название площадки |
| activity_slug | character varying(80) | False | — | Техническое имя активности |
| kind | character varying(16) | False | — | Вид записи: Planned/Immediate для игры, New/Correction для заявки |
| additional_capacity | integer | False | — | Количество дополнительных мест |
| active_participants | integer | False | — | Активные отклики, не подтверждённые посетители |
| remaining_places | integer | False | — | Свободные дополнительные места |
| starts_at | timestamp with time zone | False | — | Начало |
| ends_at | timestamp with time zone | False | — | Окончание |
| status | text | False | — | Cancelled → Expired → Full → Open, вычисляется при чтении |
