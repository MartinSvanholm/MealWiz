-- RLS on meal_plans: each user reads and writes only their own rows; anon gets nothing.
begin;
create extension if not exists pgtap with schema extensions;

select plan(14);

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.meal_plans (id, start_date, end_date, created_by, created_at) values
    (900001, '2026-01-05', '2026-01-11', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, '2026-01-05', '2026-01-11', 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

-- As user A.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select results_eq(
    $$ select id from public.meal_plans order by id $$,
    $$ values (900001::bigint) $$,
    'user A sees only their own meal plans'
);

select lives_ok(
    $$ insert into public.meal_plans (id, start_date, end_date, created_by, created_at)
       values (900003, '2026-01-12', '2026-01-18', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    'user A can insert their own meal plan'
);

select throws_ok(
    $$ insert into public.meal_plans (start_date, end_date, created_by, created_at)
       values ('2026-01-12', '2026-01-18', 'bbbbbbbb-0000-0000-0000-0000000000b2', now()) $$,
    '42501', null,
    'user A cannot insert a meal plan owned by user B'
);

select results_eq(
    $$ update public.meal_plans set end_date = '2026-01-12' where id = 900001 returning id $$,
    $$ values (900001::bigint) $$,
    'user A can update their own meal plan'
);

select throws_ok(
    $$ update public.meal_plans set created_by = 'bbbbbbbb-0000-0000-0000-0000000000b2' where id = 900001 $$,
    '42501', null,
    'user A cannot hand their meal plan over to user B'
);

select results_eq(
    $$ delete from public.meal_plans where id = 900003 returning id $$,
    $$ values (900003::bigint) $$,
    'user A can delete their own meal plan'
);

select is_empty(
    $$ update public.meal_plans set end_date = '2030-01-01' where id = 900002 returning id $$,
    'user A cannot update user B''s meal plan'
);

select is_empty(
    $$ delete from public.meal_plans where id = 900002 returning id $$,
    'user A cannot delete user B''s meal plan'
);

-- As anon.
set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);

select is_empty(
    $$ select id from public.meal_plans $$,
    'anon sees no meal plans'
);

select throws_ok(
    $$ insert into public.meal_plans (start_date, end_date, created_by, created_at)
       values ('2026-01-12', '2026-01-18', 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '42501', null,
    'anon cannot insert meal plans'
);

select is_empty(
    $$ update public.meal_plans set end_date = '2030-01-01' returning id $$,
    'anon cannot update meal plans'
);

select is_empty(
    $$ delete from public.meal_plans returning id $$,
    'anon cannot delete meal plans'
);

-- Back as postgres, check nothing leaked through.
reset role;

select results_eq(
    $$ select end_date from public.meal_plans where id = 900002 $$,
    $$ values ('2026-01-11'::date) $$,
    'user B''s meal plan is untouched'
);

select results_eq(
    $$ select id from public.meal_plans where id in (900001, 900002) order by id $$,
    $$ values (900001::bigint), (900002::bigint) $$,
    'no meal plan was deleted by another user or anon'
);

select * from finish();
rollback;
