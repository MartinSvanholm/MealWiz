-- RLS on meal_imports: a user reads their own imports and inserts them as 'pending'; the only update they
-- may make is moving their own still-pending row to 'failed' (MarkMealImportFailed). Everything else is
-- the service_role Function's job, and nobody may delete. anon gets nothing.
begin;
create extension if not exists pgtap with schema extensions;

select plan(47);

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

-- Failing an import may only touch the columns MarkMealImportFailed sends.
select throws_ok(
    $$ update public.meal_imports set status = 'failed', source_url = 'https://example.test/changed'
       where id = '00000000-0000-0000-0000-00000000a001' $$,
    '42501', null,
    'user A cannot change the source_url while failing their import'
);

select throws_ok(
    $$ update public.meal_imports set status = 'failed', parsed_meal = '{"name":"fake"}'
       where id = '00000000-0000-0000-0000-00000000a001' $$,
    '42501', null,
    'user A cannot write a parsed_meal while failing their import'
);

select throws_ok(
    $$ update public.meal_imports set status = 'failed', created_at = '2000-01-01'
       where id = '00000000-0000-0000-0000-00000000a001' $$,
    '42501', null,
    'user A cannot change created_at while failing their import'
);

-- The same update MarkMealImportFailed sends.
select results_eq(
    $$ update public.meal_imports
       set status = 'failed', error_code = 'unreachable', error_message = 'Function unreachable', updated_at = now(),
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

select results_eq(
    $$ select source_url, parsed_meal, error_code, error_message from public.meal_imports
       where id = '00000000-0000-0000-0000-00000000a001' $$,
    $$ values ('https://example.test/a1'::text, null::jsonb, 'unreachable'::text, 'Function unreachable'::text) $$,
    'failing the import changed only the columns MarkMealImportFailed sends'
);

-- error_code: nullable, and only the codes in MealWiz/tests/Contracts/meal_import_error_code.json.
select col_is_null('public', 'meal_imports', 'error_code', 'error_code is nullable');

select lives_ok(
    $$ insert into public.meal_imports (created_by, source_url, status, error_code)
       select 'aaaaaaaa-0000-0000-0000-0000000000a1', 'https://example.test/' || code, 'failed', code
       from unnest(array['invalid_url', 'no_recipe_found', 'fetch_failed', 'unreachable', 'timed_out', 'unknown',
                         'no_meal_found', 'daily_limit_reached', 'picture_unreadable', 'ai_unavailable']) as code $$,
    'every error code in the contract is accepted'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, source_url, status, error_code)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', 'https://example.test/bogus', 'failed', 'bogus') $$,
    '23514', null,
    'an unknown error code is rejected'
);

-- Picture jobs: a link job or a picture job, never both or neither. Back as user A.
reset role;
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select has_column('public', 'meal_imports', 'picture_paths', 'meal_imports has picture_paths');
select has_column('public', 'meal_imports', 'language', 'meal_imports has language');
select has_column('public', 'meal_imports', 'hint', 'meal_imports has hint');
select col_is_null('public', 'meal_imports', 'source_url', 'source_url is nullable');

select lives_ok(
    $$ insert into public.meal_imports (id, created_by, picture_paths, language)
       values ('00000000-0000-0000-0000-00000000a101', 'aaaaaaaa-0000-0000-0000-0000000000a1',
               array['aaaaaaaa-0000-0000-0000-0000000000a1/j/0.jpg'], 'en') $$,
    'a picture job with one picture and a language is accepted'
);

select lives_ok(
    $$ insert into public.meal_imports (id, created_by, picture_paths, language, hint)
       values ('00000000-0000-0000-0000-00000000a102', 'aaaaaaaa-0000-0000-0000-0000000000a1',
               array['p/0.jpg', 'p/1.jpg', 'p/2.jpg'], 'da', 'it''s lasagne') $$,
    'a picture job with three pictures, Danish and a hint is accepted'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, picture_paths, language)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', array['p/0.jpg', 'p/1.jpg', 'p/2.jpg', 'p/3.jpg'], 'en') $$,
    '23514', null, 'a picture job with four pictures is rejected'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, picture_paths, language)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', array[]::text[], 'en') $$,
    '23514', null, 'a picture job with no pictures is rejected'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, picture_paths)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', array['p/0.jpg']) $$,
    '23514', null, 'a picture job without a language is rejected'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, picture_paths, language)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', array['p/0.jpg'], 'sv') $$,
    '23514', null, 'a language other than en or da is rejected'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, picture_paths, language, hint)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', array['p/0.jpg'], 'en', '') $$,
    '23514', null, 'an empty hint is rejected'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, picture_paths, language, hint)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', array['p/0.jpg'], 'en', repeat('x', 201)) $$,
    '23514', null, 'a hint over 200 characters is rejected'
);

