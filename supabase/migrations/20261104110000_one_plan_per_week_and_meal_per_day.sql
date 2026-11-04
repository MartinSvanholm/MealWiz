-- One meal per plan and date, one plan per user and week (MEA-94).
-- Nothing stopped two devices from planning the same empty day at once, or creating the same week's plan at once.
-- Two rows for one date make MealPlan(MealPlanDb) throw (ToDictionary on the date), so that week can't load; two plans
-- for one week make GetMealPlanByDate return either one and hide the other's meals.
-- First clean up existing duplicates: merge duplicate weekly plans into the lowest id (their planned meals are moved
-- over first, since fk_meal_plan has no ON DELETE action), then keep one planned meal per plan and date, the highest
-- id (the most recent pick). Then add the constraints.
-- Runs before add_grocery_item_sources, so no grocery item source can point at a planned meal deleted here.

-- 4. Enforce it.
ALTER TABLE ONLY "public"."meal_plans"
    ADD CONSTRAINT "meal_plans_created_by_start_date_key" UNIQUE ("created_by", "start_date");

ALTER TABLE ONLY "public"."meal_plans-meals"
    ADD CONSTRAINT "meal_plans-meals_fk_meal_plan_meal_date_key" UNIQUE ("fk_meal_plan", "meal_date");
