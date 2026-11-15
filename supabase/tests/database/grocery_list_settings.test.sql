-- RLS on grocery_list_settings (MEA-139), grocery_items.picked_at, and the two section-order write functions.
-- Each user reads and writes only their own settings row, one per user. reorder_grocery_categories takes exactly the
-- caller's categories and turns learning off; apply_learned_section_order writes learned places and learned_until.
begin;
create extension if not exists pgtap with schema extensions;

select plan(17);

insert into auth.users (id, email, aud, role) values
    ('aaaaaaaa-0000-0000-0000-0000000000a1', 'user-a@rls.test', 'authenticated', 'authenticated'),
    ('bbbbbbbb-0000-0000-0000-0000000000b2', 'user-b@rls.test', 'authenticated', 'authenticated');

insert into public.grocery_categories (id, default_key, position, created_by) values
    (900001, 'produce', 1, 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (900002, 'bakery', 2, 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (900003, 'other', 3, 'aaaaaaaa-0000-0000-0000-0000000000a1'),
    (900004, 'produce', 1, 'bbbbbbbb-0000-0000-0000-0000000000b2');

insert into public.grocery_list_settings (id, order_mode, created_by) values
    (900002, 'manual', 'bbbbbbbb-0000-0000-0000-0000000000b2');

select col_is_null('public', 'grocery_items', 'picked_at', 'picked_at is nullable');

select ok(
    not has_table_privilege('anon', 'public.grocery_list_settings', 'select'),
    'anon cannot select grocery list settings'
);

-- As user A.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"aaaaaaaa-0000-0000-0000-0000000000a1","role":"authenticated"}', true);

select is_empty($$ select id from public.grocery_list_settings $$, 'user A sees no settings of user B');

select lives_ok(
    $$ insert into public.grocery_list_settings (id, created_by) values (900001, 'aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    'user A can insert their own settings'
);

select results_eq(
    $$ select order_mode from public.grocery_list_settings $$,
    $$ values ('learned') $$,
    'the order is learned by default'
);

select throws_ok(
    $$ insert into public.grocery_list_settings (created_by) values ('aaaaaaaa-0000-0000-0000-0000000000a1') $$,
    '23505', null,
    'one settings row per user'
);

select throws_ok(
    $$ insert into public.grocery_list_settings (created_by) values ('bbbbbbbb-0000-0000-0000-0000000000b2') $$,
    '42501', null,
    'user A cannot insert settings for user B'
);

select is_empty(
    $$ update public.grocery_list_settings set order_mode = 'learned' where id = 900002 returning id $$,
    'user A cannot update user B''s settings'
);

select is_empty(
    $$ delete from public.grocery_list_settings where id = 900002 returning id $$,
    'user A cannot delete user B''s settings'
);

-- reorder_grocery_categories.
select throws_ok(
    $$ select public.reorder_grocery_categories(array[900002, 900001]::bigint[]) $$,
    'P0002', 'grocery_category_not_found',
    'a reorder must list every one of the caller''s categories'
);

select throws_ok(
    $$ select public.reorder_grocery_categories(array[900002, 900001, 900004]::bigint[]) $$,
    'P0002', 'grocery_category_not_found',
    'a reorder cannot include user B''s category'
);

select lives_ok(
    $$ select public.reorder_grocery_categories(array[900002, 900001, 900003]::bigint[]) $$,
    'user A can put Bakery first'
);

select results_eq(
    $$ select id, position from public.grocery_categories order by position $$,
    $$ values (900002::bigint, 1), (900001::bigint, 2), (900003::bigint, 3) $$,
    'the positions follow the list'
);

select results_eq(
    $$ select order_mode from public.grocery_list_settings $$,
    $$ values ('manual') $$,
    'a reorder turns learning off'
);

-- apply_learned_section_order.
select lives_ok(
    $$ select public.apply_learned_section_order(
         '[{"id": 900001, "position": 1, "score": 1.4}, {"id": 900002, "position": 2, "score": 1.6}]'::jsonb,
         '2026-11-15 10:00:00+00') $$,
    'user A can apply a learned order'
);

select results_eq(
    $$ select c.id, c.position, c.order_score, s.learned_until
       from public.grocery_categories c cross join public.grocery_list_settings s
       where c.id in (900001, 900002) order by c.position $$,
    $$ values (900001::bigint, 1, 1.4::double precision, '2026-11-15 10:00:00+00'::timestamptz),
              (900002::bigint, 2, 1.6::double precision, '2026-11-15 10:00:00+00'::timestamptz) $$,
    'the learned places and learned_until are written'
);

select throws_ok(
    $$ select public.apply_learned_section_order('[{"id": 900004, "position": 1, "score": 1}]'::jsonb, now()) $$,
    'P0002', 'grocery_category_not_found',
    'user A cannot write user B''s category'
);

select * from finish();
rollback;