select lives_ok(
    $$ insert into public.meal_imports (created_by, picture_paths, language, hint)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', array['p/0.jpg'], 'en', repeat('x', 200)) $$,
    'a hint of 200 characters is accepted'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, source_url, picture_paths, language)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', 'https://example.test/both', array['p/0.jpg'], 'en') $$,
    '23514', null, 'a row with both a link and pictures is rejected'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by, source_url, hint)
       values ('aaaaaaaa-0000-0000-0000-0000000000a1', 'https://example.test/hint', 'a hint') $$,
    '23514', null, 'a link job with a hint is rejected'
);

select throws_ok(
    $$ insert into public.meal_imports (created_by) values ('aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23514', null, 'a row with neither a link nor pictures is rejected'
);

-- The client can't change a picture job afterwards, but can still fail its own pending one.
select throws_ok(
    $$ update public.meal_imports set status = 'failed', picture_paths = array['p/9.jpg']
       where id = '00000000-0000-0000-0000-00000000a101' $$,
    '42501', null, 'user A cannot change the pictures of a picture job'
);

select throws_ok(
    $$ update public.meal_imports set status = 'failed', language = 'da'
       where id = '00000000-0000-0000-0000-00000000a101' $$,
    '42501', null, 'user A cannot change the language of a picture job'
);

select throws_ok(
    $$ update public.meal_imports set status = 'failed', hint = 'changed'
       where id = '00000000-0000-0000-0000-00000000a102' $$,
    '42501', null, 'user A cannot change the hint of a picture job'
);

select results_eq(
    $$ update public.meal_imports
       set status = 'failed', error_code = 'unreachable', error_message = 'Function unreachable', updated_at = now()
       where id = '00000000-0000-0000-0000-00000000a101' and status = 'pending'
       returning id $$,
    $$ values ('00000000-0000-0000-0000-00000000a101'::uuid) $$,
    'user A can fail their own pending picture job'
);

-- created_at is the server's time for a client's insert (the Function's freshness check and daily limit rely on it).
insert into public.meal_imports (id, created_by, picture_paths, language, created_at)
values ('00000000-0000-0000-0000-00000000a201', 'aaaaaaaa-0000-0000-0000-0000000000a1',
        array['aaaaaaaa-0000-0000-0000-0000000000a1/j/0.jpg'], 'en', now() - interval '1 day');
insert into public.meal_imports (id, created_by, source_url, created_at)
values ('00000000-0000-0000-0000-00000000a202', 'aaaaaaaa-0000-0000-0000-0000000000a1',
        'https://example.test/a202', now() + interval '1 hour');

select is(
    (select created_at from public.meal_imports where id = '00000000-0000-0000-0000-00000000a201'),
    now(),
    'a picture job user A inserts with a back-dated created_at gets the server''s time'
);

select is(
    (select created_at from public.meal_imports where id = '00000000-0000-0000-0000-00000000a202'),
    now(),
    'a link job user A inserts with a future created_at gets the server''s time'
);

-- The service role (the Function, test fixtures) keeps the created_at it gives.
reset role;
set local role service_role;

insert into public.meal_imports (id, created_by, picture_paths, language, created_at)
values ('00000000-0000-0000-0000-00000000a203', 'aaaaaaaa-0000-0000-0000-0000000000a1',
        array['aaaaaaaa-0000-0000-0000-0000000000a1/j/0.jpg'], 'en', '2026-01-01T00:00:00Z');

select is(
    (select created_at from public.meal_imports where id = '00000000-0000-0000-0000-00000000a203'),
    '2026-01-01T00:00:00Z'::timestamptz,
    'the service role keeps the created_at it inserts'
);

select * from finish();
rollback;
