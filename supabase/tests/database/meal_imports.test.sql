-- RLS on meal_imports: a user reads their own imports and inserts them as 'pending'; the only update they
-- may make is moving their own still-pending row to 'failed' (MarkMealImportFailed). Everything else is
-- the service_role Function's job, and nobody may delete. anon gets nothing.
begin;
create extension if not exists pgtap with schema extensions;

select plan(17);

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.meal_imports (id, created_by, source_url, status) values
    ('00000000-0000-0000-0000-00000000a001', 'aaaaaaaa-0000-0000-0000-0000000000a1', 'https://example.test/a1', 'pending'),
    ('00000000-0000-0000-0000-00000000a002', 'aaaaaaaa-0000-0000-0000-0000000000a1', 'https://example.test/a2', 'failed'),
    ('00000000-0000-0000-0000-00000000a003', 'aaaaaaaa-0000-0000-0000-0000000000a1', 'https://example.test/a3', 'succeeded'),
    ('00000000-0000-0000-0000-00000000b001', 'bbbbbbbb-0000-0000-0000-0000000000b2', 'https://example.test/b1', 'pending');

-- As user A.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select results_eq(
    $$ select id from public.meal_imports order by id $$,
    $$ values ('00000000-0000-0000-0000-00000000a001'::uuid),
              ('00000000-0000-0000-0000-00000000a002'::uuid),
              ('00000000-0000-0000-0000-00000000a003'::uuid) $$,
    'user A sees only their own imports'
);

select lives_ok(
    $$ insert into public.meal_imports (id, created_by, source_url)
       values ('00000000-0000-0000-0000-00000000a005', 'aaaaaaaa-0000-0000-0000-0000000000a1', 'https://example.test/a5') $$,
    'user A can insert their own pending import'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, source_url, status)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', 'https://example.test/x', 'succeeded') $$,
    '42501', null,
    'user A cannot insert an import that is already resolved'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, source_url)
       values ('bbbbbbbb-0000-0000-0000-0000000000b2', 'https://example.test/x') $$,
    '42501', null,
    'user A cannot insert an import owned by user B'
);

select throws_ok(
    $$ update public.meal_imports set status = 'succeeded', parsed_meal = '{}'
       where id = '00000000-0000-0000-0000-00000000a001' $$,
    '42501', null,
    'user A cannot mark their own import as succeeded'
);

select throws_ok(
    $$ update public.meal_imports set source_url = 'https://example.test/changed'
       where id = '00000000-0000-0000-0000-00000000a001' $$,
    '42501', null,
    'user A cannot edit a pending import without failing it'
);

select is_empty(
    $$ update public.meal_imports set status = 'pending'
       where id = '00000000-0000-0000-0000-00000000a002' returning id $$,
    'user A cannot reopen a failed import'
);

select is_empty(
    $$ update public.meal_imports set status = 'failed'
       where id = '00000000-0000-0000-0000-00000000a003' returning id $$,
    'user A cannot fail an import the Function already resolved'
);

select is_empty(
    $$ update public.meal_imports set status = 'failed'
       where id = '00000000-0000-0000-0000-00000000b001' returning id $$,
    'user A cannot fail user B''s pending import'
);

select is_empty(
    $$ delete from public.meal_imports where id = '00000000-0000-0000-0000-00000000a001' returning id $$,
    'user A cannot delete their own import'
);

-- The same update MarkMealImportFailed sends.
select results_eq(
    $$ update public.meal_imports
       set status = 'failed', error_message = 'Function unreachable', updated_at = now(),
           updated_by = 'aaaaaaaa-0000-0000-0000-0000000000a1'
       where id = '00000000-0000-0000-0000-00000000a001' and status = 'pending'
       returning id $$,
    $$ values ('00000000-0000-0000-0000-00000000a001'::uuid) $$,
    'user A can move their own pending import to failed'
);

-- As user B.
select set_config('request.jwt.claims', '{"sub":"bbbbbbbb-0000-0000-0000-0000000000b2","role":"authenticated"}', true);

select results_eq(
    $$ select id from public.meal_imports order by id $$,
    $$ values ('00000000-0000-0000-0000-00000000b001'::uuid) $$,
    'user B sees only their own imports'
);

-- As anon.
set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);

select is_empty(
    $$ select id from public.meal_imports $$,
    'anon sees no imports'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, source_url)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', 'https://example.test/x') $$,
    '42501', null,
    'anon cannot insert imports'
);

select is_empty(
    $$ update public.meal_imports set status = 'failed' returning id $$,
    'anon cannot update imports'
);

select is_empty(
    $$ delete from public.meal_imports returning id $$,
    'anon cannot delete imports'
);

-- Back as postgres, check only the allowed update went through.
reset role;

select results_eq(
    $$ select id, status from public.meal_imports
       where id in ('00000000-0000-0000-0000-00000000a001', '00000000-0000-0000-0000-00000000a002',
                    '00000000-0000-0000-0000-00000000a003', '00000000-0000-0000-0000-00000000b001')
       order by id $$,
    $$ values ('00000000-0000-0000-0000-00000000a001'::uuid, 'failed'::text),
              ('00000000-0000-0000-0000-00000000a002'::uuid, 'failed'::text),
              ('00000000-0000-0000-0000-00000000a003'::uuid, 'succeeded'::text),
              ('00000000-0000-0000-0000-00000000b001'::uuid, 'pending'::text) $$,
    'only user A''s pending-to-failed update went through, and nothing was deleted'
);

select * from finish();
rollback;
