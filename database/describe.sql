-- Schema metadata only: never reads user rows or authentication values.
SELECT json_agg(row_to_json(t) ORDER BY t.schema_name,t.table_name)
FROM (
 SELECT n.nspname AS schema_name,c.relname AS table_name,c.relkind::text AS kind,
   (SELECT json_agg(json_build_object('name',a.attname,'type',format_type(a.atttypid,a.atttypmod),'required',a.attnotnull,
       'default',pg_get_expr(d.adbin,d.adrelid),'identity',a.attidentity::text) ORDER BY a.attnum)
    FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
    WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped) AS columns,
   (SELECT json_agg(json_build_object('name',con.conname,'type',con.contype::text,'definition',pg_get_constraintdef(con.oid),
       'target',CASE WHEN con.confrelid<>0 THEN (SELECT relname FROM pg_class WHERE oid=con.confrelid) END,
       'optional',EXISTS(SELECT 1 FROM pg_attribute ca WHERE ca.attrelid=c.oid AND ca.attnum=ANY(con.conkey) AND NOT ca.attnotnull),
       'unique',EXISTS(SELECT 1 FROM pg_index ui WHERE ui.indrelid=c.oid AND ui.indisunique AND ui.indnatts=cardinality(con.conkey) AND con.conkey @> ui.indkey::smallint[] AND ui.indkey::smallint[] @> con.conkey)) ORDER BY con.conname)
    FROM pg_constraint con WHERE con.conrelid=c.oid) AS constraints,
   (SELECT json_agg(indexdef ORDER BY indexname) FROM pg_indexes i WHERE i.schemaname=n.nspname AND i.tablename=c.relname) AS indexes
 FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
 WHERE n.nspname IN ('public','reporting') AND c.relkind IN ('r','v')
) t;
