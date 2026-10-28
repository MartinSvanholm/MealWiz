-- Restrict meal INSERT to the caller's own regular meals (MEA-29).
-- The original policy was WITH CHECK (true), so any signed-in user could insert a meal owned by another user,
-- or a fake system meal (created_by NULL, meal_type 'leftover' / 'eat_out') that the SELECT policy shows to
-- everyone. System meals come only from migrations and the seed, which run as postgres and bypass RLS.
-- The policy name is kept so the policy list stays the same.

DROP POLICY IF EXISTS "Enable insert for authenticated users only" ON "public"."meals";
CREATE POLICY "Enable insert for authenticated users only"
    ON "public"."meals"
    FOR INSERT
    TO "authenticated"
    WITH CHECK (
        (( SELECT "auth"."uid"() AS "uid") = "created_by")
        AND "meal_type" = 'regular'
    );
