-- The one-off data clean-up of 20261104110000_one_plan_per_week_and_meal_per_day.sql (MEA-94), not a table's RLS.
-- It drops the two constraints that migration adds, recreates duplicate plans and planned meals, runs the migration's
-- clean-up statements (copied verbatim: migrations never change after merging, DB-02) and checks what is left.
-- Everything is rolled back.
begin;
create extension if not exists pgtap with schema extensions;

select plan(5);

alter table public."meal_plans-meals" drop constraint "meal_plans-meals_fk_meal_plan_meal_date_key";
alter table public.meal_plans drop constraint meal_plans_created_by_start_date_key;

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.meals (id, name, meal_type, created_by, created_at) values
    (900001, 'A pasta', 'regular', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, 'A curry', 'regular', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900003, 'A stew', 'regular', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900004, 'B soup', 'regular', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.meal_plans (id, start_date, end_date, created_by, created_at) values
    (900001, '2026-01-05', '2026-01-11', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, '2026-01-05', '2026-01-11', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900003, '2026-01-05', '2026-01-11', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900004, '2026-01-12', '2026-01-18', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900005, '2026-01-05', '2026-01-11', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public."meal_plans-meals" (id, meal_date, fk_meal, fk_meal_plan, created_by, created_at) values
    (900001, '2026-01-05', 900001, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, '2026-01-06', 900001, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900003, '2026-01-05', 900002, 900002, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900004, '2026-01-07', 900002, 900002, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900005, '2026-01-08', 900003, 900003, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900006, '2026-01-08', 900001, 900003, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900007, '2026-01-12', 900001, 900004, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900008, '2026-01-12', 900003, 900004, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900009, '2026-01-05', 900004, 900005, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

-- The migration's clean-up, verbatim.

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


select results_eq(
    $$ select id from public.meal_plans where id between 900001 and 900005 order by id $$,
    $$ values (900001::bigint), (900004::bigint), (900005::bigint) $$,
    'duplicate weekly plans are merged into the lowest id; other weeks and users keep theirs'
);

select results_eq(
    $$ select id, fk_meal_plan, meal_date, fk_meal from public."meal_plans-meals"
       where id between 900001 and 900009 order by id $$,
    $$ values (900002::bigint, 900001::bigint, '2026-01-06'::date, 900001::bigint),
              (900003::bigint, 900001::bigint, '2026-01-05'::date, 900002::bigint),
              (900004::bigint, 900001::bigint, '2026-01-07'::date, 900002::bigint),
              (900006::bigint, 900001::bigint, '2026-01-08'::date, 900001::bigint),
              (900008::bigint, 900004::bigint, '2026-01-12'::date, 900003::bigint),
              (900009::bigint, 900005::bigint, '2026-01-05'::date, 900004::bigint) $$,
    'planned meals move to the kept plan, and each plan keeps one per date, the highest id'
);

select is_empty(
    $$ select id from public."meal_plans-meals" where fk_meal_plan in (900002, 900003) $$,
    'no planned meal points at a deleted plan'
);

select lives_ok(
    $$ alter table public.meal_plans add constraint meal_plans_created_by_start_date_key unique (created_by, start_date) $$,
    'after the clean-up the plan constraint can be added'
);

select lives_ok(
    $$ alter table public."meal_plans-meals" add constraint "meal_plans-meals_fk_meal_plan_meal_date_key" unique (fk_meal_plan, meal_date) $$,
    'after the clean-up the planned meal constraint can be added'
);

select * from finish();
rollback;
