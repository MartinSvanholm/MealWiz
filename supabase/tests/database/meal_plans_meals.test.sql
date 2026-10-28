-- RLS on meal_plans-meals (a meal planned on a day of a meal plan): each user reads and writes only
-- their own rows; anon gets nothing.
begin;
create extension if not exists pgtap with schema extensions;

select plan(20);

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.meals (id, name, meal_type, created_by, created_at) values
    (900001, 'A pasta', 'regular', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, 'B curry', 'regular', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.meal_plans (id, start_date, end_date, created_by, created_at) values
    (900001, '2026-01-05', '2026-01-11', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, '2026-01-05', '2026-01-11', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public."meal_plans-meals" (id, meal_date, fk_meal, fk_meal_plan, created_by, created_at) values
    (900001, '2026-01-05', 900001, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, '2026-01-05', 900002, 900002, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

-- As user A.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select results_eq(
    $$ select id from public."meal_plans-meals" order by id $$,
    $$ values (900001::bigint) $$,
    'user A sees only their own planned meals'
);

select lives_ok(
    $$ insert into public."meal_plans-meals" (id, meal_date, fk_meal, fk_meal_plan, created_by, created_at)
       values (900003, '2026-01-06', 900001, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    'user A can insert their own planned meal'
);

select throws_ok(
    $$ insert into public."meal_plans-meals" (meal_date, fk_meal, fk_meal_plan, created_by, created_at)
       values ('2026-01-06', 900001, 900001, 'bbbbbbbb-0000-0000-0000-0000000000b2', now()) $$,
    '42501', null,
    'user A cannot insert a planned meal owned by user B'
);

select lives_ok(
    $$ insert into public."meal_plans-meals" (id, meal_date, fk_meal, fk_meal_plan, created_by, created_at)
       select 900004, '2026-01-08', id, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()
       from public.meals where meal_type = 'leftover' and created_by is null $$,
    'user A can plan a system meal in their own plan'
);

select throws_ok(
    $$ insert into public."meal_plans-meals" (meal_date, fk_meal, fk_meal_plan, created_by, created_at)
       values ('2026-01-06', 900002, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '42501', null,
    'user A cannot plan user B''s meal'
);

select throws_ok(
    $$ insert into public."meal_plans-meals" (meal_date, fk_meal, fk_meal_plan, created_by, created_at)
       values ('2026-01-06', 900001, 900002, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '42501', null,
    'user A cannot plan a meal in user B''s plan'
);

select results_eq(
    $$ update public."meal_plans-meals" set meal_date = '2026-01-07' where id = 900001 returning id $$,
    $$ values (900001::bigint) $$,
    'user A can update their own planned meal'
);

select throws_ok(
    $$ update public."meal_plans-meals" set created_by = 'bbbbbbbb-0000-0000-0000-0000000000b2' where id = 900001 $$,
    '42501', null,
    'user A cannot hand their planned meal over to user B'
);

select throws_ok(
    $$ update public."meal_plans-meals" set fk_meal = 900002 where id = 900001 $$,
    '42501', null,
    'user A cannot swap user B''s meal into their planned meal'
);

select throws_ok(
    $$ update public."meal_plans-meals" set fk_meal_plan = 900002 where id = 900001 $$,
    '42501', null,
    'user A cannot move their planned meal into user B''s plan'
);

select results_eq(
    $$ delete from public."meal_plans-meals" where id = 900003 returning id $$,
    $$ values (900003::bigint) $$,
    'user A can delete their own planned meal'
);

select is_empty(
    $$ update public."meal_plans-meals" set meal_date = '2030-01-01' where id = 900002 returning id $$,
    'user A cannot update user B''s planned meal'
);

select is_empty(
    $$ delete from public."meal_plans-meals" where id = 900002 returning id $$,
    'user A cannot delete user B''s planned meal'
);

-- As anon.
set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);

select is_empty(
    $$ select id from public."meal_plans-meals" $$,
    'anon sees no planned meals'
);

select throws_ok(
    $$ insert into public."meal_plans-meals" (meal_date, fk_meal, fk_meal_plan, created_by, created_at)
       values ('2026-01-06', 900001, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '42501', null,
    'anon cannot insert planned meals'
);

select is_empty(
    $$ update public."meal_plans-meals" set meal_date = '2030-01-01' returning id $$,
    'anon cannot update planned meals'
);

select is_empty(
    $$ delete from public."meal_plans-meals" returning id $$,
    'anon cannot delete planned meals'
);

-- Back as postgres, check nothing leaked through.
reset role;

select results_eq(
    $$ select meal_date from public."meal_plans-meals" where id = 900002 $$,
    $$ values ('2026-01-05'::date) $$,
    'user B''s planned meal is untouched'
);

select results_eq(
    $$ select id from public."meal_plans-meals" where id in (900001, 900002) order by id $$,
    $$ values (900001::bigint), (900002::bigint) $$,
    'no planned meal was deleted by another user or anon'
);

select is_empty(
    $$ select id from public."meal_plans-meals"
       where created_by = 'aaaaaaaa-0000-0000-0000-0000000000a1' and (fk_meal = 900002 or fk_meal_plan = 900002) $$,
    'user A has no planned meal pointing at user B''s meal or plan'
);

select * from finish();
rollback;
