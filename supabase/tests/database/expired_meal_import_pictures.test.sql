-- public.expired_meal_import_pictures(cutoff): names the pictures older than the cutoff, from the pictures bucket only,
-- and only the service role may call it (the daily clean-up Function removes them through the Storage API).
begin;
create extension if not exists pgtap with schema extensions;

select plan(7);

insert into storage.buckets (id, name) values ('other-bucket', 'other-bucket') on conflict (id) do nothing;

insert into storage.objects (bucket_id, name, created_at) values
    ('meal-import-pictures', 'u/old-1/0.jpg', '2026-01-01 10:00:00+00'),
    ('meal-import-pictures', 'u/old-2/0.jpg', '2026-01-02 10:00:00+00'),
    ('meal-import-pictures', 'u/new/0.jpg',   '2026-01-10 10:00:00+00'),
    ('other-bucket',         'u/old-3/0.jpg', '2026-01-01 10:00:00+00');

select function_returns('public', 'expired_meal_import_pictures', array['timestamp with time zone'], 'setof text',
    'the function returns a set of names');

set local role service_role;

select results_eq(
    $$ select * from public.expired_meal_import_pictures('2026-01-05 00:00:00+00') $$,
    $$ values ('u/old-1/0.jpg'::text), ('u/old-2/0.jpg'::text) $$,
    'the service role gets the older pictures of the bucket, oldest first, and nothing from another bucket'
);

select is_empty(
    $$ select * from public.expired_meal_import_pictures('2025-12-31 00:00:00+00') $$,
    'nothing is older than an early cutoff'
);

select results_eq(
    $$ select count(*) from public.expired_meal_import_pictures('2026-02-01 00:00:00+00') $$,
    $$ values (3::bigint) $$,
    'a later cutoff names all three pictures of the bucket'
);

set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select throws_ok(
    $$ select * from public.expired_meal_import_pictures('2026-02-01 00:00:00+00') $$,
    '42501', null, 'a signed-in user cannot call it'
);

set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);

select throws_ok(
    $$ select * from public.expired_meal_import_pictures('2026-02-01 00:00:00+00') $$,
    '42501', null, 'anon cannot call it'
);

reset role;

select is(
    (select prosecdef from pg_proc where proname = 'expired_meal_import_pictures'),
    true, 'it runs as its owner (security definer)'
);

select * from finish();
rollback;
