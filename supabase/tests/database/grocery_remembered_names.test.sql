-- RLS on grocery_remembered_names (MEA-138), and the two category write functions.
-- Each user reads and writes only their own remembered names, which must point at their own category; one row per
-- user and name. add_grocery_category puts a category just above Other; delete_grocery_category hands its names and
-- keys to another category and refuses Other, the same id and another user's ids.
begin;
create extension if not exists pgtap with schema extensions;

select plan(26);

-- Fixtures, inserted as postgres (the table owner, so RLS doesn't apply).
insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.grocery_categories (id, default_key, name, position, created_by) values
    (900001, 'produce', null, 1, 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (900002, 'meat_fish', null, 2, 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (900003, 'other', null, 3, 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (900004, 'produce', null, 1, 'bbbbbbbb-0000-0000-0000-0000000000b2');

insert into public.grocery_remembered_names (id, name, fk_category, created_by) values
    (900001, 'tofu', 900001, 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (900002, 'tempeh', 900004, 'bbbbbbbb-0000-0000-0000-0000000000b2');

select ok(
    not has_table_privilege('anon', 'public.grocery_remembered_names', 'select'),
    'anon cannot select remembered names'
);

select ok(
    not has_function_privilege('anon', 'public.add_grocery_category(text, text)', 'execute'),
    'anon cannot add a category'
);

-- As user A.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select results_eq(
    $$ select id from public.grocery_remembered_names order by id $$,
    $$ values (900001::bigint) $$,
    'user A sees only their own remembered names'
);

select lives_ok(
    $$ insert into public.grocery_remembered_names (id, name, fk_category, created_by)
       values (900010, 'seitan', 900002, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    'user A can remember a name in their own category'
);

select throws_ok(
    $$ insert into public.grocery_remembered_names (name, fk_category, created_by)
       values ('quorn', 900004, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '42501', null,
    'user A cannot point a name at user B''s category'
);

select throws_ok(
    $$ insert into public.grocery_remembered_names (name, fk_category, created_by)
       values ('quorn', 900004, 'bbbbbbbb-0000-0000-0000-0000000000b2') $$,
    '42501', null,
    'user A cannot insert a name owned by user B'
);

select throws_ok(
    $$ insert into public.grocery_remembered_names (name, fk_category, created_by)
       values ('tofu', 900002, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23505', null,
    'a name is remembered once per user'
);

select lives_ok(
    $$ insert into public.grocery_remembered_names (name, fk_category, created_by)
       values ('tofu', 900002, 'aaaaaaaa-0000-0000-0000-0000000000a1')
       on conflict (created_by, name) do update set fk_category = excluded.fk_category $$,
    'remembering a name again moves it'
);

select results_eq(
    $$ select fk_category from public.grocery_remembered_names where name = 'tofu' $$,
    $$ values (900002::bigint) $$,
    'tofu now points at Meat & fish'
);

select throws_ok(
    $$ update public.grocery_remembered_names set fk_category = 900004 where id = 900001 $$,
    '42501', null,
    'user A cannot move a name to user B''s category'
);

select is_empty(
    $$ update public.grocery_remembered_names set name = 'mine' where id = 900002 returning id $$,
    'user A cannot update user B''s remembered name'
);

select is_empty(
    $$ delete from public.grocery_remembered_names where id = 900002 returning id $$,
    'user A cannot delete user B''s remembered name'
);

select results_eq(
    $$ delete from public.grocery_remembered_names where id = 900010 returning id $$,
    $$ values (900010::bigint) $$,
    'user A can delete their own remembered name'
);

-- Category names and emoji.
select throws_ok(
    $$ insert into public.grocery_categories (name, position, created_by)
       values ('Snacks', 4, 'aaaaaaaa-0000-0000-0000-0000000000a1'),
              ('snacks', 5, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23505', null,
    'a category name is unique per user, ignoring case'
);

select throws_ok(
    $$ insert into public.grocery_categories (name, emoji, position, created_by)
       values ('Long', repeat('x', 17), 4, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23514', null,
    'an emoji is at most 16 characters'
);

-- add_grocery_category: just above Other.
select ok(
    public.add_grocery_category('Health food', ' 🥗 ') > 0,
    'user A can add a category'
);

select results_eq(
    $$ select coalesce(default_key, name), position, emoji from public.grocery_categories order by position $$,
    $$ values ('produce', 1, null::text), ('meat_fish', 2, null), ('Health food', 3, '🥗'), ('other', 4, null) $$,
    'the new category sits just above Other, with its emoji trimmed'
);

-- delete_grocery_category.
select throws_ok(
    $$ select public.delete_grocery_category(900003, 900001) $$,
    'P0001', 'grocery_category_is_other',
    'Other cannot be deleted'
);

select throws_ok(
    $$ select public.delete_grocery_category(900001, 900001) $$,
    'P0001', 'grocery_category_same',
    'a category cannot take over its own items'
);

select throws_ok(
    $$ select public.delete_grocery_category(900004, 900001) $$,
    'P0002', 'grocery_category_not_found',
    'user A cannot delete user B''s category'
);

select throws_ok(
    $$ select public.delete_grocery_category(900002, 900004) $$,
    'P0002', 'grocery_category_not_found',
    'user A cannot move items to user B''s category'
);

select results_eq(
    $$ select count(*)::int from public.grocery_categories where id = 900002 $$,
    $$ values (1) $$,
    'a refused delete leaves the category'
);

select lives_ok(
    $$ select public.delete_grocery_category(900002, 900001) $$,
    'user A can delete Meat & fish into Fruit & vegetables'
);

select results_eq(
    $$ select fk_category from public.grocery_remembered_names where name = 'tofu' $$,
    $$ values (900001::bigint) $$,
    'its remembered names move to the picked category'
);

select results_eq(
    $$ select absorbed_keys from public.grocery_categories where id = 900001 $$,
    $$ values (array['meat_fish']::text[]) $$,
    'the picked category absorbs its default key'
);

reset role;

select results_eq(
    $$ select count(*)::int from public.grocery_categories where created_by = 'bbbbbbbb-0000-0000-0000-0000000000b2' $$,
    $$ values (1) $$,
    'user B''s categories are unchanged'
);

select * from finish();
rollback;
