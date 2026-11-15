-- The functions that do a handler's multi-step write in one transaction (add_atomic_write_functions):
-- each writes everything or nothing, runs as the caller (RLS still applies), and only signed-in users may call it.
begin;
create extension if not exists pgtap with schema extensions;

select plan(31);

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.meals (id, name, meal_type, created_by, created_at) values
    (910001, 'A pasta', 'regular', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (910002, 'B curry', 'regular', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.ingredients (id, name, fk_meal, created_by, created_at) values
    (910001, 'A flour', 910001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (910003, 'A eggs',  910001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (910002, 'B rice',  910002, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.meal_plans (id, start_date, end_date, created_by, created_at) values
    (910001, '2026-01-05', '2026-01-11', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (910002, '2026-01-05', '2026-01-11', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public."meal_plans-meals" (id, meal_date, fk_meal, fk_meal_plan, created_by, created_at) values
    (910001, '2026-01-05', 910001, 910001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (910002, '2026-01-05', 910002, 910002, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.grocery_items (id, name, amount, is_picked, is_manual, created_by, created_at) values
    (910001, 'A flour', '200 g', false, false, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (910002, 'B rice',  '1 dl',  false, false, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.grocery_item_sources (id, fk_grocery_item, fk_planned_meal, fk_ingredient, amount, created_by) values
    (910001, 910001, 910001, 910001, '200 g', 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (910002, 910002, 910002, 910002, '1 dl',  'bbbbbbbb-0000-0000-0000-0000000000b2');

-- As anon: no function may be called.
set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);

select throws_ok($$ select public.unlink_grocery_items(array[910001]::bigint[]) $$, '42501', null, 'anon cannot unlink items');
select throws_ok($$ select public.update_linked_grocery_amounts('[]'::jsonb, '[]'::jsonb) $$, '42501', null, 'anon cannot update linked amounts');
select throws_ok($$ select public.drop_grocery_item_sources(array[910001]::bigint[], '[]'::jsonb) $$, '42501', null, 'anon cannot drop sources');
select throws_ok($$ select public.add_plan_ingredients_to_grocery_list('[]'::jsonb, '[]'::jsonb, '[]'::jsonb) $$, '42501', null, 'anon cannot add plan ingredients');
select throws_ok($$ select public.save_generated_week('2026-02-02', '[]'::jsonb) $$, '42501', null, 'anon cannot save a week');

-- As user A.
reset role;
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

-- update_linked_grocery_amounts: all or nothing.
select throws_ok(
    $$ select public.update_linked_grocery_amounts(
           '[{"id": 910001, "amount": "300 g"}]'::jsonb,
           '[{"id": 999999, "amount": "300 g"}]'::jsonb) $$,
    'P0002', 'grocery_item_source_not_found',
    'a link that does not exist fails the whole update'
);
select results_eq(
    $$ select amount from public.grocery_items where id = 910001 $$,
    $$ values ('200 g'::text) $$,
    'and the item''s amount, written before the failure, is rolled back'
);
select throws_ok(
    $$ select public.update_linked_grocery_amounts(
           '[{"id": 910002, "amount": "9 dl"}]'::jsonb, '[]'::jsonb) $$,
    'P0002', 'grocery_item_not_found',
    'user A cannot change user B''s item through it'
);
select lives_ok(
    $$ select public.update_linked_grocery_amounts(
           '[{"id": 910001, "amount": "300 g"}]'::jsonb,
           '[{"id": 910001, "amount": "300 g"}]'::jsonb) $$,
    'user A updates their own item and link'
);
select results_eq(
    $$ select g.amount, s.amount from public.grocery_items g
       join public.grocery_item_sources s on s.fk_grocery_item = g.id where g.id = 910001 $$,
    $$ values ('300 g'::text, '300 g'::text) $$,
    'both amounts are written'
);

-- add_plan_ingredients_to_grocery_list: all or nothing.
select throws_ok(
    $$ select public.add_plan_ingredients_to_grocery_list(
           '[]'::jsonb,
           '[{"key": 0, "name": "A eggs", "amount": "6"}]'::jsonb,
           '[{"item_id": null, "new_item_key": 0, "planned_meal_id": 910001, "ingredient_id": 910001, "amount": "6"}]'::jsonb) $$,
    '23505', null,
    'a link that already exists for the planned day and ingredient fails the whole call'
);
select is_empty(
    $$ select id from public.grocery_items where name = 'A eggs' $$,
    'and the new item, inserted before the failure, is rolled back'
);
select is(
    (select jsonb_array_length(public.add_plan_ingredients_to_grocery_list(
        '[{"id": 910001, "amount": "350 g"}]'::jsonb,
        '[{"key": 0, "name": "A eggs", "amount": "6"}]'::jsonb,
        '[{"item_id": null, "new_item_key": 0, "planned_meal_id": 910001, "ingredient_id": 910003, "amount": "6"}]'::jsonb) -> 'inserted')),
    1,
    'user A adds a new item with its link and updates an existing item'
);
select results_eq(
    $$ select g.name, g.amount, g.is_manual, g.created_by, s.fk_ingredient
       from public.grocery_items g join public.grocery_item_sources s on s.fk_grocery_item = g.id
       where g.name = 'A eggs' $$,
    $$ values ('A eggs'::text, '6'::text, false, 'aaaaaaaa-0000-0000-0000-0000000000a1'::uuid, 910003::bigint) $$,
    'the new item is plan-added, owned by the caller and linked to its ingredient'
);
select results_eq(
    $$ select amount from public.grocery_items where id = 910001 $$,
    $$ values ('350 g'::text) $$,
    'the existing item has its new amount'
);

-- drop_grocery_item_sources: the link goes and the amount is lowered together.
select lives_ok(
    $$ select public.drop_grocery_item_sources(array[910001]::bigint[], '[{"id": 910001, "amount": "150 g"}]'::jsonb) $$,
    'user A drops a link and lowers the item''s amount'
);
select results_eq(
    $$ select g.amount, (select count(*) from public.grocery_item_sources s where s.id = 910001)
       from public.grocery_items g where g.id = 910001 $$,
    $$ values ('150 g'::text, 0::bigint) $$,
    'the link is gone and the amount is lowered'
);

-- unlink_grocery_items: RLS still applies.
select lives_ok(
    $$ select public.unlink_grocery_items(array[910001, 910002]::bigint[]) $$,
    'user A unlinks items (user B''s id is skipped by RLS)'
);

-- save_generated_week: finds the week's plan, overwrites a planned day in place and adds the others.
select lives_ok(
    $$ select public.save_generated_week('2026-01-05',
           '[{"date": "2026-01-05", "meal_id": 910001}, {"date": "2026-01-06", "meal_id": 910001}]'::jsonb) $$,
    'user A saves two days of an existing week'
);
select lives_ok(
    $$ select public.save_generated_week('2026-02-02', '[{"date": "2026-02-03", "meal_id": 910001}]'::jsonb) $$,
    'user A saves a day of a week that has no plan yet'
);

-- Back as postgres: what was written, and what was left alone.
reset role;

select results_eq(
    $$ select g.is_manual, (select count(*) from public.grocery_item_sources s where s.fk_grocery_item = g.id)
       from public.grocery_items g where g.id in (910001, 910002) order by g.id $$,
    $$ values (true, 0::bigint), (false, 1::bigint) $$,
    'user A''s item is manual with no links; user B''s item and link are untouched'
);
select results_eq(
    $$ select p.start_date, p.end_date, pm.id = 910001 as kept_its_row, pm.meal_date
       from public."meal_plans-meals" pm join public.meal_plans p on p.id = pm.fk_meal_plan
       where pm.created_by = 'aaaaaaaa-0000-0000-0000-0000000000a1' order by pm.meal_date $$,
    $$ values ('2026-01-05'::date, '2026-01-11'::date, true,  '2026-01-05'::date),
              ('2026-01-05'::date, '2026-01-11'::date, false, '2026-01-06'::date),
              ('2026-02-02'::date, '2026-02-08'::date, false, '2026-02-03'::date) $$,
    'the planned day kept its row, the new day joined the same plan, and the new week got its own plan'
);

-- The amount columns (add_amount_quantity_and_unit): each function writes quantity and unit next to the text, and a
-- payload without the keys writes null.
insert into public.ingredients (id, name, amount, fk_meal, created_by, created_at) values
    (910004, 'A milk', '1,5 dl', 910001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (910005, 'A salt', 'a pinch', 910001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now());

set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select lives_ok(
    $$ select public.add_plan_ingredients_to_grocery_list(
           '[{"id": 910001, "amount": "2 kg", "amount_quantity": 2, "amount_unit": "kg"}]'::jsonb,
           '[{"key": 0, "name": "A milk", "amount": "1,5 dl", "amount_quantity": 1.5, "amount_unit": "dl"},
             {"key": 1, "name": "A salt", "amount": "a pinch"}]'::jsonb,
           '[{"item_id": 910001, "new_item_key": null, "planned_meal_id": 910001, "ingredient_id": 910001, "amount": "2 kg", "amount_quantity": 2, "amount_unit": "kg"},
             {"item_id": null, "new_item_key": 0, "planned_meal_id": 910001, "ingredient_id": 910004, "amount": "1,5 dl", "amount_quantity": 1.5, "amount_unit": "dl"},
             {"item_id": null, "new_item_key": 1, "planned_meal_id": 910001, "ingredient_id": 910005, "amount": "a pinch"}]'::jsonb) $$,
    'adding plan ingredients with amount columns works'
);

reset role;

select results_eq(
    $$ select name, amount, amount_quantity, amount_unit from public.grocery_items
       where id = 910001 or name in ('A milk', 'A salt') order by name $$,
    $$ values ('A flour'::text, '2 kg'::text, 2::numeric, 'kg'::text),
              ('A milk'::text, '1,5 dl'::text, 1.5::numeric, 'dl'::text),
              ('A salt'::text, 'a pinch'::text, null::numeric, null::text) $$,
    'existing and new items hold the written columns, and a missing key is null'
);
select results_eq(
    $$ select fk_ingredient, amount_quantity, amount_unit from public.grocery_item_sources
       where fk_ingredient in (910001, 910004, 910005) order by fk_ingredient $$,
    $$ values (910001::bigint, 2::numeric, 'kg'::text), (910004::bigint, 1.5::numeric, 'dl'::text),
              (910005::bigint, null::numeric, null::text) $$,
    'the links hold the written columns'
);

set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select lives_ok(
    $$ select public.update_linked_grocery_amounts(
           '[{"id": 910001, "amount": "3 kg", "amount_quantity": 3, "amount_unit": "kg"}]'::jsonb,
           (select jsonb_build_array(jsonb_build_object('id', s.id, 'amount', '3 kg', 'amount_quantity', 3, 'amount_unit', 'kg'))
            from public.grocery_item_sources s where s.fk_ingredient = 910001)) $$,
    'updating linked amounts with amount columns works'
);

reset role;

select results_eq(
    $$ select g.amount_quantity, g.amount_unit, s.amount_quantity, s.amount_unit
       from public.grocery_items g join public.grocery_item_sources s on s.fk_grocery_item = g.id
       where s.fk_ingredient = 910001 $$,
    $$ values (3::numeric, 'kg'::text, 3::numeric, 'kg'::text) $$,
    'the item and the link hold the new quantity and unit'
);

set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select lives_ok(
    $$ select public.update_linked_grocery_amounts(
           '[{"id": 910001, "amount": "some"}]'::jsonb,
           (select jsonb_build_array(jsonb_build_object('id', s.id, 'amount', 'some'))
            from public.grocery_item_sources s where s.fk_ingredient = 910001)) $$,
    'updating linked amounts without the amount columns works'
);

reset role;

select results_eq(
    $$ select g.amount_quantity, g.amount_unit, s.amount_quantity, s.amount_unit
       from public.grocery_items g join public.grocery_item_sources s on s.fk_grocery_item = g.id
       where s.fk_ingredient = 910001 $$,
    $$ values (null::numeric, null::text, null::numeric, null::text) $$,
    'a payload without the keys writes null'
);

set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select lives_ok(
    $$ select public.drop_grocery_item_sources(
           (select array_agg(s.id) from public.grocery_item_sources s where s.fk_ingredient = 910004),
           (select jsonb_build_array(jsonb_build_object('id', g.id, 'amount', '2 dl', 'amount_quantity', 2, 'amount_unit', 'dl'))
            from public.grocery_items g where g.name = 'A milk')) $$,
    'dropping a link and lowering the item with amount columns works'
);

reset role;

select results_eq(
    $$ select amount, amount_quantity, amount_unit from public.grocery_items where name = 'A milk' $$,
    $$ values ('2 dl'::text, 2::numeric, 'dl'::text) $$,
    'the item holds the lowered quantity and unit'
);

select * from finish();
rollback;
