-- Optional repair/bootstrap of canonical activities only. Run as migrator.
-- No overwrite: approved user corrections and existing catalogue values survive.
BEGIN;
INSERT INTO public.activities (id,slug,name,description) VALUES
 ('11111111-1111-4111-8111-111111111111','volleyball','Волейбол','Найди площадку и компанию для игры в волейбол.'),
 ('22222222-2222-4222-8222-222222222222','basketball','Баскетбол','Собери команду для игры на открытой площадке.'),
 ('33333333-3333-4333-8333-333333333333','football','Футбол','Планируй игру и находи недостающих игроков.'),
 ('44444444-4444-4444-8444-444444444444','table-tennis','Настольный теннис','Найди место для партии и партнёра по игре.')
ON CONFLICT DO NOTHING;
COMMIT;
