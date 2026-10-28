-- RLS on meals: each user reads and writes only their own regular meals; the system meals
-- (created_by NULL, meal_type <> 'regular', from the seed) are readable by every signed-in user and
-- writable by nobody; anon gets nothing.
begin;
create extension if not exists pgtap with schema extensions;

select plan(23);

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.meals (id, name, meal_type, created_by, created_at) values
    (900001, 'A pasta',     'regular',  'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, 'B curry',     'regular',  'bbbbbbbb-0000-0000-0000-0000000000b2', now()),
    -- An owned non-regular meal can't be made through the policies, but the delete policy still has to
    -- refuse it.
    (900004, 'A leftovers', 'leftover', 'aaaaaaaa-0000-0000-0000-0000000000a1', now());

select isnt_empty(
    $$ select id from public.meals where created_by is null and meal_type in ('leftover', 'eat_out') $$,
    'the seed has the system meals'
);

-- As user A.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select results_eq(
    $$ select id from public.meals where created_by is not null order by id $$,
    $$ values (900001::bigint), (900004::bigint) $$,
    'user A sees only their own meals'
);

select results_eq(
    $$ select meal_type from public.meals where created_by is null order by meal_type $$,
    $$ values ('eat_out'::text), ('leftover'::text) $$,
    'user A sees the system meals'
);

select lives_ok(
    $$ insert into public.meals (id, name, meal_type, created_by, created_at)
       values (900003, 'A soup', 'regular', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    'user A can insert their own meal'
);

select throws_ok(
    $$ insert into public.meals (name, meal_type, created_by, created_at)
       values ('A for B', 'regular', 'bbbbbbbb-0000-0000-0000-0000000000b2', now()) $$,
    '42501', null,
    'user A cannot insert a meal owned by user B'
);

select throws_ok(
    $$ insert into public.meals (name, meal_type, created_by, created_at)
       values ('Fake leftovers', 'leftover', null, now()) $$,
    '42501', null,
    'user A cannot insert a fake system meal'
);

select throws_ok(
    $$ insert into public.meals (name, meal_type, created_by, created_at)
       values ('A eat out', 'eat_out', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '42501', null,
    'user A cannot insert a non-regular meal, even their own'
);

select results_eq(
    $$ update public.meals set name = 'A pasta v2' where id = 900001 returning id $$,
    $$ values (900001::bigint) $$,
    'user A can update their own meal'
);

select throws_ok(
    $$ update public.meals set created_by = 'bbbbbbbb-0000-0000-0000-0000000000b2' where id = 900001 $$,
    '42501', null,
    'user A cannot hand their meal over to user B'
);

select throws_ok(
    $$ update public.meals set meal_type = 'leftover' where id = 900001 $$,
    '42501', null,
    'user A cannot turn their meal into a system meal type'
);

select results_eq(
    $$ delete from public.meals where id = 900003 returning id $$,
    $$ values (900003::bigint) $$,
    'user A can delete their own meal'
);

select is_empty(
    $$ delete from public.meals where id = 900004 returning id $$,
    'user A cannot delete a non-regular meal, even their own'
);

select is_empty(
    $$ update public.meals set name = 'hacked' where id = 900002 returning id $$,
    'user A cannot update user B''s meal'
);

select is_empty(
    $$ delete from public.meals where id = 900002 returning id $$,
    'user A cannot delete user B''s meal'
);

select is_empty(
    $$ update public.meals set name = 'hacked' where created_by is null returning id $$,
    'user A cannot update the system meals'
);

select is_empty(
    $$ delete from public.meals where created_by is null returning id $$,
    'user A cannot delete the system meals'
);

-- As user B, the system meals are visible too.
select set_config('request.jwt.claims', '{"sub":"bbbbbbbb-0000-0000-0000-0000000000b2","role":"authenticated"}', true);

select results_eq(
    $$ select meal_type from public.meals where created_by is null order by meal_type $$,
    $$ values ('eat_out'::text), ('leftover'::text) $$,
    'user B sees the system meals'
);

-- As anon.
set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);

select is_empty(
    $$ select id from public.meals $$,
    'anon sees no meals, not even the system meals'
);

select throws_ok(
    $$ insert into public.meals (name, meal_type, created_by, created_at)
       values ('anon', 'regular', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '42501', null,
    'anon cannot insert meals'
);

select is_empty(
    $$ update public.meals set name = 'hacked' returning id $$,
    'anon cannot update meals'
);

select is_empty(
    $$ delete from public.meals returning id $$,
    'anon cannot delete meals'
);

-- Back as postgres, check nothing leaked through.
reset role;

select results_eq(
    $$ select id, name from public.meals where id in (900001, 900002, 900004) order by id $$,
    $$ values (900001::bigint, 'A pasta v2'::text), (900002::bigint, 'B curry'::text), (900004::bigint, 'A leftovers'::text) $$,
    'only user A''s own update went through, and nothing was deleted by another user or anon'
);

select results_eq(
    $$ select name from public.meals where created_by is null order by name $$,
    $$ values ('Eat Out'::text), ('Leftovers'::text) $$,
    'the system meals are untouched'
);

select * from finish();
rollback;
