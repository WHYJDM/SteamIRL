-- Run in psql as the APPLICATION login, not administrator.
SELECT current_user, current_database(), rolsuper, rolcreatedb, rolcreaterole, rolbypassrls
FROM pg_roles WHERE rolname=current_user;
SELECT has_schema_privilege(current_user,'public','CREATE') AS can_create_objects,
       has_table_privilege(current_user,'public."AspNetUserRoles"','INSERT') AS can_promote_users,
       has_table_privilege(current_user,'public.moderation_decisions','UPDATE') AS can_edit_audit,
       has_table_privilege(current_user,'public.moderation_decisions','DELETE') AS can_delete_audit,
       has_table_privilege(current_user,'public.sessions','INSERT') AS can_create_sessions;
-- Expected: false for every elevated capability; true only for can_create_sessions.
