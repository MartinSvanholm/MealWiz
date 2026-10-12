-- Let the client mark its own still-pending meal_imports row as failed (MEA-14).
-- When the client can't reach the import Function (network/CORS/non-2xx), nothing
-- would otherwise ever progress the row, leaving it 'pending' forever.
--
-- This narrows, rather than reverses, the "no UPDATE policy" stance from
-- 20260806120000_add_meal_imports_table.sql: an authenticated user may only move
-- their own row from 'pending' to 'failed'. Success (status 'succeeded' +
-- parsed_meal) can still only be written by the service_role Function, and a
-- row the Function has already resolved can't be touched — the client's update
-- simply matches no rows, and it shows whatever the Function wrote instead.

DROP POLICY IF EXISTS "Enable users to fail their own pending imports" ON "public"."meal_imports";
CREATE POLICY "Enable users to fail their own pending imports"
    ON "public"."meal_imports"
    FOR UPDATE
    TO "authenticated"
    USING (
        (( SELECT "auth"."uid"() AS "uid") = "created_by")
        AND "status" = 'pending'
    )
    WITH CHECK (
        (( SELECT "auth"."uid"() AS "uid") = "created_by")
        AND "status" = 'failed'
    );
