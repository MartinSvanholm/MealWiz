-- RLS on ingredients: each user reads and writes only their own rows; anon gets nothing.
begin;
create extension if not exists pgtap with schema extensions;

select plan(20);

-- MEA-38: an ingredient goes on the grocery list unless someone turns it off.
select col_default_is('public', 'ingredients', 'show_on_grocery_list', 'true',
    'show_on_grocery_list defaults to true');

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.meals (id, name, meal_type, created_by, created_at) values
    (900001, 'A pasta', 'regular', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, 'B curry', 'regular', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

insert into public.ingredients (id, name, show_on_grocery_list, fk_meal, created_by, created_at) values
    (900001, 'A flour', true, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, 'B rice',  true, 900002, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

-- As user A.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select results_eq(
    $$ select id from public.ingredients order by id $$,
    $$ values (900001::bigint) $$,
    'user A sees only their own ingredients'
);

select is_empty(
    $$ select id from public.ingredients where fk_meal = 900002 $$,
    'user A cannot see the ingredients of user B''s meal'
);

select lives_ok(
    $$ insert into public.ingredients (id, name, show_on_grocery_list, fk_meal, created_by, created_at)
       values (900003, 'A eggs', true, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    'user A can insert their own ingredient'
);

select throws_ok(
    $$ insert into public.ingredients (name, show_on_grocery_list, fk_meal, created_by, created_at)
       values ('A for B', true, 900001, 'bbbbbbbb-0000-0000-0000-0000000000b2', now()) $$,
    '42501', null,
    'user A cannot insert a ingredient owned by user B'
);

select throws_ok(
    $$ insert into public.ingredients (name, show_on_grocery_list, fk_meal, created_by, created_at)
       values ('A on B', true, 900002, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '42501', null,
    'user A cannot add an ingredient to user B''s meal'
);

select throws_ok(
    $$ insert into public.ingredients (name, show_on_grocery_list, fk_meal, created_by, created_at)
       select 'A on leftovers', true, id, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()
       from public.meals where meal_type = 'leftover' and created_by is null $$,
    '42501', null,
    'user A cannot add an ingredient to a system meal'
);

select results_eq(
    $$ update public.ingredients set amount = '500 g' where id = 900001 returning id $$,
    $$ values (900001::bigint) $$,
    'user A can update their own ingredient'
);

select throws_ok(
    $$ update public.ingredients set created_by = 'bbbbbbbb-0000-0000-0000-0000000000b2' where id = 900001 $$,
    '42501', null,
    'user A cannot hand their ingredient over to user B'
);

select throws_ok(
    $$ update public.ingredients set fk_meal = 900002 where id = 900001 $$,
    '42501', null,
    'user A cannot move their ingredient onto user B''s meal'
);

select results_eq(
    $$ delete from public.ingredients where id = 900003 returning id $$,
    $$ values (900003::bigint) $$,
    'user A can delete their own ingredient'
);

select is_empty(
    $$ update public.ingredients set name = 'hacked' where id = 900002 returning id $$,
    'user A cannot update user B''s ingredient'
);

select is_empty(
    $$ delete from public.ingredients where id = 900002 returning id $$,
    'user A cannot delete user B''s ingredient'
);

-- As anon.
set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);

select is_empty(
    $$ select id from public.ingredients $$,
    'anon sees no ingredients'
);

select throws_ok(
    $$ insert into public.ingredients (name, show_on_grocery_list, fk_meal, created_by, created_at)
       values ('anon', true, 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '42501', null,
    'anon cannot insert ingredients'
);

select is_empty(
    $$ update public.ingredients set name = 'hacked' returning id $$,
    'anon cannot update ingredients'
);

select is_empty(
    $$ delete from public.ingredients returning id $$,
    'anon cannot delete ingredients'
);

-- Back as postgres, check nothing leaked through.
reset role;

select results_eq(
    $$ select name, amount from public.ingredients where id = 900002 $$,
    $$ values ('B rice'::text, null::text) $$,
    'user B''s ingredient is untouched'
);

select results_eq(
    $$ select id from public.ingredients where id in (900001, 900002) order by id $$,
    $$ values (900001::bigint), (900002::bigint) $$,
    'no ingredient was deleted by another user or anon'
);

select is_empty(
    $$ select id from public.ingredients
       where fk_meal = 900002 and created_by <> 'bbbbbbbb-0000-0000-0000-0000000000b2'
          or fk_meal in (select id from public.meals where created_by is null) $$,
    'no ingredient was attached to user B''s meal or a system meal'
);

select * from finish();
rollback;
