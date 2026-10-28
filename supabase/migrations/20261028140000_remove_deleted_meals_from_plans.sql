-- Deleting a meal removes it from every plan it was on (MEA-39).
-- fk_meal had no ON DELETE action, so a meal that had ever been planned, even in a past week, couldn't be deleted:
-- the delete failed with an FK violation. Users can only delete their own meals, and since MEA-30 they can only plan
-- their own meals or system meals (which nobody can delete), so the cascade only ever reaches the user's own rows.

ALTER TABLE ONLY "public"."meal_plans-meals"
    DROP CONSTRAINT "meal_plans-meals_fk_meal_fkey";

ALTER TABLE ONLY "public"."meal_plans-meals"
    ADD CONSTRAINT "meal_plans-meals_fk_meal_fkey" FOREIGN KEY ("fk_meal") REFERENCES "public"."meals"("id") ON DELETE CASCADE;
