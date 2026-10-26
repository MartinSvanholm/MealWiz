-- Schema guard for every public table: RLS is on, and each table has exactly the policies the other
-- test files cover. A new table or policy fails here until it gets tests of its own.
begin;
create extension if not exists pgtap with schema extensions;

select plan(8);

select is_empty(
    $$ select c.relname::text
       from pg_class c
       join pg_namespace n on n.oid = c.relnamespace
       where n.nspname = 'public'
         and c.relkind in ('r', 'p')
         and not c.relrowsecurity $$,
    'every public table has row level security enabled'
);

select tables_are(
    'public',
    array['grocery_items', 'ingredients', 'meal_imports', 'meal_plans', 'meal_plans-meals', 'meals'],
    'public has only the tables these tests cover'
);

select policies_are('public', 'grocery_items', array[
    'Delete based on userid',
    'Insert baed on userid',
    'Select based on userid',
    'Updated based on userid'
]);

select policies_are('public', 'ingredients', array[
    'Enable delete for users based on user_id',
    'Enable insert for users based on user_id',
    'Enable update for users based on user_id',
    'Enable users to view their own data only'
]);

select policies_are('public', 'meal_imports', array[
    'Enable insert for users based on user_id',
    'Enable users to fail their own pending imports',
    'Enable users to view their own data only'
]);

select policies_are('public', 'meal_plans', array[
    'Delete based on userid',
    'Insert baed on userid',
    'Select based on userid',
    'Updated based on userid'
]);

select policies_are('public', 'meal_plans-meals', array[
    'Delete based on userid',
    'Insert based on userid',
    'Select based on userid',
    'Update based userid'
]);

select policies_are('public', 'meals', array[
    'Enable delete for users based on user_id',
    'Enable insert for authenticated users only',
    'Enable update for users based on user_id',
    'Enable users to view their own data only'
]);

select * from finish();
rollback;
