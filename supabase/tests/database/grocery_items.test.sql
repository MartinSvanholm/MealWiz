-- RLS on grocery_items: each user reads and writes only their own rows; anon gets nothing.
begin;
create extension if not exists pgtap with schema extensions;

select plan(14);

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.grocery_items (id, name, is_picked, created_by, created_at) values
    (900001, 'A milk',  false, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()),
    (900002, 'B bread', false, 'bbbbbbbb-0000-0000-0000-0000000000b2', now());

-- As user A.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select results_eq(
    $$ select id from public.grocery_items order by id $$,
    $$ values (900001::bigint) $$,
    'user A sees only their own grocery items'
);

select lives_ok(
    $$ insert into public.grocery_items (id, name, is_picked, created_by, created_at)
       values (900003, 'A eggs', false, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    'user A can insert their own grocery item'
);

select throws_ok(
    $$ insert into public.grocery_items (name, is_picked, created_by, created_at)
       values ('A for B', false, 'bbbbbbbb-0000-0000-0000-0000000000b2', now()) $$,
    '42501', null,
    'user A cannot insert a grocery item owned by user B'
);

select results_eq(
    $$ update public.grocery_items set is_picked = true where id = 900001 returning id $$,
    $$ values (900001::bigint) $$,
    'user A can update their own grocery item'
);

select throws_ok(
    $$ update public.grocery_items set created_by = 'bbbbbbbb-0000-0000-0000-0000000000b2' where id = 900001 $$,
    '42501', null,
    'user A cannot hand their grocery item over to user B'
);

select results_eq(
    $$ delete from public.grocery_items where id = 900003 returning id $$,
    $$ values (900003::bigint) $$,
    'user A can delete their own grocery item'
);

select is_empty(
    $$ update public.grocery_items set name = 'hacked' where id = 900002 returning id $$,
    'user A cannot update user B''s grocery item'
);

select is_empty(
    $$ delete from public.grocery_items where id = 900002 returning id $$,
    'user A cannot delete user B''s grocery item'
);

-- As anon.
set local role anon;
select set_config('request.jwt.claims', '{"role":"anon"}', true);

select is_empty(
    $$ select id from public.grocery_items $$,
    'anon sees no grocery items'
);

select throws_ok(
    $$ insert into public.grocery_items (name, is_picked, created_by, created_at)
       values ('anon', false, 'aaaaaaaa-0000-0000-0000-0000000000a1', now()) $$,
    '42501', null,
    'anon cannot insert grocery items'
);

select is_empty(
    $$ update public.grocery_items set name = 'hacked' returning id $$,
    'anon cannot update grocery items'
);

select is_empty(
    $$ delete from public.grocery_items returning id $$,
    'anon cannot delete grocery items'
);

-- Back as postgres, check nothing leaked through.
reset role;

select results_eq(
    $$ select name, is_picked from public.grocery_items where id = 900002 $$,
    $$ values ('B bread'::text, false) $$,
    'user B''s grocery item is untouched'
);

select results_eq(
    $$ select id from public.grocery_items where id in (900001, 900002) order by id $$,
    $$ values (900001::bigint), (900002::bigint) $$,
    'no grocery item was deleted by another user or anon'
);

select * from finish();
rollback;
