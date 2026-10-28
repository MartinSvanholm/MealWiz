-- Check that the meal or plan a row points at belongs to the caller (MEA-30).
-- FK checks bypass RLS, so the INSERT/UPDATE policies on ingredients and meal_plans-meals, which only checked
-- created_by, let a user attach ingredients to another user's meal (or a system meal), plan another user's
-- meal (which then blocked its owner from deleting it), or put a meal into another user's plan.
--
-- - ingredients.fk_meal must be the caller's own meal.
-- - meal_plans-meals.fk_meal_plan must be the caller's own plan.
-- - meal_plans-meals.fk_meal must be the caller's own meal or a system meal (created_by NULL), since plans
--   legitimately contain Leftovers / Eat Out.
--
-- Policy names and roles are kept so the policy list stays the same.

DROP POLICY IF EXISTS "Enable insert for users based on user_id" ON "public"."ingredients";
CREATE POLICY "Enable insert for users based on user_id"
    ON "public"."ingredients"
    FOR INSERT
    WITH CHECK (
        (( SELECT "auth"."uid"() AS "uid") = "created_by")
        AND EXISTS (
            SELECT 1 FROM "public"."meals" "m"
            WHERE "m"."id" = "fk_meal"
              AND "m"."created_by" = ( SELECT "auth"."uid"() AS "uid")
        )
    );

DROP POLICY IF EXISTS "Enable update for users based on user_id" ON "public"."ingredients";
CREATE POLICY "Enable update for users based on user_id"
    ON "public"."ingredients"
    FOR UPDATE
    USING ((( SELECT "auth"."uid"() AS "uid") = "created_by"))
    WITH CHECK (
        (( SELECT "auth"."uid"() AS "uid") = "created_by")
        AND EXISTS (
            SELECT 1 FROM "public"."meals" "m"
            WHERE "m"."id" = "fk_meal"
              AND "m"."created_by" = ( SELECT "auth"."uid"() AS "uid")
        )
    );

DROP POLICY IF EXISTS "Insert based on userid" ON "public"."meal_plans-meals";
CREATE POLICY "Insert based on userid"
    ON "public"."meal_plans-meals"
    FOR INSERT
    WITH CHECK (
        (( SELECT "auth"."uid"() AS "uid") = "created_by")
        AND EXISTS (
            SELECT 1 FROM "public"."meal_plans" "p"
            WHERE "p"."id" = "fk_meal_plan"
              AND "p"."created_by" = ( SELECT "auth"."uid"() AS "uid")
        )
        AND EXISTS (
            SELECT 1 FROM "public"."meals" "m"
            WHERE "m"."id" = "fk_meal"
              AND ("m"."created_by" = ( SELECT "auth"."uid"() AS "uid") OR "m"."created_by" IS NULL)
        )
    );

DROP POLICY IF EXISTS "Update based userid" ON "public"."meal_plans-meals";
CREATE POLICY "Update based userid"
    ON "public"."meal_plans-meals"
    FOR UPDATE
    USING ((( SELECT "auth"."uid"() AS "uid") = "created_by"))
    WITH CHECK (
        (( SELECT "auth"."uid"() AS "uid") = "created_by")
        AND EXISTS (
            SELECT 1 FROM "public"."meal_plans" "p"
            WHERE "p"."id" = "fk_meal_plan"
              AND "p"."created_by" = ( SELECT "auth"."uid"() AS "uid")
        )
        AND EXISTS (
            SELECT 1 FROM "public"."meals" "m"
            WHERE "m"."id" = "fk_meal"
              AND ("m"."created_by" = ( SELECT "auth"."uid"() AS "uid") OR "m"."created_by" IS NULL)
        )
    );
