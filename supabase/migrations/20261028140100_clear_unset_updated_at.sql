-- Clear the placeholder updated_at that the app wrote on every insert (MEA-40).
-- The client models had a non-nullable UpdatedAt, so new meals, ingredients, plans and planned meals were stored with
-- 0001-01-01 instead of NULL. The models are nullable now; this sets the rows written before that back to NULL,
-- meaning "never updated". Nothing reads updated_at to make decisions, so this only makes the data honest.

UPDATE "public"."meals" SET "updated_at" = NULL WHERE "updated_at" < '0001-01-02';

UPDATE "public"."ingredients" SET "updated_at" = NULL WHERE "updated_at" < '0001-01-02';

UPDATE "public"."meal_plans" SET "updated_at" = NULL WHERE "updated_at" < '0001-01-02';

UPDATE "public"."meal_plans-meals" SET "updated_at" = NULL WHERE "updated_at" < '0001-01-02';
