-- RLS on grocery_categories (MEA-137): the supermarket sections of the grocery list.
-- Each user reads and writes only their own rows; a user has each default category at most once; anon holds no
-- privilege on the table at all.
begin;
create extension if not exists pgtap with schema extensions;

select plan(15);

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.grocery_categories (id, default_key, name, position, created_by) values
    (900001, 'produce', null, 1, 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (900002, 'produce', null, 1, 'bbbbbbbb-0000-0000-0000-0000000000b2');

select ok(
    not has_table_privilege('anon', 'public.grocery_categories', 'select'),
    'anon cannot select grocery categories'
);

-- As user A.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select results_eq(
    $$ select id from public.grocery_categories order by id $$,
    $$ values (900001::bigint) $$,
    'user A sees only their own categories'
);

select lives_ok(
    $$ insert into public.grocery_categories (id, default_key, position, created_by)
       values (900003, 'other', 11, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    'user A can insert their own default category'
);

select lives_ok(
    $$ insert into public.grocery_categories (id, name, emoji, position, created_by)
       values (900004, 'Health food', '🥗', 10, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    'user A can insert their own custom category'
);

select throws_ok(
    $$ insert into public.grocery_categories (default_key, position, created_by)
       values ('produce', 2, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23505', null,
    'user A cannot have the same default category twice'
);

select lives_ok(
    $$ insert into public.grocery_categories (default_key, position, created_by)
       values ('produce', 2, 'aaaaaaaa-0000-0000-0000-0000000000a1')
       on conflict (created_by, default_key) do nothing $$,
    'inserting an existing default with on conflict do nothing is a no-op'
);

select throws_ok(
    $$ insert into public.grocery_categories (position, created_by)
       values (3, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23514', null,
    'a category needs a default key or a name'
);

select throws_ok(
    $$ insert into public.grocery_categories (default_key, position, created_by)
       values ('sweets', 3, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23514', null,
    'an unknown default key is refused'
);

select throws_ok(
    $$ insert into public.grocery_categories (default_key, position, created_by)
       values ('bakery', 2, 'bbbbbbbb-0000-0000-0000-0000000000b2') $$,
    '42501', null,
    'user A cannot insert a category owned by user B'
);

select results_eq(
    $$ update public.grocery_categories set name = 'Veg' where id = 900001 returning id $$,
    $$ values (900001::bigint) $$,
    'user A can update their own category'
);

select is_empty(
    $$ update public.grocery_categories set name = 'Mine' where id = 900002 returning id $$,
    'user A cannot update user B''s category'
);

select throws_ok(
    $$ update public.grocery_categories set created_by = 'bbbbbbbb-0000-0000-0000-0000000000b2' where id = 900001 $$,
    '42501', null,
    'user A cannot hand a category to user B'
);

select is_empty(
    $$ delete from public.grocery_categories where id = 900002 returning id $$,
    'user A cannot delete user B''s category'
);

select results_eq(
    $$ delete from public.grocery_categories where id = 900004 returning id $$,
    $$ values (900004::bigint) $$,
    'user A can delete their own category'
);

reset role;

select results_eq(
    $$ select name from public.grocery_categories where id = 900002 $$,
    $$ values (null::text) $$,
    'user B''s category is unchanged'
);

select * from finish();
rollback;
