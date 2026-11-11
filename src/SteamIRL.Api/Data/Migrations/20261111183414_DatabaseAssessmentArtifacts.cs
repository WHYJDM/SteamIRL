using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteamIRL.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DatabaseAssessmentArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE SCHEMA reporting;
                REVOKE ALL ON SCHEMA reporting FROM PUBLIC;

                CREATE VIEW reporting.account_directory WITH (security_barrier=true) AS
                SELECT u."Id" AS user_id, p."DisplayName" AS display_name,
                    CASE WHEN u."Email" IS NULL THEN NULL ELSE '***@***' END AS email_masked
                FROM public."AspNetUsers" u JOIN public.public_profiles p ON p."UserId"=u."Id";

                CREATE VIEW reporting.location_catalogue WITH (security_barrier=true) AS
                SELECT l."Id" AS location_id,l."Name" AS location_name,l."Address" AS address,
                    l."Latitude" AS latitude,l."Longitude" AS longitude,l."UpdatedAt" AS updated_at,
                    a.id AS activity_id,a.slug AS activity_slug,a.name AS activity_name
                FROM public.locations l JOIN public.location_activities la ON la."LocationId"=l."Id" AND la."IsEnabled"
                JOIN public.activities a ON a.id=la."ActivityId"
                WHERE l."ApprovalStatus"='Approved';

                CREATE VIEW reporting.session_overview WITH (security_barrier=true) AS
                SELECT s."Id" AS session_id,s."LocationId" AS location_id,l."Name" AS location_name,
                    a.slug AS activity_slug,s."Kind" AS kind,s."Capacity" AS additional_capacity,
                    counts.active_participants,GREATEST(0,s."Capacity"-counts.active_participants) AS remaining_places,
                    s."StartsAt" AS starts_at,s."EndsAt" AS ends_at,
                    CASE WHEN s."CancelledAt" IS NOT NULL THEN 'Cancelled'
                         WHEN s."EndsAt"<=clock_timestamp() THEN 'Expired'
                         WHEN counts.active_participants>=s."Capacity" THEN 'Full' ELSE 'Open' END AS status
                FROM public.sessions s
                JOIN public.locations l ON l."Id"=s."LocationId" AND l."ApprovalStatus"='Approved'
                JOIN public.location_activities la ON la."LocationId"=s."LocationId" AND la."ActivityId"=s."ActivityId" AND la."IsEnabled"
                JOIN public.activities a ON a.id=s."ActivityId"
                CROSS JOIN LATERAL (SELECT count(*)::int AS active_participants FROM public.participations p WHERE p."SessionId"=s."Id" AND p."LeftAt" IS NULL) counts;

                CREATE FUNCTION public.protect_moderation_decision() RETURNS trigger
                LANGUAGE plpgsql SET search_path=pg_catalog AS $body$
                BEGIN
                    RAISE EXCEPTION 'Moderation decisions are immutable; use a reviewed administrative retention procedure.' USING ERRCODE='55000';
                END;
                $body$;
                REVOKE ALL ON FUNCTION public.protect_moderation_decision() FROM PUBLIC;
                CREATE TRIGGER moderation_decisions_immutable BEFORE UPDATE OR DELETE ON public.moderation_decisions
                    FOR EACH ROW EXECUTE FUNCTION public.protect_moderation_decision();
                CREATE TRIGGER moderation_decisions_no_truncate BEFORE TRUNCATE ON public.moderation_decisions
                    FOR EACH STATEMENT EXECUTE FUNCTION public.protect_moderation_decision();
                COMMENT ON VIEW reporting.account_directory IS 'Controlled expert report: email fully masked; no authentication fields. Not a public API.';
                COMMENT ON VIEW reporting.session_overview IS 'Responses are intentions, not verified attendance or bookings. Capacity means additional players.';
                COMMENT ON FUNCTION public.protect_moderation_decision() IS 'Preserves US14 audit. Retention/deletion under US19 requires a separately reviewed administrative process.';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER moderation_decisions_no_truncate ON public.moderation_decisions;
                DROP TRIGGER moderation_decisions_immutable ON public.moderation_decisions;
                DROP FUNCTION public.protect_moderation_decision();
                DROP VIEW reporting.session_overview;
                DROP VIEW reporting.location_catalogue;
                DROP VIEW reporting.account_directory;
                DROP SCHEMA reporting;
                """);
        }
    }
}
