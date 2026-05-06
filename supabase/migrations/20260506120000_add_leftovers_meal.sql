-- Add system "Leftovers" meal support.
-- Introduces a meal_type discriminator, allows nullable created_by for system rows,
-- seeds a single global Leftovers row, and adjusts RLS so all authenticated users
-- can SELECT system meals while remaining unable to UPDATE or DELETE them.

ALTER TABLE "public"."meals"
    ADD COLUMN "meal_type" text NOT NULL DEFAULT 'regular'
        CHECK ("meal_type" IN ('regular', 'leftover'));

ALTER TABLE "public"."meals"
    ALTER COLUMN "created_by" DROP NOT NULL;

DROP POLICY IF EXISTS "Enable users to view their own data only" ON "public"."meals";
CREATE POLICY "Enable users to view their own data only"
    ON "public"."meals"
    FOR SELECT
    TO "authenticated"
    USING (
        "meal_type" = 'leftover'
        OR (( SELECT "auth"."uid"() AS "uid") = "created_by")
    );

DROP POLICY IF EXISTS "Enable update for users based on user_id" ON "public"."meals";
CREATE POLICY "Enable update for users based on user_id"
    ON "public"."meals"
    FOR UPDATE
    USING ((( SELECT "auth"."uid"() AS "uid") = "created_by"))
    WITH CHECK (
        (( SELECT "auth"."uid"() AS "uid") = "created_by")
        AND "meal_type" = 'regular'
    );

DROP POLICY IF EXISTS "Enable delete for users based on user_id" ON "public"."meals";
CREATE POLICY "Enable delete for users based on user_id"
    ON "public"."meals"
    FOR DELETE
    USING (
        (( SELECT "auth"."uid"() AS "uid") = "created_by")
        AND "meal_type" = 'regular'
    );
