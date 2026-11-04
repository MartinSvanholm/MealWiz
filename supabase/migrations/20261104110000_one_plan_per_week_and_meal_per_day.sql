-- One meal per plan and date, one plan per user and week (MEA-94).
-- Nothing stopped two devices from planning the same empty day at once, or creating the same week's plan at once.
-- Two rows for one date make MealPlan(MealPlanDb) throw (ToDictionary on the date), so that week can't load; two plans
-- for one week make GetMealPlanByDate return either one and hide the other's meals.
-- First clean up existing duplicates: merge duplicate weekly plans into the lowest id (their planned meals are moved
-- over first, since fk_meal_plan has no ON DELETE action), then keep one planned meal per plan and date, the highest
-- id (the most recent pick). Then add the constraints.
-- Runs before add_grocery_item_sources, so no grocery item source can point at a planned meal deleted here.

-- 1. Move the planned meals of duplicate plans to the plan that is kept (lowest id per user and start date).
WITH "ranked" AS (
    SELECT "id", min("id") OVER (PARTITION BY "created_by", "start_date") AS "keep_id"
    FROM "public"."meal_plans"
)
UPDATE "public"."meal_plans-meals" AS "pm"
SET "fk_meal_plan" = "r"."keep_id"
FROM "ranked" AS "r"
WHERE "pm"."fk_meal_plan" = "r"."id"
  AND "r"."id" <> "r"."keep_id";

-- 2. Delete the duplicate plans, now empty.
DELETE FROM "public"."meal_plans" AS "p"
USING "public"."meal_plans" AS "k"
WHERE "k"."created_by" = "p"."created_by"
  AND "k"."start_date" = "p"."start_date"
  AND "k"."id" < "p"."id";

-- 3. Keep one planned meal per plan and date: the highest id.
DELETE FROM "public"."meal_plans-meals" AS "pm"
USING "public"."meal_plans-meals" AS "newer"
WHERE "newer"."fk_meal_plan" = "pm"."fk_meal_plan"
  AND "newer"."meal_date" = "pm"."meal_date"
  AND "newer"."id" > "pm"."id";

-- 4. Enforce it.
ALTER TABLE ONLY "public"."meal_plans"
    ADD CONSTRAINT "meal_plans_created_by_start_date_key" UNIQUE ("created_by", "start_date");

ALTER TABLE ONLY "public"."meal_plans-meals"
    ADD CONSTRAINT "meal_plans-meals_fk_meal_plan_meal_date_key" UNIQUE ("fk_meal_plan", "meal_date");
