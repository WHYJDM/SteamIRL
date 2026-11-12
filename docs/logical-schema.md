# Логическая схема реализованной БД

Схема связей реализованных таблиц после шести миграций. На диаграмме показаны первичные и внешние ключи; полный набор полей, типов, уникальных и CHECK-ограничений перечислен в [словаре данных](data-dictionary.md). Поля с комментарием `составной` в пределах одной таблицы входят в один составной ключ. Параллельные FK к одной таблице обозначают разные роли аккаунта. Планируемые функции описаны отдельно в [соответствии BA](ba-traceability.md).

```mermaid
erDiagram
    AspNetRoles ||..o{ AspNetRoleClaims : содержит утверждения
    AspNetUsers ||..o{ AspNetUserClaims : содержит утверждения
    AspNetUsers ||..o{ AspNetUserLogins : использует внешние входы
    AspNetRoles ||--o{ AspNetUserRoles : назначается пользователям
    AspNetUsers ||--o{ AspNetUserRoles : получает роли
    AspNetUsers ||--o{ AspNetUserTokens : хранит токены
    AspNetUsers ||..o{ content_reports : подает жалобы
    locations |o..o{ content_reports : получает жалобы
    sessions |o..o{ content_reports : получает жалобы
    AspNetUsers ||..o{ idempotency_requests : отправляет команды
    activities ||--o{ location_activities : доступна на площадках
    locations ||--o{ location_activities : поддерживает активности
    AspNetUsers ||..o{ location_submissions : предлагает изменения
    locations ||..o{ location_submissions : имеет предложения
    AspNetUsers ||..o{ moderation_decisions : принимает решения
    content_reports |o..o| moderation_decisions : имеет решение
    location_submissions |o..o| moderation_decisions : имеет решение
    AspNetUsers ||--o{ participations : участвует в играх
    sessions ||--o{ participations : содержит участников
    AspNetUsers ||--o| public_profiles : имеет публичный профиль
    AspNetUsers ||..o{ sessions : организует игры
    location_activities ||..o{ sessions : разрешает игры
    activities ||--o{ submission_activities : включена в предложения
    location_submissions ||--o{ submission_activities : содержит активности

    AspNetRoles {
        uuid Id PK
    }
    AspNetRoleClaims {
        int Id PK
        uuid RoleId FK
    }
    AspNetUsers {
        uuid Id PK
    }
    AspNetUserClaims {
        int Id PK
        uuid UserId FK
    }
    AspNetUserLogins {
        string LoginProvider PK "составной PK"
        string ProviderKey PK "составной PK"
        uuid UserId FK
    }
    AspNetUserRoles {
        uuid UserId PK, FK "составной PK"
        uuid RoleId PK, FK "составной PK"
    }
    AspNetUserTokens {
        uuid UserId PK, FK "составной PK"
        string LoginProvider PK "составной PK"
        string Name PK "составной PK"
    }
    activities {
        uuid id PK
    }
    locations {
        uuid Id PK
    }
    location_activities {
        uuid LocationId PK, FK "составной PK"
        uuid ActivityId PK, FK "составной PK"
    }
    public_profiles {
        uuid UserId PK, FK
    }
    sessions {
        uuid Id PK
        uuid LocationId FK "составной FK с ActivityId"
        uuid ActivityId FK "составной FK с LocationId"
        uuid OrganiserId FK
    }
    participations {
        uuid SessionId PK, FK "составной PK"
        uuid UserId PK, FK "составной PK"
    }
    idempotency_requests {
        uuid Id PK
        uuid UserId FK
    }
    location_submissions {
        uuid Id PK
        uuid AuthorId FK
        uuid LocationId FK
    }
    submission_activities {
        uuid SubmissionId PK, FK "составной PK"
        uuid ActivityId PK, FK "составной PK"
    }
    content_reports {
        uuid Id PK
        uuid AuthorId FK
        uuid LocationId FK "nullable; exactly one target"
        uuid SessionId FK "nullable; exactly one target"
    }
    moderation_decisions {
        uuid Id PK
        uuid ModeratorId FK
        uuid SubmissionId FK "nullable; exactly one target"
        uuid ReportId FK "nullable; exactly one target"
    }
```

Представления reporting не являются дополнительными хранилищами: account_directory читает AspNetUsers/public_profiles с полной маской email; location_catalogue читает locations/location_activities/activities; session_overview добавляет sessions/participations и вычисляет статус. Журнал __EFMigrationsHistory технический.
