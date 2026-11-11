-- Run as the READER login. Only masked/controlled reports are available.
SELECT * FROM reporting.account_directory ORDER BY user_id LIMIT 10;
SELECT * FROM reporting.location_catalogue ORDER BY location_name,activity_slug;
SELECT * FROM reporting.session_overview ORDER BY starts_at LIMIT 20;
-- Responses indicate intentions; neither attendance nor reservations are verified.
