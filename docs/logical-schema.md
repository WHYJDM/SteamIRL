# Логическая схема реализованной БД

Схема связей реализованных таблиц после шести миграций. PK/FK и ограничения перечислены в [словаре данных](data-dictionary.md). Параллельные FK к одной таблице обозначают разные роли аккаунта. Планируемые функции описаны отдельно в [соответствии BA](ba-traceability.md).

```mermaid
erDiagram
    AspNetRoles ||--o{ AspNetRoleClaims : references
    AspNetUsers ||--o{ AspNetUserClaims : references
    AspNetUsers ||--o{ AspNetUserLogins : references
    AspNetRoles ||--o{ AspNetUserRoles : references
    AspNetUsers ||--o{ AspNetUserRoles : references
    AspNetUsers ||--o{ AspNetUserTokens : references
    AspNetUsers ||--o{ content_reports : references
    locations ||--o{ content_reports : references
    sessions ||--o{ content_reports : references
    AspNetUsers ||--o{ idempotency_requests : references
    activities ||--o{ location_activities : references
    locations ||--o{ location_activities : references
    AspNetUsers ||--o{ location_submissions : references
    locations ||--o{ location_submissions : references
    AspNetUsers ||--o{ moderation_decisions : references
    content_reports ||--o{ moderation_decisions : references
    location_submissions ||--o{ moderation_decisions : references
    AspNetUsers ||--o{ participations : references
    sessions ||--o{ participations : references
    AspNetUsers ||--o{ public_profiles : references
    AspNetUsers ||--o{ sessions : references
    location_activities ||--o{ sessions : references
    activities ||--o{ submission_activities : references
    location_submissions ||--o{ submission_activities : references
```

Представления reporting не являются дополнительными хранилищами: account_directory читает AspNetUsers/public_profiles с полной маской email; location_catalogue читает locations/location_activities/activities; session_overview добавляет sessions/participations и вычисляет статус. Журнал __EFMigrationsHistory технический.
