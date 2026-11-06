-- RLS on grocery_item_sources (MEA-84): which planned day and ingredient a grocery item came from.
-- Each user reads and writes only their own rows; INSERT and UPDATE also require the user's own item, planned day
-- and ingredient (planned day and ingredient may be null); anon gets nothing.
begin;
create extension if not exists pgtap with schema extensions;

select plan(28);

select col_default_is('public', 'grocery_items', 'is_manual', 'true', 'is_manual defaults to true');
select col_not_null('public', 'grocery_items', 'is_manual', 'is_manual is not null');

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.meals (id, name, meal_type, created_by, created_at) values
    (900001, 'A pasta', 'regular', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, 'B curry', 'regular', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.ingredients (id, name, fk_meal, created_by, created_at) values
    (900001, 'A flour', 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900003, 'A eggs',  900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, 'B rice',  900002, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.meal_plans (id, start_date, end_date, created_by, created_at) values
    (900001, '2026-01-05', '2026-01-11', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, '2026-01-05', '2026-01-11', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public."meal_plans-meals" (id, meal_date, fk_meal, fk_meal_plan, created_by, created_at) values
    (900001, '2026-01-05', 900001, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, '2026-01-05', 900002, 900002, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.grocery_items (id, name, is_picked, created_by, created_at) values
    (900001, 'A flour', false, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900003, 'A eggs',  false, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, 'B rice',  false, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.grocery_item_sources (id, fk_grocery_item, fk_planned_meal, fk_ingredient, amount, created_by) values
    (900001, 900001, 900001, 900001, '200 g', 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (900002, 900002, 900002, 900002, '1 dl',  'bbbbbbbb-0000-0000-0000-0000000000b2');

-- As user A.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select results_eq(
    $$ select id from public.grocery_item_sources order by id $$,
    $$ values (900001::bigint) $$,
    'user A sees only their own sources'
);

select lives_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, fk_planned_meal, fk_ingredient, amount, created_by)
       values (900003, 900001, 900003, '6', 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    'user A can insert their own source'
);

select lives_ok(
    $$ insert into public.grocery_item_sources (id, fk_grocery_item, created_by)
       values (900010, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    'user A can insert a source without planned day and ingredient'
);

select lives_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, created_by)
       values (900001, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    'NULL planned day and ingredient do not collide on the unique constraint'
);

select throws_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, created_by)
       values (900001, 'bbbbbbbb-0000-0000-0000-0000000000b2') $$,
    '42501', null,
    'user A cannot insert a source owned by user B'
);

select throws_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, created_by)
       values (900002, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '42501', null,
    'user A cannot link user B''s grocery item'
);

select throws_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, fk_planned_meal, created_by)
       values (900001, 900002, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '42501', null,
    'user A cannot link user B''s planned day'
);

select throws_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, fk_ingredient, created_by)
       values (900001, 900002, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '42501', null,
    'user A cannot link user B''s ingredient'
);

select throws_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, fk_planned_meal, fk_ingredient, created_by)
       values (900003, 900001, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23505', null,
    'the same planned day and ingredient cannot be linked twice'
);

select results_eq(
    $$ update public.grocery_item_sources set amount = '300 g' where id = 900001 returning id $$,
    $$ values (900001::bigint) $$,
    'user A can update their own source'
);

select results_eq(
    $$ update public.grocery_item_sources set amount = '1' where id = 900010 returning id $$,
    $$ values (900010::bigint) $$,
    'user A can update a source without planned day and ingredient'
);

select throws_ok(
    $$ update public.grocery_item_sources set fk_planned_meal = 900002 where id = 900001 $$,
    '42501', null,
    'user A cannot repoint their source to user B''s planned day'
);

select throws_ok(
    $$ update public.grocery_item_sources set fk_ingredient = 900002 where id = 900001 $$,
    '42501', null,
    'user A cannot repoint their source to user B''s ingredient'
);

select throws_ok(
    $$ update public.grocery_item_sources set fk_grocery_item = 900002 where id = 900001 $$,
    '42501', null,
    'user A cannot repoint their source to user B''s grocery item'
);

select throws_ok(
    $$ update public.grocery_item_sources set created_by = 'bbbbbbbb-0000-0000-0000-0000000000b2' where id = 900001 $$,
    '42501', null,
    'user A cannot hand their source over to user B'
);

select is_empty(
    $$ update public.grocery_item_sources set amount = 'hacked' where id = 900002 returning id $$,
    'user A cannot update user B''s source'
);

select results_eq(
    $$ delete from public.grocery_item_sources where id = 900010 returning id $$,
    $$ values (900010::bigint) $$,
    'user A can delete their own source'
);

select is_empty(
    $$ delete from public.grocery_item_sources where id = 900002 returning id $$,
    'user A cannot delete user B''s source'
);

-- As anon.
set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);

select is_empty(
    $$ select id from public.grocery_item_sources $$,
    'anon sees no sources'
);

select throws_ok(
    $$ insert into public.grocery_item_sources (fk_grocery_item, created_by)
       values (900001, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '42501', null,
    'anon cannot insert sources'
);

select is_empty(
    $$ update public.grocery_item_sources set amount = 'hacked' returning id $$,
    'anon cannot update sources'
);

select is_empty(
    $$ delete from public.grocery_item_sources returning id $$,
    'anon cannot delete sources'
);

-- Back as postgres, check nothing leaked through and the foreign keys behave.
reset role;

select results_eq(
    $$ select amount from public.grocery_item_sources where id = 900002 $$,
    $$ values ('1 dl'::text) $$,
    'user B''s source is untouched'
);

delete from public."meal_plans-meals" where id = 900001;
select results_eq(
    $$ select fk_planned_meal from public.grocery_item_sources where id = 900001 $$,
    $$ values (null::bigint) $$,
    'deleting the planned day keeps the source and clears its planned day'
);

delete from public.ingredients where id = 900001;
select results_eq(
    $$ select fk_ingredient from public.grocery_item_sources where id = 900001 $$,
    $$ values (null::bigint) $$,
    'deleting the ingredient keeps the source and clears its ingredient'
);

delete from public.grocery_items where id = 900001;
select is_empty(
    $$ select id from public.grocery_item_sources where id = 900001 $$,
    'deleting the grocery item deletes its sources'
);

select * from finish();
rollback;
