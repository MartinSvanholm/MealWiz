-- Add system "Eat Out" meal support.
-- Extends the meal_type CHECK constraint to allow 'eat_out', widens the SELECT
-- RLS carve-out so any non-regular (system) meal is visible to all authenticated
-- users, and seeds a single global Eat Out row. UPDATE/DELETE policies already
-- restrict to meal_type = 'regular' and need no change.

ALTER TABLE "public"."meals"
    DROP CONSTRAINT IF EXISTS "meals_meal_type_check";

ALTER TABLE "public"."meals"
    ADD CONSTRAINT "meals_meal_type_check"
        CHECK ("meal_type" IN ('regular', 'leftover', 'eat_out'));

DROP POLICY IF EXISTS "Enable users to view their own data only" ON "public"."meals";
CREATE POLICY "Enable users to view their own data only"
    ON "public"."meals"
    FOR SELECT
    TO "authenticated"
    USING (
        "meal_type" <> 'regular'
        OR (( SELECT "auth"."uid"() AS "uid") = "created_by")
    );

INSERT INTO public.meals (name, recipe, meal_type, created_by, created_at)
SELECT 'Eat Out', '', 'eat_out', NULL, now()
WHERE NOT EXISTS (
    SELECT 1 FROM public.meals WHERE meal_type = 'eat_out'
);
