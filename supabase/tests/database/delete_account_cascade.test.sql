-- Deleting an account (MEA-127): the DeleteAccount Function removes the auth user, and every row the user owns goes
-- with it through the created_by foreign keys. One delete from auth.users must succeed and leave nothing behind,
-- while another user's rows stay.
begin;
create extension if not exists pgtap with schema extensions;

select plan(9);

insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.meals (id, name, meal_type, created_by, created_at) values
    (910001, 'A pasta', 'regular', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (910002, 'B curry', 'regular', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.ingredients (id, name, fk_meal, created_by, created_at) values
    (910001, 'A flour', 910001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (910002, 'B rice',  910002, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.meal_plans (id, start_date, end_date, created_by, created_at) values
    (910001, '2026-01-05', '2026-01-11', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (910002, '2026-01-05', '2026-01-11', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public."meal_plans-meals" (id, meal_date, fk_meal, fk_meal_plan, created_at, created_by) values
    (910001, '2026-01-05', 910001, 910001, now(), 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (910002, '2026-01-05', 910002, 910002, now(), 'bbbbbbbb-0000-0000-0000-0000000000b2');

insert into public.grocery_items (id, name, is_picked, created_by, created_at) values
    (910001, 'A flour', false, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (910002, 'B rice',  false, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.grocery_item_sources (id, fk_grocery_item, fk_planned_meal, fk_ingredient, amount, created_by) values
    (910001, 910001, 910001, 910001, '200 g', 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (910002, 910002, 910002, 910002, '1 dl',  'bbbbbbbb-0000-0000-0000-0000000000b2');

insert into public.meal_imports (id, created_by, source_url, status) values
    ('00000000-0000-0000-0000-0000000a1001', 'aaaaaaaa-0000-0000-0000-0000000000a1', 'https://example.test/a', 'pending'),
    ('00000000-0000-0000-0000-0000000b1001', 'bbbbbbbb-0000-0000-0000-0000000000b2', 'https://example.test/b', 'pending');

select lives_ok(
    $$ delete from auth.users where id = 'aaaaaaaa-0000-0000-0000-0000000000a1' $$,
    'a single delete from auth.users succeeds');

select is((select count(*)::int from public.meals where created_by = 'aaaaaaaa-0000-0000-0000-0000000000a1'), 0, 'meals are gone');
select is((select count(*)::int from public.ingredients where created_by = 'aaaaaaaa-0000-0000-0000-0000000000a1'), 0, 'ingredients are gone');
select is((select count(*)::int from public.meal_plans where created_by = 'aaaaaaaa-0000-0000-0000-0000000000a1'), 0, 'meal plans are gone');
select is((select count(*)::int from public."meal_plans-meals" where created_by = 'aaaaaaaa-0000-0000-0000-0000000000a1'), 0, 'planned meals are gone');
select is((select count(*)::int from public.grocery_items where created_by = 'aaaaaaaa-0000-0000-0000-0000000000a1'), 0, 'grocery items are gone');
select is((select count(*)::int from public.grocery_item_sources where created_by = 'aaaaaaaa-0000-0000-0000-0000000000a1'), 0, 'grocery item sources are gone');
select is((select count(*)::int from public.meal_imports where created_by = 'aaaaaaaa-0000-0000-0000-0000000000a1'), 0, 'meal imports are gone');

select is(
    (select count(*)::int from public.meals m
       join public.ingredients i on i.fk_meal = m.id
       join public."meal_plans-meals" pm on pm.fk_meal = m.id
       join public.grocery_item_sources s on s.fk_planned_meal = pm.id
       join public.meal_imports mi on mi.created_by = m.created_by
      where m.created_by = 'bbbbbbbb-0000-0000-0000-0000000000b2'),
    1, 'user B keeps all their rows');

select * from finish();
rollback;
